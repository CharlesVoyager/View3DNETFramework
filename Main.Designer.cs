namespace View3D
{
    partial class Main
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.splitLog = new System.Windows.Forms.SplitContainer();
            ((System.ComponentModel.ISupportInitialize)(this.splitLog)).BeginInit();
            this.splitLog.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitLog
            // 
            this.splitLog.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitLog.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitLog.Location = new System.Drawing.Point(0, 0);
            this.splitLog.Margin = new System.Windows.Forms.Padding(4);
            this.splitLog.Name = "splitLog";
            this.splitLog.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitLog.Panel2Collapsed = true;
            this.splitLog.Size = new System.Drawing.Size(1105, 765);
            this.splitLog.SplitterDistance = 359;
            this.splitLog.SplitterWidth = 5;
            this.splitLog.TabIndex = 4;
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1105, 765);
            this.Controls.Add(this.splitLog);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(796, 737);
            this.Name = "Main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "OpenGL 3D Viewer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Main_FormClosing);
            this.Load += new System.EventHandler(this.Main_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Main_KeyDown);
            this.Move += new System.EventHandler(this.Main_Move);
            ((System.ComponentModel.ISupportInitialize)(this.splitLog)).EndInit();
            this.splitLog.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion
        private System.Windows.Forms.SplitContainer splitLog;
    }
}

