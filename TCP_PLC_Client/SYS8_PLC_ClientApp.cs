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
                if (_driver == null)
                {
                    throw new Exception("Not connected to PLC.");
                }

                ReadButtonControls(false);

                string datatypeCombox = DataTypeComBox.Text;

                var (dbNumber, byteOffset, bitIndex) = _driver.ParseStringAddress(AddressTextBox.Text);


                switch (datatypeCombox) { 
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
                        LogTextBox.AppendText($"Read Int16 from {AddressTextBox.Text}: {readInt32}\r\n");
                        break;
                    case "Int64":
                        long readInt64 = await _driver.ReadInt64Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int16 from {AddressTextBox.Text}: {readInt64}\r\n");
                        break;
                    case "UInt16":
                        ushort readUInt16 = await _driver.ReadUInt16Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int16 from {AddressTextBox.Text}: {readUInt16}\r\n");
                        break;
                    case "UInt32":
                        uint readUInt32 = await _driver.ReadUInt32Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int16 from {AddressTextBox.Text}: {readUInt32}\r\n");
                        break;
                    case "UInt64":
                        ulong readUInt64 = await _driver.ReadUInt64Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int16 from {AddressTextBox.Text}: {readUInt64}\r\n");
                        break;
                    case "Float32":
                        float readFloat32 = await _driver.ReadFloat32Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int16 from {AddressTextBox.Text}: {readFloat32}\r\n");
                        break;
                    case "Float64":
                        double readFloat64 = await _driver.ReadFloat64Async(dbNumber, byteOffset, bitIndex);
                        LogTextBox.AppendText($"Read Int16 from {AddressTextBox.Text}: {readFloat64}\r\n");
                        break;
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

                bool isBool = DataTypeComBox.SelectedItem?.ToString() == "Bool";
                if (!isBool)
                {
                    throw new Exception("Only Bool data type is supported in this demo. Please select Bool from the Data Type dropdown.");
                }

                if (!bool.TryParse(DataTextBox.Text, out bool writeBool))
                {
                    throw new Exception("Invalid value for Bool. Please enter 'true' or 'false' in the Data textbox.");
                }

                if (_driver == null)
                {
                    throw new Exception("Not connected to PLC.");
                }

                // Async write
                await _driver.WriteBoolAsync(1, 0, 0, writeBool);

                LogTextBox.AppendText($"Wrote Bool to {AddressTextBox.Text}: {writeBool}\r\n");
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
