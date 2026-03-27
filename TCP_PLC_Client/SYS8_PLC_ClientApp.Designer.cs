namespace S7CommunicationApp
{
    partial class SYS8_PLC_ClientApp
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            IpTextBox = new TextBox();
            PortTextBox = new TextBox();
            IpLabel = new Label();
            PortLabel = new Label();
            LogTextBox = new TextBox();
            LogLabel = new Label();
            ConnectButton = new Button();
            DisconnectButton = new Button();
            StatusTextBox = new TextBox();
            AddressTextBox = new TextBox();
            AddressLabel = new Label();
            DataTypeComBox = new ComboBox();
            DataTypeLabel = new Label();
            ReadButton = new Button();
            WriteButton = new Button();
            DataTextBox = new TextBox();
            DataTextBoxLabel = new Label();
            SuspendLayout();
            // 
            // IpTextBox
            // 
            IpTextBox.Location = new Point(154, 105);
            IpTextBox.Name = "IpTextBox";
            IpTextBox.Size = new Size(275, 31);
            IpTextBox.TabIndex = 0;
            IpTextBox.Text = "192.168.0.1";
            // 
            // PortTextBox
            // 
            PortTextBox.Location = new Point(154, 142);
            PortTextBox.Name = "PortTextBox";
            PortTextBox.Size = new Size(275, 31);
            PortTextBox.TabIndex = 1;
            PortTextBox.Text = "102";
            // 
            // IpLabel
            // 
            IpLabel.AutoSize = true;
            IpLabel.Location = new Point(112, 108);
            IpLabel.Name = "IpLabel";
            IpLabel.Size = new Size(36, 25);
            IpLabel.TabIndex = 2;
            IpLabel.Text = "IP: ";
            // 
            // PortLabel
            // 
            PortLabel.AutoSize = true;
            PortLabel.Location = new Point(100, 145);
            PortLabel.Name = "PortLabel";
            PortLabel.Size = new Size(48, 25);
            PortLabel.TabIndex = 3;
            PortLabel.Text = "Port:";
            // 
            // LogTextBox
            // 
            LogTextBox.Location = new Point(100, 237);
            LogTextBox.Multiline = true;
            LogTextBox.Name = "LogTextBox";
            LogTextBox.ReadOnly = true;
            LogTextBox.Size = new Size(561, 364);
            LogTextBox.TabIndex = 4;
            // 
            // LogLabel
            // 
            LogLabel.AutoSize = true;
            LogLabel.Location = new Point(100, 209);
            LogLabel.Name = "LogLabel";
            LogLabel.Size = new Size(46, 25);
            LogLabel.TabIndex = 5;
            LogLabel.Text = "Log:";
            // 
            // ConnectButton
            // 
            ConnectButton.Location = new Point(435, 105);
            ConnectButton.Name = "ConnectButton";
            ConnectButton.Size = new Size(110, 31);
            ConnectButton.TabIndex = 6;
            ConnectButton.Text = "Connect";
            ConnectButton.UseVisualStyleBackColor = true;
            ConnectButton.Click += ConnectButton_Click;
            // 
            // DisconnectButton
            // 
            DisconnectButton.Location = new Point(551, 105);
            DisconnectButton.Name = "DisconnectButton";
            DisconnectButton.Size = new Size(110, 31);
            DisconnectButton.TabIndex = 7;
            DisconnectButton.Text = "Disconnect";
            DisconnectButton.UseVisualStyleBackColor = true;
            DisconnectButton.Click += DisconnectButton_Click;
            // 
            // StatusTextBox
            // 
            StatusTextBox.Location = new Point(435, 142);
            StatusTextBox.Name = "StatusTextBox";
            StatusTextBox.ReadOnly = true;
            StatusTextBox.Size = new Size(226, 31);
            StatusTextBox.TabIndex = 8;
            StatusTextBox.Text = "Disconnected";
            // 
            // AddressTextBox
            // 
            AddressTextBox.Location = new Point(700, 142);
            AddressTextBox.Name = "AddressTextBox";
            AddressTextBox.PlaceholderText = "DB1.DBX0.0";
            AddressTextBox.Size = new Size(400, 31);
            AddressTextBox.TabIndex = 9;
            // 
            // AddressLabel
            // 
            AddressLabel.AutoSize = true;
            AddressLabel.Location = new Point(700, 105);
            AddressLabel.Name = "AddressLabel";
            AddressLabel.Size = new Size(162, 25);
            AddressLabel.TabIndex = 10;
            AddressLabel.Text = "Absolute Address: ";
            // 
            // DataTypeComBox
            // 
            DataTypeComBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            DataTypeComBox.FormattingEnabled = true;
            DataTypeComBox.Items.AddRange(new object[] { "Bool" });
            DataTypeComBox.Location = new Point(700, 237);
            DataTypeComBox.Name = "DataTypeComBox";
            DataTypeComBox.Size = new Size(400, 33);
            DataTypeComBox.TabIndex = 11;
            // 
            // DataTypeLabel
            // 
            DataTypeLabel.AutoSize = true;
            DataTypeLabel.Location = new Point(700, 209);
            DataTypeLabel.Name = "DataTypeLabel";
            DataTypeLabel.Size = new Size(95, 25);
            DataTypeLabel.TabIndex = 12;
            DataTypeLabel.Text = "Data Type:";
            // 
            // ReadButton
            // 
            ReadButton.Location = new Point(700, 567);
            ReadButton.Name = "ReadButton";
            ReadButton.Size = new Size(194, 34);
            ReadButton.TabIndex = 13;
            ReadButton.Text = "Read";
            ReadButton.UseVisualStyleBackColor = true;
            ReadButton.Click += ReadButton_Click;
            // 
            // WriteButton
            // 
            WriteButton.Location = new Point(906, 567);
            WriteButton.Name = "WriteButton";
            WriteButton.Size = new Size(194, 34);
            WriteButton.TabIndex = 14;
            WriteButton.Text = "Write";
            WriteButton.UseVisualStyleBackColor = true;
            WriteButton.Click += WriteButton_Click;
            // 
            // DataTextBox
            // 
            DataTextBox.Location = new Point(700, 327);
            DataTextBox.Multiline = true;
            DataTextBox.Name = "DataTextBox";
            DataTextBox.Size = new Size(400, 234);
            DataTextBox.TabIndex = 15;
            // 
            // DataTextBoxLabel
            // 
            DataTextBoxLabel.AutoSize = true;
            DataTextBoxLabel.Location = new Point(700, 299);
            DataTextBoxLabel.Name = "DataTextBoxLabel";
            DataTextBoxLabel.Size = new Size(58, 25);
            DataTextBoxLabel.TabIndex = 16;
            DataTextBoxLabel.Text = "Data: ";
            // 
            // SYS8_PLC_ClientApp
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1204, 660);
            Controls.Add(DataTextBoxLabel);
            Controls.Add(DataTextBox);
            Controls.Add(WriteButton);
            Controls.Add(ReadButton);
            Controls.Add(DataTypeLabel);
            Controls.Add(DataTypeComBox);
            Controls.Add(AddressLabel);
            Controls.Add(AddressTextBox);
            Controls.Add(StatusTextBox);
            Controls.Add(DisconnectButton);
            Controls.Add(ConnectButton);
            Controls.Add(LogLabel);
            Controls.Add(LogTextBox);
            Controls.Add(PortLabel);
            Controls.Add(IpLabel);
            Controls.Add(PortTextBox);
            Controls.Add(IpTextBox);
            Name = "SYS8_PLC_ClientApp";
            Text = "SYS8_PLC_ClientApp";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox IpTextBox;
        private TextBox PortTextBox;
        private Label IpLabel;
        private Label PortLabel;
        private TextBox LogTextBox;
        private Label LogLabel;
        private Button ConnectButton;
        private Button DisconnectButton;
        private TextBox StatusTextBox;
        private TextBox AddressTextBox;
        private Label AddressLabel;
        private ComboBox DataTypeComBox;
        private Label DataTypeLabel;
        private Button ReadButton;
        private Button WriteButton;
        private TextBox DataTextBox;
        private Label DataTextBoxLabel;
    }
}