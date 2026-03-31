using System;
using System.Collections.Generic;
using System.Text;

namespace SYS8.Core.Protocol
{
    internal static class S7ProtocolHelpers
    {
        //internal: accessible in the same project
        private static byte _lastSequence = 0; // for tracking sequence numbers in messages
        internal static byte NextSequence()
        { 
            _lastSequence++;
            if (_lastSequence == 0)
            {
                _lastSequence = 1; // sequence numbers start at 1, wrap around to 1 after 255
            }
            return _lastSequence;
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
        /// Build ReadVar request PDU for reading a boolean value from the PLC.
        /// </summary>
        /// <param name="dbNumber">Datablock Numbers</param>
        /// <param name="byteOffset">The index of bytes the user want to read from</param>
        /// <param name="bitIndex">The index of bit that the user want to read from that byte</param>
        /// <param name="transportSize">Transport size of data (0x01 for bit, 0x02 for Int16, etc)</param>
        /// <param name="dataLength">Length of data</param>
        /// <returns></returns>
        internal static byte[] BuildReadRequest(ushort dbNumber, int byteOffset, int bitIndex, byte transportSize, ushort dataLength)
        {
            byte[] header = BuildS7Header(0x01, 14, 0); // Job, 14 bytes of parameters, 0 bytes of data
            byte[] parameters = new byte[14];
            parameters[0] = 0x04; // Read Var function code
            parameters[1] = 0x01; // Number of items to read
            parameters[2] = 0x12; // Variable specification for S7 (0x12 means S7 variable)
            parameters[3] = 0x0A; // Length of following address specification (10 bytes for S7)
            parameters[4] = 0x10; // Syntax ID for S7Any
            parameters[5] = transportSize; // Transport size asking what type we want to read
            parameters[6] = (byte)(dataLength >> 8); parameters[7] = (byte)(dataLength & 0xFF); // Length of data to read
            parameters[8] = (byte)(dbNumber >> 8);
            parameters[9] = (byte)(dbNumber & 0xFF); // DB number
            parameters[10] = 0x84; // DB number specifier
            int bitAddress = (byteOffset * 8) + bitIndex; // calculate bit address from byte offset and bit index
            parameters[11] = (byte)((bitAddress >> 16) & 0xFF);
            parameters[12] = (byte)((bitAddress >> 8) & 0xFF);
            parameters[13] = (byte)(bitAddress & 0xFF); // Address within DB for the bit to read

            //Combine header and parameters into a single PDU
            byte[] pdu = new byte[header.Length + parameters.Length];
            Buffer.BlockCopy(header, 0, pdu, 0, header.Length);
            Buffer.BlockCopy(parameters, 0, pdu, header.Length, parameters.Length);
            return pdu;


            /*
             * Problem: Work with S7-1500 but not S7-1200
             * The problem may lie under TIA portal which access is not granted in S7-1200 due to error 81 04
             * 
            Fix to try:
            - header changed to 4 bytes of data
            - pack 00 transprotSize 00 and 01 with it

            Try and failed to get response from s7-1200 but works for s7-1500
            */

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
            ushort bitLen = (ushort)((respPayload[dataHeaderStartIndex + 2] << 8) | respPayload[dataHeaderStartIndex + 3]);

            //Check transportSize == S7Types.DataTransport.(type)

            if (returnCode != 0xFF)
            {
                throw new Exception($"ReadVar failed, return code: 0x{returnCode:X2}");
            }

            if (bitLen < minimumBitLength)
            {
                throw new Exception($"ReadVar response indicates less than {minimumBitLength} bits of data.");
            
            }

            int databyte = (bitLen + 7) / 8; // calculate how many bytes of data are returned for the bits read (should be 1 byte for a single bit)

            // check if response contains enough bytes for the data based on bit length 
            // The data bytes are sent after the 4 bytes.
            if (respPayload.Length < dataHeaderStartIndex + 4 + databyte)
            {
                throw new Exception("ReadVar response missing data bytes.");
            }

            return (paramLength, dataLength, dataHeaderStartIndex);
        }
    }
}
