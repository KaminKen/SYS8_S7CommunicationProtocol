using Org.BouncyCastle.Bcpg;
using SYS8.Core.Driver;
using System.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace S7CommunicationApp
{
    public partial class PublishAndSubscribe : Form
    {
        private SYS8Monitoring? ps;
        private readonly SYS8_PLC_ClientApp _mainForm;

        public PublishAndSubscribe(SYS8_PLC_ClientApp mainForm)
        {
            InitializeComponent();
            _mainForm = mainForm;

            if (SYS8_PLC_ClientApp._driver == null)
            {
                MessageBox.Show("Driver is not initialized. Please connect first.");
                return;
            }

            ps = new SYS8Monitoring(SYS8_PLC_ClientApp._driver);
            ps.OnValueChanged = OnValueChanged;
        }


        private async void SubscribeButton_Click(object sender, EventArgs e)
        {
            try
            {
                int length = 1; // default length
                if (ps == null)
                {
                    throw new InvalidOperationException("Monitoring model is not initialized.");
                }
                if(!string.IsNullOrEmpty(LengthTextBox.Text))
                {
                    length = int.Parse(LengthTextBox.Text);
                }

                if (length == 1)
                {
                    await ps.Subscribe(AddressTextBox.Text, DataTypeTextBox.Text);
                    SubscribeListTextBox.AppendText($"{AddressTextBox.Text} ({DataTypeTextBox.Text}){Environment.NewLine}");
                }
                else
                {
                    string lastTopic = await ps.SubscribeArray(AddressTextBox.Text, length,DataTypeTextBox.Text);
                    SubscribeListTextBox.AppendText($"{AddressTextBox.Text} ({DataTypeTextBox.Text}), Length: {length}, last topic: {lastTopic}{Environment.NewLine}");
                }

                
                if (!ps.IsPolling)
                {
                    ps.StartPolling();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void OnValueChanged(string topic, object value)
        {
            this.Invoke(() =>
            {
                _mainForm.LogMessage($"[PubSub] {topic} changed to: {value}");
            });
        }

        private void UnsubscribeButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (ps == null)
                {
                    throw new InvalidOperationException("Monitoring model is not initialized.");
                }
                ps.Unsubscribe(AddressTextBox.Text);


                if (ps.IsEmpty)
                {
                    ps.StopPolling();
                    SubscribeListTextBox.Clear();
                }
                else
                {
                    var remaining = SubscribeListTextBox.Text
                        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(line => !line.StartsWith(AddressTextBox.Text))
                        .ToArray();

                    SubscribeListTextBox.Text = remaining.Length > 0
                        ? string.Join(Environment.NewLine, remaining)
                        : string.Empty;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void ReadCheckButton_Click(object sender, EventArgs e)
        {
            // This is just a placeholder for the read check functionality.
        }
    }
}
