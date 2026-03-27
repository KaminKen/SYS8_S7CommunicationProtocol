using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SYS8.Core.Protocol
{
    public class S7ProtocolLayer
    {
        private readonly TpktCotpLayer _tpktCotp;
        private ushort _pduLength = 0x480; //default, will be negotiated

        private byte _lastSequence = 0; // for tracking sequence numbers in messages

        public S7ProtocolLayer(TpktCotpLayer tpktCotp)
        {
            _tpktCotp = tpktCotp;
        }

        private byte NextSequence()
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
        private byte[] BuildS7Header(byte rosctr, ushort paramLength, ushort dataLength)
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
        private byte[] BuildReadRequest(ushort dbNumber, int byteOffset, int bitIndex, byte transportSize, ushort dataLength)
        {
            byte[] header = BuildS7Header(0x01, 14, 0); // Job, 14 bytes of parameters, 0 bytes of data
            byte[] parameters = new byte[14];
            parameters[0] = 0x04; // Read Var function code
            parameters[1] = 0x01; // Number of items to read
            parameters[2] = 0x12; // Variable specification for S7 (0x12 means S7 variable)
            parameters[3] = 0x0A; // Length of following address specification (10 bytes for S7)
            parameters[4] = 0x10; // Syntax ID for S7Any
            parameters[5] = transportSize; // Transport size for the type being read (e.g., 0x01 for BIT, 0x02 for INT16, etc.)
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
            Fix to try:
            - header changed to 4 bytes of data
            - pack 00 transprotSize 00 and 01 with it

            Try and failed to get response from s7-1200 but works for s7-1500
            */

        }


        /// <summary>
        /// Must be called once after COTP ConnectAsync().
        /// Negotiates PDU length with the PLC.
        /// </summary>
        public async Task SetupCommunicationAsync()
        {
            //Build S7 Setup Communication request
            //S7 header is 10 bytes, followed by parameters and data

            Debug.WriteLine("Building S7 Setup Communication request...");

            //S7Header
            byte[] header = BuildS7Header(0x01, 8, 0); //Setup Communication is a Job (0x01) with 8 bytes of parameters and 0 bytes of data

            //Parameters for Setup Communication
            byte[] parameters = new byte[8];
            parameters[0] = 0xF0; // Function code for Setup Communication
            parameters[1] = 0x00; // Reserved
            parameters[2] = 0x00; parameters[3] = 0x01; // Max AMQ caller
            parameters[4] = 0x00; parameters[5] = 0x01; // Max AMQ callee
            parameters[6] = 0x04; parameters[7] = 0x80; // PDU length = 0x0480 

            //Combine S7 PDU
            byte[] s7Pdu = new byte[header.Length + parameters.Length];
            Buffer.BlockCopy(header, 0, s7Pdu, 0, header.Length);
            Buffer.BlockCopy(parameters, 0, s7Pdu, header.Length, parameters.Length);

            Debug.WriteLine("S7 SetupComm request PDU: " + BitConverter.ToString(s7Pdu));
            //send via tpkt and cotp layer
            await _tpktCotp.SendPayloadAsync(s7Pdu);

            Debug.WriteLine("S7 Setup Communication request sent, waiting for response...");

            //receive response
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

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


        public ushort NegotiatedPduLength => _pduLength; // expose negotiated PDU length obtained from PLC in class to public, so that it can be used by higher layers when building messages


        //TODO: optimize and refine. Possibly create helper function for all Read for checking header and params sent from PLC as they are mostly the same

        /// <summary>
        /// Read boolean value from the PLC
        /// </summary>
        /// <param name="dbNumber">Datablock Numbers</param>
        /// <param name="byteOffset">The index of bytes the user want to read from</param>
        /// <param name="bitIndex">The index of bit that the user want to read from that byte</param>
        /// <returns>bool</returns>
        /// <exception cref="Exception"></exception>
        public async Task<bool> ReadBoolAsync(ushort dbNumber, int byteOffset, int bitIndex)
        {
            Debug.WriteLine($"Reading boolean from DB{dbNumber}.DBX{byteOffset}.{bitIndex}...");

            byte[] pdu = BuildReadRequest(dbNumber, byteOffset, bitIndex, 0x01, 1); // transport size 0x01 for bit, data length 1 bit

            Debug.WriteLine("S7 ReadVar request PDU: " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu);

            Debug.WriteLine($"Reading boolean from DB{dbNumber}.DBX{byteOffset}.{bitIndex}...");

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));
            Debug.WriteLine("S7 Read response received, validating response...");

            //Header check

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


            //Parameters function and item counts

            // int pIndex = 12; // parameters start at index 12 (10 bytes header + 2 bytes padding)

            // Testing Code
            // Some S7 models (like S7-1500) include 2 bytes of padding between the header and parameters, while others may not.
            // To handle this variety, we calculate the padding dynamically based on the total length of the response and the lengths of the header, parameters, and data as specified in the header.
            // As we know the header is always 10 bytes, and paramLength and dataLength are specified in the header.
            // Initially, we subtract the header length, parameter length, and data length from the total response length to calculate padding, 
            // and then calculate pIndex by adding the header length and padding together.
            // To simplify, we can break down pIndex as 10 + (total length - 10 - paramLength - dataLength) to respPayload.Length - (paramLength + dataLength)
            int pIndex = respPayload.Length - (paramLength + dataLength); // parameters start after header and any padding, which is total length minus param and data length

            byte functionCode = respPayload[pIndex];
            if (functionCode != 0x04)
            {
                throw new Exception($"Unexpected function code in read response parameters: 0x{functionCode:X2}");
            }

            byte itemCount = respPayload[pIndex + 1];
            if (itemCount != 0x01)
            {
                throw new Exception("Unexpected item count in read response parameters. Expected 1, got " + itemCount);
            }

            // Data

            int dIndex = pIndex + paramLength; // data starts after header and parameters
            if (respPayload.Length < dIndex + dataLength) // + 4 for data sent after parameters which includes return code, transport size, and bit length (2bytes) for the data read
            {
                throw new Exception("Read response data block too short.");
            }

            byte returnCode = respPayload[dIndex];
            byte transportSize = respPayload[dIndex + 1];
            ushort bitLen = (ushort)((respPayload[dIndex + 2] << 8) | respPayload[dIndex + 3]);

            if (returnCode != 0xFF)
            {
                throw new Exception($"ReadVar failed, return code: 0x{returnCode:X2}");
            }

            if (bitLen < 1)
            {
                throw new Exception("ReadVar response indicates 0 bits of data, expected at least 1 bit for boolean read.");
            }

            int databyte = (bitLen + 7) / 8; // calculate how many bytes of data are returned for the bits read (should be 1 byte for a single bit)

            // check if response contains enough bytes for the data based on bit length 
            // The data bytes are sent after the 4 bytes.
            if (respPayload.Length < dIndex + 4 + databyte)
            {
                throw new Exception("ReadVar response missing data bytes.");
            }

            byte valueByte = respPayload[dIndex + 4]; // First value byte is the 4th index after data start.

            //shift 00000001 to left by bitIndex digit so it would become something like 00000100 if bitIndex is 2,
            //then do bitwise AND with the value byte to extract the specific bit value,
            //and check if it's not equal to 0 to determine if the bit is true or false.
            bool value = (valueByte & (1 << (bitIndex & 0x07))) != 0; // extract the specific bit value from the byte based on bit index

            return value;

        }


        //TODO: optimize and refine. Possibly create helper function for all Read for checking header and params sent from PLC as they are mostly the same
        public async Task<Int16> ReadInt16Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            // Similar to ReadBoolAsync, but with parameters set for reading a 16-bit integer
            // transport size = 0x02 for INT, 4 data params and data length = 2 bytes
            // The data parsing would also need to be adjusted to read 2 bytes of data and convert it to an Int16 value.
            byte[] pdu = BuildReadRequest(dbNumber, byteOffset, bitIndex, 0x02, 2); // transport size 0x02 for Int16, data length 2 bytes

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

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

            int pIndex = respPayload.Length - (paramLength + dataLength); // parameters start after header and any padding

            byte functionCode = respPayload[pIndex];

            if (functionCode != 0x04)
            {
                throw new Exception($"Unexpected function code in read response parameters: 0x{functionCode:X2}");
            }

            byte itemCount = respPayload[pIndex + 1];
            if (itemCount != 0x01)
            {
                throw new Exception("Unexpected item count in read response parameters. Expected 1, got " + itemCount);
            }


            // Can optimize and refine


            // Data

            int dIndex = pIndex + paramLength; // data starts after header and parameters
            if (respPayload.Length < dIndex + dataLength) // + 4 for data sent after parameters which includes return code, transport size, and bit length (2bytes) for the data read
            {
                throw new Exception("Read response data block too short.");
            }

            byte returnCode = respPayload[dIndex];
            byte transportSize = respPayload[dIndex + 1];
            ushort bitLen = (ushort)((respPayload[dIndex + 2] << 8) | respPayload[dIndex + 3]);

            if (returnCode != 0xFF)
            {
                throw new Exception($"ReadVar failed, return code: 0x{returnCode:X2}");
            }

            if (bitLen < 2)
            {
                throw new Exception("ReadVar response indicates less than 2 bits of data, expected at least 2 bit for Int16 read.");
            }

            int databyte = (bitLen + 7) / 8; // calculate how many bytes of data are returned for the bits read (should be 1 byte for a single bit)

            // check if response contains enough bytes for the data based on bit length 
            // The data bytes are sent after the 4 bytes.
            if (respPayload.Length < dIndex + 4 + databyte)
            {
                throw new Exception("ReadVar response missing data bytes.");
            }

            int dataStartIndex = dIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            Int16 value = (short)((respPayload[dataStartIndex] << 8) | respPayload[dataStartIndex + 1]); // combine 2 bytes of data into an Int16 value
            return value;

        }



        /// <summary>
        /// Write boolean value to PLC
        /// </summary>
        /// <param name="dbNumber">Datablock Numbers</param>
        /// <param name="byteOffset">The index of bytes the user want to read from</param>
        /// <param name="bitIndex">The index of bit that the user want to read from that byte</param>
        /// <param name="value">true or false</param>
        /// <exception cref="Exception"></exception>
        public async Task WriteBoolAsync(ushort dbNumber, int byteOffset, int bitIndex, bool value)
        {
            // Similar to ReadBoolAsync, but with function code for Write Var and including the value in the data section of the message.
            byte[] header = BuildS7Header(0x01, 14, 5); // 4 bytes for data header and 1 byte for the boolean value
            byte[] parameters = new byte[14];
            parameters[0] = 0x05;    // Write Var
            parameters[1] = 0x01;    // 1 item

            parameters[2] = 0x12;    // variable spec
            parameters[3] = 0x0A;    // length of address spec (10)
            parameters[4] = 0x10;    // S7ANY
            parameters[5] = 0x01;    // transport size = BIT

            parameters[6] = 0x00; parameters[7] = 0x01;  // 1 element (1 bit)

            parameters[8] = (byte)(dbNumber >> 8);
            parameters[9] = (byte)(dbNumber & 0xFF);    // DB number
            parameters[10] = 0x84;                       // area = DB

            int bitAddress = byteOffset * 8 + bitIndex;
            parameters[11] = (byte)((bitAddress >> 16) & 0xFF);
            parameters[12] = (byte)((bitAddress >> 8) & 0xFF);
            parameters[13] = (byte)(bitAddress & 0xFF);

            byte[] data = new byte[5];
            data[0] = 0x00; //reserved
            data[1] = 0x01; // transport size = BIT
            data[2] = 0x00; data[3] = 0x01; // bit length = 1

            byte valueByte = value ? (byte)0x01 : (byte)0x00; // set to 0x01 if true, 0x00 if false
            data[4] = valueByte;

            // Combine and send
            byte[] pdu = new byte[header.Length + parameters.Length + data.Length];
            Buffer.BlockCopy(header, 0, pdu, 0, header.Length);
            Buffer.BlockCopy(parameters, 0, pdu, header.Length, parameters.Length);
            Buffer.BlockCopy(data, 0, pdu, (header.Length + parameters.Length), data.Length);

            Debug.WriteLine("S7 WriteVar request PDU: " + BitConverter.ToString(pdu));

            await _tpktCotp.SendPayloadAsync(pdu);

            Debug.WriteLine($"Writing boolean to DB{dbNumber}.DBX{byteOffset}.{bitIndex}...");

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            if (respPayload[0] != 0x32 || respPayload[1] != 0x03)
            {
                throw new Exception("Invalid response to WriteVar request.");
            }

            ushort paramLength = (ushort)((respPayload[6] << 8) | respPayload[7]);
            ushort dataLength = (ushort)((respPayload[8] << 8) | respPayload[9]);

            //int pIndex = 12; // parameters start at index 12 (10 bytes header + 2 bytes padding)

            // Testing Code
            // Some S7 models (like S7-1500) include 2 bytes of padding between the header and parameters, while others may not.
            // To handle this variety, we calculate the padding dynamically based on the total length of the response and the lengths of the header, parameters, and data as specified in the header.
            // As we know the header is always 10 bytes, and paramLength and dataLength are specified in the header.
            // Initially, we subtract the header length, parameter length, and data length from the total response length to calculate padding, 
            // and then calculate pIndex by adding the header length and padding together.
            // To simplify, we can break down pIndex as 10 + (total length - 10 - paramLength - dataLength) to respPayload.Length - (paramLength + dataLength)
            int pIndex = respPayload.Length - (paramLength + dataLength); // parameters start after header and any padding, which is total length minus param and data length

            byte functionCode = respPayload[pIndex];
            if (functionCode != 0x05)
            {
                throw new Exception($"Unexpected function code in write response parameters: 0x{functionCode:X2}");
            }

            int dIndex = pIndex + paramLength; // data starts after header and parameters
            byte returnCode = respPayload[dIndex];
            if (returnCode != 0xFF)
            {
                throw new Exception($"WriteVar failed, return code: 0x{returnCode:X2}");
            }

        }
    }

}
