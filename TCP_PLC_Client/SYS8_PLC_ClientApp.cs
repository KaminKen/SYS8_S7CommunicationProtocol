using SYS8.Core.Driver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace S7Plus_PLC_Client
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

        private void ReadButton_Click(object sender, EventArgs e)
        {
            try
            {
                ReadButtonControls(false);
                bool isBool = DataTypeComBox.SelectedItem?.ToString() == "Bool";
                if (!isBool)
                {
                    throw new Exception("Only Bool data type is supported in this demo. Please select Bool from the Data Type dropdown.");
                }
                bool readBool = _driver?.ReadBoolAsync(1, 0, 0).Result ?? false; // For demonstration, we read DB1.DBX0.0. In a real application, you would parse the AddressTextBox to determine the correct DB number, byte offset, and bit index.
                LogTextBox.AppendText($"Read {(isBool ? "Bool" : "Data")} from {AddressTextBox.Text}: {readBool}\r\n");
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

        private void WriteButton_Click(object sender, EventArgs e)
        {
            try
            {
                WriteButtonControls(false);
                bool isBool = DataTypeComBox.SelectedItem?.ToString() == "Bool";
                if (!isBool)
                {
                    throw new Exception("Only Bool data type is supported in this demo. Please select Bool from the Data Type dropdown.");
                }
                bool validValue = bool.TryParse(DataTextBox.Text, out bool writeBool);
                if (!validValue)
                {
                    throw new Exception("Invalid value for Bool. Please enter 'true' or 'false' in the Data textbox.");
                }
                _driver?.WriteBoolAsync(1, 0, 0, writeBool).Wait(); // For demonstration, we write to DB1.DBX0.0. In a real application, you would parse the AddressTextBox to determine the correct DB number, byte offset, and bit index.
                LogTextBox.AppendText($"Wrote {(isBool ? "Bool" : "Data")} to {AddressTextBox.Text}: {writeBool}\r\n");
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
