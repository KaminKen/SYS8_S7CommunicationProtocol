using SYS8.Core.Driver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace S7CommunicationApp
{
    public partial class SYS8_PLC_ClientApp : Form
    {
        private SYS8Driver? _driver;
        private bool _isConnected = false;

        public SYS8_PLC_ClientApp()
        {
            InitializeComponent();
            UpdateControls();
        }

        private void ReadButtonControls(bool enable)
        {
            ReadButton.Enabled = enable;
        }

        private void WriteButtonControls(bool enable)
        {
            WriteButton.Enabled = enable;
        }

        private void UpdateControls()
        {
            ConnectButton.Enabled = !_isConnected;
            DisconnectButton.Enabled = _isConnected;
            ReadButtonControls(_isConnected);
            WriteButtonControls(_isConnected);
        }

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                ConnectButton.Enabled = false;
                _driver = new SYS8Driver();
                await _driver.ConnectAsync(IpTextBox.Text, 102);
                _isConnected = true;
                UpdateControls();
                StatusTextBox.Text = "Connected!";
                LogTextBox.AppendText($"Connected to {IpTextBox.Text}\r\n");
                LogTextBox.AppendText($"Negotiated PDU Length: {_driver.NegotiatedPduLength}\r\n"); //Negotiated PDU length is publicly announced by the driver at the bottom of the file from S7ProtocolLayer.cs, and is determined during the connection handshake. It represents the maximum size of data that can be sent in a single S7 message, and is crucial for optimizing communication with the PLC.
            }
            catch (Exception ex)
            {
                ConnectButton.Enabled = true;
                MessageBox.Show($"Connection failed: {ex.Message}");
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            try 
            {
                DisconnectButton.Enabled = false;
                _driver?.Disconnect();
                _driver = null;
                _isConnected = false;
                UpdateControls();
                StatusTextBox.Text = "Disconnected";
                LogTextBox.AppendText("Disconnected\r\n");
            }
            catch (Exception ex)
            {
                DisconnectButton.Enabled= true;
                MessageBox.Show($"Disconnection failed: {ex.Message}");
            }

        }

        private async void ReadButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (_driver == null || !_driver.IsConnected)
                {
                    throw new Exception("Not connected to PLC.");
                }

                if (string.IsNullOrWhiteSpace(AddressTextBox.Text))
                    throw new Exception("Address is required.");

                ReadButtonControls(false);

                string datatypeCombox = DataTypeComBox.SelectedItem?.ToString() ?? DataTypeComBox.Text;

                var (dbNumber, byteOffset, bitIndex) = _driver.ParseStringAddress(AddressTextBox.Text);

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                switch (datatypeCombox)
                {
                    case "Bool":
                        bool readBool = await _driver.ReadBoolAsync(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Bool from {AddressTextBox.Text}: {readBool}\r\n");
                        break;
                    case "Int16":
                        short readInt16 = await _driver.ReadInt16Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int16 from {AddressTextBox.Text}: {readInt16}\r\n");
                        break;
                    case "Int32":
                        int readInt32 = await _driver.ReadInt32Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int32 from {AddressTextBox.Text}: {readInt32}\r\n");
                        break;
                    case "Int64":
                        long readInt64 = await _driver.ReadInt64Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int64 from {AddressTextBox.Text}: {readInt64}\r\n");
                        break;
                    case "UInt16":
                        ushort readUInt16 = await _driver.ReadUInt16Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read UInt16 from {AddressTextBox.Text}: {readUInt16}\r\n");
                        break;
                    case "UInt32":
                        uint readUInt32 = await _driver.ReadUInt32Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read UInt32 from {AddressTextBox.Text}: {readUInt32}\r\n");
                        break;
                    case "UInt64":
                        ulong readUInt64 = await _driver.ReadUInt64Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read UInt64 from {AddressTextBox.Text}: {readUInt64}\r\n");
                        break;
                    case "Float32":
                        float readFloat32 = await _driver.ReadFloat32Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Float32 from {AddressTextBox.Text}: {readFloat32}\r\n");
                        break;
                    case "Float64":
                        double readFloat64 = await _driver.ReadFloat64Async(dbNumber, byteOffset, bitIndex, cts.Token);
                        LogTextBox.AppendText($"Read Float64 from {AddressTextBox.Text}: {readFloat64}\r\n");
                        break;
                    case "String":
                        // Attempt to read up to 256 characters unless the Data textbox contains a smaller max length
                        int maxRead = 256;
                        if (int.TryParse(DataTextBox.Text, out int userMax) && userMax > 0)
                            maxRead = userMax;
                        string readString = await _driver.ReadStringAsync(dbNumber, byteOffset, bitIndex, maxRead, cts.Token);
                        LogTextBox.AppendText($"Read String from {AddressTextBox.Text}: '{readString}'\r\n");
                        break;
                    //case "String":
                    //    string readString = await _driver.ReadString(dbNumber, byteOffset, bitIndex);
                    //    LogTextBox.AppendText($"Read Float64 from {AddressTextBox.Text}: {readString}\r\n");
                    //    break;
                    default:
                        throw new Exception("Please select from drop down list or if still fail then this datatype manipulation is not implemented.");
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Read failed: {ex.Message}");
            }
            finally
            {
                ReadButtonControls(true);
            }
        }


        private async void WriteButton_Click(object sender, EventArgs e)
        {
            try
            {
                WriteButtonControls(false);
                if (_driver == null || !_driver.IsConnected)
                    throw new Exception("Not connected to PLC.");

                if (string.IsNullOrWhiteSpace(AddressTextBox.Text))
                    throw new Exception("Address is required.");

                string datatype = DataTypeComBox.SelectedItem?.ToString() ?? DataTypeComBox.Text;

                var (dbNumber, byteOffset, bitIndex) = _driver.ParseStringAddress(AddressTextBox.Text);

                // Use a short timeout to avoid UI hangs; callers can pass explicit CancellationToken if needed
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));

                switch (datatype)
                {
                    case "Bool":
                        if (!bool.TryParse(DataTextBox.Text, out bool writeBool))
                            throw new Exception("Invalid value for Bool. Please enter 'true' or 'false' in the Data textbox.");
                        await _driver.WriteBoolAsync(dbNumber, byteOffset, bitIndex, writeBool, cts.Token);
                        LogTextBox.AppendText($"Wrote Bool to {AddressTextBox.Text}: {writeBool}\r\n");
                        break;
                    case "Int16":
                        if (!short.TryParse(DataTextBox.Text, out short writeInt16))
                            throw new Exception("Invalid Int16 value.");
                        await _driver.WriteInt16Async(dbNumber, byteOffset, bitIndex, writeInt16, cts.Token);
                        LogTextBox.AppendText($"Wrote Int16 to {AddressTextBox.Text}: {writeInt16}\r\n");
                        break;
                    case "Int32":
                        if (!int.TryParse(DataTextBox.Text, out int writeInt32))
                            throw new Exception("Invalid Int32 value.");
                        await _driver.WriteInt32Async(dbNumber, byteOffset, bitIndex, writeInt32, cts.Token);
                        LogTextBox.AppendText($"Wrote Int32 to {AddressTextBox.Text}: {writeInt32}\r\n");
                        break;
                    case "Int64":
                        if (!long.TryParse(DataTextBox.Text, out long writeInt64))
                            throw new Exception("Invalid Int64 value.");
                        await _driver.WriteInt64Async(dbNumber, byteOffset, bitIndex, writeInt64, cts.Token);
                        LogTextBox.AppendText($"Wrote Int64 to {AddressTextBox.Text}: {writeInt64}\r\n");
                        break;
                    case "UInt16":
                        if (!ushort.TryParse(DataTextBox.Text, out ushort writeUInt16))
                            throw new Exception("Invalid UInt16 value.");
                        await _driver.WriteUInt16Async(dbNumber, byteOffset, bitIndex, writeUInt16, cts.Token);
                        LogTextBox.AppendText($"Wrote UInt16 to {AddressTextBox.Text}: {writeUInt16}\r\n");
                        break;
                    case "UInt32":
                        if (!uint.TryParse(DataTextBox.Text, out uint writeUInt32))
                            throw new Exception("Invalid UInt32 value.");
                        await _driver.WriteUInt32Async(dbNumber, byteOffset, bitIndex, writeUInt32, cts.Token);
                        LogTextBox.AppendText($"Wrote UInt32 to {AddressTextBox.Text}: {writeUInt32}\r\n");
                        break;
                    case "UInt64":
                        if (!ulong.TryParse(DataTextBox.Text, out ulong writeUInt64))
                            throw new Exception("Invalid UInt64 value.");
                        await _driver.WriteUInt64Async(dbNumber, byteOffset, bitIndex, writeUInt64, cts.Token);
                        LogTextBox.AppendText($"Wrote UInt64 to {AddressTextBox.Text}: {writeUInt64}\r\n");
                        break;
                    case "Float32":
                        if (!float.TryParse(DataTextBox.Text, out float writeFloat32))
                            throw new Exception("Invalid Float32 value.");
                        await _driver.WriteFloat32Async(dbNumber, byteOffset, bitIndex, writeFloat32, cts.Token);
                        LogTextBox.AppendText($"Wrote Float32 to {AddressTextBox.Text}: {writeFloat32}\r\n");
                        break;
                    case "Float64":
                        if (!double.TryParse(DataTextBox.Text, out double writeFloat64))
                            throw new Exception("Invalid Float64 value.");
                        await _driver.WriteFloat64Async(dbNumber, byteOffset, bitIndex, writeFloat64, cts.Token);
                        LogTextBox.AppendText($"Wrote Float64 to {AddressTextBox.Text}: {writeFloat64}\r\n");
                        break;
                    case "String":
                        string text = DataTextBox.Text ?? string.Empty;
                        int maxLen = Math.Max(text.Length, 1); // declare at least current length
                        await _driver.WriteStringAsync(dbNumber, byteOffset, bitIndex, maxLen, text, cts.Token);
                        LogTextBox.AppendText($"Wrote String to {AddressTextBox.Text}: '{text}' (max {maxLen})\r\n");
                        break;
                    default:
                        throw new Exception("Please select a supported data type from the dropdown.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Write failed: {ex.Message}");
            }
            finally
            {
                WriteButtonControls(true);
            }
        }

    }
}
