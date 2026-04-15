namespace S7CommunicationApp
{
    partial class PublishAndSubscribe
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
            ReadCheckButton = new Button();
            SubscribeListTextBox = new TextBox();
            SubscribeListLabel = new Label();
            AddressLabel = new Label();
            AddressTextBox = new TextBox();
            DataTypeLabel = new Label();
            DataTypeTextBox = new TextBox();
            SubscribeButton = new Button();
            UnsubscribeButton = new Button();
            ReadCheckTextBox = new TextBox();
            LengthTextBox = new TextBox();
            LengthLabel = new Label();
            SuspendLayout();
            // 
            // ReadCheckButton
            // 
            ReadCheckButton.Location = new Point(433, 272);
            ReadCheckButton.Name = "ReadCheckButton";
            ReadCheckButton.Size = new Size(356, 34);
            ReadCheckButton.TabIndex = 0;
            ReadCheckButton.Text = "Read Check";
            ReadCheckButton.UseVisualStyleBackColor = true;
            ReadCheckButton.Click += ReadCheckButton_Click;
            // 
            // SubscribeListTextBox
            // 
            SubscribeListTextBox.Location = new Point(57, 95);
            SubscribeListTextBox.Multiline = true;
            SubscribeListTextBox.Name = "SubscribeListTextBox";
            SubscribeListTextBox.ReadOnly = true;
            SubscribeListTextBox.Size = new Size(335, 309);
            SubscribeListTextBox.TabIndex = 1;
            // 
            // SubscribeListLabel
            // 
            SubscribeListLabel.AutoSize = true;
            SubscribeListLabel.Location = new Point(57, 67);
            SubscribeListLabel.Name = "SubscribeListLabel";
            SubscribeListLabel.Size = new Size(129, 25);
            SubscribeListLabel.TabIndex = 2;
            SubscribeListLabel.Text = "Subscribe List: ";
            // 
            // AddressLabel
            // 
            AddressLabel.AutoSize = true;
            AddressLabel.Location = new Point(433, 67);
            AddressLabel.Name = "AddressLabel";
            AddressLabel.Size = new Size(86, 25);
            AddressLabel.TabIndex = 3;
            AddressLabel.Text = "Address: ";
            // 
            // AddressTextBox
            // 
            AddressTextBox.Location = new Point(433, 95);
            AddressTextBox.Name = "AddressTextBox";
            AddressTextBox.PlaceholderText = "DB1.DBX0.0";
            AddressTextBox.Size = new Size(213, 31);
            AddressTextBox.TabIndex = 4;
            // 
            // DataTypeLabel
            // 
            DataTypeLabel.AutoSize = true;
            DataTypeLabel.Location = new Point(433, 150);
            DataTypeLabel.Name = "DataTypeLabel";
            DataTypeLabel.Size = new Size(105, 25);
            DataTypeLabel.TabIndex = 5;
            DataTypeLabel.Text = "Data Type:  ";
            // 
            // DataTypeTextBox
            // 
            DataTypeTextBox.Location = new Point(433, 178);
            DataTypeTextBox.Name = "DataTypeTextBox";
            DataTypeTextBox.PlaceholderText = "Bool";
            DataTypeTextBox.Size = new Size(356, 31);
            DataTypeTextBox.TabIndex = 6;
            // 
            // SubscribeButton
            // 
            SubscribeButton.Location = new Point(433, 330);
            SubscribeButton.Name = "SubscribeButton";
            SubscribeButton.Size = new Size(356, 34);
            SubscribeButton.TabIndex = 7;
            SubscribeButton.Text = "Subscribe";
            SubscribeButton.UseVisualStyleBackColor = true;
            SubscribeButton.Click += SubscribeButton_Click;
            // 
            // UnsubscribeButton
            // 
            UnsubscribeButton.Location = new Point(433, 370);
            UnsubscribeButton.Name = "UnsubscribeButton";
            UnsubscribeButton.Size = new Size(356, 34);
            UnsubscribeButton.TabIndex = 8;
            UnsubscribeButton.Text = "Unsubscribe";
            UnsubscribeButton.UseVisualStyleBackColor = true;
            UnsubscribeButton.Click += UnsubscribeButton_Click;
            // 
            // ReadCheckTextBox
            // 
            ReadCheckTextBox.Location = new Point(433, 235);
            ReadCheckTextBox.Name = "ReadCheckTextBox";
            ReadCheckTextBox.ReadOnly = true;
            ReadCheckTextBox.Size = new Size(356, 31);
            ReadCheckTextBox.TabIndex = 9;
            // 
            // LengthTextBox
            // 
            LengthTextBox.Location = new Point(652, 95);
            LengthTextBox.Name = "LengthTextBox";
            LengthTextBox.PlaceholderText = "10 (For Array)";
            LengthTextBox.Size = new Size(137, 31);
            LengthTextBox.TabIndex = 11;
            // 
            // LengthLabel
            // 
            LengthLabel.AutoSize = true;
            LengthLabel.Location = new Point(652, 67);
            LengthLabel.Name = "LengthLabel";
            LengthLabel.Size = new Size(66, 25);
            LengthLabel.TabIndex = 10;
            LengthLabel.Text = "Length";
            // 
            // PublishAndSubscribe
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(848, 450);
            Controls.Add(LengthTextBox);
            Controls.Add(LengthLabel);
            Controls.Add(ReadCheckTextBox);
            Controls.Add(UnsubscribeButton);
            Controls.Add(SubscribeButton);
            Controls.Add(DataTypeTextBox);
            Controls.Add(DataTypeLabel);
            Controls.Add(AddressTextBox);
            Controls.Add(AddressLabel);
            Controls.Add(SubscribeListLabel);
            Controls.Add(SubscribeListTextBox);
            Controls.Add(ReadCheckButton);
            Name = "PublishAndSubscribe";
            Text = "PublishAndSubscribe";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button ReadCheckButton;
        private TextBox SubscribeListTextBox;
        private Label SubscribeListLabel;
        private Label AddressLabel;
        private TextBox AddressTextBox;
        private Label DataTypeLabel;
        private TextBox DataTypeTextBox;
        private Button SubscribeButton;
        private Button UnsubscribeButton;
        private TextBox ReadCheckTextBox;
        private TextBox LengthTextBox;
        private Label LengthLabel;
    }
}