using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SYS8.Core.Transport
{
    public class TcpTransport
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        // private const int DefaultBufferSize = 1024;

        // This is called Property in C#, not a method. It is used to represent state/property with logic behind it.
        // It is a read-only
        public bool IsConnected 
        {
            get
            {
                return _client != null && _client.Connected;
            }
        }

        /// <summary>
        /// Connect TCP/IP from PC to PLC without blocking the thread
        /// </summary>
        /// <param name="ip">string value of ip address of PLC</param>
        /// <param name="port">integer value of PLC port, usually 102</param>
        /// <remarks>
        /// This function initialise TcpClient and connect to PLC, then get the NetworkStream for later use. 
        /// It is an asynchronous function, so it will not block the thread while connecting. 
        /// You can await this function in your code to ensure that the connection is established 
        /// before proceeding with any operations that require the connection.
        /// </remarks>
        /// <returns></returns>
        public async Task ConnectAsync(string ip, int port, CancellationToken cancellationToken = default)
        {
            _client = new TcpClient();
            // ConnectAsync on TcpClient does not accept a cancellation token directly.
            // Honor cancellation by observing the token before/after the connect call.
            cancellationToken.ThrowIfCancellationRequested();
            await _client.ConnectAsync(ip, port);
            cancellationToken.ThrowIfCancellationRequested();

            // GetStream() gets the NetworkStream used to send and receive data.
            _stream = _client.GetStream();
        }

        /// <summary>
        /// Send bytes data to PLC
        /// </summary>
        /// <param name="data">bytes data of the message</param>
        /// <remarks>
        /// Sends raw bytes through the active TCP connection.
        /// Higher protocol layers are responsible for building the correct message format.
        /// </remarks>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (_stream == null) 
            {
                throw new InvalidOperationException("Not connected.");
            }
            //WriteAsync writes data to the stream asynchronously. It takes the byte array, the offset (0 in this case), and the number of bytes to write (data.Length).
            await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
        }


        private async Task<byte[]> ReadExactAsync(int count, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                int bytesRead = await _stream!.ReadAsync(buffer, offset, count - offset, cancellationToken);
                if (bytesRead == 0)
                {
                    throw new Exception("Connection closed!");
                }
                offset += bytesRead;
            }

            return buffer;
        }


        /// <summary>
        /// Read data from the PLC
        /// </summary>
        /// <remarks>
        /// This function read bytes from the stream without blocking the thread and return the bytes read
        /// The current max size of buffer is 1024, it can be adjusted as needed
        /// </remarks>
        /// <returns>Bytes Read from PLC</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            if (_stream == null) 
            {
                throw new InvalidOperationException("Not connected.");
            }

            // Step 1: always exactly 4 bytes — the TPKT header
            byte[] header = await ReadExactAsync(4, cancellationToken);

            // Step 2: bytes [2] and [3] are big-endian total length
            // e.g. 00 16 = 22 → means read 18 more bytes
            int totalLength = (header[2] << 8) | header[3];

            // Step 3: read exactly the remaining bytes
            byte[] rest = await ReadExactAsync(totalLength - 4, cancellationToken);

            // Step 4: combine into one clean message
            byte[] full = new byte[totalLength];
            Array.Copy(header, 0, full, 0, 4);
            Array.Copy(rest, 0, full, 4, rest.Length);

            return full;
        }


        /// <summary>
        /// Disconnect the client from PLC
        /// <remarks>
        /// Close stream and then set it to null before closing client and set to null
        /// </remarks>
        /// </summary>
        public void Disconnect() 
        {
            _stream?.Close();
            _stream = null;
            _client?.Close();
            _client = null;
        }
    }
}
