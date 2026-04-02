using SYS8.Core.Transport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SYS8.Core.Protocol
{
    public class TpktCotpLayer
    {
        private readonly TcpTransport _transport;

        public TpktCotpLayer(TcpTransport transport)
        {
            _transport = transport; // uses the one passed in from SYS8Driver
        }

        //public void SetTimeout(TimeSpan timeout)
        //{
        //    if (timeout.TotalMilliseconds > int.MaxValue)
        //        throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout is too large");

        //    if (timeout.TotalMilliseconds < 0)
        //        throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout is negative");

        //    lock (_lock)
        //    {
        //        _timeout = timeout;
        //    }
        //}

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            //byte[] part1 = new byte[]
            //{
            //    0x03, 0x00, 0x00, 0x24, // TPKT — total length 36
            //    0x1f, 0xE0,             // COTP length, CR type
            //    0x00, 0x00,             // dst ref
            //    0x00, 0x01,             // src ref
            //    0x00,                   // class
            //    0xC0, 0x01, 0x0A,       // TPDU size
            //    0xC1, 0x02, 0x06, 0x00, // src TSAP = 0x0600
            //    0xC2, 0x10              // dst TSAP id, length 16
            //};
            //byte[] dstTsap = Encoding.ASCII.GetBytes("SIMATIC-ROOT-HMI"); // Destination TSAP (Transport Service Access Point)

            //byte[] packet = new byte[part1.Length + dstTsap.Length]; // full message buffer
            //Array.Copy(part1, 0, packet, 0, part1.Length);
            //Array.Copy(dstTsap, 0, packet, part1.Length, dstTsap.Length);

            byte[] packet = new byte[]
            {
                0x03, 0x00, 0x00, 0x16, // TPKT len = 22 bytes total
                0x11, 0xE0,             // COTP length=17, CR
                0x00, 0x00,             // dst ref
                0x00, 0x01,             // src ref
                0x00,                   // class 0
                0xC0, 0x01, 0x0A,       // TPDU size 1024
                0xC1, 0x02, 0x01, 0x00, // src TSAP = 01 00 (PG)
                0xC2, 0x02, 0x01, 0x01  // dst TSAP = 01 01 (CPU rack0/slot1 example)
            };

            //var cts = new CancellationTokenSource(_timeout)
            await _transport.SendAsync(packet, cancellationToken); // TODO: send the connection request but may need to set timeout


            byte[] response = await _transport.ReceiveAsync(cancellationToken); // wait for the response

            if (response.Length < 7 || response[0] != 0x03 || response[1] != 0x00)
            {
                throw new Exception("Invalid TPKT header in COTP connect response.");
            }

            byte cotpLength = response[4]; // cotp length count all bytes of cotp after it (not counting itself).
            byte pduType = response[5];

            if (pduType != 0xD0) //0xD0 means connection confirm. Handshake.
            {
                throw new Exception($"COTP handshake failed. Expected 0xD0, got 0x{pduType:X2}");
            }

            int expectedLength = cotpLength + 4 + 1; // 4 TPKT + 1 length byte + cotpLength bytes

            if (expectedLength != response.Length) //cotpLength + 4 bytes of TPKT header should equal total length of response
            {
                throw new Exception($"COTP length mismatch. Expected {expectedLength}, got {response.Length}");
            }
        }

        public async Task SendPayloadAsync(byte[] s7Payload, CancellationToken cancellationToken = default)
        {
            int totalLength = 7 + s7Payload.Length; // 7 bytes for TPKT+COTP header

            byte len_hi = (byte)((totalLength >> 8) & 0xFF); // high byte of length
            byte len_lo = (byte)(totalLength & 0xFF); // low byte of length

            byte[] tpkt = new byte[]
            {
                0x03, 0x00, len_hi, len_lo // TPKT header with total length
            };

            byte[] cotp = new byte[]
            {
                0x02, 0xF0, 0x80
            };

            byte[] dataBytes = new byte[totalLength]; //totalLength is equivalent to tpkt.Length + cotp.Length + s7Payload.Length
            Array.Copy(tpkt, 0, dataBytes, 0, tpkt.Length);
            Array.Copy(cotp, 0, dataBytes, tpkt.Length, cotp.Length);
            Array.Copy(s7Payload, 0, dataBytes, tpkt.Length + cotp.Length, s7Payload.Length);

            await _transport.SendAsync(dataBytes, cancellationToken);

        }

        public async Task<byte[]> ReceivePayloadAsync(CancellationToken cancellationToken = default)
        {
            Debug.WriteLine("Waiting for response from PLC...");
            byte[] response = await _transport.ReceiveAsync(cancellationToken);
            Debug.WriteLine($"Received {response.Length} bytes from PLC.");
            // Basic validation of TPKT header
            if (response.Length < 7 || response[0] != 0x03 || response[1] != 0x00)
            {
                throw new Exception("Invalid TPKT header in response.");
            }
            int totalLength = (response[2] << 8) | response[3]; // Extract total length from TPKT header

            //Checking the case if the total number of bytes received and declared is the same or not. If not, meaning there is some bytes missing.
            if (response.Length != totalLength)
            {
                throw new Exception($"TPKT length mismatch. Expected {totalLength}, got {response.Length}");
            }

            byte cotpLength = response[4]; // COTP length is at byte 4 of the response, its all bytes after this length byte in the COTP header.
            if (cotpLength < 2 || response[5] != 0xF0 || response[6] != 0x80)
            {
                throw new Exception("Invalid COTP DATA header in response.");
            }

            int headerLength = 4 + 1 + cotpLength; // 4 TPKT + (1 cotp length bytes + cotpLength bytes)
            int payloadLength = totalLength - headerLength; // total length minus TPKT(4) and COTP(3) header
            if (payloadLength < 0)
            {
                throw new Exception("Negative payload length calculated.");
            }

            byte[] payloadByte = new byte[payloadLength];
            Array.Copy(response, headerLength, payloadByte, 0, payloadLength); // Copy the payload part of the response to a new byte array
            return payloadByte;

        }
    }
}
