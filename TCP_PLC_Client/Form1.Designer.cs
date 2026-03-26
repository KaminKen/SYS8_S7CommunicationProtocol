namespace S7Plus_PLC_Client
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            LogTextBox = new TextBox();
            LogLabel = new Label();
            IpLabel = new Label();
            PortLabel = new Label();
            IPTextBox = new TextBox();
            PortTextBox = new TextBox();
            ConnectButton = new Button();
            DisconnectButton = new Button();
            ConnectionStatusTextBox = new TextBox();
            AddressTextBox = new TextBox();
            AddressLabel = new Label();
            DataTypeComBox = new ComboBox();
            DataTypeComBoxLabel = new Label();
            ReadAllButton = new Button();
            WriteToPlcButton = new Button();
            DataBoxLabel = new Label();
            DataBoxTextBox = new TextBox();
            SuspendLayout();
            // 
            // LogTextBox
            // 
            LogTextBox.Location = new Point(76, 172);
            LogTextBox.Multiline = true;
            LogTextBox.Name = "LogTextBox";
            LogTextBox.ReadOnly = true;
            LogTextBox.ScrollBars = ScrollBars.Vertical;
            LogTextBox.Size = new Size(532, 316);
            LogTextBox.TabIndex = 0;
            // 
            // LogLabel
            // 
            LogLabel.AutoSize = true;
            LogLabel.Location = new Point(77, 144);
            LogLabel.Name = "LogLabel";
            LogLabel.Size = new Size(46, 25);
            LogLabel.TabIndex = 2;
            LogLabel.Text = "Log:";
            // 
            // IpLabel
            // 
            IpLabel.AutoSize = true;
            IpLabel.Location = new Point(77, 53);
            IpLabel.Name = "IpLabel";
            IpLabel.Size = new Size(31, 25);
            IpLabel.TabIndex = 3;
            IpLabel.Text = "IP:";
            // 
            // PortLabel
            // 
            PortLabel.AutoSize = true;
            PortLabel.Location = new Point(77, 91);
            PortLabel.Name = "PortLabel";
            PortLabel.Size = new Size(48, 25);
            PortLabel.TabIndex = 4;
            PortLabel.Text = "Port:";
            // 
            // IPTextBox
            // 
            IPTextBox.Location = new Point(148, 50);
            IPTextBox.Name = "IPTextBox";
            IPTextBox.PlaceholderText = "192.168.0.1";
            IPTextBox.Size = new Size(202, 31);
            IPTextBox.TabIndex = 5;
            IPTextBox.TextChanged += IPTextBox_TextChanged;
            // 
            // PortTextBox
            // 
            PortTextBox.Location = new Point(148, 88);
            PortTextBox.Name = "PortTextBox";
            PortTextBox.PlaceholderText = "102";
            PortTextBox.Size = new Size(202, 31);
            PortTextBox.TabIndex = 7;
            // 
            // ConnectButton
            // 
            ConnectButton.Location = new Point(356, 48);
            ConnectButton.Name = "ConnectButton";
            ConnectButton.Size = new Size(125, 34);
            ConnectButton.TabIndex = 8;
            ConnectButton.Text = "Connect";
            ConnectButton.UseVisualStyleBackColor = true;
            ConnectButton.Click += ConnectButton_Click;
            // 
            // DisconnectButton
            // 
            DisconnectButton.Enabled = false;
            DisconnectButton.Location = new Point(483, 48);
            DisconnectButton.Name = "DisconnectButton";
            DisconnectButton.Size = new Size(125, 34);
            DisconnectButton.TabIndex = 9;
            DisconnectButton.Text = "Disconnect";
            DisconnectButton.UseVisualStyleBackColor = true;
            DisconnectButton.Click += DisconnectButton_Click;
            // 
            // ConnectionStatusTextBox
            // 
            ConnectionStatusTextBox.Location = new Point(356, 91);
            ConnectionStatusTextBox.Name = "ConnectionStatusTextBox";
            ConnectionStatusTextBox.PlaceholderText = "Disconnected";
            ConnectionStatusTextBox.ReadOnly = true;
            ConnectionStatusTextBox.Size = new Size(252, 31);
            ConnectionStatusTextBox.TabIndex = 10;
            // 
            // AddressTextBox
            // 
            AddressTextBox.Location = new Point(648, 172);
            AddressTextBox.Name = "AddressTextBox";
            AddressTextBox.Size = new Size(309, 31);
            AddressTextBox.TabIndex = 11;
            // 
            // AddressLabel
            // 
            AddressLabel.AutoSize = true;
            AddressLabel.Location = new Point(648, 144);
            AddressLabel.Name = "AddressLabel";
            AddressLabel.Size = new Size(81, 25);
            AddressLabel.TabIndex = 12;
            AddressLabel.Text = "Address:";
            // 
            // DataTypeComBox
            // 
            DataTypeComBox.CausesValidation = false;
            DataTypeComBox.FormattingEnabled = true;
            DataTypeComBox.Items.AddRange(new object[] { "Boolean", "Int16", "Int32", "Float", "String" });
            DataTypeComBox.Location = new Point(648, 256);
            DataTypeComBox.Name = "DataTypeComBox";
            DataTypeComBox.Size = new Size(309, 33);
            DataTypeComBox.TabIndex = 13;
            // 
            // DataTypeComBoxLabel
            // 
            DataTypeComBoxLabel.AutoSize = true;
            DataTypeComBoxLabel.Location = new Point(648, 228);
            DataTypeComBoxLabel.Name = "DataTypeComBoxLabel";
            DataTypeComBoxLabel.Size = new Size(95, 25);
            DataTypeComBoxLabel.TabIndex = 14;
            DataTypeComBoxLabel.Text = "Data Type:";
            // 
            // ReadAllButton
            // 
            ReadAllButton.Location = new Point(648, 313);
            ReadAllButton.Name = "ReadAllButton";
            ReadAllButton.Size = new Size(309, 52);
            ReadAllButton.TabIndex = 15;
            ReadAllButton.Text = "Read";
            ReadAllButton.UseVisualStyleBackColor = true;
            ReadAllButton.Click += ReadAllButton_Click;
            // 
            // WriteToPlcButton
            // 
            WriteToPlcButton.Location = new Point(978, 313);
            WriteToPlcButton.Name = "WriteToPlcButton";
            WriteToPlcButton.Size = new Size(309, 52);
            WriteToPlcButton.TabIndex = 18;
            WriteToPlcButton.Text = "Write";
            WriteToPlcButton.UseVisualStyleBackColor = true;
            WriteToPlcButton.Click += WriteToPlcButton_Click;
            // 
            // DataBoxLabel
            // 
            DataBoxLabel.AutoSize = true;
            DataBoxLabel.Location = new Point(978, 144);
            DataBoxLabel.Name = "DataBoxLabel";
            DataBoxLabel.Size = new Size(108, 25);
            DataBoxLabel.TabIndex = 17;
            DataBoxLabel.Text = "Data to PLC:";
            // 
            // DataBoxTextBox
            // 
            DataBoxTextBox.Location = new Point(978, 172);
            DataBoxTextBox.Multiline = true;
            DataBoxTextBox.Name = "DataBoxTextBox";
            DataBoxTextBox.ScrollBars = ScrollBars.Vertical;
            DataBoxTextBox.Size = new Size(309, 117);
            DataBoxTextBox.TabIndex = 19;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1340, 554);
            Controls.Add(DataBoxTextBox);
            Controls.Add(WriteToPlcButton);
            Controls.Add(DataBoxLabel);
            Controls.Add(ReadAllButton);
            Controls.Add(DataTypeComBoxLabel);
            Controls.Add(DataTypeComBox);
            Controls.Add(AddressLabel);
            Controls.Add(AddressTextBox);
            Controls.Add(ConnectionStatusTextBox);
            Controls.Add(DisconnectButton);
            Controls.Add(ConnectButton);
            Controls.Add(PortTextBox);
            Controls.Add(IPTextBox);
            Controls.Add(PortLabel);
            Controls.Add(IpLabel);
            Controls.Add(LogLabel);
            Controls.Add(LogTextBox);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox LogTextBox;
        private Label LogLabel;
        private Label IpLabel;
        private Label PortLabel;
        private TextBox IPTextBox;
        private TextBox PortTextBox;
        private Button ConnectButton;
        private Button DisconnectButton;
        private TextBox ConnectionStatusTextBox;
        private TextBox AddressTextBox;
        private Label AddressLabel;
        private ComboBox DataTypeComBox;
        private Label DataTypeComBoxLabel;
        private Button ReadAllButton;
        private Button WriteToPlcButton;
        private Label DataBoxLabel;
        private TextBox DataBoxTextBox;
    }
}
