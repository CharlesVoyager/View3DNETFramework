namespace View3D.view
{
    partial class ThreeDControl
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

        #region Vom Komponenten-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung. 
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.contextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.landObjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mminchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.inchmmToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resetObjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeObjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.objectInfomationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gl = new View3D.view.utils.RHOpenGL();
            this.contextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // timer
            // 
            this.timer.Interval = 1;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // contextMenu
            // 
            this.contextMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.landObjectToolStripMenuItem,
            this.mminchToolStripMenuItem,
            this.inchmmToolStripMenuItem,
            this.resetObjectToolStripMenuItem,
            this.removeObjectToolStripMenuItem,
            this.cloneToolStripMenuItem,
            this.toolStripSeparator3,
            this.objectInfomationToolStripMenuItem});
            this.contextMenu.Name = "contextMenuStrip1";
            this.contextMenu.Size = new System.Drawing.Size(234, 178);
            this.contextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenu_Opening);
            // 
            // landObjectToolStripMenuItem
            // 
            this.landObjectToolStripMenuItem.Name = "landObjectToolStripMenuItem";
            this.landObjectToolStripMenuItem.Size = new System.Drawing.Size(233, 24);
            this.landObjectToolStripMenuItem.Text = "Land Object";
            this.landObjectToolStripMenuItem.Click += new System.EventHandler(this.landObjectToolStripMenuItem_Click);
            // 
            // mminchToolStripMenuItem
            // 
            this.mminchToolStripMenuItem.Name = "mminchToolStripMenuItem";
            this.mminchToolStripMenuItem.Size = new System.Drawing.Size(233, 24);
            this.mminchToolStripMenuItem.Text = "Scale Up (mm>inch)";
            this.mminchToolStripMenuItem.Click += new System.EventHandler(this.mminchToolStripMenuItem_Click);
            // 
            // inchmmToolStripMenuItem
            // 
            this.inchmmToolStripMenuItem.Name = "inchmmToolStripMenuItem";
            this.inchmmToolStripMenuItem.Size = new System.Drawing.Size(233, 24);
            this.inchmmToolStripMenuItem.Text = "Scale Down (inch>mm)";
            this.inchmmToolStripMenuItem.Click += new System.EventHandler(this.inchmmToolStripMenuItem_Click);
            // 
            // resetObjectToolStripMenuItem
            // 
            this.resetObjectToolStripMenuItem.Name = "resetObjectToolStripMenuItem";
            this.resetObjectToolStripMenuItem.Size = new System.Drawing.Size(233, 24);
            this.resetObjectToolStripMenuItem.Text = "Reset Object";
            this.resetObjectToolStripMenuItem.Click += new System.EventHandler(this.resetObjectToolStripMenuItem_Click);
            // 
            // removeObjectToolStripMenuItem
            // 
            this.removeObjectToolStripMenuItem.Name = "removeObjectToolStripMenuItem";
            this.removeObjectToolStripMenuItem.Size = new System.Drawing.Size(233, 24);
            this.removeObjectToolStripMenuItem.Text = "Remove Object";
            this.removeObjectToolStripMenuItem.Click += new System.EventHandler(this.removeObjectToolStripMenuItem_Click);
            // 
            // cloneToolStripMenuItem
            // 
            this.cloneToolStripMenuItem.Name = "cloneToolStripMenuItem";
            this.cloneToolStripMenuItem.Size = new System.Drawing.Size(233, 24);
            this.cloneToolStripMenuItem.Text = "Clone";
            this.cloneToolStripMenuItem.Click += new System.EventHandler(this.cloneToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(230, 6);
            // 
            // objectInfomationToolStripMenuItem
            // 
            this.objectInfomationToolStripMenuItem.Name = "objectInfomationToolStripMenuItem";
            this.objectInfomationToolStripMenuItem.Size = new System.Drawing.Size(233, 24);
            this.objectInfomationToolStripMenuItem.Text = "Object infomation";
            this.objectInfomationToolStripMenuItem.Click += new System.EventHandler(this.objectInfomationToolStripMenuItem_Click);
            // 
            // gl
            // 
            this.gl.BackColor = System.Drawing.Color.Transparent;
            this.gl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gl.Font = new System.Drawing.Font("Verdana", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gl.Location = new System.Drawing.Point(0, 0);
            this.gl.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.gl.Name = "gl";
            this.gl.Size = new System.Drawing.Size(830, 624);
            this.gl.TabIndex = 2;
            this.gl.VSync = false;
            this.gl.Paint += new System.Windows.Forms.PaintEventHandler(this.gl_Paint);
            this.gl.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ThreeDControl_KeyDown);
            this.gl.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.ThreeDControl_KeyPress);
            this.gl.MouseDown += new System.Windows.Forms.MouseEventHandler(this.gl_MouseDown);
            this.gl.MouseEnter += new System.EventHandler(this.ThreeDControl_MouseEnter);
            this.gl.MouseMove += new System.Windows.Forms.MouseEventHandler(this.gl_MouseMove);
            this.gl.MouseUp += new System.Windows.Forms.MouseEventHandler(this.gl_MouseUp);
            this.gl.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.gl_MouseWheel);
            this.gl.Resize += new System.EventHandler(this.gl_Resize);
            // 
            // ThreeDControl
            // 
            this.Controls.Add(this.gl);
            this.Name = "ThreeDControl";
            this.Size = new System.Drawing.Size(830, 624);
            this.Load += new System.EventHandler(this.ThreeDControl_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ThreeDControl_KeyDown);
            this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.ThreeDControl_KeyPress);
            this.MouseEnter += new System.EventHandler(this.ThreeDControl_MouseEnter);
            this.contextMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.ContextMenuStrip contextMenu;
        private System.Windows.Forms.ToolStripMenuItem landObjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem resetObjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeObjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem objectInfomationToolStripMenuItem;
        public utils.RHOpenGL gl;
        private System.Windows.Forms.ToolStripMenuItem mminchToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem inchmmToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cloneToolStripMenuItem;
    }
}
