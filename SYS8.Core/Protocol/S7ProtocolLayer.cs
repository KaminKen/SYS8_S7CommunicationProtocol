using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
        /// Must be called once after COTP ConnectAsync().
        /// Negotiates PDU length with the PLC.
        /// </summary>
        public async Task SetupCommunicationAsync()
        {
            //Build S7 Setup Communication request
            //S7 header is 10 bytes, followed by parameters and data

            Debug.WriteLine("Building S7 Setup Communication request...");

            //S7Header
            byte[] header = S7ProtocolHelpers.BuildS7Header(0x01, 8, 0); //Setup Communication is a Job (0x01) with 8 bytes of parameters and 0 bytes of data

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

            /*
             * S7 SetupComm request PDU: 32-01-00-00-00-01-00-08-00-00-F0-00-00-01-00-01-04-80
             */

            //send via tpkt and cotp layer
            await _tpktCotp.SendPayloadAsync(s7Pdu);

            Debug.WriteLine("S7 Setup Communication request sent, waiting for response...");

            //receive response
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

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


        public ushort NegotiatedPduLength => _pduLength; // expose negotiated PDU length obtained from PLC in class to public, so that it can be used by higher layers when building messages


        //TODO: optimize and refine. Possibly create helper function for checking header and params sent from PLC as they are mostly the same

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

            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Bit, 1); // transport size 0x01 for bit, data length 1 bit

            Debug.WriteLine("S7 ReadVar request PDU: " + BitConverter.ToString(pdu));

            /*
             * Example: S7 ReadVar request PDU: 32-01-00-00-00-03-00-0E-00-04-04-01-12-0A-10-01-00-01-00-01-84-00-00-00
             */

            await _tpktCotp.SendPayloadAsync(pdu);

            Debug.WriteLine($"Reading boolean from DB{dbNumber}.DBX{byteOffset}.{bitIndex}...");

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));
            Debug.WriteLine("S7 Read response received, validating response...");

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 1);

            byte valueByte = respPayload[dataHeaderStartIndex + 4]; // First value byte is the 4th index after data start.

            //shift 00000001 to left by bitIndex digit so it would become something like 00000100 if bitIndex is 2,
            //then do bitwise AND with the value byte to extract the specific bit value,
            //and check if it's not equal to 0 to determine if the bit is true or false.
            bool value = (valueByte & (1 << (bitIndex & 0x07))) != 0; // extract the specific bit value from the byte based on bit index

            return value;

        }


        //TODO: optimize and refine. Possibly create helper function for all Read for checking header and params sent from PLC as they are mostly the same
        /// <summary>
        /// Read Int (Int 16 bits) value from the PLC
        /// </summary>
        /// <param name="dbNumber">Datablock Numbers</param>
        /// <param name="byteOffset">The index of bytes the user want to read from</param>
        /// <param name="bitIndex">The index of bit that the user want to read from that byte</param>
        /// <returns>Int16</returns>
        public async Task<Int16> ReadInt16Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            // Similar to ReadBoolAsync, but with parameters set for reading a 16-bit integer
            // transport size = 0x05 for INT, 4 data header parameters and data length = 2 bytes
            // The data parsing would also need to be adjusted to read 2 bytes of data and convert it to an Int16 value.
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Int, 2); // transport size 0x05 for all integer, data length 4 bytes

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 16);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            Int16 value = (short)((respPayload[dataStartIndex] << 8) | respPayload[dataStartIndex + 1]); // combine 2 bytes of data into an Int16 value
            return value;

        }


        public async Task<Int32> ReadInt32Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.DInt, 4);

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

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

        public async Task<long> ReadInt64Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(
                dbNumber,
                byteOffset,
                bitIndex,
                S7Types.ItemTransport.DInt,
                8);

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) =
                S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 64);

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


        public async Task<UInt16> ReadUInt16Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Word, 2);

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) = S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 16);

            int dataStartIndex = dataHeaderStartIndex + 4; // data starts after the 4 bytes of return code, transport size, and bit length
            UInt16 value = (ushort)((respPayload[dataStartIndex] << 8) | respPayload[dataStartIndex + 1]); 
            return value;

        }

        public async Task<UInt32> ReadUInt32Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.DWord, 4); 

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

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

        public async Task<ulong> ReadUInt64Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(
                dbNumber,
                byteOffset,
                bitIndex,
                S7Types.ItemTransport.DWord,
                8);

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

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

        public async Task<float> ReadFloat32Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(dbNumber, byteOffset, bitIndex,S7Types.ItemTransport.Real, 4);

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) =
                S7ProtocolHelpers.ValidateReadResponse(respPayload, 0x04, 0x01, 32);

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

        public async Task<double> ReadFloat64Async(ushort dbNumber, int byteOffset, int bitIndex)
        {
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(dbNumber, byteOffset, bitIndex, S7Types.ItemTransport.Real, 8); // or a specific LReal code if you define one

            await _tpktCotp.SendPayloadAsync(pdu);
            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

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

        public async Task<string> ReadStringAsync(ushort dbNumber, int byteOffset, int maxStringLength)
        {
            byte[] pdu = S7ProtocolHelpers.BuildReadRequest(
                dbNumber,
                byteOffset,
                0,
                S7Types.ItemTransport.Byte,
                (ushort)(maxStringLength + 2));

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

            var (_, _, dataHeaderStartIndex) =
                S7ProtocolHelpers.ValidateReadResponse(
                    respPayload,
                    0x04,
                    0x01,
                    (ushort)((maxStringLength + 2) * 8));

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
        /// Write boolean value to PLC
        /// </summary>
        /// <param name="dbNumber">Datablock Numbers</param>
        /// <param name="byteOffset">The index of bytes the user want to read from</param>
        /// <param name="bitIndex">The index of bit that the user want to read from that byte</param>
        /// <param name="value">true or false</param>
        /// <exception cref="Exception"></exception>
        //public async Task WriteBoolAsync(ushort dbNumber, int byteOffset, int bitIndex, bool value)
        //{
        //    // Similar to ReadBoolAsync, but with function code for Write Var and including the value in the data section of the message.
        //    byte[] header = S7ProtocolHelpers.BuildS7Header(0x01, 14, 5); // 4 bytes for data header and 1 byte for the boolean value
        //    byte[] parameters = new byte[14];
        //    parameters[0] = 0x05;    // Write Var
        //    parameters[1] = 0x01;    // 1 item

        //    parameters[2] = 0x12;    // variable spec
        //    parameters[3] = 0x0A;    // length of address spec (10)
        //    parameters[4] = 0x10;    // S7ANY
        //    parameters[5] = S7Types.ItemTransport.Bit;    // transport size = BIT

        //    parameters[6] = 0x00; parameters[7] = 0x01;  // 1 element (1 bit)

        //    parameters[8] = (byte)(dbNumber >> 8);
        //    parameters[9] = (byte)(dbNumber & 0xFF);    // DB number
        //    parameters[10] = 0x84;                       // area = DB

        //    int bitAddress = byteOffset * 8 + bitIndex;
        //    parameters[11] = (byte)((bitAddress >> 16) & 0xFF);
        //    parameters[12] = (byte)((bitAddress >> 8) & 0xFF);
        //    parameters[13] = (byte)(bitAddress & 0xFF);

        //    byte[] data = new byte[5];
        //    data[0] = 0x00; //reserved
        //    data[1] = 0x01; // transport size = BIT
        //    data[2] = 0x00; data[3] = 0x01; // bit length = 1

        //    byte valueByte = value ? (byte)0x01 : (byte)0x00; // set to 0x01 if true, 0x00 if false
        //    data[4] = valueByte;

        //    // Combine and send
        //    byte[] pdu = new byte[header.Length + parameters.Length + data.Length];
        //    Buffer.BlockCopy(header, 0, pdu, 0, header.Length);
        //    Buffer.BlockCopy(parameters, 0, pdu, header.Length, parameters.Length);
        //    Buffer.BlockCopy(data, 0, pdu, (header.Length + parameters.Length), data.Length);

        //    Debug.WriteLine("S7 WriteVar request PDU: " + BitConverter.ToString(pdu));

        //    await _tpktCotp.SendPayloadAsync(pdu);

        //    Debug.WriteLine($"Writing boolean to DB{dbNumber}.DBX{byteOffset}.{bitIndex}...");

        //    byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

        //    Debug.WriteLine("Resp payload: " + BitConverter.ToString(respPayload));

        //    if (respPayload[0] != 0x32 || respPayload[1] != 0x03)
        //    {
        //        throw new Exception("Invalid response to WriteVar request.");
        //    }

        //    ushort paramLength = (ushort)((respPayload[6] << 8) | respPayload[7]);
        //    ushort dataLength = (ushort)((respPayload[8] << 8) | respPayload[9]);

        //    //int pIndex = 12; // parameters start at index 12 (10 bytes header + 2 bytes padding)

        //    // Testing Code
        //    // Some S7 models (like S7-1500) include 2 bytes of padding between the header and parameters, while others may not.
        //    // To handle this variety, we calculate the padding dynamically based on the total length of the response and the lengths of the header, parameters, and data as specified in the header.
        //    // As we know the header is always 10 bytes, and paramLength and dataLength are specified in the header.
        //    // Initially, we subtract the header length, parameter length, and data length from the total response length to calculate padding, 
        //    // and then calculate pIndex by adding the header length and padding together.
        //    // To simplify, we can break down pIndex as 10 + (total length - 10 - paramLength - dataLength) to respPayload.Length - (paramLength + dataLength)
        //    int pIndex = respPayload.Length - (paramLength + dataLength); // parameters start after header and any padding, which is total length minus param and data length

        //    byte functionCode = respPayload[pIndex];
        //    if (functionCode != 0x05)
        //    {
        //        throw new Exception($"Unexpected function code in write response parameters: 0x{functionCode:X2}");
        //    }

        //    int dIndex = pIndex + paramLength; // data starts after header and parameters
        //    byte returnCode = respPayload[dIndex];
        //    if (returnCode != 0xFF)
        //    {
        //        throw new Exception($"WriteVar failed, return code: 0x{returnCode:X2}");
        //    }

        //}

        public async Task WriteBoolAsync(ushort dbNumber, int byteOffset, int bitIndex, bool value)
        {
            byte[] header = S7ProtocolHelpers.BuildS7Header(0x01, 14, 5);
            byte[] parameters = new byte[14];
            parameters[0] = 0x05; // WriteVar
            parameters[1] = 0x01; // 1 item
            parameters[2] = 0x12;
            parameters[3] = 0x0A;
            parameters[4] = 0x10; // S7ANY
            parameters[5] = S7Types.ItemTransport.Bit; // 0x01
            parameters[6] = 0x00; parameters[7] = 0x01; // 1 bit
            parameters[8] = (byte)(dbNumber >> 8);
            parameters[9] = (byte)(dbNumber & 0xFF);
            parameters[10] = 0x84; // DB area
            int bitAddress = byteOffset * 8 + bitIndex;
            parameters[11] = (byte)((bitAddress >> 16) & 0xFF);
            parameters[12] = (byte)((bitAddress >> 8) & 0xFF);
            parameters[13] = (byte)(bitAddress & 0xFF);

            byte[] data = new byte[5];
            data[0] = 0x00; // reserved
            data[1] = S7Types.DataTransport.Bit; // 0x03 (important)
            data[2] = 0x00; data[3] = 0x01; // 1 bit
            data[4] = value ? (byte)0x01 : (byte)0x00;

            byte[] pdu = new byte[header.Length + parameters.Length + data.Length];
            Buffer.BlockCopy(header, 0, pdu, 0, header.Length);
            Buffer.BlockCopy(parameters, 0, pdu, header.Length, parameters.Length);
            Buffer.BlockCopy(data, 0, pdu, header.Length + parameters.Length, data.Length);

            await _tpktCotp.SendPayloadAsync(pdu);

            byte[] respPayload = await _tpktCotp.ReceivePayloadAsync();

            if (respPayload.Length < 12 || respPayload[0] != 0x32)
                throw new Exception("Invalid S7 response to WriteVar.");

            if (respPayload[1] != 0x03)
                throw new Exception($"Unexpected ROSCTR 0x{respPayload[1]:X2} in WriteVar response.");

            ushort paramLength = (ushort)((respPayload[6] << 8) | respPayload[7]);
            ushort dataLength = (ushort)((respPayload[8] << 8) | respPayload[9]);

            int pIndex = respPayload.Length - (paramLength + dataLength);
            int dIndex = pIndex + paramLength;

            if (dIndex >= respPayload.Length)
                throw new Exception("WriteVar response data index out of range.");

            byte returnCode = respPayload[dIndex];

            if (returnCode != 0xFF)
                throw new Exception($"WriteVar failed, return code: 0x{returnCode:X2}");
        }
    }

}
