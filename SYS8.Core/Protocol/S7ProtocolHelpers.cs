using System;
using System.Collections.Generic;
using System.Text;

namespace SYS8.Core.Protocol
{
    internal static class S7ProtocolHelpers
    {
        //internal: accessible in the same project
        private static byte _lastSequence = 0; // for tracking sequence numbers in messages
        private static readonly object _seqLock = new object();
        internal static byte NextSequence()
        {
            // Use a simple lock to make sequence generation thread-safe and preserve
            // the original wrap-around semantics (1..255).
            lock (_seqLock)
            {
                _lastSequence++;
                if (_lastSequence == 0)
                {
                    _lastSequence = 1; // sequence numbers start at 1, wrap around to 1 after 255
                }
                return _lastSequence;
            }
        }


        /// <summary>
        /// Helper funcion: Template for building S7 header
        /// </summary>
        /// <param name="rosctr">What are the type of message sending to the PLC</param>
        /// <param name="paramLength">Length of the parameters</param>
        /// <param name="dataLength">Length of data sending to the PLC</param>
        /// <remarks>
        /// ROSCTR (Request/Response Control) indicates the type of S7 message, such as Job (0x01), Ack_Data (0x03), etc.
        /// ParamLength and DataLength are included in the S7 header to specify how many bytes of parameters and data are included in the message.
        /// For Setup Communication and Read, dataLength is typically 0 for requests, while writing data to the PLC would include the data length in the header
        /// </remarks>
        /// <returns>bytes of s7 header</returns>
        internal static byte[] BuildS7Header(byte rosctr, ushort paramLength, ushort dataLength)
        {
            byte[] header = new byte[10];
            header[0] = 0x32; // Protocol ID for S7
            header[1] = rosctr; // ROSCTR: message type (e.g., Job, Ack_Data, etc.)
            header[2] = 0x00; // Redundancy identification -- unused padding
            header[3] = 0x00; // Redundancy identification
            byte seq = NextSequence();
            header[4] = 0x00; header[5] = seq; // Sequence number
            header[6] = (byte)(paramLength >> 8); header[7] = (byte)(paramLength & 0xFF); // Parameter length
            header[8] = (byte)(dataLength >> 8); header[9] = (byte)(dataLength & 0xFF); // Data length
            return header;
        }


        /// <summary>
        /// Build Read or Write request PDU header+parameters for S7 operations.
        /// This helper centralizes common parameter construction and adjusts the S7 header data length
        /// depending on whether the request is a Read (no data payload) or Write (includes 4-byte data header + payload length).
        /// </summary>
        /// <param name="function">Function code (ReadVar or WriteVar)</param>
        /// <param name="dbNumber">Datablock Numbers</param>
        /// <param name="byteOffset">The index of bytes the user want to read/write from</param>
        /// <param name="bitIndex">The index of bit within the byte</param>
        /// <param name="transportSize">S7Any item transport size (written into parameter[5]).</param>
        /// <param name="dataUnitLength">Requested data size in BYTES for byte-aligned types (BYTE/CHAR/WORD/INT/DWORD/DINT/REAL).
        /// For BIT this should be the number of bits (normally 1). This method converts to the correct S7Any element count field.</param>
        /// <returns>Combined header+parameters PDU</returns>
        internal static byte[] BuildReadWriteSetupRequest(FunctionCode function, ushort dbNumber, int byteOffset, int bitIndex, byte transportSize, ushort dataUnitLength)
        {
            if (dataUnitLength == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dataUnitLength), "Requested length must be > 0.");
            }

            // S7Any length field (parameter[6..7]) is an ELEMENT COUNT, not a byte count, for most types.
            // For BIT it is the number of bits; for BYTE/CHAR it is number of bytes.
            // For WORD/INT it is number of 16-bit elements; for DWORD/DINT/REAL it is number of 32-bit elements.
            ushort elementCount;
            int payloadBytes;

