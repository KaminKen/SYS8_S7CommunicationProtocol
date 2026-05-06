using SYS8.Core.Protocol;
using SYS8.Core.Transport;
using SYS8.Core.StringManipulation;
using System;
using System.Threading;
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

        /// <summary>
        /// Indicates whether the underlying TCP transport is currently connected to a remote device.
        /// </summary>
        public bool IsConnected => _transport.IsConnected;

        /// <summary>
        /// Create a new <see cref="SYS8Driver"/> instance and initialize transport and protocol layers.
        /// </summary>
        public SYS8Driver()
        {
            _transport = new TcpTransport();
            _tpktCotp = new TpktCotpLayer(_transport);
            _s7Protocol = new S7ProtocolLayer(_tpktCotp);
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> when the driver is not connected.
        /// </summary>
        private void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected.");
        }

        /// <summary>
        /// Connect to a remote S7 PLC at the specified IP and port and perform the
        /// underlying TCP, COTP and S7 setup sequences. After this call completes
        /// the driver is ready for read/write operations and <see cref="IsConnected"/>
        /// will return <c>true</c> until <see cref="Disconnect"/> is called.
        /// </summary>
        /// <param name="ip">Target IPv4 address or DNS name.</param>
        /// <param name="port">Target TCP port (commonly 102 for S7).</param>
        /// <param name="cancellationToken">Optional cancellation token used to cancel the connect sequence.</param>
        public async Task ConnectAsync(string ip, int port, CancellationToken cancellationToken = default)
        {
            await _transport.ConnectAsync(ip, port, cancellationToken);
            Debug.WriteLine($"TCP connection to {ip}:{port} established successfully.");
            await _tpktCotp.ConnectAsync(cancellationToken);
            Debug.WriteLine("COTP connection established successfully.");
            await _s7Protocol.SetupCommunicationAsync(cancellationToken);
            Debug.WriteLine("S7 communication setup completed successfully.");
        }

        /// <summary>
        /// Disconnect the underlying transport immediately and mark the driver as not connected.
        /// This method is synchronous and will close the TCP connection.
        /// </summary>
        public void Disconnect()
        {
            _transport.Disconnect();
        }

        /// <summary>
        /// Send a raw S7 payload to the connected PLC. The method sends the provided
        /// S7 payload through the TPKT/COTP transport layer; callers are responsible
        /// for providing a correctly formed S7 payload (header + parameters + data).
        /// </summary>
        /// <param name="data">Raw S7 payload bytes (header + parameters + optional data).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task SendRawAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            //Data formatting and protocol handling is done in the TpktCotpLayer.
            await _tpktCotp.SendPayloadAsync(data, cancellationToken);
        }

        /// <summary>
        /// Receive a raw S7 payload from the connected PLC. The returned bytes are
        /// the payload delivered by the PLC with TPKT/COTP framing removed.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Byte array containing the S7 payload delivered by the PLC.</returns>
        public async Task<byte[]> ReceiveRawAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _tpktCotp.ReceivePayloadAsync(cancellationToken);
        }

        /// <summary>
        /// Gets the negotiated S7 PDU length (in bytes) that the PLC and client agreed during
        /// the setup communication sequence. This value should be used by callers who need
        /// to size large requests to fit within the negotiated PDU.
        /// </summary>
        public ushort NegotiatedPduLength => _s7Protocol.NegotiatedPduLength; //set variable for public user to access


        /// <summary>
        /// Parse a Siemens DB address string (for example "DB1.DBX0.1") into numeric components.
        /// This is a convenience wrapper around <see cref="SYS8.Core.StringManipulation.StringAddressToAbsoluteAddress"/>.
        /// </summary>
        /// <param name="address">Address string to parse (e.g. "DB1.DBD0", "DB1.DBW2", "DB1.DBX0.1").</param>
        /// <returns>Tuple of (<c>dbNumber</c>, <c>byteOffset</c>, <c>bitIndex</c>).</returns>
        public (ushort dbNumber, int byteOffset, int bitIndex) ParseStringAddress(string address)
        {
            var parser = new StringAddressToAbsoluteAddress();
            return parser.ParseStringAddress(address);
        }

        /// <summary>
        /// Read a single boolean (bit) from a data block in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read (for example "DB1.DBX0.1").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True when the addressed bit is set; otherwise false.</returns>
        public async Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadBoolAsync(address, cancellationToken);
        }

        /// <summary>
        /// Read a single boolean (bit) from a data block in the PLC using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index within the byte (0..7).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True when the addressed bit is set; otherwise false.</returns>
        public async Task<bool> ReadBoolAsync(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadBoolAsync(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read an array of boolean values using numeric DB parameters.
        /// </summary>
        /// <param name="address">Textual DB address to read (for example "DB1.DBX0.1").</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of boolean values read from the PLC.</returns>
        public async Task<bool[]> ReadBoolArrayAsync(string address, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await _s7Protocol.ReadBoolArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read an array of boolean values using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for REAL).</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of boolean values read from the PLC.</returns>
        public async Task<bool[]> ReadBoolArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadBoolArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read a 16-bit signed integer (INT) from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read (for example "DB1.DBW0").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit signed value read from the PLC.</returns>
        public async Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt16Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 16-bit signed integer (INT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number (DBx) to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (for bit-addressed types). Ignored for INT.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit signed value read from the PLC.</returns>
        /// <remarks>Use this overload when you already have numeric DB coordinates (for example from parsing an address).
        /// For a textual address use <see cref="ReadInt16Async(string, CancellationToken)"/> instead.</remarks>
        public async Task<short> ReadInt16Async(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt16Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read an array of 16-bit signed integer (INT) using numeric DB parameters.
        /// </summary>
        /// <param name="address">Textual DB address to read (for example "DB1.DBW0").</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 16-bit signed values read from the PLC.</returns>
        public async Task<short[]> ReadInt16ArrayAsync(string address, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await _s7Protocol.ReadInt16ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read an array of 16-bit signed integer (INT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for REAL).</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 16-bit signed values read from the PLC.</returns>
        public async Task<short[]> ReadInt16ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt16ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }


        /// <summary>
        /// Read a 32-bit signed integer (DINT) from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read (for example "DB1.DBD0").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit signed value read from the PLC.</returns>
        public async Task<int> ReadInt32Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt32Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 32-bit signed integer (DINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for DINT).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit signed value read from the PLC.</returns>
        /// <remarks>Prefer this overload when you already have numeric coordinates. For textual addresses use <see cref="ReadInt32Async(string, CancellationToken)"/>.</remarks>
        public async Task<int> ReadInt32Async(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt32Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read an array of 32-bit signed integer (DINT) using numeric DB parameters.
        /// </summary>
        /// <param name="address">Textual DB address to read (for example "DB1.DBD0").</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 32-bit signed values read from the PLC.</returns>
        public async Task<int[]> ReadInt32ArrayAsync(string address, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await _s7Protocol.ReadInt32ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read an array of 32-bit signed integer (DINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for REAL).</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 32-bit signed values read from the PLC.</returns>
        public async Task<int[]> ReadInt32ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt32ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }


        /// <summary>
        /// Read a 64-bit signed integer (LINT) from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit signed value read from the PLC.</returns>
        public async Task<Int64> ReadInt64Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt64Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 64-bit signed integer (LINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for LINT).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit signed value read from the PLC.</returns>
        /// <remarks>This requests 8 raw octets from the PLC and converts them to <see cref="long"/>.</remarks>
        public async Task<long> ReadInt64Async(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt64Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }


        /// <summary>
        /// Read a 16-bit unsigned integer (WORD/UINT) from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read (for example "DB1.DBW0").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit unsigned value read from the PLC.</returns>
        public async Task<UInt16> ReadUInt16Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt16Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 16-bit unsigned integer (WORD/UINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for WORD/UINT).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit unsigned value read from the PLC.</returns>
        public async Task<ushort> ReadUInt16Async(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt16Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read an array of 16-bit unsigned integer (WORD/UINT) using numeric DB parameters.
        /// </summary>
        /// <param name="address">Textual DB address to read (for example "DB1.DBW0").</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 16-bit unsigned values read from the PLC.</returns>
        public async Task<UInt16[]> ReadUInt16ArrayAsync(string address, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await _s7Protocol.ReadUInt16ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read an array of 16-bit unsigned integer (WORD/UINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for REAL).</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 16-bit unsigned values read from the PLC.</returns>
        public async Task<UInt16[]> ReadUInt16ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt16ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }



        /// <summary>
        /// Read a 32-bit unsigned integer (DWORD/UDINT) from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit unsigned value read from the PLC.</returns>
        public async Task<UInt32> ReadUInt32Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt32Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 32-bit unsigned integer (DWORD/UDINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for DWORD/UDINT).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit unsigned values read from the PLC.</returns>
        public async Task<uint> ReadUInt32Async(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt32Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read an array of 32-bit unsigned integer (DWORD/UDINT) using numeric DB parameters.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 32-bit unsigned values read from the PLC.</returns>
        public async Task<UInt32[]> ReadUInt32ArrayAsync(string address, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await _s7Protocol.ReadUInt32ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }


        /// <summary>
        /// Read an array of 32-bit unsigned integer (DWORD/UDINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for REAL).</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 32-bit unsigned values read from the PLC.</returns>
        public async Task<UInt32[]> ReadUInt32ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt32ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read a 64-bit unsigned integer (ULINT) from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit unsigned value read from the PLC.</returns>
        public async Task<UInt64> ReadUInt64Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt64Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 64-bit unsigned integer (ULINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for ULINT).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit unsigned value read from the PLC.</returns>
        public async Task<ulong> ReadUInt64Async(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt64Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read a 32-bit floating point value (REAL) from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit floating point value read from the PLC.</returns>
        public async Task<float> ReadFloat32Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadFloat32Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 32-bit floating point value (REAL) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for REAL).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit floating point value read from the PLC.</returns>
        public async Task<float> ReadFloat32Async(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadFloat32Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read an array of 32-bit floating point value (REAL) using numeric DB parameters.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 32-bit floating point values read from the PLC.</returns>
        public async Task<float[]> ReadFloat32ArrayAsync(string address, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await _s7Protocol.ReadFloat32ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read an array of 32-bit floating point value (REAL) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for REAL).</param>
        /// <param name="elementCount">Number of elements to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An array of 32-bit floating point values read from the PLC.</returns>
        public async Task<float[]> ReadFloat32ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadFloat32ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read a 64-bit floating point value (LREAL/DOUBLE) from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit floating point value read from the PLC.</returns>
        public async Task<double> ReadFloat64Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadFloat64Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 64-bit floating point value (LREAL/DOUBLE) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for LREAL).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit floating point value read from the PLC.</returns>
        public async Task<double> ReadFloat64Async(ushort dbNumber, int byteOffset, int bitIndex, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadFloat64Async(dbNumber, byteOffset, bitIndex, cancellationToken);
        }

        /// <summary>
        /// Read an array of 64-bit floating point values (LREAL/DOUBLE) using numeric DB parameters.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="elementCount">Number of elements to read.</param>  
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The array of 64-bit floating point value read from the PLC.</returns>
        public async Task<double[]> ReadFloat64ArrayAsync(string address, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return await _s7Protocol.ReadFloat64ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read an array of 64-bit floating point values (LREAL/DOUBLE) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the value starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for LREAL).</param>
        /// <param name="elementCount">Number of elements to read.</param>  
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The array of 64-bit floating point value read from the PLC.</returns>
        public async Task<double[]> ReadFloat64ArrayAsync(ushort dbNumber, int byteOffset, int bitIndex, int elementCount, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadFloat64ArrayAsync(dbNumber, byteOffset, bitIndex, elementCount, cancellationToken);
        }

        /// <summary>
        /// Read a Siemens STRING from a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to read.</param>
        /// <param name="maxStringLength">Maximum expected string length (characters).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The decoded string read from the PLC.</returns>
        public async Task<string> ReadStringAsync(string address, int maxStringLength, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadStringAsync(address, maxStringLength, cancellationToken);
        }

        /// <summary>
        /// Read a Siemens STRING using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The data block number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB where the STRING starts.</param>
        /// <param name="bitIndex">Bit index within the byte (ignored for STRING).</param>
        /// <param name="maxStringLength">Maximum expected number of characters to read from the STRING.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The decoded STRING value read from the PLC.</returns>
        /// <remarks>Siemens STRING layout: first byte = declared max length, second byte = current length, followed by character bytes.</remarks>
        public async Task<string> ReadStringAsync(ushort dbNumber, int byteOffset, int bitIndex, int maxStringLength, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadStringAsync(dbNumber, byteOffset, bitIndex, maxStringLength, cancellationToken);
        }


        /// <summary>
        /// Write a boolean (single bit) to a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write (for example "DB1.DBX0.1").</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteBoolAsync(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a boolean (single bit) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB.</param>
        /// <param name="bitIndex">The bit index within the byte (0..7).</param>
        /// <param name="value">Boolean value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <remarks>Use this overload when you have numeric coordinates. For textual addresses use <see cref="WriteBoolAsync(string,bool,CancellationToken)"/>.</remarks>
        public async Task WriteBoolAsync(ushort dbNumber, int byteOffset, int bitIndex, bool value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteBoolAsync(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a boolean (single bit) to a DB in the PLC using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write (for example "DB1.DBX0.1").</param>
        /// <param name="value">Value to write.</param>
        /// <param name="length">The length of the array</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task<string> WriteBoolArrayAsync(string address, bool value, uint length, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.WriteBoolArrayAsync(address, value, length, cancellationToken);
        }

        /// <summary>
        /// Write a 16-bit signed integer (INT) to the PLC DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write (for example "DB1.DBW0").</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt16Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 16-bit signed integer (INT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the 2-byte INT will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for INT).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt16Async(ushort dbNumber, int byteOffset, int bitIndex, short value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt16Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit signed integer (DINT) to the PLC DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write (for example "DB1.DBD0").</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt32Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit signed integer (DINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the 4-byte DINT will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for DINT).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt32Async(ushort dbNumber, int byteOffset, int bitIndex, int value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt32Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }


        /// <summary>
        /// Write a 64-bit signed integer to the PLC DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt64Async(string address, long value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt64Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 64-bit signed integer (LINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the 8-byte value will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for LINT).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt64Async(ushort dbNumber, int byteOffset, int bitIndex, long value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt64Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 16-bit unsigned integer (WORD/UINT) to the PLC DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt16Async(string address, ushort value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt16Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 16-bit unsigned integer (WORD/UINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the 2-byte value will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for WORD/UINT).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt16Async(ushort dbNumber, int byteOffset, int bitIndex, ushort value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt16Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit unsigned integer (DWORD/UDINT) to the PLC DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt32Async(string address, uint value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt32Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit unsigned integer (DWORD/UDINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the 4-byte value will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for DWORD/UDINT).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt32Async(ushort dbNumber, int byteOffset, int bitIndex, uint value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt32Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 64-bit unsigned integer (ULINT) to the PLC DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt64Async(string address, ulong value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt64Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 64-bit unsigned integer (ULINT) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the 8-byte value will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for ULINT).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt64Async(ushort dbNumber, int byteOffset, int bitIndex, ulong value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt64Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit floating point value (REAL) to the PLC DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteFloat32Async(string address, float value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteFloat32Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit floating point value (REAL) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the 4-byte REAL will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for REAL).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteFloat32Async(ushort dbNumber, int byteOffset, int bitIndex, float value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteFloat32Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }

        /// <summary>
        /// Write a 64-bit floating point value (LREAL/DOUBLE) to the PLC DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteFloat64Async(string address, double value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteFloat64Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 64-bit floating point value (LREAL/DOUBLE) using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the 8-byte LREAL will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for LREAL).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteFloat64Async(ushort dbNumber, int byteOffset, int bitIndex, double value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteFloat64Async(dbNumber, byteOffset, bitIndex, value, cancellationToken);
        }


        /// <summary>
        /// Write a Siemens STRING into a DB using a textual address.
        /// </summary>
        /// <param name="address">Textual DB address to write.</param>
        /// <param name="maxStringLength">Declared maximum length for the STRING (characters).</param>
        /// <param name="value">String value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteStringAsync(string address, int maxStringLength, string value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteStringAsync(address, maxStringLength, value, cancellationToken);
        }

        /// <summary>
        /// Write a Siemens STRING using numeric DB parameters.
        /// </summary>
        /// <param name="dbNumber">The DB number to write to.</param>
        /// <param name="byteOffset">The byte offset inside the DB where the STRING will be written.</param>
        /// <param name="bitIndex">Bit index (ignored for STRING).</param>
        /// <param name="maxStringLength">Declared maximum length for the STRING (characters).</param>
        /// <param name="value">String value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <remarks>The string will be written as Siemens STRING format (declared max, current length, characters).</remarks>
        public async Task WriteStringAsync(ushort dbNumber, int byteOffset, int bitIndex, int maxStringLength, string value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteStringAsync(dbNumber, byteOffset, bitIndex, maxStringLength, value, cancellationToken);
        }
    }
}
