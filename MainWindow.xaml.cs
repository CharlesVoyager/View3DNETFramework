using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using View3D.model;
using View3D.model.geom;
using View3D.view;
using View3D.view.utils;

namespace View3D
{
    public delegate void languageChangedEvent();

    public partial class MainWindow : Window
    {
        public event languageChangedEvent languageChanged;

        public static MainWindow main;

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
            if (z < -0.1 || z > PrintAreaHeight)
                return false;

            if (x < -epsilon || x > PrintAreaWidth + epsilon) return false;
            if (y < -epsilon || y > PrintAreaDepth + epsilon) return false;

            return true;
        }
        #endregion

        public MainWindow()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US", false);

            main = this;

            InitializeComponent();

            // Retrieve DPI from WPF presentation source after initialization
            Loaded += (s, e) =>
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpiX = (float)(96.0 * source.CompositionTarget.TransformToDevice.M11);
                    dpiY = (float)(96.0 * source.CompositionTarget.TransformToDevice.M22);
                }
            };

            // Translator
            trans = new Trans(AppDomain.CurrentDomain.BaseDirectory
                              + Path.DirectorySeparatorChar + "Resources");

            // ThreeDSettings
            threeDSettings = new ThreeDSettings();
            threeDSettings.Hide();

            // STLComposer
            objectPlacement = new View3D.view.wpf.STLComposer();
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(objectPlacement);
            objectPlacement.Hide();

            // ThreeDControl (WinForms control hosted via WindowsFormsHost)
            threedview = new ThreeDControl();
            threedview.Dock = System.Windows.Forms.DockStyle.Fill;
            ThreeDHost.Child = threedview;

            threedview.SetComp(objectPlacement);
            threedview.SetView(objectPlacement.cont);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //try
            //{
            //    // Maximize to working area minus a small margin
            //    var screen = System.Windows.Forms.Screen.PrimaryScreen;
            //    this.Width  = screen.WorkingArea.Width  - 100;
            //    this.Height = screen.WorkingArea.Height - 100;
            //    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            //    ProcessCommandLine();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.ToString());
            //}
        }

        private void ProcessCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 1) return;

            for (int i = 1; i < args.Length; i++)
            {
                string file = args[i];
                if (File.Exists(file))
                    LoadGCodeOrSTL(file);
            }
        }

        private void MainWindow_DragEnter(object sender, DragEventArgs e)
        {
            bool canSupport = true;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (!file.ToUpper().EndsWith(".STL"))
                    {
                        canSupport = false;
                        break;
                    }
                }
                e.Effects = canSupport ? DragDropEffects.Copy : DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                    LoadGCodeOrSTL(file);
            }
        }

        public void LoadGCodeOrSTL(string file)
        {
            if (!File.Exists(file)) return;

            string fileLow = file.ToLower();
            if (fileLow.EndsWith(".stl"))
                objectPlacement.openAndAddObject(file);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
       //     RegMemory.StoreWindowPos("mainWindow", this, true, true);
            Environment.Exit(Environment.ExitCode);
        }

        public void Update3D()
        {
            if (threedview != null)
                threedview.UpdateChanges();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Convert WPF KeyEventArgs to WinForms KeyEventArgs for ThreeDControl
                var winFormsKey = (System.Windows.Forms.Keys)KeyInterop.VirtualKeyFromKey(e.Key);
                var winFormsArgs = new System.Windows.Forms.KeyEventArgs(winFormsKey);
                threedview.ThreeDControl_KeyDown(sender, winFormsArgs);
            }
            catch { }
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (threedview?.gl == null) return;

            // Convert WinForms screen point to WPF logical units
            var screenPoint = threedview.gl.PointToScreen(System.Drawing.Point.Empty);
            threedview.ui.Left = (double)screenPoint.X / dpiX * 96;
            threedview.ui.Top  = (double)screenPoint.Y / dpiY * 96;
        }

        public void DoCommand(PrintModel stl)
        {
            try
            {
                float[] pos = new float[9];
                pos[0] = stl.Position.x; pos[1] = stl.Position.y; pos[2] = stl.Position.z;
                pos[3] = stl.Rotation.x; pos[4] = stl.Rotation.y; pos[5] = stl.Rotation.z;
                pos[6] = stl.Scale.x;    pos[7] = stl.Scale.y;    pos[8] = stl.Scale.z;
            }
            catch { }
        }
    }
}