            if (transportSize == S7Types.ItemTransport.Bit)
            {
                // For BIT, caller passes number of bits (normally 1).
                elementCount = dataUnitLength;
                payloadBytes = (dataUnitLength + 7) / 8;
            }
            else if (transportSize == S7Types.ItemTransport.Byte || transportSize == S7Types.ItemTransport.Char)
            {
                // BYTE/CHAR: caller passes bytes, and elementCount is bytes.
                elementCount = dataUnitLength;
                payloadBytes = dataUnitLength;
            }
            else if (transportSize == S7Types.ItemTransport.Word || transportSize == S7Types.ItemTransport.Int)
            {
                // WORD/INT: caller passes bytes (2 per element).
                if ((dataUnitLength % 2) != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(dataUnitLength), "WORD/INT requests must be a multiple of 2 bytes.");
                }
                elementCount = (ushort)(dataUnitLength / 2);
                payloadBytes = dataUnitLength;
            }
            else if (transportSize == S7Types.ItemTransport.DWord || transportSize == S7Types.ItemTransport.DInt || transportSize == S7Types.ItemTransport.Real || transportSize == S7Types.ItemTransport.LReal)
            {
                // DWORD/DINT/REAL: caller passes bytes (4 per element for these S7Any types).
                if ((dataUnitLength % 4) != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(dataUnitLength), "DWORD/DINT/REAL requests must be a multiple of 4 bytes.");
                }
                elementCount = (ushort)(dataUnitLength / 4);
                payloadBytes = dataUnitLength;
            }
            else
            {
                // Unknown/unsupported item transport size; fall back to treating as raw bytes to avoid silently building a malformed PDU.
                throw new ArgumentOutOfRangeException(nameof(transportSize), $"Unsupported S7Any item transport size: 0x{transportSize:X2}");
            }
            


            // Determine S7 header data length: read requests include no data payload (0),
            // write requests include 4 bytes of data header + payload length in bytes.
            ushort s7DataLength = function == FunctionCode.ReadVar ? (ushort)0 : (ushort)(4 + payloadBytes);

            byte[] header = BuildS7Header(0x01, 14, s7DataLength); // Job, 14 bytes of parameters, data length depends on function
            byte[] parameters = new byte[14];
            parameters[0] = (byte)function; // ReadVar (0x04) or WriteVar (0x05)
            parameters[1] = 0x01; // Number of items
            parameters[2] = 0x12; // Variable specification for S7
            parameters[3] = 0x0A; // Length of following address specification
            parameters[4] = 0x10; // Syntax ID for S7Any
            parameters[5] = transportSize; // Transport size
            parameters[6] = (byte)(elementCount >> 8);
            parameters[7] = (byte)(elementCount & 0xFF); // Length (element count / bits for BIT)
            parameters[8] = (byte)(dbNumber >> 8);
            parameters[9] = (byte)(dbNumber & 0xFF); // DB number
            parameters[10] = 0x84; // DB area specifier
            int bitAddress = (byteOffset * 8) + bitIndex;
            parameters[11] = (byte)((bitAddress >> 16) & 0xFF);
            parameters[12] = (byte)((bitAddress >> 8) & 0xFF);
            parameters[13] = (byte)(bitAddress & 0xFF);

            // Combine header and parameters into a single PDU
            byte[] pdu = new byte[header.Length + parameters.Length];
            Buffer.BlockCopy(header, 0, pdu, 0, header.Length);
            Buffer.BlockCopy(parameters, 0, pdu, header.Length, parameters.Length);
            return pdu;
        }

        internal static (ushort paramLength, ushort dataLength, int dataStartIndex) ValidateReadResponse(byte[] respPayload, byte expectedFunctionCode, byte expectedItemCount, ushort minimumBitLength) 
        {

            if (respPayload == null) 
            {
                throw new ArgumentNullException(nameof(respPayload));
            }

            if (respPayload.Length < 18)
            {
                throw new Exception("S7 Read response too short.");
            }

            if (respPayload[0] != 0x32)
            {
                throw new Exception("Invalid S7 protocol ID in read response.");
            }

            if (respPayload[1] != 0x03) // rosctr == Ack_Data
            {
                throw new Exception($"Unexpected ROSCTR in read response: 0x{respPayload[1]:X2}");
            }

            ushort paramLength = (ushort)((respPayload[6] << 8) | respPayload[7]);
            ushort dataLength = (ushort)((respPayload[8] << 8) | respPayload[9]);

            if (paramLength < 2)
            {
                throw new Exception("ReadVar response parameter block too short.");
            }

            // Testing Code
            // Some S7 models (like S7-1500) include 2 bytes of padding between the header and parameters, while others may not.
            // To handle this variety, we calculate the padding dynamically based on the total length of the response and the lengths of the header, parameters, and data as specified in the header.
            // As we know the header is always 10 bytes, and paramLength and dataLength are specified in the header.
            // Initially, we subtract the header length, parameter length, and data length from the total response length to calculate padding, 
            // and then calculate pIndex by adding the header length and padding together.
            // To simplify, we can break down pIndex as 10 + (total length - 10 - paramLength - dataLength) to respPayload.Length - (paramLength + dataLength)
            int pIndex = respPayload.Length - (paramLength + dataLength); // parameters start after header and any padding

            if (pIndex < 10 || pIndex + paramLength > respPayload.Length)
            {
                throw new Exception("ReadVar response parameter block index is invalid.");
            }

            byte functionCode = respPayload[pIndex];

            if (functionCode != expectedFunctionCode)
            {
                throw new Exception($"Unexpected function code in read response parameters: 0x{functionCode:X2}");
            }

            byte itemCount = respPayload[pIndex + 1];
            if (itemCount != expectedItemCount)
            {
                throw new Exception("Unexpected item count in read response parameters. Expected 1, got " + itemCount);
            }


            // Can optimize and refine


            // Data

            int dataHeaderStartIndex = pIndex + paramLength; // data starts after header and parameters
            if (respPayload.Length < dataHeaderStartIndex + dataLength) // + 4 for data sent after parameters which includes return code, transport size, and bit length (2bytes) for the data read
            {
                throw new Exception("Read response data block too short.");
            }

            byte returnCode = respPayload[dataHeaderStartIndex];
            byte transportSize = respPayload[dataHeaderStartIndex + 1];
            ushort lenField = (ushort)((respPayload[dataHeaderStartIndex + 2] << 8) | respPayload[dataHeaderStartIndex + 3]);

            // Check return code
            if (returnCode != 0xFF)
            {
                throw new Exception($"ReadVar failed, return code: 0x{returnCode:X2}");
            }

            // The length field meaning varies by transport size. For most transport sizes the length is in bits.
            // For Real and OctetString transport sizes the length field is expressed in bytes.
            int lengthInBits;
            int dataBytes;
            if (transportSize == S7Types.DataTransport.Real || transportSize == S7Types.DataTransport.OctetString)
            {
                // lenField contains number of bytes for these transport types
                dataBytes = lenField;
                lengthInBits = dataBytes * 8;
            }
            else
            {
                // lenField contains number of bits
                lengthInBits = lenField;
                dataBytes = (lenField + 7) / 8;
            }

            if (lengthInBits < minimumBitLength)
            {
                throw new Exception($"ReadVar response indicates less than {minimumBitLength} bits of data.");
            }

            // check if response contains enough bytes for the data based on computed dataBytes
            // The data bytes are sent after the 4-byte data header.
            if (respPayload.Length < dataHeaderStartIndex + 4 + dataBytes)
            {
                throw new Exception("ReadVar response missing data bytes.");
            }

            return (paramLength, dataLength, dataHeaderStartIndex);
        }

        /// <summary>
        /// Validate a WriteVar response from the PLC.
        /// Throws an exception when the response is invalid or indicates a failure.
        /// </summary>
        /// <param name="respPayload">Full S7 response payload (header + params + data).</param>
        internal static void ValidateWriteResponse(byte[] respPayload)
        {
            if (respPayload == null)
                throw new ArgumentNullException(nameof(respPayload));

            if (respPayload.Length < 12)
                throw new Exception("S7 Write response too short.");

            if (respPayload[0] != 0x32)
                throw new Exception("Invalid S7 protocol ID in write response.");

            byte rosctr = respPayload[1];
            if (rosctr == 0x02)
            {
                // ACK without data: error class/code are present directly after the 10-byte header.
                byte errorClass = respPayload.Length > 10 ? respPayload[10] : (byte)0x00;
                byte errorCode = respPayload.Length > 11 ? respPayload[11] : (byte)0x00;
                throw new Exception($"WriteVar rejected by PLC (ROSCTR=0x02 ACK). ErrorClass=0x{errorClass:X2}, ErrorCode=0x{errorCode:X2}.");
            }

            if (rosctr != 0x03)
                throw new Exception($"Unexpected ROSCTR in write response: 0x{rosctr:X2}");

            ushort paramLength = (ushort)((respPayload[6] << 8) | respPayload[7]);
            ushort dataLength = (ushort)((respPayload[8] << 8) | respPayload[9]);

            int pIndex = respPayload.Length - (paramLength + dataLength);
            if (pIndex < 10 || pIndex + paramLength > respPayload.Length)
                throw new Exception("WriteVar response parameter block index is invalid.");

            int dIndex = pIndex + paramLength;
            if (dIndex >= respPayload.Length)
                throw new Exception("WriteVar response data index out of range.");

            byte returnCode = respPayload[dIndex];
            if (returnCode != 0xFF)
                throw new Exception($"WriteVar failed, return code: 0x{returnCode:X2}");
        }

        /// <summary>
        /// Build the write-data block (data header + payload) for a WriteVar request.
        /// </summary>
        /// <param name="dataTransport">S7 data transport code (from S7Types.DataTransport).</param>
        /// <param name="length">Length field meaning depends on transport: bytes for Real/OctetString; bits otherwise.</param>
        /// <param name="payload">Payload bytes to send (raw octets for octet-string or big-endian encoded values).</param>
        internal static byte[] BuildWriteDataBlock(byte dataTransport, ushort length, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            // Determine expected payload length in bytes based on transport
            int expectedBytes;
            if (dataTransport == S7Types.DataTransport.Real || dataTransport == S7Types.DataTransport.OctetString)
            {
                expectedBytes = length; // length is in bytes
            }
            else
            {
                expectedBytes = (length + 7) / 8; // length is in bits
            }

            if (payload.Length != expectedBytes)
            {
                throw new ArgumentException($"Payload length {payload.Length} does not match expected {expectedBytes} for transport 0x{dataTransport:X2} and length {length}.");
            }

            byte[] data = new byte[4 + payload.Length];
            data[0] = 0x00; // reserved
            data[1] = dataTransport;
            data[2] = (byte)(length >> 8);
            data[3] = (byte)(length & 0xFF);
            Buffer.BlockCopy(payload, 0, data, 4, payload.Length); //adding payload after the 4-byte data header
            return data;
        }

        internal static byte[] BuildWriteDataBlockFromBool(bool value)
        {
            byte[] payload = new byte[1] { value ? (byte)0x01 : (byte)0x00 };
            return BuildWriteDataBlock(S7Types.DataTransport.Bit, 1, payload);
        }

        internal static byte[] BuildWriteDataBlockFromInt16(short value)
        {
            byte[] payload = new byte[2];
            payload[0] = (byte)((value >> 8) & 0xFF);
            payload[1] = (byte)(value & 0xFF);
            return BuildWriteDataBlock(S7Types.DataTransport.Integer, 16, payload);
        }

        internal static byte[] BuildWriteDataBlockFromInt32(int value)
        {
            byte[] payload = new byte[4];
            payload[0] = (byte)((value >> 24) & 0xFF);
            payload[1] = (byte)((value >> 16) & 0xFF);
            payload[2] = (byte)((value >> 8) & 0xFF);
            payload[3] = (byte)(value & 0xFF);
            return BuildWriteDataBlock(S7Types.DataTransport.Integer, 32, payload);
        }

        internal static byte[] BuildWriteDataBlockFromInt64(long value)
        {
            byte[] payload = new byte[8];
            payload[0] = (byte)((value >> 56) & 0xFF);
            payload[1] = (byte)((value >> 48) & 0xFF);
            payload[2] = (byte)((value >> 40) & 0xFF);
            payload[3] = (byte)((value >> 32) & 0xFF);
            payload[4] = (byte)((value >> 24) & 0xFF);
            payload[5] = (byte)((value >> 16) & 0xFF);
            payload[6] = (byte)((value >> 8) & 0xFF);
            payload[7] = (byte)(value & 0xFF);
            // Use octet string for 8-byte payload
            return BuildWriteDataBlock(S7Types.DataTransport.OctetString, 8, payload);
        }

        internal static byte[] BuildWriteDataBlockFromUInt16(ushort value)
        {
            byte[] payload = new byte[2];
            payload[0] = (byte)((value >> 8) & 0xFF);
            payload[1] = (byte)(value & 0xFF);
            return BuildWriteDataBlock(S7Types.DataTransport.Integer, 16, payload);
        }

        internal static byte[] BuildWriteDataBlockFromUInt32(uint value)
        {
            byte[] payload = new byte[4];
            payload[0] = (byte)((value >> 24) & 0xFF);
            payload[1] = (byte)((value >> 16) & 0xFF);
            payload[2] = (byte)((value >> 8) & 0xFF);
            payload[3] = (byte)(value & 0xFF);
            return BuildWriteDataBlock(S7Types.DataTransport.Integer, 32, payload);
        }

        internal static byte[] BuildWriteDataBlockFromUInt64(ulong value)
        {
            byte[] payload = new byte[8];
            payload[0] = (byte)((value >> 56) & 0xFF);
            payload[1] = (byte)((value >> 48) & 0xFF);
            payload[2] = (byte)((value >> 40) & 0xFF);
            payload[3] = (byte)((value >> 32) & 0xFF);
            payload[4] = (byte)((value >> 24) & 0xFF);
            payload[5] = (byte)((value >> 16) & 0xFF);
            payload[6] = (byte)((value >> 8) & 0xFF);
            payload[7] = (byte)(value & 0xFF);
            return BuildWriteDataBlock(S7Types.DataTransport.OctetString, 8, payload);
        }

        internal static byte[] BuildWriteDataBlockFromFloat(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BuildWriteDataBlock(S7Types.DataTransport.Real, 4, bytes);
        }

        internal static byte[] BuildWriteDataBlockFromDouble(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            // LREAL is 8 bytes; S7 BREAL (0x07) is for 4-byte REAL. Use octet string like other 8-byte raw writes.
            return BuildWriteDataBlock(S7Types.DataTransport.OctetString, 8, bytes); //Changed from Real to OctetString
        }

        internal static byte[] BuildWriteDataBlockFromString(int maxLength, string value)
        {
            if (value == null) value = string.Empty;
            byte[] strBytes = Encoding.ASCII.GetBytes(value);
            if (strBytes.Length > maxLength) throw new ArgumentException("String is longer than declared maxLength", nameof(value));
            byte[] payload = new byte[2 + strBytes.Length];
            payload[0] = (byte)maxLength; // declared max length
            payload[1] = (byte)strBytes.Length; // current length
            Buffer.BlockCopy(strBytes, 0, payload, 2, strBytes.Length);
            return BuildWriteDataBlock(S7Types.DataTransport.OctetString, (ushort)payload.Length, payload);
        }
    }
}
