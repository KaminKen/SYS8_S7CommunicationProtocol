using SYS8.Core.Protocol;
using SYS8.Core.Transport;
using System;
using System.Threading.Tasks;
using System.Diagnostics;


namespace SYS8.Core.Driver
{
    /// <summary>
    /// Provides high-level communication methods for SYS8 device access.
    /// </summary>
    public class SYS8Driver
    {
        //Field can only be assigned in constructor
        //Cannot be reassigned later
        //Prevents bugs
        private readonly TcpTransport _transport;
        private readonly TpktCotpLayer _tpktCotp;
        private readonly S7ProtocolLayer _s7Protocol;

        public bool IsConnected => _transport.IsConnected;

        /// <summary>
        /// Constructor for the SYS8Driver class. Initializes the TCP transport layer for communication with the SYS8 device.
        /// </summary>
        public SYS8Driver()
        {
            _transport = new TcpTransport();
            _tpktCotp = new TpktCotpLayer(_transport);

            Debug.WriteLine("TPKT COTP connected successfully");

            _s7Protocol = new S7ProtocolLayer(_tpktCotp);

            Debug.WriteLine("SetupCommunication OK. PDU=" + _s7Protocol.NegotiatedPduLength);
        }

        /// <summary>
        /// Check if the connection to the target device is established. 
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected.");
        }

        /// <summary>
        /// Establishes a TCP connection to the target device through the transport layer.
        /// </summary>
        /// <param name="ip">string value of the ip</param>
        /// <param name="port">integer value of the port, usually it is 102 to connect to Siemen PLC</param>
        public async Task ConnectAsync(string ip, int port)
        {
            await _transport.ConnectAsync(ip, port);
            Debug.WriteLine($"TCP connection to {ip}:{port} established successfully.");
            await _tpktCotp.ConnectAsync();
            Debug.WriteLine("COTP connection established successfully.");
            await _s7Protocol.SetupCommunicationAsync();
            Debug.WriteLine("S7 communication setup completed successfully.");
        }

        /// <summary>
        /// Disconnects from the target device through the transport layer.
        /// </summary>
        public void Disconnect()
        {
            _transport.Disconnect();
        }

        /// <summary>
        /// Send raw bytes through the active connection with proper format
        /// </summary>
        /// <param name="data">Bytes of the message</param>
        /// <returns></returns>
        public async Task SendRawAsync(byte[] data)
        {
            EnsureConnected();

            //Data formatting and protocol handling is done in the TpktCotpLayer.
            await _tpktCotp.SendPayloadAsync(data);
        }

        /// <summary>
        /// Receive bytes sent from connected port
        /// </summary>
        /// <returns>Bytes sent from PLC</returns>
        public async Task<byte[]> ReceiveRawAsync()
        {
            EnsureConnected();
            return await _tpktCotp.ReceivePayloadAsync();
        }

        public ushort NegotiatedPduLength => _s7Protocol.NegotiatedPduLength; //set variable for public user to access


        public async Task<bool> ReadBoolAsync(ushort dbNumber, int byteOffset, int bitIndex)
        {
            return await _s7Protocol.ReadBoolAsync(dbNumber, byteOffset, bitIndex);
        }

        public async Task WriteBoolAsync(ushort dbNumber, int byteOffset, int bitIndex, bool value)
        {
            await _s7Protocol.WriteBoolAsync(dbNumber, byteOffset, bitIndex, value);
        }
    }
}
