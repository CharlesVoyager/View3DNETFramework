using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using View3D.model;
using View3D.model.geom;
using View3D.view;
using View3D.view.utils;

namespace View3D
{
    public delegate void languageChangedEvent();

    public partial class Main : Form
    {
        public event languageChangedEvent languageChanged;

        public static Main main;

        public ThreeDSettings threeDSettings = null;
        public ThreeDControl threedview = null;
        public View3D.view.wpf.STLComposer objectPlacement = null;

        public Trans trans = null;

        public float dpiX, dpiY;

        #region Print Area settings
        public float PrintAreaWidth = 128;  // x-axis direction
        public float PrintAreaDepth = 128;  // y-axis direction
        public float PrintAreaHeight = 200; // z-axis direction
        double epsilon = 1e-4; // 0.0001
        public bool PointInside(float x, float y, float z)
        {
            if (z < -0.1 || z > PrintAreaHeight) //0.0005
                return false;

            if (x < -epsilon || x > PrintAreaWidth + epsilon) return false;
            if (y < -epsilon || y > PrintAreaDepth + epsilon) return false;

            return true;
        }
        #endregion

        private void Main_Load(object sender, EventArgs e)
        {
            try
            {
                var mainScreen = Screen.PrimaryScreen;
                this.Width = mainScreen.WorkingArea.Width - 100;
                this.Height = mainScreen.WorkingArea.Height - 100;
                CenterToScreen();

                //everything done.  Now look at command line
                ProcessCommandLine();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public Main()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US", false);

            Graphics graphics = this.CreateGraphics();
            dpiX = graphics.DpiX;
            dpiY = graphics.DpiY;

            main = this;

            InitializeComponent();

            if (WindowState == FormWindowState.Maximized)
                Application.DoEvents();


            // Translator
            trans = new Trans(Application.StartupPath + Path.DirectorySeparatorChar + "Resources");

            // ThreeDSettings
            threeDSettings = new ThreeDSettings();
            threeDSettings.Hide();

            // STLComposer
            objectPlacement = new View3D.view.wpf.STLComposer();
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(objectPlacement);
            objectPlacement.Hide();

            // ThreeDControl
            threedview = new ThreeDControl();
            threedview.Dock = DockStyle.Fill;
            panel1.Controls.Add(threedview);

            threedview.SetComp(objectPlacement);            // STLComposer object
            threedview.SetView(objectPlacement.cont);

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
        }

        void ProcessCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();

            if (args.Length < 1) return;

            //for now, just check the last arg and load it. Could add other inputs/commands later.
            for (int i = 1; i < args.Length; i++)
            {
                string file = args[i];

                if (File.Exists(file))
                {
                    LoadGCodeOrSTL(file);
                }
            }
        }

        void Form1_DragEnter(object sender, DragEventArgs e)
        {
            bool tCanSupport = true;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string file in files)
                {
                    if ( file.ToUpper().EndsWith(".STL") == false )
                    {
                        tCanSupport = false;
                        break;
                    }
                }
                if (tCanSupport)
                {
                    e.Effect = DragDropEffects.Copy;
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                }
            }
        }

        void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (string file in files) LoadGCodeOrSTL(file);
        }

        // Called when importing a STL file.
        public void LoadGCodeOrSTL(string file)
        {
            if (!File.Exists(file)) return;

            string fileLow = file.ToLower();
            if (fileLow.EndsWith(".stl"))
                objectPlacement.openAndAddObject(file);
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            RegMemory.StoreWindowPos("mainWindow", this, true, true);

            Environment.Exit(Environment.ExitCode);
        }

        public void Update3D()
        {
            if (threedview != null)
                threedview.UpdateChanges();
        }

        private void Main_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                threedview.ThreeDControl_KeyDown(sender, e);
            }
            catch { }
        }

        private void Main_Move(object sender, EventArgs e)
        {
            Point location = threedview.gl.PointToScreen(Point.Empty);
            threedview.ui.Left = (double)location.X / dpiX * 96;
            threedview.ui.Top = (double)location.Y / dpiY * 96;
        }

        public void DoCommand(PrintModel stl)
        {
            try
            {
                float[] pos = new float[9];
                pos[0] = stl.Position.x; pos[1] = stl.Position.y; pos[2] = stl.Position.z;
                pos[3] = stl.Rotation.x; pos[4] = stl.Rotation.y; pos[5] = stl.Rotation.z;
                pos[6] = stl.Scale.x; pos[7] = stl.Scale.y; pos[8] = stl.Scale.z;
            }
            catch { }
        }


        public Color GetColorSetting(Submesh.MeshColor color, Color frontBackColor)
        {
            switch (color)
            {
                case Submesh.MeshColor.FrontBack:
                    return frontBackColor;
                case Submesh.MeshColor.Back:
                    return threeDSettings.InsideFacesBackgroundColor();
                case Submesh.MeshColor.ErrorFace:
                    return threeDSettings.ErrorModelBackgroundColor();
                case Submesh.MeshColor.ErrorEdge:
                    return threeDSettings.ErrorModelEdgeBackgroundColor();
                case Submesh.MeshColor.OutSide:
                    return threeDSettings.OutsidePrintbedBackgroundColor();
                case Submesh.MeshColor.EdgeLoop:
                    return threeDSettings.EdgesLoopBackgroundColor();
                case Submesh.MeshColor.CutEdge:
                    return threeDSettings.CutFacesBackgroundColor();
                case Submesh.MeshColor.Normal:
                    return Color.Blue;
                case Submesh.MeshColor.Edge:
                    return threeDSettings.EdgesBackgroundColor();
                case Submesh.MeshColor.TransBlue:
                    return Color.FromArgb(128, 0, 0, 255);
                case Submesh.MeshColor.OverhangLv1: // pink
                    return Color.FromArgb(255, 255, 140, 140);
                case Submesh.MeshColor.OverhangLv2: // light pink
                    return Color.FromArgb(255, 255, 190, 190);
                case Submesh.MeshColor.OverhangLv3: // light pink white
                    return Color.FromArgb(255, 250, 215, 205);
                default:
                    return Color.White;
            }
        }
    }
}