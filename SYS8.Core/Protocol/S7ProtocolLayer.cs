using Microsoft.VisualBasic;
using SYS8.Core.StringManipulation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SYS8.Core.Protocol
{
    /// <summary>
    /// Implements the Siemens S7 protocol operations (read/write/setup) on top of a TPKT/COTP transport.
    /// This layer exposes methods that accept either a textual address (for example "DB1.DBX0.1") or
    /// raw numeric DB parameters (<c>dbNumber</c>, <c>byteOffset</c>, <c>bitIndex</c>).
    /// High level callers can use the string-based API which delegates to the numeric overloads after parsing.
    /// </summary>
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

        private string ConvertToAbsoluteAddress(ushort dbNumber, int byteOffset, int bitIndex)
        {
            var parser = new StringAddressToAbsoluteAddress();
            return parser.ConvertToAbsoluteAddress(dbNumber, byteOffset, bitIndex);
        }


        /// <summary>
        /// Read a single boolean (bit) from a DB specified by a textual address.
        /// The address is parsed (for example "DB1.DBX0.1") and the request is delegated
        /// to the numeric overload that accepts <c>dbNumber</c>, <c>byteOffset</c> and <c>bitIndex</c>.
        /// </summary>
        /// <param name="address">Textual DB address (for example "DB1.DBX0.1").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True when the addressed bit is set; otherwise false.</returns>
        public async Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadBoolAsync(dbNumber, byteOffset, bitIndex, cancellationToken);
        }


        /// <summary>
        /// Read a boolean (single bit) from a DB in the PLC using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">DB number to read from (DBx).</param>
        /// <param name="byteOffset">Byte offset inside the DB (byte index).</param>
        /// <param name="bitIndex">Bit index inside the byte (0..7).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True when the addressed bit is set; otherwise false.</returns>
        /// <exception cref="Exception">Thrown when the PLC response is invalid or indicates an error.</exception>
        public async Task<bool> ReadBoolAsync(ushort dbNumber,int byteOffset,int bitIndex,CancellationToken cancellationToken = default)
        { 
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


        public async Task<bool[]> ReadBoolArrayAsync (string address, int count, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadBoolArrayAsync(dbNumber, byteOffset, bitIndex, count, cancellationToken);
        }

        public async Task<bool[]> ReadBoolArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int count, CancellationToken cancellationToken = default)
        {
            if(!(count > 0))
            {
                throw new ArgumentException("Count must be greater than 0 for reading boolean array.");
            }

            // Robust approach: many PLCs respond unreliably to multi-element BIT reads.
            // Instead, read the minimal byte range that covers the requested bits and unpack locally.

            if (bitIndex < 0 || bitIndex > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be in range 0..7.");
            }
            if (byteOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset), "Byte offset must be >= 0.");
            }

            int startBit = (byteOffset * 8) + bitIndex;
            int endBit = startBit + count - 1;
            int startByte = startBit / 8;
            int endByte = endBit / 8;
            ushort bytesToRead = (ushort)(endByte - startByte + 1);

            // Read raw bytes (BYTE) starting at the first covered byte. bitIndex is irrelevant for byte reads.
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, startByte, 0, S7Types.ItemTransport.Byte, bytesToRead); //read the whole bytes

            Debug.WriteLine("S7 ReadVar request PDU for boolean array (BYTE read): " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine($"Response payload from BYTE read for bool array: {BitConverter.ToString(respPayload)}");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, (ushort)(bytesToRead * 8)); // minimum bit length is number of bytes read * 8

            int dataStartIndex = dataHeaderStartIndex + 4;
            bool[] result = new bool[count]; // prepare result array and count is the number of bits requested (all boolean value)

            // Siemens bit numbering: DBX<Byte>.<Bit> where Bit 0 is the LSB of the byte.
            for (int i = 0; i < count; i++)
            {
                int absBit = startBit + i; //pad the bit index to get the absolute bit position in the DB  (by + starting bit), then calculate the byte and bit within that byte
                int absByte = absBit / 8;
                int bitInByte = absBit % 8; // 0..7 (LSB..MSB)
                int relByte = absByte - startByte;

                int bytePos = dataStartIndex + relByte;
                if (bytePos < dataStartIndex || bytePos >= respPayload.Length)
                {
                    throw new Exception("Response payload too short for requested boolean array.");
                }

                byte b = respPayload[bytePos];
                result[i] = (b & (1 << bitInByte)) != 0;
            }

            return result;
        }



        /// <summary>
        /// Read a 16-bit signed integer (INT) from a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address (for example "DB1.DBW0" or "DB1.DBD0").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit signed value read from the PLC.</returns>
        public async Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default)
        {
            var(dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadInt16Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }



        //TODO: optimize and refine. Possibly create helper function for all Read for checking header and params sent from PLC as they are mostly the same
        /// <summary>
        /// Read a 16-bit signed integer (INT) from a DB using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types; kept for API symmetry.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit signed value read from the PLC.</returns>
        /// <exception cref="Exception">Thrown when the PLC response is invalid or indicates an error.</exception>
        public async Task<short> ReadInt16Async(ushort dbNumber,int byteOffset,int bitIndex,CancellationToken cancellationToken = default)
        {
            // Similar to ReadBoolAsync, but with parameters set for reading a 16-bit integer
            // transport size = 0x05 for INT, 4 data header parameters and data length = 2 bytes
            // The data parsing would also need to be adjusted to read 2 bytes of data and convert it to an Int16 value.
            // For INT transport, request 2 bytes (the helper will encode the parameter length in bits)

            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Int, 2);

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 16);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            short value = (short)((respPayload[dataStartIndex] << 8) | respPayload[dataStartIndex + 1]); // combine 2 bytes of data into an Int16 value
            return value;

        }


        public async Task<short[]> ReadInt16ArrayAsync(string address, int count, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadInt16ArrayAsync(dbNumber, byteOffset, bitIndex, count, cancellationToken);
        }

        public async Task<short[]> ReadInt16ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int count, CancellationToken cancellationToken = default)
        {
            if (!(count > 0))
            {
                throw new ArgumentException("Count must be greater than 0 for reading Int16 array.");
            }

            if (bitIndex < 0 || bitIndex > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be in range 0..7.");
            }
            if (byteOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset), "Byte offset must be >= 0.");
            }

            int startByte = byteOffset;
            ushort bytesToRead = (ushort)(count * 2);

            // Read raw bytes (BYTE) starting at the first covered byte. bitIndex is irrelevant for byte reads.
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, startByte, 0, S7Types.ItemTransport.Byte, bytesToRead); //read the whole bytes

            Debug.WriteLine("S7 ReadVar request PDU for Int16 array (BYTE read): " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine($"Response payload from BYTE read for Int16 array: {BitConverter.ToString(respPayload)}");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, (ushort)(bytesToRead * 8)); // minimum bit length is number of bytes read * 8

            int dataStartIndex = dataHeaderStartIndex + 4;
            short[] result = new short[count]; // prepare result array and count is the number of Int16 values requested

            // Siemens bit numbering: DBX<Byte>.<Bit> where Bit 0 is the LSB of the byte.
            for (int i = 0; i < count; i++)
            {
                // i * 2 for each int16 value
                int idx = i * 2;
                // idx is an offset inside the data area. Ensure the two bytes for this Int16 are present
                if (dataStartIndex + idx + 1 >= respPayload.Length)
                {
                    throw new Exception("Response payload too short for requested Int16 array.");
                }
                result[i] = (short)((respPayload[dataStartIndex + idx] << 8) | respPayload[dataStartIndex + idx + 1]);
            }

            return result;
        }



        /// <summary>
        /// Read a 32-bit signed integer (DINT) from a DB specified by a textual address.
        /// Delegates to the numeric overload after parsing the address string.
        /// </summary>
        /// <param name="address">Textual DB address (for example "DB1.DBD0").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit signed value read from the PLC.</returns>
        public async Task<int> ReadInt32Async(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadInt32Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }


        /// <summary>
        /// Read a 32-bit signed integer (DINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 32-bit signed value read from the PLC.</returns>
        public async Task<int> ReadInt32Async(ushort dbNumber,int byteOffset,int bitIndex,CancellationToken cancellationToken = default)
        {
            // DINT: request 4 bytes (helper will set parameter length to 32 bits)
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.DInt, 4);

            Debug.WriteLine($"ReadInt32: DB={dbNumber} Offset={byteOffset} Bit={bitIndex} -> PDU: {BitConverter.ToString(pdu)}");
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            Debug.WriteLine($"ReadInt32 response: {BitConverter.ToString(respPayload)}");

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 32);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            int value =
                (respPayload[dataStartIndex] << 24) |
                (respPayload[dataStartIndex + 1] << 16) |
                (respPayload[dataStartIndex + 2] << 8) |
                 respPayload[dataStartIndex + 3];
            return value;
        }

        public async Task<int[]> ReadInt32ArrayAsync(string address, int count, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadInt32ArrayAsync(dbNumber, byteOffset, bitIndex, count, cancellationToken);
        }

        public async Task<int[]> ReadInt32ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int count, CancellationToken cancellationToken = default)
        {
            if (!(count > 0))
            {
                throw new ArgumentException("Count must be greater than 0 for reading Int32 array.");
            }


            if (bitIndex < 0 || bitIndex > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be in range 0..7.");
            }
            if (byteOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset), "Byte offset must be >= 0.");
            }

            int startByte = byteOffset;
            ushort bytesToRead = (ushort)(count * 4);

            // Read raw bytes (BYTE) starting at the first covered byte. bitIndex is irrelevant for byte reads.
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, startByte, 0, S7Types.ItemTransport.Byte, bytesToRead); //read the whole bytes

            Debug.WriteLine("S7 ReadVar request PDU for Int32 array (BYTE read): " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine($"Response payload from BYTE read for Int32 array: {BitConverter.ToString(respPayload)}");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, (ushort)(bytesToRead * 8)); // minimum bit length is number of bytes read * 8

            int dataStartIndex = dataHeaderStartIndex + 4;
            int[] result = new int[count]; // prepare result array and count is the number of Int32 values requested

            // Siemens bit numbering: DBX<Byte>.<Bit> where Bit 0 is the LSB of the byte.
            for (int i = 0; i < count; i++)
            {
                // i * 2 for each int16 value
                int idx = i * 4;
                // idx is an offset inside the data area. Ensure the four bytes for this Int32 are present
                if (dataStartIndex + idx + 3 >= respPayload.Length)
                {
                    throw new Exception("Response payload too short for requested Int32 array.");
                }
                result[i] = (int)((respPayload[dataStartIndex + idx] << 24) | (respPayload[dataStartIndex + idx + 1] << 16) | (respPayload[dataStartIndex + idx + 2] << 8) | respPayload[dataStartIndex + idx + 3]);
            }

            return result;
        }

        /// <summary>
        /// Read a 64-bit signed integer (LINT) from a DB specified by a textual address.
        /// Delegates to the numeric overload after parsing the address string.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit signed value read from the PLC.</returns>
        public async Task<long> ReadInt64Async(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadInt64Async(dbNumber, byteOffset, bitIndex, cancellationToken);
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
        public async Task<long> ReadInt64Async(ushort dbNumber,int byteOffset,int bitIndex,CancellationToken cancellationToken = default)
        {
            // Request raw 8 bytes from the PLC and combine them into a signed 64-bit value.
            // Use ItemTransport.Byte so the PLC returns the data as an octet string / raw bytes.
            // 8 bytes requested for 64-bit values; for octet/real transports the helper expects dataUnitLength in bytes
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
        /// Read a 16-bit unsigned integer (WORD/UINT) from a DB specified by a textual address.
        /// Delegates to the numeric overload after parsing the address string.
        /// </summary>
        /// <param name="address">Textual DB address (for example "DB1.DBW0").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit unsigned value read from the PLC.</returns>
        public async Task<UInt16> ReadUInt16Async(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadUInt16Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }



        /// <summary>
        /// Read a 16-bit unsigned integer (WORD/UINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 16-bit unsigned value read from the PLC.</returns>
        public async Task<UInt16> ReadUInt16Async(ushort dbNumber,int byteOffset,int bitIndex,CancellationToken cancellationToken = default)
        {
            // WORD/UINT: request 2 bytes (helper will set parameter length to 16 bits)
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Word, 2);

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 16);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            UInt16 value = (ushort)((respPayload[dataStartIndex] << 8) | respPayload[dataStartIndex + 1]); 
            return value;

        }


        public async Task<UInt16[]> ReadUInt16ArrayAsync(string address, int count, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadUInt16ArrayAsync(dbNumber, byteOffset, bitIndex, count, cancellationToken);
        }

        public async Task<UInt16[]> ReadUInt16ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int count, CancellationToken cancellationToken = default)
        {
            if (!(count > 0))
            {
                throw new ArgumentException("Count must be greater than 0 for reading UInt16 array.");
            }

            if (bitIndex < 0 || bitIndex > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be in range 0..7.");
            }
            if (byteOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset), "Byte offset must be >= 0.");
            }
            int startByte = byteOffset;
            ushort bytesToRead = (ushort)(count * 2);

            // Read raw bytes (BYTE) starting at the first covered byte. bitIndex is irrelevant for byte reads.
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, startByte, 0, S7Types.ItemTransport.Byte, bytesToRead); //read the whole bytes

            Debug.WriteLine("S7 ReadVar request PDU for UInt16 array (BYTE read): " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine($"Response payload from BYTE read for UInt16 array: {BitConverter.ToString(respPayload)}");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, (ushort)(bytesToRead * 8)); // minimum bit length is number of bytes read * 8

            int dataStartIndex = dataHeaderStartIndex + 4;
            UInt16[] result = new UInt16[count]; // prepare result array and count is the number of Int16 values requested

            // Siemens bit numbering: DBX<Byte>.<Bit> where Bit 0 is the LSB of the byte.
            for (int i = 0; i < count; i++)
            {
                // i * 2 for each int16 value
                int idx = i * 2;
                // idx is an offset inside the data area. Ensure the two bytes for this Int16 are present
                if (dataStartIndex + idx + 1 >= respPayload.Length)
                {
                    throw new Exception("Response payload too short for requested UInt16 array.");
                }
                result[i] = (ushort)((respPayload[dataStartIndex + idx] << 8) | respPayload[dataStartIndex + idx + 1]);
            }

            return result;
        }



        public async Task<UInt32> ReadUInt32Async(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadUInt32Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read a 32-bit unsigned integer (DWORD/UDINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <returns>The 32-bit unsigned value read from the PLC.</returns>
        public async Task<UInt32> ReadUInt32Async(ushort dbNumber,int byteOffset,int bitIndex,CancellationToken cancellationToken = default)
        {
            // DWORD/UDINT: request 4 bytes (helper will set parameter length to 32 bits)
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

        public async Task<UInt32[]> ReadUInt32ArrayAsync(string address, int count, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadUInt32ArrayAsync(dbNumber, byteOffset, bitIndex, count, cancellationToken);
        }

        public async Task<UInt32[]> ReadUInt32ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int count, CancellationToken cancellationToken = default)
        {
            if (!(count > 0))
            {
                throw new ArgumentException("Count must be greater than 0 for reading UInt32 array.");
            }

            if (bitIndex < 0 || bitIndex > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be in range 0..7.");
            }
            if (byteOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset), "Byte offset must be >= 0.");
            }

            int startByte = byteOffset;
            ushort bytesToRead = (ushort)(count * 4);

            // Read raw bytes (BYTE) starting at the first covered byte. bitIndex is irrelevant for byte reads.
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, startByte, 0, S7Types.ItemTransport.Byte, bytesToRead); //read the whole bytes

            Debug.WriteLine("S7 ReadVar request PDU for UInt32 array (BYTE read): " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine($"Response payload from BYTE read for UInt32 array: {BitConverter.ToString(respPayload)}");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, (ushort)(bytesToRead * 8)); // minimum bit length is number of bytes read * 8

            int dataStartIndex = dataHeaderStartIndex + 4;
            UInt32[] result = new UInt32[count]; // prepare result array and count is the number of UInt32 values requested

            // Siemens bit numbering: DBX<Byte>.<Bit> where Bit 0 is the LSB of the byte.
            for (int i = 0; i < count; i++)
            {
                // i * 4 for each UInt32 value
                int idx = i * 4;
                // idx is an offset inside the data area. Ensure the four bytes for this UInt32 are present
                if (dataStartIndex + idx + 3 >= respPayload.Length)
                {
                    throw new Exception("Response payload too short for requested UInt32 array.");
                }
                result[i] = (uint)((respPayload[dataStartIndex + idx] << 24) | (respPayload[dataStartIndex + idx + 1] << 16) | (respPayload[dataStartIndex + idx + 2] << 8) | respPayload[dataStartIndex + idx + 3]);
            }

            return result;
        }

        /// <summary>
        /// Read a 64-bit unsigned integer (ULINT) from a DB specified by a textual address.
        /// Delegates to the numeric overload after parsing the address string.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit unsigned value read from the PLC.</returns>
        public async Task<ulong> ReadUInt64Async(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadUInt64Async(dbNumber, byteOffset, bitIndex, cancellationToken);
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
        public async Task<ulong> ReadUInt64Async(ushort dbNumber,int byteOffset,int bitIndex,CancellationToken cancellationToken = default)
        {
            // Request 8 raw octets for consistent cross-CPU behavior
            // 8 bytes requested for 64-bit 
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
        /// Read a 32-bit floating point value (REAL) from a DB specified by a textual address.
        /// Delegates to the numeric overload after parsing the address string.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit floating point value read from the PLC.</returns>
        public async Task<float> ReadFloat32Async(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadFloat32Async(dbNumber, byteOffset, bitIndex, cancellationToken);
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
        public async Task<float> ReadFloat32Async(ushort dbNumber,int byteOffset,int bitIndex, CancellationToken cancellationToken = default)
        {
            // Request 4 raw bytes (REAL) as octet string to ensure byte-level control
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Real, 4);

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

        public async Task<float[]> ReadFloat32ArrayAsync(string address, int count, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadFloat32ArrayAsync(dbNumber, byteOffset, bitIndex, count, cancellationToken);
        }

        public async Task<float[]> ReadFloat32ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int count, CancellationToken cancellationToken = default)
        {
            if (!(count > 0))
            {
                throw new ArgumentException("Count must be greater than 0 for reading Float32 array.");
            }

            if (bitIndex < 0 || bitIndex > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be in range 0..7.");
            }
            if (byteOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset), "Byte offset must be >= 0.");
            }

            int startByte = byteOffset;
            ushort bytesToRead = (ushort)(count * 4);

            // Read raw bytes (BYTE) starting at the first covered byte. bitIndex is irrelevant for byte reads.
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, startByte, 0, S7Types.ItemTransport.Byte, bytesToRead); //read the whole bytes

            Debug.WriteLine("S7 ReadVar request PDU for Float32 array (BYTE read): " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine($"Response payload from BYTE read for Float32 array: {BitConverter.ToString(respPayload)}");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, (ushort)(bytesToRead * 8)); // minimum bit length is number of bytes read * 8

            int dataStartIndex = dataHeaderStartIndex + 4;
            float[] result = new float[count]; // prepare result array and count is the number of Float32 values requested

            // Siemens bit numbering: DBX<Byte>.<Bit> where Bit 0 is the LSB of the byte.
            for (int i = 0; i < count; i++)
            {
                // i * 4 for each float32 value
                int idx = i * 4;
                // idx is an offset inside the data area. Ensure the four bytes for this Float32 are present
                if (dataStartIndex + idx + 3 >= respPayload.Length)
                {
                    throw new Exception("Response payload too short for requested Float32 array.");
                }
                byte[] valueBytes = new byte[4];
                Buffer.BlockCopy(respPayload, dataStartIndex + idx, valueBytes, 0, 4);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valueBytes);
                }
                result[i] = BitConverter.ToSingle(valueBytes, 0);
            }

            return result;
        }

        /// <summary>
        /// Read a 64-bit floating point value (LREAL/DOUBLE) from a DB specified by a textual address.
        /// Delegates to the numeric overload after parsing the address string.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit floating point value read from the PLC.</returns>
        public async Task<double> ReadFloat64Async(string address, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadFloat64Async(dbNumber, byteOffset, bitIndex, cancellationToken);
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
        public async Task<double> ReadFloat64Async(ushort dbNumber,int byteOffset,int bitIndex,CancellationToken cancellationToken = default)
        {
            // Request 8 raw octets to decode IEEE-754 double 
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

        public async Task<double[]> ReadFloat64ArrayAsync(string address, int count, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadFloat64ArrayAsync(dbNumber, byteOffset, bitIndex, count, cancellationToken);
        }

        public async Task<double[]> ReadFloat64ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int count, CancellationToken cancellationToken = default)
        {
            if (!(count > 0))
            {
                throw new ArgumentException("Count must be greater than 0 for reading Float64 array.");
            }

            if (bitIndex < 0 || bitIndex > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be in range 0..7.");
            }
            if (byteOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteOffset), "Byte offset must be >= 0.");
            }

            int startByte = byteOffset;
            ushort bytesToRead = (ushort)(count * 8);

            // Read raw bytes (BYTE) starting at the first covered byte. bitIndex is irrelevant for byte reads.
            byte[] pdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.ReadVar, dbNumber, startByte, 0, S7Types.ItemTransport.Byte, bytesToRead); //read the whole bytes

            Debug.WriteLine("S7 ReadVar request PDU for Float64 array (BYTE read): " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            Debug.WriteLine($"Response payload from BYTE read for Float64 array: {BitConverter.ToString(respPayload)}");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, (ushort)(bytesToRead * 8)); // minimum bit length is number of bytes read * 8

            int dataStartIndex = dataHeaderStartIndex + 4;
            double[] result = new double[count]; // prepare result array and count is the number of Float64 values requested

            // Siemens bit numbering: DBX<Byte>.<Bit> where Bit 0 is the LSB of the byte.
            for (int i = 0; i < count; i++)
            {
                // i * 8 for each float64 value
                int idx = i * 8;
                // idx is an offset inside the data area. Ensure the eight bytes for this Float64 are present
                if (dataStartIndex + idx + 7 >= respPayload.Length)
                {
                    throw new Exception("Response payload too short for requested Float64 array.");
                }
                byte[] valueBytes = new byte[8];
                Buffer.BlockCopy(respPayload, dataStartIndex + idx, valueBytes, 0, 8);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(valueBytes);
                }
                result[i] = BitConverter.ToDouble(valueBytes, 0);
            }

            return result;
        }

        /// <summary>
        /// Read a Siemens STRING from a DB specified by a textual address.
        /// Delegates to the numeric overload after parsing the address string.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="maxStringLength">Maximum expected string length (characters).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The decoded string read from the PLC.</returns>
        public async Task<string> ReadStringAsync(string address, int maxStringLength, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await ReadStringAsync(dbNumber, byteOffset, bitIndex, maxStringLength, cancellationToken);
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
        public async Task<string> ReadStringAsync(ushort dbNumber,int byteOffset,int bitIndex,int maxStringLength, CancellationToken cancellationToken = default)
        {
            // PLC may negotiate a small PDU (e.g. 240). Reading (maxStringLength + 2) bytes can exceed it and be rejected.
            // Robust approach: read STRING header (2 bytes) first, then read currentLength bytes in chunks.

            // 1) Read 2-byte STRING header (declaredMax, currentLength)
            byte[] headerPdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(
                FunctionCode.ReadVar,
                dbNumber,
                byteOffset,
                0,
                S7Types.ItemTransport.Byte,
                2);

            await _tpktCotp.SendPayloadAsync(headerPdu, cancellationToken);
            byte[] headerResp = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

            var (_, _, headerDataHeaderStart) = S7ProtocolHelpers.ValidateReadResponse(headerResp, 0x04, 0x01, 16);
            int headerDataStart = headerDataHeaderStart + 4;
            byte declaredMaxLength = headerResp[headerDataStart];
            byte currentLength = headerResp[headerDataStart + 1];

            if (declaredMaxLength == 0 || currentLength == 0)
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

            // 2) Read currentLength bytes starting at byteOffset+2 (string body)
            int remaining = currentLength;
            int readOffset = byteOffset + 2;
            byte[] stringBytes = new byte[currentLength];
            int dst = 0;

            // Conservative bound for data payload to fit within negotiated PDU.
            int maxChunk = Math.Max(1, _pduLength - 40);

            while (remaining > 0)
            {
                ushort chunk = (ushort)Math.Min(remaining, maxChunk);
                byte[] chunkPdu = S7ProtocolHelpers.BuildReadWriteSetupRequest(
                    FunctionCode.ReadVar,
                    dbNumber,
                    readOffset,
                    0,
                    S7Types.ItemTransport.Byte,
                    chunk);

                await _tpktCotp.SendPayloadAsync(chunkPdu, cancellationToken);
                byte[] chunkResp = await _tpktCotp.ReceivePayloadAsync(cancellationToken);

                var (_, _, chunkDataHeaderStart) = S7ProtocolHelpers.ValidateReadResponse(chunkResp, 0x04, 0x01, (ushort)(chunk * 8));
                int chunkDataStart = chunkDataHeaderStart + 4;

                Buffer.BlockCopy(chunkResp, chunkDataStart, stringBytes, dst, chunk);
                dst += chunk;
                remaining -= chunk;
                readOffset += chunk;
            }

            return Encoding.ASCII.GetString(stringBytes);
        }

        public async Task<string> WriteBoolArrayAsync(string address, bool value, uint length, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            int initialOffset = byteOffset * 8 + bitIndex;
            int localByteOffset = byteOffset ;
            int localBitIndex = bitIndex;
            for (int i = initialOffset; i < initialOffset + length; i++)
            {
                localByteOffset = i / 8;
                localBitIndex = i % 8;
                await WriteBoolAsync(dbNumber, localByteOffset, localBitIndex, value, cancellationToken);
            }
            return ConvertToAbsoluteAddress(dbNumber, localByteOffset, localBitIndex);
        }



        /// <summary>
        /// Write a boolean (single bit) to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteBoolAsync(dbNumber, byteOffset, bitIndex, value, cancellationToken);
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
        public async Task WriteBoolAsync(ushort dbNumber,int byteOffset,int bitIndex,bool value, CancellationToken cancellationToken = default)
        {
            // Build header+parameters for a WriteVar request (item transport = Bit, length = 1)
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
        /// Write a 16-bit signed integer (INT) to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteInt16Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 16-bit signed integer (INT) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteInt16Async(ushort dbNumber,int byteOffset,int bitIndex,short value, CancellationToken cancellationToken = default)
        {
            // INT: request 2 bytes (helper will encode parameter length as 16 bits)
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
        /// Write a 32-bit signed integer (DINT) to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteInt32Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }


        /// <summary>
        /// Write a 32-bit signed integer (DINT) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteInt32Async(ushort dbNumber,int byteOffset,int bitIndex,int value, CancellationToken cancellationToken = default)
        {
            // DINT: request 4 bytes (helper will encode parameter length as 32 bits)
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
        /// Write a 64-bit signed integer to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt64Async(string address, long value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteInt64Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
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
        public async Task WriteInt64Async(ushort dbNumber,int byteOffset,int bitIndex,long value, CancellationToken cancellationToken = default)
        {
            // Use octet string payload to write 8 bytes
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
        /// Write a 16-bit unsigned integer (WORD/UINT) to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt16Async(string address, ushort value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteUInt16Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 16-bit unsigned integer (WORD/UINT) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteUInt16Async(ushort dbNumber,int byteOffset,int bitIndex,ushort value, CancellationToken cancellationToken = default)
        {
            // WORD/UINT: request 2 bytes (helper will encode parameter length as 16 bits)
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
        /// Write a 32-bit unsigned integer (DWORD/UDINT) to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt32Async(string address, uint value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteUInt32Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }   

        /// <summary>
        /// Write a 32-bit unsigned integer (DWORD/UDINT) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteUInt32Async(ushort dbNumber,int byteOffset,int bitIndex,uint value, CancellationToken cancellationToken = default)
        {
            // DWORD/UDINT: request 4 bytes (helper will encode parameter length as 32 bits)
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
        /// Write a 64-bit unsigned integer (ULINT) to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt64Async(string address, ulong value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteUInt64Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
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
        public async Task WriteUInt64Async(ushort dbNumber,int byteOffset,int bitIndex,ulong value, CancellationToken cancellationToken = default)
        {
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
        /// Write a 32-bit floating point value (REAL) to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteFloat32Async(string address, float value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteFloat32Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit floating point value (REAL) to a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Uses S7ANY REAL transport with a BREAL (0x07) data block and big-endian IEEE-754 payload.
        /// </remarks>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteFloat32Async(ushort dbNumber,int byteOffset,int bitIndex,float value, CancellationToken cancellationToken = default)
        {
            // S7ANY transport REAL (0x08) must match the write data block (DataTransport.Real / BREAL).
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Real, 4);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromFloat(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }

        /// <summary>
        /// Write a 64-bit floating point value (LREAL/DOUBLE) to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteFloat64Async(string address, double value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteFloat64Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 64-bit floating point value (LREAL/DOUBLE) to a DB in the PLC.
        /// </summary>
        /// <remarks>
        /// Uses S7ANY BYTE transport for 8 bytes with an octet-string data block (BREAL is only for 4-byte REAL).
        /// </remarks>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="value">Value to write.</param>
        public async Task WriteFloat64Async(ushort dbNumber,int byteOffset,int bitIndex,double value, CancellationToken cancellationToken = default)
        {
            byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(FunctionCode.WriteVar, dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.LReal, 8);
            byte[] data = S7ProtocolHelpers.BuildWriteDataBlockFromDouble(value);
            byte[] pdu = new byte[headerAndParams.Length + data.Length];
            Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
            Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);
            await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
            S7ProtocolHelpers.ValidateWriteResponse(respPayload);
        }
        /// <summary>
        /// Write a Siemens STRING to a DB specified by a textual address.
        /// The address is parsed and the request is delegated to the numeric overload.
        /// </summary>
        /// <param name="address">Textual DB address.</param>
        /// <param name="maxStringLength">Declared maximum length for the STRING (characters).</param>
        /// <param name="value">String value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteStringAsync(string address, int maxStringLength, string value, CancellationToken cancellationToken = default)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            await WriteStringAsync(dbNumber, byteOffset, bitIndex, maxStringLength, value, cancellationToken);
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
        public async Task WriteStringAsync(ushort dbNumber,int byteOffset,int bitIndex,int maxStringLength, string value, CancellationToken cancellationToken = default)
        {
            if (value == null) value = string.Empty;
            byte[] strBytes = Encoding.ASCII.GetBytes(value);
            if (strBytes.Length > maxStringLength) throw new ArgumentException("String is longer than declared maxStringLength", nameof(value));

            // Only write the bytes we need: [declaredMax][currentLen][chars...]
            byte[] fullPayload = new byte[2 + strBytes.Length];
            fullPayload[0] = (byte)maxStringLength;
            fullPayload[1] = (byte)strBytes.Length;
            Buffer.BlockCopy(strBytes, 0, fullPayload, 2, strBytes.Length);

            // Conservative payload bound for negotiated PDU (keeps both request and response well below limit).
            int maxChunk = Math.Max(1, _pduLength - 40);

            int remaining = fullPayload.Length;
            int src = 0;
            int writeOffset = byteOffset;

            while (remaining > 0)
            {
                int chunkLen = Math.Min(remaining, maxChunk);
                byte[] chunk = new byte[chunkLen];
                Buffer.BlockCopy(fullPayload, src, chunk, 0, chunkLen);

                byte[] headerAndParams = S7ProtocolHelpers.BuildReadWriteSetupRequest(
                    FunctionCode.WriteVar,
                    dbNumber,
                    writeOffset,
                    0,
                    S7Types.ItemTransport.Byte,
                    (ushort)chunkLen);

                // For DB byte writes, many PLCs expect transport "BYTE/WORD/DWORD" (0x04) with length in bits.
                // Using OctetString (0x09) can be rejected with return code 0x07.
                byte[] data = S7ProtocolHelpers.BuildWriteDataBlock(S7Types.DataTransport.ByteWordDword, (ushort)(chunkLen * 8), chunk);

                byte[] pdu = new byte[headerAndParams.Length + data.Length];
                Buffer.BlockCopy(headerAndParams, 0, pdu, 0, headerAndParams.Length);
                Buffer.BlockCopy(data, 0, pdu, headerAndParams.Length, data.Length);

                await _tpktCotp.SendPayloadAsync(pdu, cancellationToken);
                byte[] respPayload = await _tpktCotp.ReceivePayloadAsync(cancellationToken);
                S7ProtocolHelpers.ValidateWriteResponse(respPayload);

                remaining -= chunkLen;
                src += chunkLen;
                writeOffset += chunkLen;
            }
        }
    }

}
