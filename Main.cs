/*
   Copyright 2011 repetier repetierdev@gmail.com

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using Microsoft.Win32;
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
        public static FormPrinterSettings printerSettings;
        public static PrinterModel printerModel;
        public static ThreeDSettings threeDSettings;

        private string basicTitle = "";

        public ThreeDControl threedview = null;
        public STLComposer objectPlacement = null;
        public ObjectInformation gObjectInformation = new ObjectInformation();

        public volatile GCodeVisual newVisual = null;

        public volatile Thread previewThread = null;
        public RegMemory.FilesHistory fileHistory = new RegMemory.FilesHistory("fileHistory", 2);
        public Trans trans = null;

        public float dpiX, dpiY;

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

        public Main(string args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US", false);

            Graphics graphics = this.CreateGraphics();
            dpiX = graphics.DpiX;
            dpiY = graphics.DpiY;

            main = this;

            trans = new Trans(Application.StartupPath + Path.DirectorySeparatorChar + "Resources");
            printerSettings = new FormPrinterSettings();
            printerModel = new PrinterModel();
            threeDSettings = new ThreeDSettings();

            InitializeComponent();

            if (WindowState == FormWindowState.Maximized)
                Application.DoEvents();

            splitLog.Panel2Collapsed = true;

            // STLComposer
            View3D.view.wpf.StlComposerWindow stlComposerWnd = new View3D.view.wpf.StlComposerWindow();
            objectPlacement = stlComposerWnd.stlComposerWindow;
            stlComposerWnd.Show();

            threedview = new ThreeDControl();
            threedview.Dock = DockStyle.Fill;

            threedview.SetComp(objectPlacement);
            splitLog.Panel1.Controls.Add(threedview);   

            basicTitle = Text;
            // Modify UI font size
            Main.main.threedview.ui.modifyUITextSize();
            Main.main.threedview.ui.UI_view.modifyViewTextSize();

            assign3DView();

            string titleAdd = "";

            if (titleAdd.Length > 0)
            {
                int p = basicTitle.IndexOf(' ');
                basicTitle = basicTitle.Substring(0, p) + titleAdd + basicTitle.Substring(p);
                Text = basicTitle;
            }

            UpdateToolbarSize();
            // Add languages
            languageChanged += translate;
            translate();

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
 
            gObjectInformation.Owner = this;
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
                    if (
                        file.ToUpper().EndsWith(".STL") == false
                        && file.ToUpper().EndsWith(".3WN") == false
                        && file.ToUpper().EndsWith(".3WS") == false
                        && file.ToUpper().EndsWith(".3W") == false
                        && file.ToUpper().EndsWith(".NKG") == false
                        )
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

        public void translate()
        {
            openGCode.Title = Trans.T("W_IMPORT_FILE");
            saveJobDialog.Title = Trans.T("W_SAVE_FILE_3WN");     
        }

        public void UpdateToolbarSize()
        {
        }

        private void languageSelected(object sender, EventArgs e)
        {
            ToolStripItem it = (ToolStripItem)sender;
            trans.selectLanguage((Translation)it.Tag);
            if (languageChanged != null)
                languageChanged();
        }

        public void toolGCodeLoad_Click(object sender, EventArgs e)
        {
            if (openGCode.ShowDialog() == DialogResult.OK)
            {
                Main.main.threedview.Enabled = false;
                LoadGCodeOrSTL(openGCode.FileName);
                Main.main.threedview.Enabled = true;

                //Modified by RCGREY for STL Slice Previewer
                Main.main.threedview.setMinMaxClippingLayer();
            }
        }

        // Called when importing a STL file.
        public void LoadGCodeOrSTL(string file)
        {
            if (!File.Exists(file)) return;

            FileInfo f = new FileInfo(file);
            Main.threeDSettings.filament.BackColor = System.Drawing.Color.Chocolate;

            string fileLow = file.ToLower();

            if (fileLow.EndsWith(".stl"))
                objectPlacement.openAndAddObject(file);
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            RegMemory.StoreWindowPos("mainWindow", this, true, true);

            if (previewThread != null)
                previewThread.Join();

            Environment.Exit(Environment.ExitCode);
        }

        public void Update3D()
        {
            if (threedview != null)
                threedview.UpdateChanges();
        }

        public void assign3DView()
        {
            threedview.SetView(objectPlacement.cont);

            //threedview.SetView(jobPreview);
            //threedview.ui.info_toggleButton.Visibility = System.Windows.Visibility.Visible;

            //threedview.SetView(printPreview);
        }

        private void Main_Resize(object sender, EventArgs e)
        {

        }

        private void Main_Shown(object sender, EventArgs e)
        {
        }

        private void Main_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                threedview.ThreeDControl_KeyDown(sender, e);
            }
            catch { }
        }

        static bool firstSizeCall = true;

        private void Main_SizeChanged(object sender, EventArgs e)
        {
            if (firstSizeCall)
            {
                firstSizeCall = false;
            }
        }

        public void toolStripButton_helpInfo_Click(object sender, EventArgs e)
        {
        }

        private void Main_Move(object sender, EventArgs e)
        {
            Point location = threedview.gl.PointToScreen(Point.Empty);
            threedview.ui.Left = (double)location.X / dpiX * 96;
            threedview.ui.Top = (double)location.Y / dpiY * 96;
        }

        public bool toolStripSaveJobFun()
        {
            objectPlacement.saveSTL.FileName = "";


            if (objectPlacement.saveSTL.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Main.main.threedview.ui.BusyWindow.labelBusyMessage.Text = Trans.T("B_SAVING");
                    Main.main.threedview.ui.BusyWindow.Visibility = System.Windows.Visibility.Visible;
                    Main.main.threedview.ui.BusyWindow.buttonCancel.Visibility = System.Windows.Visibility.Visible;
                    Main.main.threedview.ui.BusyWindow.busyProgressbar.IsIndeterminate = false;
                    Main.main.threedview.ui.BusyWindow.busyProgressbar.Value = 0;
                    Main.main.threedview.ui.BusyWindow.busyProgressbar.Maximum = 100;
                    Main.main.threedview.ui.BusyWindow.StartTimer();

                    objectPlacement.saveComposition(objectPlacement.saveSTL.FileName);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
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
                    return threeDSettings.insideFaces.BackColor;
                case Submesh.MeshColor.ErrorFace:
                    return threeDSettings.errorModel.BackColor;
                case Submesh.MeshColor.ErrorEdge:
                    return threeDSettings.errorModelEdge.BackColor;
                case Submesh.MeshColor.OutSide:
                    return threeDSettings.outsidePrintbed.BackColor;
                case Submesh.MeshColor.EdgeLoop:
                    return threeDSettings.edges.BackColor;
                case Submesh.MeshColor.CutEdge:
                    return threeDSettings.cutFaces.BackColor;
                case Submesh.MeshColor.Normal:
                    return Color.Blue;
                case Submesh.MeshColor.Edge:
                    return threeDSettings.edges.BackColor;
                case Submesh.MeshColor.TransBlue:
                    return Color.FromArgb(128, 0, 0, 255);
                case Submesh.MeshColor.OverhangLv1: // pink
                    return Color.FromArgb(255, 255, 140, 140);
                case Submesh.MeshColor.OverhangLv2: // light pink
                    return Color.FromArgb(255, 255, 190, 190);
                case Submesh.MeshColor.OverhangLv3: // light pink white
                    return Color.FromArgb(255, 250, 215, 205);
                case Submesh.MeshColor.ConeSupport:
                    return Color.FromArgb(255, 128, 0); //orange
                case Submesh.MeshColor.TreeSymbol:
                    return Color.FromArgb(255, 255, 102); //yellow
                case Submesh.MeshColor.TreeMesh:
                    return Color.FromArgb(0, 255, 128); //light green
                case Submesh.MeshColor.TreeError:
                    return Color.FromArgb(255, 102, 102); //red
                case Submesh.MeshColor.TreeSlect:
                    return Color.FromArgb(102, 255, 255); //light blue
                case Submesh.MeshColor.TreeTest:
                    return Color.Transparent; //yellow

                default:
                    return Color.White;
            }
        }
    }
}