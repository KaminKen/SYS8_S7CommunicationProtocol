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

        private async void ReadCheckButton_Click(object sender, EventArgs e)
        {
            // Quick subscription-array tests to validate SubscribeArray behavior.
            try
            {
                if (ps == null)
                {
                    throw new InvalidOperationException("Monitoring model is not initialized.");
                }

                // unsubscribe all before running tests to ensure a clean slate
                try { ps.UnsubscribeAll(); } catch { }
                SubscribeListTextBox.Clear();

                var tests = new List<(string topic, int length, string type)>()
                {
                    // single bool
                    (AddressTextBox.Text ?? "DB1.DBX0.0", 1, "bool"),
                    // cross-byte bools (start near end of byte)
                    ("DB1.DBX0.6", 4, "bool"),
                    // int16 array
                    ("DB1.DBW0", 3, "int16"),
                    // int32 array
                    ("DB1.DBD0", 2, "int32")
                };

                foreach (var t in tests)
                {
                    try
                    {
                        string lastTopic = await ps.SubscribeArray(t.topic, t.length, t.type);
                        SubscribeListTextBox.AppendText($"TEST: {t.topic} ({t.type}) len={t.length} last={lastTopic}{Environment.NewLine}");
                        _mainForm.LogMessage($"[PubSub Test] Subscribed {t.topic} {t.type} len={t.length} last={lastTopic}");
                    }
                    catch (Exception ex)
                    {
                        SubscribeListTextBox.AppendText($"TEST FAILED: {t.topic} ({t.type}) -> {ex.Message}{Environment.NewLine}");
                        _mainForm.LogMessage($"[PubSub Test] Failed {t.topic}: {ex.Message}");
                    }
                }

                if (!ps.IsPolling)
                {
                    ps.StartPolling();
                    _mainForm.LogMessage("[PubSub Test] Polling started.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Test error: {ex.Message}");
            }
        }
    }
}
