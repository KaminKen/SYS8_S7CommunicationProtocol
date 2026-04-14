using SYS8.Core.StringManipulation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SYS8.Core.Protocol
{
    public class S7ProtocolLayer
    {
        private readonly TpktCotpLayer _tpktCotp;
        private ushort _pduLength = 0x480; //default, will be negotiated

        public S7ProtocolLayer(TpktCotpLayer tpktCotp)
        {
            _tpktCotp = tpktCotp;
        }

        //private byte NextSequence()
    
        //private byte[] BuildS7Header(byte rosctr, ushort paramLength, ushort dataLength)

        //private byte[] BuildReadRequest(ushort dbNumber, int byteOffset, int bitIndex, byte transportSize, ushort dataLength)



        /// <summary>
        /// Perform S7 Setup Communication sequence.
        /// </summary>
        /// <remarks>
        /// Call this once after the underlying COTP connection is established. The method
        /// sends an S7 "Setup Communication" request and parses the PLC response to
        /// obtain the negotiated PDU length. The negotiated value is stored in
        /// <see cref="NegotiatedPduLength"/> and should be used by higher layers when
        /// building larger PDUs.
        /// </remarks>
        /// <returns>A task that completes when setup/negotiation is finished.</returns>
        public async Task SetupCommunicationAsync(CancellationToken cancellationToken = default)
        {
            //Build S7 Setup Communication request
            //S7 header is 10 bytes, followed by parameters and data

            Debug.WriteLine("Building S7 Setup Communication request...");

            //S7Header
            byte[] header = S7ProtocolHelpers.BuildS7Header(0x01, 8, 0); //Setup Communication is a Job (0x01) with 8 bytes of parameters and 0 bytes of data

            //Parameters for Setup Communication
            byte[] parameters = new byte[8];
            parameters[0] = (byte)FunctionCode.SetupCommunication; // Function code for Setup Communication
            parameters[1] = 0x00; // Reserved
            parameters[2] = 0x00; parameters[3] = 0x01; // Max AMQ caller
            parameters[4] = 0x00; parameters[5] = 0x01; // Max AMQ callee
            parameters[6] = 0x04; parameters[7] = 0x80; // PDU length = 0x0480 

            //Combine S7 PDU
            byte[] s7Pdu = new byte[header.Length + parameters.Length];
            Buffer.BlockCopy(header, 0, s7Pdu, 0, header.Length);
            Buffer.BlockCopy(parameters, 0, s7Pdu, header.Length, parameters.Length);

            Debug.WriteLine("S7 SetupComm request PDU: " + BitConverter.ToString(s7Pdu));

            /*
             * S7 SetupComm request PDU: 32-01-00-00-00-01-00-08-00-00-F0-00-00-01-00-01-04-80
             */

            //send via tpkt and cotp layer
            await _tpktCotp.SendPayloadAsync(s7Pdu, cancellationToken);

            Debug.WriteLine("S7 Setup Communication request sent, waiting for response...");

            //receive response
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            /*
             * From S7-1500: 32-03-00-00-00-01-00-08-00-00-00-00-F0-00-00-01-00-01-00-F0
             * This is the response received during testing phase, which indicates a successful setup communication with the PLC.
             */

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));
            Debug.WriteLine("S7 Setup Communication response received, validating response...");

            //basic check on s7 response
            if (respPayload.Length < 18) //10 for header and 8 for params
            {
                throw new Exception("S7 Setup Communication Response too short.");
            }

            if (respPayload[0] != 0x32)
            {
                throw new Exception("Invalid S7 protocol ID in setup response.");
            }

            byte rosctr = respPayload[1];
            if (rosctr != 0x03) // 0x03 = Ack_Data
            {
                throw new Exception($"Unexpected ROSCTR in setup response: 0x{rosctr:X2}");
            }

            ushort paramLength = (ushort)((respPayload[6] << 8) | respPayload[7]); //Parameter length is mentioned in the TPKT header at 6 and 7 index
            if (paramLength != 8)
            {
                throw new Exception($"Unexpected parameter length in setup response: {paramLength}");
            }

            Debug.WriteLine("S7 Setup Communication response header validated successfully, parsing parameters...");


            // for this CPU, parameters start at index 12 (10 + 2 padding)
            int pIndex = 12;              // func = respPayload[12] (F0)
            byte function = respPayload[pIndex]; //First byte of parameters is function code
            if (function != 0x00 && function != 0xF0)
            {
                throw new Exception($"Unexpected function code in setup response: 0x{function:X2}");
            }

            ushort negotiatedPdu = (ushort)((respPayload[pIndex + 6] << 8) | respPayload[pIndex + 7
                ]); //pdu length reponse from the PLC at 6 and 7 index of params (+ pIndex for padding)

            if (negotiatedPdu == 0)
            {
                throw new Exception("Negotiated PDU length is 0.");
            }

            _pduLength = negotiatedPdu; //update pdu length based on negotiation

            Debug.WriteLine($"S7 Setup Communication successful. Negotiated PDU length: {_pduLength}");
        }


        /// <summary>
        /// Negotiated S7 PDU length (in bytes).
        /// </summary>
        /// <remarks>
        /// This value is set by <see cref="SetupCommunicationAsync"/> and represents the
        /// maximum S7 PDU payload size the PLC agreed to during negotiation.
        /// </remarks>
        public ushort NegotiatedPduLength => _pduLength;


        /// <summary>
        /// Parse a Siemens DB address string (for example "DB1.DBX0.1") into numeric components.
        /// </summary>
        /// <param name="address">Address string to parse.</param>
        /// <returns>Tuple of (dbNumber, byteOffset, bitIndex).</returns>
        private (ushort dbNumber, int byteOffset, int bitIndex) ParseStringAddress(string address)
        {
            var parser = new StringAddressToAbsoluteAddress();
            return parser.ParseStringAddress(address);
        }


        //TODO: optimize and refine. Possibly create helper function for checking header and params sent from PLC as they are mostly the same

        /// <summary>
        /// Read a boolean (single bit) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from (DBx).</param>
        /// <param name="byteOffset">Byte offset inside the DB (DBXn byte index).</param>
        /// <param name="bitIndex">Bit index inside the byte (0..7).</param>
        /// <returns>True when the addressed bit is set; otherwise false.</returns>
        /// <exception cref="Exception">Thrown when the PLC response is invalid or indicates an error.</exception>
        public async Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);

            Debug.WriteLine($"Reading boolean from DB{dbNumber}.DBX{byteOffset}.{bitIndex}...");

            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Bit, 1); // transport size bit, data length 1 byte

            Debug.WriteLine("S7 ReadVar request PDU: " + BitConverter.ToString(pdu));

            /*
             * Example: S7 ReadVar request PDU: 32-01-00-00-00-03-00-0E-00-04-04-01-12-0A-10-01-00-01-00-01-84-00-00-00
             */

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            Debug.WriteLine($"Reading boolean from DB{dbNumber}.DBX{byteOffset}.{bitIndex}...");

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));
            Debug.WriteLine("S7 Read response received, validating response...");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 1);

            byte valueByte = respPayload[dataHeaderStartIndex + 4]; // First value byte is the 4th index after data start.

            return valueByte == 0x01 ? true : false;

        }


        //TODO: optimize and refine. Possibly create helper function for all Read for checking header and params sent from PLC as they are mostly the same
        /// <summary>
        /// Read a 16-bit signed integer (INT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types; kept for API symmetry.</param>
        /// <returns>The 16-bit signed value read from the PLC.</returns>
        /// <exception cref="Exception">Thrown when the PLC response is invalid or indicates an error.</exception>
        public async Task<Int16> ReadInt16Async(string address, CancellationToken cancellationToken = default)
        {
            // Similar to ReadBoolAsync, but with parameters set for reading a 16-bit integer
            // transport size = 0x05 for INT, 4 data header parameters and data length = 2 bytes
            // The data parsing would also need to be adjusted to read 2 bytes of data and convert it to an Int16 value.
            // For INT transport, request 2 bytes (the helper will encode the parameter length in bits)

            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);

            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Int, 2);

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 16);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            Int16 value = (short)((respPayload[dataStartIndex] << 8) | respPayload[dataStartIndex + 1]); // combine 2 bytes of data into an Int16 value
            return value;

        }


        /// <summary>
        /// Read a 32-bit signed integer (DINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 32-bit signed value read from the PLC.</returns>
        public async Task<Int32> ReadInt32Async(string address, CancellationToken cancellationToken = default)
        {
            // DINT: request 4 bytes (helper will set parameter length to 32 bits)

            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.DInt, 4);

            Debug.WriteLine($"ReadInt32: DB={dbNumber} Offset={byteOffset} Bit={bitIndex} -> PDU: {BitConverter.ToString(pdu)}");
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            Debug.WriteLine($"ReadInt32 response: {BitConverter.ToString(respPayload)}");

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 32);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            Int32 value =
                (respPayload[dataStartIndex] << 24) |
                (respPayload[dataStartIndex + 1] << 16) |
                (respPayload[dataStartIndex + 2] << 8) |
                 respPayload[dataStartIndex + 3];
            return value;
        }

        /// <summary>
        /// Read a 64-bit signed integer from a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Implementation requests 8 raw bytes (octet string) from the PLC and combines them
        /// as big-endian into a signed 64-bit value. The helper uses <see cref="S7Types.ItemTransport.Byte"/>
        /// to ensure the PLC returns raw octets.
        /// </remarks>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 64-bit signed value read from the PLC.</returns>
        public async Task<long> ReadInt64Async(string address, CancellationToken cancellationToken = default)
        {
            // Request raw 8 bytes from the PLC and combine them into a signed 64-bit value.
            // Use ItemTransport.Byte so the PLC returns the data as an octet string / raw bytes.
            // 8 bytes requested for 64-bit values; for octet/real transports the helper expects dataUnitLength in bytes
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, 8);

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 64);

            int dataStartIndex = dataHeaderStartIndex + 4;

            long value =
                ((long)respPayload[dataStartIndex] << 56) |
                ((long)respPayload[dataStartIndex + 1] << 48) |
                ((long)respPayload[dataStartIndex + 2] << 40) |
                ((long)respPayload[dataStartIndex + 3] << 32) |
                ((long)respPayload[dataStartIndex + 4] << 24) |
                ((long)respPayload[dataStartIndex + 5] << 16) |
                ((long)respPayload[dataStartIndex + 6] << 8) |
                 (long)respPayload[dataStartIndex + 7];

            return value;
        }


        /// <summary>
        /// Read a 16-bit unsigned integer (WORD/UINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 16-bit unsigned value read from the PLC.</returns>
        public async Task<UInt16> ReadUInt16Async(string address, CancellationToken cancellationToken = default)
        {
            // WORD/UINT: request 2 bytes (helper will set parameter length to 16 bits)
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Word, 2);

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 16);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            UInt16 value = (ushort)((respPayload[dataStartIndex] << 8) | respPayload[dataStartIndex + 1]); 
            return value;

        }

        /// <summary>
        /// Read a 32-bit unsigned integer (DWORD/UDINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 32-bit unsigned value read from the PLC.</returns>
        public async Task<UInt32> ReadUInt32Async(string address, CancellationToken cancellationToken = default)
        {
            // DWORD/UDINT: request 4 bytes (helper will set parameter length to 32 bits)
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.DWord, 4); 

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 32);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            UInt32 value =
                ((UInt32)respPayload[dataStartIndex] << 24) |
                ((UInt32)respPayload[dataStartIndex + 1] << 16) |
                ((UInt32)respPayload[dataStartIndex + 2] << 8) |
                 (UInt32)respPayload[dataStartIndex + 3];
            return value;
        }

        /// <summary>
        /// Read a 64-bit unsigned integer from a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Implementation requests 8 raw bytes (octet string) from the PLC and combines them
        /// as big-endian into an unsigned 64-bit value. Uses <see cref="S7Types.ItemTransport.Byte"/>.
        /// </remarks>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 64-bit unsigned value read from the PLC.</returns>
        public async Task<ulong> ReadUInt64Async(string address, CancellationToken cancellationToken = default)
        {
            // Request 8 raw octets for consistent cross-CPU behavior
            // 8 bytes requested for 64-bit unsigned
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, 8);

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) =
                S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 64);

            int dataStartIndex = dataHeaderStartIndex + 4;

            ulong value =
                ((ulong)respPayload[dataStartIndex] << 56) |
                ((ulong)respPayload[dataStartIndex + 1] << 48) |
                ((ulong)respPayload[dataStartIndex + 2] << 40) |
                ((ulong)respPayload[dataStartIndex + 3] << 32) |
                ((ulong)respPayload[dataStartIndex + 4] << 24) |
                ((ulong)respPayload[dataStartIndex + 5] << 16) |
                ((ulong)respPayload[dataStartIndex + 6] << 8) |
                 (ulong)respPayload[dataStartIndex + 7];

            return value;
        }

        /// <summary>
        /// Read a 32-bit floating point value (REAL) from a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// The implementation requests 4 raw bytes and converts them into a IEEE-754
        /// float. To ensure consistent results across PLCs we request raw octets via
        /// <see cref="S7Types.ItemTransport.Byte"/> and reinterpret the bytes (big-endian).
        /// </remarks>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 32-bit floating point value read from the PLC.</returns>
        public async Task<float> ReadFloat32Async(string address, CancellationToken cancellationToken = default)
        {
            // Request 4 raw bytes (REAL) as octet string to ensure byte-level control
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, 4);

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 32);

            int dataStartIndex = dataHeaderStartIndex + 4;

            byte[] valueBytes = new byte[4];
            Buffer.BlockCopy(respPayload, dataStartIndex, valueBytes, 0, 4);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(valueBytes);
            }

            float value = BitConverter.ToSingle(valueBytes, 0);
            return value;
        }

        /// <summary>
        /// Read a 64-bit floating point value (LREAL/DOUBLE) from a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Implementation requests 8 raw bytes and converts them into a double using
        /// IEEE-754 interpretation. The helper uses raw octet reads to avoid PLC-specific
        /// length encoding differences.
        /// </remarks>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 64-bit floating point value read from the PLC.</returns>
        public async Task<double> ReadFloat64Async(string address, CancellationToken cancellationToken = default)
        {
            // Request 8 raw octets to decode IEEE-754 double consistently
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, 8);

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            var (_, _, dataHeaderStartIndex) =
                S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 64);

            int dataStartIndex = dataHeaderStartIndex + 4;

            byte[] valueBytes = new byte[8];
            Buffer.BlockCopy(respPayload, dataStartIndex, valueBytes, 0, 8);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(valueBytes);
            }

            double value = BitConverter.ToDouble(valueBytes, 0);
            return value;
        }

        /// <summary>
        /// Read a Siemens STRING from a DB.
        /// </summary>
        /// <remarks>
        /// Siemens STRING layout: first byte = declared max length, second byte = current length,
        /// followed by the character bytes. This method requests <c>maxStringLength + 2</c>
        /// octets and returns the decoded ASCII string.
        /// </remarks>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="maxStringLength">Maximum expected string length (characters).</param>
        /// <returns>The decoded string (empty if declared max is 0).</returns>
        public async Task<string> ReadStringAsync(string address, int maxStringLength, CancellationToken cancellationToken = default)
        {
            // Request as octet string: expected returned length is (maxStringLength + 2) bytes where first byte = declared max and second = current length
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, (ushort)(maxStringLength + 2));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, (ushort)((maxStringLength + 2) * 8));

            int dataStartIndex = dataHeaderStartIndex + 4;

            byte declaredMaxLength = respPayload[dataStartIndex];
            byte currentLength = respPayload[dataStartIndex + 1];

            if (declaredMaxLength == 0)
            {
                return string.Empty;
            }

            if (currentLength > declaredMaxLength)
            {
                throw new Exception($"Invalid STRING length. Current length {currentLength} exceeds declared max length {declaredMaxLength}.");
            }

            if (currentLength > maxStringLength)
            {
                throw new Exception($"Returned STRING length {currentLength} exceeds requested max length {maxStringLength}.");
            }

            byte[] stringBytes = new byte[currentLength];
            Buffer.BlockCopy(respPayload, dataStartIndex + 2, stringBytes, 0, currentLength);

            string value = Encoding.ASCII.GetString(stringBytes);
            return value;
        }


        /// <summary>
        /// Write a boolean (single bit) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index inside the byte (0..7).</param>
        /// <param name="value">Value to write.</param>
        /// <remarks>
        /// This method builds a WriteVar request header+parameters via
        /// <see cref="S7ProtocolHelpers.BuildReadWriteSetupRequest"/>, appends the 4-byte
        /// data header and payload, and sends the final PDU. The response is validated
        /// and an exception is thrown if the PLC reports an error.
        /// </remarks>
        /// <exception cref="Exception">Thrown when the PLC response indicates a failure.</exception>
        public async Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default)
        {
            // Build header+parameters for a WriteVar request (item transport = Bit, length = 1)
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Bit, 1);

            // Use helper to build the write-data block for a boolean value
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromBool(value);

            // Concatenate header+parameters and data to form final PDU
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);

            Debug.WriteLine("S7 WriteVar (BOOL) request PDU: " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 16-bit signed integer (INT) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>


        public async Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default)
        {
            // INT: request 2 bytes (helper will encode parameter length as 16 bits)
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Int, 2);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromInt16(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);

            Debug.WriteLine("S7 WriteVar (INT) request PDU: " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 32-bit signed integer (DINT) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default)
        {
            // DINT: request 4 bytes (helper will encode parameter length as 32 bits)
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.DInt, 4);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromInt32(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 64-bit signed integer to a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Uses an octet-string payload (8 raw bytes) to ensure consistent encoding.
        /// </remarks>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteInt64Async(string address, long value, CancellationToken cancellationToken = default)
        {
            // Use octet string payload to write 8 bytes
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, 8);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromInt64(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 16-bit unsigned integer (WORD/UINT) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteUInt16Async(string address, ushort value, CancellationToken cancellationToken = default)
        {
            // WORD/UINT: request 2 bytes (helper will encode parameter length as 16 bits)
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Word, 2);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromUInt16(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 32-bit unsigned integer (DWORD/UDINT) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteUInt32Async(string address, uint value, CancellationToken cancellationToken = default)
        {
            // DWORD/UDINT: request 4 bytes (helper will encode parameter length as 32 bits)
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.DWord, 4);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromUInt32(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 64-bit unsigned integer to a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Uses an octet-string payload (8 raw bytes) to ensure consistent encoding.
        /// </remarks>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteUInt64Async(string address, ulong value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, 8);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromUInt64(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 32-bit floating point value (REAL) to a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Encodes the float as 4 big-endian bytes and sends as an octet-string payload.
        /// </remarks>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteFloat32Async(string address, float value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, 4);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromFloat(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 64-bit floating point value (LREAL/DOUBLE) to a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Encodes the double as 8 big-endian bytes and sends as an octet-string payload.
        /// </remarks>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteFloat64Async(string address, double value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, 8);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromDouble(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a Siemens STRING to a DB.
        /// </summary>
        /// <remarks>
        /// Builds a STRING payload (declared max, current length, data) and writes it as an octet-string.
        /// </remarks>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="maxStringLength">Declared maximum length for the STRING (characters).</param>
        /// <param name="value">String value to write.</param>
        public async Task WriteStringAsync(string address, int maxStringLength, string value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Byte, (ushort)(maxStringLength + 2));
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromString(maxStringLength, value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }
    }

}
