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
            PublishAndSubscribeModeButton = new Button();
            ReadWriteModeButton = new Button();
            ModeLabel = new Label();
            ModeStatusTextBox = new TextBox();
            LengthLabel = new Label();
            LengthTextBox = new TextBox();
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
            LogTextBox.ScrollBars = ScrollBars.Vertical;
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
            AddressTextBox.Location = new Point(700, 237);
            AddressTextBox.Name = "AddressTextBox";
            AddressTextBox.PlaceholderText = "DB1.DBX0.0";
            AddressTextBox.Size = new Size(265, 31);
            AddressTextBox.TabIndex = 9;
            // 
            // AddressLabel
            // 
            AddressLabel.AutoSize = true;
            AddressLabel.Location = new Point(700, 209);
            AddressLabel.Name = "AddressLabel";
            AddressLabel.Size = new Size(162, 25);
            AddressLabel.TabIndex = 10;
            AddressLabel.Text = "Absolute Address: ";
            // 
            // DataTypeComBox
            // 
            DataTypeComBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            DataTypeComBox.FormattingEnabled = true;
            DataTypeComBox.Items.AddRange(new object[] { "Bool", "Int16", "Int32", "Int64", "UInt16", "UInt32", "UInt64", "Float32", "Float64", "String" });
            DataTypeComBox.Location = new Point(700, 314);
            DataTypeComBox.Name = "DataTypeComBox";
            DataTypeComBox.Size = new Size(400, 33);
            DataTypeComBox.TabIndex = 11;
            // 
            // DataTypeLabel
            // 
            DataTypeLabel.AutoSize = true;
            DataTypeLabel.Location = new Point(700, 286);
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
            DataTextBox.Location = new Point(700, 392);
            DataTextBox.Multiline = true;
            DataTextBox.Name = "DataTextBox";
            DataTextBox.ScrollBars = ScrollBars.Vertical;
            DataTextBox.Size = new Size(400, 169);
            DataTextBox.TabIndex = 15;
            // 
            // DataTextBoxLabel
            // 
            DataTextBoxLabel.AutoSize = true;
            DataTextBoxLabel.Location = new Point(700, 364);
            DataTextBoxLabel.Name = "DataTextBoxLabel";
            DataTextBoxLabel.Size = new Size(58, 25);
            DataTextBoxLabel.TabIndex = 16;
            DataTextBoxLabel.Text = "Data: ";
            // 
            // PublishAndSubscribeModeButton
            // 
            PublishAndSubscribeModeButton.AccessibleRole = AccessibleRole.None;
            PublishAndSubscribeModeButton.Location = new Point(700, 140);
            PublishAndSubscribeModeButton.Name = "PublishAndSubscribeModeButton";
            PublishAndSubscribeModeButton.Size = new Size(194, 34);
            PublishAndSubscribeModeButton.TabIndex = 17;
            PublishAndSubscribeModeButton.Text = "Publish and Subscribe";
            PublishAndSubscribeModeButton.UseVisualStyleBackColor = true;
            PublishAndSubscribeModeButton.Click += PublishAndSubscribeModeButton_Click;
            // 
            // ReadWriteModeButton
            // 
            ReadWriteModeButton.AccessibleRole = AccessibleRole.None;
            ReadWriteModeButton.Location = new Point(906, 140);
            ReadWriteModeButton.Name = "ReadWriteModeButton";
            ReadWriteModeButton.Size = new Size(194, 34);
            ReadWriteModeButton.TabIndex = 18;
            ReadWriteModeButton.Text = "Read/Write";
            ReadWriteModeButton.UseVisualStyleBackColor = true;
            ReadWriteModeButton.Click += ReadWriteModeButton_Click;
            // 
            // ModeLabel
            // 
            ModeLabel.AutoSize = true;
            ModeLabel.Location = new Point(700, 111);
            ModeLabel.Name = "ModeLabel";
            ModeLabel.Size = new Size(68, 25);
            ModeLabel.TabIndex = 19;
            ModeLabel.Text = "Mode: ";
            // 
            // ModeStatusTextBox
            // 
            ModeStatusTextBox.Location = new Point(774, 108);
            ModeStatusTextBox.Name = "ModeStatusTextBox";
            ModeStatusTextBox.ReadOnly = true;
            ModeStatusTextBox.Size = new Size(326, 31);
            ModeStatusTextBox.TabIndex = 20;
            ModeStatusTextBox.Text = "Read/Write";
            // 
            // LengthLabel
            // 
            LengthLabel.AutoSize = true;
            LengthLabel.Location = new Point(971, 209);
            LengthLabel.Name = "LengthLabel";
            LengthLabel.Size = new Size(70, 25);
            LengthLabel.TabIndex = 22;
            LengthLabel.Text = "Length:";
            // 
            // LengthTextBox
            // 
            LengthTextBox.Location = new Point(971, 237);
            LengthTextBox.Name = "LengthTextBox";
            LengthTextBox.PlaceholderText = "10 (For Array)";
            LengthTextBox.Size = new Size(129, 31);
            LengthTextBox.TabIndex = 21;
            // 
            // SYS8_PLC_ClientApp
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1288, 660);
            Controls.Add(LengthLabel);
            Controls.Add(LengthTextBox);
            Controls.Add(ModeStatusTextBox);
            Controls.Add(ModeLabel);
            Controls.Add(ReadWriteModeButton);
            Controls.Add(PublishAndSubscribeModeButton);
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
        private Button PublishAndSubscribeModeButton;
        private Button ReadWriteModeButton;
        private Label ModeLabel;
        private TextBox ModeStatusTextBox;
        private Label LengthLabel;
        private TextBox LengthTextBox;
    }
}