using Org.BouncyCastle.Utilities.Net;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using TKH.S7Plus.Net;
using TKH.S7Plus.Net.DriverExtensions;
using TKH.S7Plus.Net.Models;
using TKH.S7Plus.Net.S7Variables;

namespace S7CommunicationApp
{
    public partial class S7PlusNET_ClientApp : Form
    {
        //TcpClient tcpClient;
        //NetworkStream networkStream;

        private S7Driver? s7Driver; // ? set for nullable type
        private bool isConnected = false;


        public S7PlusNET_ClientApp()
        {
            InitializeComponent();
            ConnectionStatus(false);

        }

        private void ReadableControl(bool Boolean)
        {
            ReadAllButton.Enabled = Boolean;
            return;
        }

        private void WriteableControl(bool Boolean)
        {
            WriteToPlcButton.Enabled = Boolean;
            return;
        }

        private void ClearLog()
        {
            LogTextBox.Clear();
        }

        private void ReadWriteEnabled(bool Boolean)
        {
            ReadableControl(Boolean);
            WriteableControl(Boolean);
            return;
        }

        private void ConnectionStatus(bool Boolean)
        {
            isConnected = Boolean;

            if (Boolean)
            {
                ConnectionStatusTextBox.Text = "Connected!";
            }
            else
            {
                ConnectionStatusTextBox.Text = "Disconnected!";
            }

            ConnectButton.Enabled = !Boolean;
            DisconnectButton.Enabled = Boolean;
            //ReadableControl(Boolean);
            //WriteableControl(Boolean);
            ReadWriteEnabled(Boolean);
        }

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                
                if (isConnected)
                {
                    MessageBox.Show("Already connected!");
                    return;
                }
                if (!System.Net.IPAddress.TryParse(IPTextBox.Text, out _))
                {
                    MessageBox.Show("Invalid IP address!");
                    return;
                }
                if (!int.TryParse(PortTextBox.Text, out int port))
                {
                    MessageBox.Show("Invalid port number!");
                    return;
                }

                //S7Driver use function SetTimeout and Connect from TcpClient under the hood
                // Under S7Driver.cs of s7plus library
                s7Driver = new S7Driver();
                s7Driver.SetTimeout(TimeSpan.FromSeconds(5));

                await s7Driver.Connect(IPTextBox.Text, port); //connect parse string to int not IP class
                //tcpClient.Connect(IPAddress.Parse(IPTextBox.Text), port);
                //networkStream = tcpClient.GetStream();

                ConnectionStatus(true);

            }
            catch (Exception ex)
            {
                s7Driver = null; // Ensure driver is null if connection fails
                ConnectionStatus(false);
                MessageBox.Show($"Connection failed: {ex.Message}");
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!isConnected)
                {
                    MessageBox.Show("Not connected!");
                    return;
                }

                //Using Disconnect from TcpClient implemented in S7Plus
                s7Driver?.Disconnect();
                s7Driver = null;

