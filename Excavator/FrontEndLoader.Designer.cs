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
            this.SuspendLayout();
            // 
            // btnUpload
            // 
            this.btnUpload.Font = new System.Drawing.Font("Open Sans", 12.25F);
            this.btnUpload.Location = new System.Drawing.Point(518, 228);
            this.btnUpload.Name = "btnUpload";
            this.btnUpload.Size = new System.Drawing.Size(85, 36);
            this.btnUpload.TabIndex = 0;
            this.btnUpload.Text = "Upload";
            this.btnUpload.UseVisualStyleBackColor = true;
            this.btnUpload.Click += new System.EventHandler(this.btnUpload_Click);
            // 
            // lstDatabaseType
            // 
            this.lstDatabaseType.FormattingEnabled = true;
            this.lstDatabaseType.ItemHeight = 24;
            this.lstDatabaseType.Location = new System.Drawing.Point(216, 236);
            this.lstDatabaseType.Name = "lstDatabaseType";
            this.lstDatabaseType.Size = new System.Drawing.Size(233, 28);
            this.lstDatabaseType.TabIndex = 1;
            this.lstDatabaseType.SelectedIndexChanged += new System.EventHandler(this.lstDatabaseType_SelectedIndexChanged);
            // 
            // lblDatabaseType
            // 
            this.lblDatabaseType.AutoSize = true;
            this.lblDatabaseType.Location = new System.Drawing.Point(212, 209);
            this.lblDatabaseType.Name = "lblDatabaseType";
            this.lblDatabaseType.Size = new System.Drawing.Size(183, 24);
            this.lblDatabaseType.TabIndex = 2;
            this.lblDatabaseType.Text = "Database to Convert:";
            // 
            // FrontEndLoader
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.SystemColors.ControlDark;
            this.ClientSize = new System.Drawing.Size(884, 561);
            this.Controls.Add(this.lblDatabaseType);
            this.Controls.Add(this.lstDatabaseType);
            this.Controls.Add(this.btnUpload);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(6);
            this.MinimumSize = new System.Drawing.Size(550, 350);
            this.Name = "FrontEndLoader";
            this.Text = "Rock Excavator v.1";
            this.Load += new System.EventHandler(this.Program_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnUpload;
        private System.Windows.Forms.ListBox lstDatabaseType;
        private System.Windows.Forms.Label lblDatabaseType;
    }
}

