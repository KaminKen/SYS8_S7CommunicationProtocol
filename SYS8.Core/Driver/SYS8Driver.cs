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
        /// Connect to a remote S7 PLC at the specified IP and port and perform COTP/S7 setup.
        /// </summary>
        /// <param name="ip">Target IPv4 address or hostname.</param>
        /// <param name="port">Target TCP port (commonly 102 for S7).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
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
        /// Disconnect the underlying transport immediately.
        /// </summary>
        public void Disconnect()
        {
            _transport.Disconnect();
        }

        /// <summary>
        /// Send a raw S7 payload (the TPKT/COTP framing will be applied) to the connected PLC.
        /// </summary>
        /// <param name="data">Raw S7 payload bytes (header+params+data as required).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task SendRawAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            //Data formatting and protocol handling is done in the TpktCotpLayer.
            await _tpktCotp.SendPayloadAsync(data, cancellationToken);
        }

        /// <summary>
        /// Receive a raw payload from the connected PLC (COTP/TPKT headers stripped).
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Byte array containing the payload delivered by the PLC.</returns>
        public async Task<byte[]> ReceiveRawAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _tpktCotp.ReceivePayloadAsync(cancellationToken);
        }

        /// <summary>
        /// The negotiated S7 PDU length the PLC agreed during setup communication (in bytes).
        /// </summary>
        public ushort NegotiatedPduLength => _s7Protocol.NegotiatedPduLength; //set variable for public user to access


        /// <summary>
        /// Parse a Siemens DB address string (for example "DB1.DBX0.1") into numeric components.
        /// </summary>
        /// <param name="address">Address string to parse.</param>
        /// <returns>Tuple of (dbNumber, byteOffset, bitIndex).</returns>
        public (ushort dbNumber, int byteOffset, int bitIndex) ParseStringAddress(string address)
        {
            var parser = new StringAddressToAbsoluteAddress();
            return parser.ParseStringAddress(address);
        }

        /// <summary>
        /// Read a single boolean (bit) from a data block in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index within the byte (0..7).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True when the addressed bit is set; otherwise false.</returns>
        public async Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadBoolAsync(address, cancellationToken);
        }

        /// <summary>
        /// Read a 16-bit signed integer (INT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types; kept for API symmetry.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit signed value read from the PLC.</returns>
        public async Task<Int16> ReadInt16Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt16Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 32-bit signed integer (DINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit signed value read from the PLC.</returns>
        public async Task<Int32> ReadInt32Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt32Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 64-bit signed integer (LINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit signed value read from the PLC.</returns>
        public async Task<Int64> ReadInt64Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadInt64Async(address, cancellationToken);
        }
        /// <summary>
        /// Read a 16-bit unsigned integer (WORD/UINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 16-bit unsigned value read from the PLC.</returns>
        public async Task<UInt16> ReadUInt16Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt16Async(address, cancellationToken);
        }
        /// <summary>
        /// Read a 32-bit unsigned integer (DWORD/UDINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit unsigned value read from the PLC.</returns>
        public async Task<UInt32> ReadUInt32Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt32Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 64-bit unsigned integer (ULINT) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit unsigned value read from the PLC.</returns>
        public async Task<UInt64> ReadUInt64Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadUInt64Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 32-bit floating point value (REAL) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 32-bit floating point value read from the PLC.</returns>
        public async Task<float> ReadFloat32Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadFloat32Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a 64-bit floating point value (LREAL/DOUBLE) from a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The 64-bit floating point value read from the PLC.</returns>
        public async Task<double> ReadFloat64Async(string address, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadFloat64Async(address, cancellationToken);
        }

        /// <summary>
        /// Read a Siemens STRING from a DB.
        /// </summary>
        /// <param name="dbNumber">DB number to read from.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="maxStringLength">Maximum expected string length (characters).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The decoded string read from the PLC.</returns>
        public async Task<string> ReadStringAsync(string address, int maxStringLength, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return await _s7Protocol.ReadStringAsync(address, maxStringLength, cancellationToken);
        }


        /// <summary>
        /// Write a boolean (single bit) to a DB in the PLC.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index within the byte (0..7).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteBoolAsync(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 16-bit signed integer (INT) to the PLC DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index (ignored for byte-aligned types).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt16Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit signed integer (DINT) to the PLC DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index (ignored for byte-aligned types).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt32Async(address, value, cancellationToken);
        }


        /// <summary>
        /// Write a 64-bit signed integer to the PLC DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index (ignored for byte-aligned types).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteInt64Async(string address, long value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteInt64Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 16-bit unsigned integer (WORD/UINT) to the PLC DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index (ignored for byte-aligned types).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt16Async(string address, ushort value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt16Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit unsigned integer (DWORD/UDINT) to the PLC DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index (ignored for byte-aligned types).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt32Async(string address, uint value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt32Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 64-bit unsigned integer (ULINT) to the PLC DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index (ignored for byte-aligned types).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteUInt64Async(string address, ulong value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteUInt64Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 32-bit floating point value (REAL) to the PLC DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index (ignored for byte-aligned types).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteFloat32Async(string address, float value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteFloat32Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a 64-bit floating point value (LREAL/DOUBLE) to the PLC DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Bit index (ignored for byte-aligned types).</param>
        /// <param name="value">Value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteFloat64Async(string address, double value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteFloat64Async(address, value, cancellationToken);
        }

        /// <summary>
        /// Write a Siemens STRING into a DB.
        /// </summary>
        /// <param name="dbNumber">DB number to write to.</param>
        /// <param name="byteOffset">Byte offset inside the DB.</param>
        /// <param name="bitIndex">Ignored for byte-aligned types.</param>
        /// <param name="maxStringLength">Declared maximum length for the STRING (characters).</param>
        /// <param name="value">String value to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task WriteStringAsync(string address, int maxStringLength, string value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            await _s7Protocol.WriteStringAsync(address, maxStringLength, value, cancellationToken);
        }

        //public async Task Subscribe(string absoluteAddress) 
        //{
        //    EnsureConnected();
        //    await _publishSubscribe.SubscribeAsync(absoluteAddress);
        //}

    }
}