                ConnectionStatus(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Disconnect Error: " + ex.Message);
            }
        }


        private async void ReadAllButton_Click(object sender, EventArgs e)
        {
            try
            {
                //ReadAllButton.Enabled = false;
                ReadWriteEnabled(false);
                if (!isConnected || s7Driver == null)
                {
                    MessageBox.Show("Not connected!");
                    return;
                }

                //Located in DriverExploreExtensions.cs in s7plus library
                // Ask PLC what datablocks exist
                List<Datablock> datablocks = await s7Driver.GetDatablocks(); //TODO: Find the implementatation of GetDatablocks in S7Driver.cs, it should send a request to PLC and parse the response to get the list of datablocks
                if (datablocks.Count == 0)
                {
                    MessageBox.Show("No datablocks found!");
                    return;
                }

                string symbol = AddressTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    MessageBox.Show("Please enter a symbol like DATA_BLOCK.TEST_BOOL, using symbolic address");
                    return;
                }

                var info = await s7Driver.GetVariableInfoBySymbol(symbol, datablocks); //TODO: Understand how it works

                if (info == null)
                {
                    MessageBox.Show($"Symbol not found: {symbol}");
                    return;
                }

                var result = await s7Driver.GetVariable(info.Address); //TODO: Understand how it works

                //if (result is S7VariableBool b) 
                //{
                //    LogTextBox.AppendText($"{symbol} = {b.Value}\r\n");
                //}
                //else
                //{
                //    MessageBox.Show($"Variable is not a boolean: {symbol}");
                //}
                switch (result)
                {
                    case S7VariableBool b when DataTypeComBox.SelectedItem?.ToString() == "Boolean":
                        LogTextBox.AppendText($"{symbol} = {b.Value}" + Environment.NewLine);
                        break;
                    case S7VariableDInt di when (DataTypeComBox.SelectedItem?.ToString() == "Int32" || DataTypeComBox.SelectedItem?.ToString() == "DInt"):
                        LogTextBox.AppendText($"{symbol} = {di.Value}" + Environment.NewLine);
                        break;
                    case S7VariableInt i16 when DataTypeComBox.SelectedItem?.ToString() == "Int16":
                        LogTextBox.AppendText($"{symbol} = {i16.Value}" + Environment.NewLine);
                        break;
                    case S7VariableReal r when DataTypeComBox.SelectedItem?.ToString() == "Float":
                        LogTextBox.AppendText($"{symbol} = {r.Value}" + Environment.NewLine);
                        break;
                    case S7VariableByteArray s when DataTypeComBox.SelectedItem?.ToString() == "String":
                        byte[] data = s.Value;
                        //The bytes sent from PC are in the following format: Max Length, Current Length, Chars
                        int lenght = data[1];
                        string strValue = Encoding.ASCII.GetString(data, 2, lenght); // Start from index 2 to skip max and current length
                        MessageBox.Show($"{symbol} = \"{s.Value}\"" + Environment.NewLine);
                        break;
                    default:
                        LogTextBox.AppendText($"{symbol}: Read OK, but mismatched or unhandled type: {result.GetType().Name}" + Environment.NewLine);
                        break;
                }
                //if (result is S7VariableBool b && DataTypeComBox.SelectedItem?.ToString() == "Boolean")
                //    LogTextBox.AppendText($"{symbol} = {b.Value}" + Environment.NewLine);

                //else if (result is S7VariableDInt di && (DataTypeComBox.SelectedItem?.ToString() == "Int32" || DataTypeComBox.SelectedItem?.ToString() == "DInt"))
                //    LogTextBox.AppendText($"{symbol} = {di.Value}" + Environment.NewLine);
                //else if (result is S7VariableInt i16 && DataTypeComBox.SelectedItem?.ToString() == "Int16")
                //    LogTextBox.AppendText($"{symbol} = {i16.Value}" + Environment.NewLine);
                //else if (result is S7VariableReal r && DataTypeComBox.SelectedItem?.ToString() == "Real")
                //    LogTextBox.AppendText($"{symbol} = {r.Value}" + Environment.NewLine);
                ////else if (result is S7VariableString s)
                ////    MessageBox.Show($"{symbol} = \"{s.Value}\"");
                //else
                //    LogTextBox.AppendText($"{symbol}: Read OK, but unhandled type: {result.GetType().Name}" + Environment.NewLine);

            }
            catch (Exception ex)
            {
                MessageBox.Show("Read Error: " + ex.Message);
            }
            finally
            {
                //ReadAllButton.Enabled = true;
                ReadWriteEnabled(true);
            }
        }

        private async void WriteToPlcButton_Click(object sender, EventArgs e)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            try
            {
                ReadWriteEnabled(false);
                if (!isConnected || s7Driver == null)
                {
                    MessageBox.Show("Not connected!");
                    return;
                }

                List<Datablock> datablocks = await s7Driver.GetDatablocks();
                string symbol = AddressTextBox.Text.Trim();

                var info = await s7Driver.GetVariableInfoBySymbol(symbol, datablocks);

                if (info == null)
                {
                    MessageBox.Show($"Symbol not found: {symbol}");
                    return;
                }

                var result = await s7Driver.GetVariable(info.Address);

                switch (DataTypeComBox.SelectedItem?.ToString())
                {
                    case "Boolean" when result is S7VariableBool:
                        if (bool.TryParse(DataBoxTextBox.Text, out bool boolValue))
                        {
                            await s7Driver.SetVariable(info.Address, new S7VariableBool(boolValue));
                            LogTextBox.AppendText($"{symbol} set to {boolValue}" + Environment.NewLine);
                        }
                        else
                        {
                            MessageBox.Show("Invalid boolean value! Use 'true' or 'false'.");
                        }
                        break;
                    case "Int32" when result is S7VariableDInt:
                        if (int.TryParse(DataBoxTextBox.Text, out int intValue))
                        {
                            await s7Driver.SetVariable(info.Address, new S7VariableDInt(intValue));
                            LogTextBox.AppendText($"{symbol} set to {intValue}" + Environment.NewLine);
                        }
                        else
                        {
                            MessageBox.Show("Invalid Int32 value! Enter a valid integer.");
                        }
                        break;
                    case "Int16" when result is S7VariableInt:
                        if (short.TryParse(DataBoxTextBox.Text, out short shortValue))
                        {
                            await s7Driver.SetVariable(info.Address, new S7VariableInt(shortValue));
                            LogTextBox.AppendText($"{symbol} set to {shortValue}" + Environment.NewLine);
                        }
                        else
                        {
                            MessageBox.Show("Invalid Int16 value! Enter a valid value.");
                        }
                        break;

                    case "Float" when result is S7VariableReal:
                        if (float.TryParse(DataBoxTextBox.Text, out float floatValue))
                        {
                            await s7Driver.SetVariable(info.Address, new S7VariableReal(floatValue));
                            LogTextBox.AppendText($"{symbol} set to {floatValue}" + Environment.NewLine);
                        }
                        else
                        {
                            MessageBox.Show("Invalid Float(Real) value! Enter a valid number.");
                        }
                        break;
                    default:
                        MessageBox.Show("Unsupported type or mismatched type selection! Ensure the PLC variable type matches the selected data type.");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Write Error: " + ex.Message);
            }
            finally
            {
                ReadWriteEnabled(true);
            }
            sw.Stop();
            var time = sw.ElapsedMilliseconds;
            Debug.WriteLine($"Write operation took {time} ms");
        }

        private void IPTextBox_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
