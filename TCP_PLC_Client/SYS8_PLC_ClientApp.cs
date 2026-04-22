using SYS8.Core.Driver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace S7CommunicationApp
{
    public partial class SYS8_PLC_ClientApp : Form
    {
        public static SYS8Driver? _driver;
        private bool _isConnected = false;
        private bool isReadAndWrite = true; //false is its publish and subscribe mode, true is its read and write mode

        PublishAndSubscribe? publishAndSubscribeForm = null;

        public SYS8_PLC_ClientApp()
        {
            InitializeComponent();
            UpdateControls();
        }

        public void LogMessage(string message)
        {
            LogTextBox.AppendText(message + "\r\n");
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
            ReadButtonControls(_isConnected && isReadAndWrite);
            WriteButtonControls(_isConnected && isReadAndWrite);
            PublishAndSubscribeModeButton.Enabled = _isConnected && isReadAndWrite;
            ReadWriteModeButton.Enabled = _isConnected && !isReadAndWrite;
            ModeStatusTextBox.Text = isReadAndWrite ? "Read/Write" : "Publish/Subscribe";
        }


        private void ReadWriteModeButton_Click(object sender, EventArgs e)
        {
            isReadAndWrite = true;
            LogTextBox.AppendText("Switched to Read/Write mode\r\n");
            if (publishAndSubscribeForm != null && !publishAndSubscribeForm.IsDisposed)
            {
                publishAndSubscribeForm.Close();
                publishAndSubscribeForm = null;
            }
            UpdateControls();
        }

        private void PublishAndSubscribeModeButton_Click(object sender, EventArgs e)
        {
            isReadAndWrite = false;
            LogTextBox.AppendText("Switched to Publish/Subscribe mode\r\n");
            if (publishAndSubscribeForm == null || publishAndSubscribeForm.IsDisposed)
            {
                publishAndSubscribeForm = new PublishAndSubscribe(this); ; //create a new instance of ClientForm if it doesn't exist or has been disposed
                publishAndSubscribeForm.Show();
            }
            UpdateControls();
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
                DisconnectButton.Enabled = true;
                MessageBox.Show($"Disconnection failed: {ex.Message}");
            }

        }

        private async void ReadOneData(SYS8Driver _driver, string datatype, string address)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                switch (datatype)
                {
                    case "bool":
                        bool readBool = await _driver.ReadBoolAsync(address);
                        LogTextBox.AppendText($"Read Bool from {address}: {readBool}\r\n");
                        break;
                    case "int16":
                        short readInt16 = await _driver.ReadInt16Async(address);
                        LogTextBox.AppendText($"Read Int16 from {address}: {readInt16}\r\n");
                        break;
                    case "int32":
                        int readInt32 = await _driver.ReadInt32Async(address);
                        LogTextBox.AppendText($"Read Int32 from {address}: {readInt32}\r\n");
                        break;
                    case "int64":
                        long readInt64 = await _driver.ReadInt64Async(address);
                        LogTextBox.AppendText($"Read Int64 from {address}: {readInt64}\r\n");
                        break;
                    case "uint16":
                        ushort readUInt16 = await _driver.ReadUInt16Async(address);
                        LogTextBox.AppendText($"Read UInt16 from {address}: {readUInt16}\r\n");
                        break;
                    case "uint32":
                        uint readUInt32 = await _driver.ReadUInt32Async(address);
                        LogTextBox.AppendText($"Read UInt32 from {address}: {readUInt32}\r\n");
                        break;
                    case "uint64":
                        ulong readUInt64 = await _driver.ReadUInt64Async(address);
                        LogTextBox.AppendText($"Read UInt64 from {address}: {readUInt64}\r\n");
                        break;
                    case "float32":
                        float readFloat32 = await _driver.ReadFloat32Async(address);
                        LogTextBox.AppendText($"Read Float32 from {address}: {readFloat32}\r\n");
                        break;
                    case "float64":
                        double readFloat64 = await _driver.ReadFloat64Async(address, cts.Token);
                        LogTextBox.AppendText($"Read Float64 from {address}: {readFloat64}\r\n");
                        break;
                    case "string":
                        // Attempt to read up to 256 characters unless the Data textbox contains a smaller max length
                        int maxRead = 256;
                        if (int.TryParse(address, out int userMax) && userMax > 0)
                            maxRead = userMax;
                        string readString = await _driver.ReadStringAsync(address, maxRead, cts.Token);
                        LogTextBox.AppendText($"Read String from {address}: '{readString}'\r\n");
                        break;
                    default:
                        throw new Exception("Please select from drop down list or if still fail then this datatype manipulation is not implemented.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Read failed: {ex.Message}");
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

                string datatype = (DataTypeComBox.SelectedItem?.ToString() ?? DataTypeComBox.Text).ToLowerInvariant();

                //var (dbNumber, byteOffset, bitIndex) = _driver.ParseStringAddress(AddressTextBox.Text);

                int length = 1;
                if (!string.IsNullOrEmpty(LengthTextBox.Text) || !string.IsNullOrWhiteSpace(LengthTextBox.Text))
                {
                    length = int.Parse(LengthTextBox.Text);
                }

                if (length == 1)
                {
                    ReadOneData(_driver, datatype, AddressTextBox.Text);
                }
                else
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    if (datatype == "bool")
                    { 
                        bool[] result = await _driver.ReadBoolArrayAsync(AddressTextBox.Text, length, cts.Token);
                        if (result.Length != length)
                        {
                            throw new Exception($"Expected {length} boolean values, but received {result.Length}.");
                        }
                        for (int i = 0; i < result.Length; i++)
                        {
                            LogTextBox.AppendText($"Read Bool[{i}] from {AddressTextBox.Text}: {result[i]}\r\n");
                        }
                    }
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


        private async void WriteData(SYS8Driver _driver, string datatype, string data, string address)
        {
            try 
            {
                // Use a short timeout to avoid UI hangs; callers can pass explicit CancellationToken if needed
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));


                switch (datatype)
                {
                    case "bool":
                        if (!bool.TryParse(data, out bool writeBool))
                            throw new Exception("Invalid value for Bool. Please enter 'true' or 'false' in the Data textbox.");
                        await _driver.WriteBoolAsync(address, writeBool, cts.Token);
                        LogTextBox.AppendText($"Wrote Bool to {address}: {writeBool}\r\n");
                        break;
                    case "int16":
                        if (!short.TryParse(data, out short writeInt16))
                            throw new Exception("Invalid Int16 value.");
                        await _driver.WriteInt16Async(address, writeInt16, cts.Token);
                        LogTextBox.AppendText($"Wrote Int16 to {address}: {writeInt16}\r\n");
                        break;
                    case "int32":
                        if (!int.TryParse(data, out int writeInt32))
                            throw new Exception("Invalid Int32 value.");
                        await _driver.WriteInt32Async(address, writeInt32, cts.Token);
                        LogTextBox.AppendText($"Wrote Int32 to {address}: {writeInt32}\r\n");
                        break;
                    case "int64":
                        if (!long.TryParse(data, out long writeInt64))
                            throw new Exception("Invalid Int64 value.");
                        await _driver.WriteInt64Async(address, writeInt64, cts.Token);
                        LogTextBox.AppendText($"Wrote Int64 to {address}: {writeInt64}\r\n");
                        break;
                    case "uint16":
                        if (!ushort.TryParse(data, out ushort writeUInt16))
                            throw new Exception("Invalid UInt16 value.");
                        await _driver.WriteUInt16Async(address, writeUInt16, cts.Token);
                        LogTextBox.AppendText($"Wrote UInt16 to {address}: {writeUInt16}\r\n");
                        break;
                    case "uint32":
                        if (!uint.TryParse(data, out uint writeUInt32))
                            throw new Exception("Invalid UInt32 value.");
                        await _driver.WriteUInt32Async(address, writeUInt32, cts.Token);
                        LogTextBox.AppendText($"Wrote UInt32 to {address}: {writeUInt32}\r\n");
                        break;
                    case "uint64":
                        if (!ulong.TryParse(data, out ulong writeUInt64))
                            throw new Exception("Invalid UInt64 value.");
                        await _driver.WriteUInt64Async(address, writeUInt64, cts.Token);
                        LogTextBox.AppendText($"Wrote UInt64 to {address}: {writeUInt64}\r\n");
                        break;
                    case "float32":
                        if (!float.TryParse(data, out float writeFloat32))
                            throw new Exception("Invalid Float32 value.");
                        await _driver.WriteFloat32Async(address, writeFloat32, cts.Token);
                        LogTextBox.AppendText($"Wrote Float32 to {address}: {writeFloat32}\r\n");
                        break;
                    case "float64":
                        if (!double.TryParse(data, out double writeFloat64))
                            throw new Exception("Invalid Float64 value.");
                        await _driver.WriteFloat64Async(address, writeFloat64, cts.Token);
                        LogTextBox.AppendText($"Wrote Float64 to {address}: {writeFloat64}\r\n");
                        break;
                    case "string":
                        string text = data ?? string.Empty;
                        int maxLen = Math.Max(text.Length, 1); // declare at least current length
                        await _driver.WriteStringAsync(address, maxLen, text, cts.Token);
                        LogTextBox.AppendText($"Wrote String to {address}: '{text}' (max {maxLen})\r\n");
                        break;
                    default:
                        throw new Exception("Please select a supported data type from the dropdown.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Write failed: {ex.Message}");
            }
        }


        private async void WriteButton_Click(object sender, EventArgs e)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                WriteButtonControls(false);
                if (_driver == null || !_driver.IsConnected)
                    throw new Exception("Not connected to PLC.");

                if (string.IsNullOrWhiteSpace(AddressTextBox.Text))
                    throw new Exception("Address is required.");

                string datatype = (DataTypeComBox.SelectedItem?.ToString() ?? DataTypeComBox.Text).ToLowerInvariant();

                //var (dbNumber, byteOffset, bitIndex) = _driver.ParseStringAddress(AddressTextBox.Text);


                int length = 1;
                if (!string.IsNullOrEmpty(LengthTextBox.Text)) 
                {
                    length  = int.Parse(LengthTextBox.Text); 
                }

                if (length == 1)
                {
                    WriteData(_driver, datatype, DataTextBox.Text, AddressTextBox.Text);
                }
                else 
                {
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    if (datatype == "bool")
                    {
                        if (!bool.TryParse(DataTextBox.Text, out bool writeBool))
                            throw new Exception("Invalid value for Bool. Please enter 'true' or 'false' in the Data textbox.");
                        string lastTopic = await _driver.WriteBoolArrayAsync(AddressTextBox.Text, writeBool, (uint)length, cts.Token);
                        LogTextBox.AppendText($"Wrote Bool to {AddressTextBox.Text}: {writeBool}\r\n");
                        LogTextBox.AppendText($"Last topic: {lastTopic}\r\n");
                    }
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
            sw.Stop();
            var time = sw.ElapsedMilliseconds;
            Debug.WriteLine($"Write operation took {time} ms");
        }
    }
}
