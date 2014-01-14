namespace Excavator
{
    partial class FrontEndLoader
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrontEndLoader));
            this.btnUpload = new System.Windows.Forms.Button();
            this.lstDatabaseType = new System.Windows.Forms.ListBox();
            this.lblDatabaseType = new System.Windows.Forms.Label();
            this.btnSQLConnect = new System.Windows.Forms.Button();
            this.tabNavigation = new System.Windows.Forms.TabControl();
            this.Connection = new System.Windows.Forms.TabPage();
            this.Transformation = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.Confirmation = new System.Windows.Forms.TabPage();
            this.Preview = new System.Windows.Forms.TabPage();
            this.tabNavigation.SuspendLayout();
            this.Connection.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnUpload
            // 
            this.btnUpload.Font = new System.Drawing.Font("Open Sans", 12F);
            this.btnUpload.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnUpload.Location = new System.Drawing.Point(65, 305);
            this.btnUpload.Name = "btnUpload";
            this.btnUpload.Size = new System.Drawing.Size(85, 28);
            this.btnUpload.TabIndex = 0;
            this.btnUpload.Text = "Upload";
            this.btnUpload.UseVisualStyleBackColor = true;
            this.btnUpload.Click += new System.EventHandler(this.btnUpload_Click);
            // 
            // lstDatabaseType
            // 
            this.lstDatabaseType.BackColor = System.Drawing.SystemColors.ControlLight;
            this.lstDatabaseType.ForeColor = System.Drawing.SystemColors.ControlText;
            this.lstDatabaseType.FormattingEnabled = true;
            this.lstDatabaseType.ItemHeight = 22;
            this.lstDatabaseType.Location = new System.Drawing.Point(65, 418);
            this.lstDatabaseType.Name = "lstDatabaseType";
            this.lstDatabaseType.Size = new System.Drawing.Size(145, 26);
            this.lstDatabaseType.TabIndex = 1;
            this.lstDatabaseType.SelectedIndexChanged += new System.EventHandler(this.lstDatabaseType_SelectedIndexChanged);
            // 
            // lblDatabaseType
            // 
            this.lblDatabaseType.AutoSize = true;
            this.lblDatabaseType.ForeColor = System.Drawing.SystemColors.Control;
            this.lblDatabaseType.Location = new System.Drawing.Point(19, 370);
            this.lblDatabaseType.Name = "lblDatabaseType";
            this.lblDatabaseType.Size = new System.Drawing.Size(199, 22);
            this.lblDatabaseType.TabIndex = 2;
            this.lblDatabaseType.Text = "Select the database type:";
            // 
            // btnSQLConnect
            // 
            this.btnSQLConnect.Font = new System.Drawing.Font("Open Sans", 12F);
            this.btnSQLConnect.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnSQLConnect.Location = new System.Drawing.Point(65, 123);
            this.btnSQLConnect.Name = "btnSQLConnect";
            this.btnSQLConnect.Size = new System.Drawing.Size(85, 28);
            this.btnSQLConnect.TabIndex = 4;
            this.btnSQLConnect.Text = "Connect";
            this.btnSQLConnect.UseVisualStyleBackColor = true;
            // 
            // tabNavigation
            // 
            this.tabNavigation.Alignment = System.Windows.Forms.TabAlignment.Right;
            this.tabNavigation.Controls.Add(this.Connection);
            this.tabNavigation.Controls.Add(this.Transformation);
            this.tabNavigation.Controls.Add(this.Preview);
            this.tabNavigation.Controls.Add(this.Confirmation);
            this.tabNavigation.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tabNavigation.Font = new System.Drawing.Font("Open Sans", 12F);
            this.tabNavigation.ItemSize = new System.Drawing.Size(40, 150);
            this.tabNavigation.Location = new System.Drawing.Point(-4, -5);
            this.tabNavigation.Multiline = true;
            this.tabNavigation.Name = "tabNavigation";
            this.tabNavigation.SelectedIndex = 0;
            this.tabNavigation.Size = new System.Drawing.Size(888, 566);
            this.tabNavigation.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabNavigation.TabIndex = 5;
            // 
            // Connection
            // 
            this.Connection.BackColor = System.Drawing.Color.DarkGray;
            this.Connection.Controls.Add(this.label2);
            this.Connection.Controls.Add(this.btnSQLConnect);
            this.Connection.Controls.Add(this.lstDatabaseType);
            this.Connection.Controls.Add(this.label1);
            this.Connection.Controls.Add(this.btnUpload);
            this.Connection.Controls.Add(this.lblDatabaseType);
            this.Connection.Font = new System.Drawing.Font("Open Sans", 12F);
            this.Connection.ForeColor = System.Drawing.SystemColors.Control;
            this.Connection.Location = new System.Drawing.Point(4, 4);
            this.Connection.Name = "Connection";
            this.Connection.Padding = new System.Windows.Forms.Padding(3);
            this.Connection.Size = new System.Drawing.Size(730, 558);
            this.Connection.TabIndex = 0;
            this.Connection.Text = "Connect";
            // 
            // Transformation
            // 
            this.Transformation.BackColor = System.Drawing.Color.DarkGray;
            this.Transformation.Font = new System.Drawing.Font("Open Sans", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Transformation.Location = new System.Drawing.Point(4, 4);
            this.Transformation.Name = "Transformation";
            this.Transformation.Padding = new System.Windows.Forms.Padding(3);
            this.Transformation.Size = new System.Drawing.Size(730, 558);
            this.Transformation.TabIndex = 1;
            this.Transformation.Text = "Transformation";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.SystemColors.Control;
            this.label1.Location = new System.Drawing.Point(19, 75);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(196, 22);
            this.label1.TabIndex = 6;
            this.label1.Text = "Connect to a SQL Server:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.SystemColors.Control;
            this.label2.Location = new System.Drawing.Point(19, 261);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(141, 22);
            this.label2.TabIndex = 7;
            this.label2.Text = "Upload a SQL file:";
            // 
            // Confirmation
            // 
            this.Confirmation.BackColor = System.Drawing.Color.DarkGray;
            this.Confirmation.Font = new System.Drawing.Font("Open Sans", 12F);
            this.Confirmation.Location = new System.Drawing.Point(4, 4);
            this.Confirmation.Name = "Confirmation";
            this.Confirmation.Size = new System.Drawing.Size(730, 558);
            this.Confirmation.TabIndex = 2;
            this.Confirmation.Text = "Confirmation";
            // 
            // Preview
            // 
            this.Preview.BackColor = System.Drawing.Color.DarkGray;
            this.Preview.Font = new System.Drawing.Font("Open Sans", 12F);
            this.Preview.Location = new System.Drawing.Point(4, 4);
            this.Preview.Name = "Preview";
            this.Preview.Size = new System.Drawing.Size(730, 558);
            this.Preview.TabIndex = 3;
            this.Preview.Text = "Preview";
            // 
            // FrontEndLoader
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.SystemColors.ControlDark;
            this.ClientSize = new System.Drawing.Size(884, 561);
            this.Controls.Add(this.tabNavigation);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(6);
            this.MinimumSize = new System.Drawing.Size(550, 350);
            this.Name = "FrontEndLoader";
            this.Text = "Rock Excavator v.1";
            this.Load += new System.EventHandler(this.Program_Load);
            this.tabNavigation.ResumeLayout(false);
            this.Connection.ResumeLayout(false);
            this.Connection.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnUpload;
        private System.Windows.Forms.ListBox lstDatabaseType;
        private System.Windows.Forms.Label lblDatabaseType;
        private System.Windows.Forms.Button btnSQLConnect;
        private System.Windows.Forms.TabControl tabNavigation;
        private System.Windows.Forms.TabPage Connection;
        private System.Windows.Forms.TabPage Transformation;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TabPage Preview;
        private System.Windows.Forms.TabPage Confirmation;
    }
}

