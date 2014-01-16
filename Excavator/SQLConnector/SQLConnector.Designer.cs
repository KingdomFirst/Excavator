using System;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Diagnostics;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Security.Permissions;

namespace Excavator
{
    partial class SQLConnector : UserControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            if ( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.sqlAuthenticationRadioButton = new System.Windows.Forms.RadioButton();
            this.windowsAuthenticationRadioButton = new System.Windows.Forms.RadioButton();
            this.userNameLabel = new System.Windows.Forms.Label();
            this.userNameTextBox = new System.Windows.Forms.TextBox();
            this.passwordLabel = new System.Windows.Forms.Label();
            this.passwordTextBox = new System.Windows.Forms.TextBox();
            this.savePasswordCheckBox = new System.Windows.Forms.CheckBox();
            this.loginTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.logonGroupBox = new System.Windows.Forms.GroupBox();
            this.refreshButton = new System.Windows.Forms.Button();
            this.serverTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.serverComboBox = new System.Windows.Forms.ComboBox();
            this.serverLabel = new System.Windows.Forms.Label();
            this.selectDatabaseRadioButton = new System.Windows.Forms.RadioButton();
            this.selectDatabaseComboBox = new System.Windows.Forms.ComboBox();
            this.databaseGroupBox = new System.Windows.Forms.GroupBox();
            this.acceptButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.testConnectionButton = new System.Windows.Forms.Button();
            this.loginTableLayoutPanel.SuspendLayout();
            this.logonGroupBox.SuspendLayout();
            this.serverTableLayoutPanel.SuspendLayout();
            this.databaseGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // sqlAuthenticationRadioButton
            // 
            this.sqlAuthenticationRadioButton.AutoSize = true;
            this.sqlAuthenticationRadioButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.sqlAuthenticationRadioButton.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.sqlAuthenticationRadioButton.Location = new System.Drawing.Point(12, 46);
            this.sqlAuthenticationRadioButton.Margin = new System.Windows.Forms.Padding(9, 3, 9, 0);
            this.sqlAuthenticationRadioButton.Name = "sqlAuthenticationRadioButton";
            this.sqlAuthenticationRadioButton.Size = new System.Drawing.Size(179, 18);
            this.sqlAuthenticationRadioButton.TabIndex = 1;
            this.sqlAuthenticationRadioButton.Text = "Use S&QL Server Authentication";
            // 
            // windowsAuthenticationRadioButton
            // 
            this.windowsAuthenticationRadioButton.AutoSize = true;
            this.windowsAuthenticationRadioButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.windowsAuthenticationRadioButton.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.windowsAuthenticationRadioButton.Location = new System.Drawing.Point(12, 25);
            this.windowsAuthenticationRadioButton.Margin = new System.Windows.Forms.Padding(9, 9, 9, 0);
            this.windowsAuthenticationRadioButton.Name = "windowsAuthenticationRadioButton";
            this.windowsAuthenticationRadioButton.Size = new System.Drawing.Size(168, 18);
            this.windowsAuthenticationRadioButton.TabIndex = 0;
            this.windowsAuthenticationRadioButton.Text = "Use &Windows Authentication";
            // 
            // userNameLabel
            // 
            this.userNameLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.userNameLabel.AutoSize = true;
            this.userNameLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.userNameLabel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.userNameLabel.Location = new System.Drawing.Point(0, 6);
            this.userNameLabel.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.userNameLabel.Name = "userNameLabel";
            this.userNameLabel.Size = new System.Drawing.Size(61, 13);
            this.userNameLabel.TabIndex = 0;
            this.userNameLabel.Text = "&User name:";
            // 
            // userNameTextBox
            // 
            this.userNameTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.userNameTextBox.Location = new System.Drawing.Point(67, 3);
            this.userNameTextBox.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.userNameTextBox.MaxLength = 128;
            this.userNameTextBox.Name = "userNameTextBox";
            this.userNameTextBox.Size = new System.Drawing.Size(241, 20);
            this.userNameTextBox.TabIndex = 1;
            // 
            // passwordLabel
            // 
            this.passwordLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.passwordLabel.AutoSize = true;
            this.passwordLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.passwordLabel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.passwordLabel.Location = new System.Drawing.Point(0, 32);
            this.passwordLabel.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.passwordLabel.Name = "passwordLabel";
            this.passwordLabel.Size = new System.Drawing.Size(56, 13);
            this.passwordLabel.TabIndex = 2;
            this.passwordLabel.Text = "&Password:";
            // 
            // passwordTextBox
            // 
            this.passwordTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.passwordTextBox.Location = new System.Drawing.Point(67, 29);
            this.passwordTextBox.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.passwordTextBox.Name = "passwordTextBox";
            this.passwordTextBox.PasswordChar = '●';
            this.passwordTextBox.Size = new System.Drawing.Size(241, 20);
            this.passwordTextBox.TabIndex = 3;
            this.passwordTextBox.UseSystemPasswordChar = true;
            // 
            // savePasswordCheckBox
            // 
            this.savePasswordCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.savePasswordCheckBox.AutoSize = true;
            this.savePasswordCheckBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.savePasswordCheckBox.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.savePasswordCheckBox.Location = new System.Drawing.Point(67, 52);
            this.savePasswordCheckBox.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.savePasswordCheckBox.Name = "savePasswordCheckBox";
            this.savePasswordCheckBox.Size = new System.Drawing.Size(121, 18);
            this.savePasswordCheckBox.TabIndex = 5;
            this.savePasswordCheckBox.Text = "&Save my password";
            // 
            // loginTableLayoutPanel
            // 
            this.loginTableLayoutPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.loginTableLayoutPanel.AutoSize = true;
            this.loginTableLayoutPanel.ColumnCount = 2;
            this.loginTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.loginTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.loginTableLayoutPanel.Controls.Add(this.userNameLabel, 0, 0);
            this.loginTableLayoutPanel.Controls.Add(this.userNameTextBox, 1, 0);
            this.loginTableLayoutPanel.Controls.Add(this.passwordLabel, 0, 1);
            this.loginTableLayoutPanel.Controls.Add(this.passwordTextBox, 1, 1);
            this.loginTableLayoutPanel.Controls.Add(this.savePasswordCheckBox, 1, 2);
            this.loginTableLayoutPanel.Location = new System.Drawing.Point(30, 64);
            this.loginTableLayoutPanel.Margin = new System.Windows.Forms.Padding(27, 0, 9, 9);
            this.loginTableLayoutPanel.Name = "loginTableLayoutPanel";
            this.loginTableLayoutPanel.RowCount = 3;
            this.loginTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.loginTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.loginTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.loginTableLayoutPanel.Size = new System.Drawing.Size(308, 70);
            this.loginTableLayoutPanel.TabIndex = 2;
            // 
            // logonGroupBox
            // 
            this.logonGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.logonGroupBox.Controls.Add(this.loginTableLayoutPanel);
            this.logonGroupBox.Controls.Add(this.sqlAuthenticationRadioButton);
            this.logonGroupBox.Controls.Add(this.windowsAuthenticationRadioButton);
            this.logonGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.logonGroupBox.Location = new System.Drawing.Point(10, 57);
            this.logonGroupBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.logonGroupBox.Name = "logonGroupBox";
            this.logonGroupBox.Size = new System.Drawing.Size(350, 151);
            this.logonGroupBox.TabIndex = 6;
            this.logonGroupBox.TabStop = false;
            this.logonGroupBox.Text = "Log on to the server";
            // 
            // refreshButton
            // 
            this.refreshButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.refreshButton.AutoSize = true;
            this.refreshButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.refreshButton.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.refreshButton.Location = new System.Drawing.Point(275, 0);
            this.refreshButton.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.refreshButton.MinimumSize = new System.Drawing.Size(75, 23);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(75, 23);
            this.refreshButton.TabIndex = 1;
            this.refreshButton.Text = "&Refresh";
            // 
            // serverTableLayoutPanel
            // 
            this.serverTableLayoutPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.serverTableLayoutPanel.AutoSize = true;
            this.serverTableLayoutPanel.ColumnCount = 2;
            this.serverTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.serverTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.serverTableLayoutPanel.Controls.Add(this.serverComboBox, 0, 0);
            this.serverTableLayoutPanel.Controls.Add(this.refreshButton, 1, 0);
            this.serverTableLayoutPanel.Location = new System.Drawing.Point(10, 27);
            this.serverTableLayoutPanel.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this.serverTableLayoutPanel.Name = "serverTableLayoutPanel";
            this.serverTableLayoutPanel.RowCount = 1;
            this.serverTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.serverTableLayoutPanel.Size = new System.Drawing.Size(350, 24);
            this.serverTableLayoutPanel.TabIndex = 5;
            // 
            // serverComboBox
            // 
            this.serverComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.serverComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.serverComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.serverComboBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.serverComboBox.FormattingEnabled = true;
            this.serverComboBox.Location = new System.Drawing.Point(0, 1);
            this.serverComboBox.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.serverComboBox.Name = "serverComboBox";
            this.serverComboBox.Size = new System.Drawing.Size(269, 21);
            this.serverComboBox.TabIndex = 0;
            // 
            // serverLabel
            // 
            this.serverLabel.AutoSize = true;
            this.serverLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.serverLabel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.serverLabel.Location = new System.Drawing.Point(10, 11);
            this.serverLabel.Margin = new System.Windows.Forms.Padding(0);
            this.serverLabel.Name = "serverLabel";
            this.serverLabel.Size = new System.Drawing.Size(70, 13);
            this.serverLabel.TabIndex = 4;
            this.serverLabel.Text = "S&erver name:";
            // 
            // selectDatabaseRadioButton
            // 
            this.selectDatabaseRadioButton.AutoSize = true;
            this.selectDatabaseRadioButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.selectDatabaseRadioButton.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.selectDatabaseRadioButton.Location = new System.Drawing.Point(12, 25);
            this.selectDatabaseRadioButton.Margin = new System.Windows.Forms.Padding(9, 9, 9, 0);
            this.selectDatabaseRadioButton.Name = "selectDatabaseRadioButton";
            this.selectDatabaseRadioButton.Size = new System.Drawing.Size(188, 18);
            this.selectDatabaseRadioButton.TabIndex = 0;
            this.selectDatabaseRadioButton.Text = "Select or enter a &database name:";
            // 
            // selectDatabaseComboBox
            // 
            this.selectDatabaseComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.selectDatabaseComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.selectDatabaseComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.selectDatabaseComboBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.selectDatabaseComboBox.FormattingEnabled = true;
            this.selectDatabaseComboBox.Location = new System.Drawing.Point(30, 43);
            this.selectDatabaseComboBox.Margin = new System.Windows.Forms.Padding(27, 0, 9, 3);
            this.selectDatabaseComboBox.Name = "selectDatabaseComboBox";
            this.selectDatabaseComboBox.Size = new System.Drawing.Size(308, 21);
            this.selectDatabaseComboBox.TabIndex = 1;
            // 
            // databaseGroupBox
            // 
            this.databaseGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.databaseGroupBox.Controls.Add(this.selectDatabaseComboBox);
            this.databaseGroupBox.Controls.Add(this.selectDatabaseRadioButton);
            this.databaseGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.databaseGroupBox.Location = new System.Drawing.Point(10, 209);
            this.databaseGroupBox.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.databaseGroupBox.Name = "databaseGroupBox";
            this.databaseGroupBox.Size = new System.Drawing.Size(350, 86);
            this.databaseGroupBox.TabIndex = 7;
            this.databaseGroupBox.TabStop = false;
            this.databaseGroupBox.Text = "Connect to a database";
            // 
            // acceptButton
            // 
            this.acceptButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.acceptButton.AutoSize = true;
            this.acceptButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.acceptButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.acceptButton.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.acceptButton.Location = new System.Drawing.Point(204, 301);
            this.acceptButton.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.acceptButton.MinimumSize = new System.Drawing.Size(75, 23);
            this.acceptButton.Name = "acceptButton";
            this.acceptButton.Size = new System.Drawing.Size(75, 23);
            this.acceptButton.TabIndex = 8;
            this.acceptButton.Text = "OK";
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.AutoSize = true;
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.cancelButton.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.cancelButton.Location = new System.Drawing.Point(285, 301);
            this.cancelButton.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.cancelButton.MinimumSize = new System.Drawing.Size(75, 23);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 9;
            this.cancelButton.Text = "Cancel";
            // 
            // testConnectionButton
            // 
            this.testConnectionButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.testConnectionButton.AutoSize = true;
            this.testConnectionButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.testConnectionButton.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.testConnectionButton.Location = new System.Drawing.Point(13, 301);
            this.testConnectionButton.Margin = new System.Windows.Forms.Padding(3, 3, 6, 3);
            this.testConnectionButton.MinimumSize = new System.Drawing.Size(101, 23);
            this.testConnectionButton.Name = "testConnectionButton";
            this.testConnectionButton.Size = new System.Drawing.Size(101, 23);
            this.testConnectionButton.TabIndex = 10;
            this.testConnectionButton.Text = "&Test Connection";
            // 
            // SQLConnector
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.testConnectionButton);
            this.Controls.Add(this.acceptButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.databaseGroupBox);
            this.Controls.Add(this.logonGroupBox);
            this.Controls.Add(this.serverTableLayoutPanel);
            this.Controls.Add(this.serverLabel);
            this.Name = "SQLConnector";
            this.Size = new System.Drawing.Size(371, 330);
            this.loginTableLayoutPanel.ResumeLayout(false);
            this.loginTableLayoutPanel.PerformLayout();
            this.logonGroupBox.ResumeLayout(false);
            this.logonGroupBox.PerformLayout();
            this.serverTableLayoutPanel.ResumeLayout(false);
            this.serverTableLayoutPanel.PerformLayout();
            this.databaseGroupBox.ResumeLayout(false);
            this.databaseGroupBox.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private RadioButton sqlAuthenticationRadioButton;
        private RadioButton windowsAuthenticationRadioButton;
        private Label userNameLabel;
        private TextBox userNameTextBox;
        private Label passwordLabel;
        private TextBox passwordTextBox;
        private CheckBox savePasswordCheckBox;
        private TableLayoutPanel loginTableLayoutPanel;
        private GroupBox logonGroupBox;
        private Button refreshButton;
        private TableLayoutPanel serverTableLayoutPanel;
        private ComboBox serverComboBox;
        private Label serverLabel;
        private RadioButton selectDatabaseRadioButton;
        private ComboBox selectDatabaseComboBox;
        private GroupBox databaseGroupBox;
        private Button acceptButton;
        private Button cancelButton;
        private Button testConnectionButton;

    }
}
