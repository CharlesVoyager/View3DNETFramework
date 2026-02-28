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
        public View3D.view.wpf.UI ui = null;        // WPF overlay UI window

        public Trans trans = null;

        public float dpiX, dpiY;

        #region Print Area settings
        public float PrintAreaWidth = 256;  // x-axis direction
        public float PrintAreaDepth = 256;  // y-axis direction
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

        // Signals that ThreeDControl is fully constructed and ready
        private readonly System.Threading.ManualResetEventSlim _glReady
            = new System.Threading.ManualResetEventSlim(false);

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

                    MainWindow_LocationChanged(null, null);
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

            // ui
            ui = new View3D.view.wpf.UI();
            ui.Show();

            // Launch the GameWindow on its own thread so it doesn't block WPF
            var glThread = new Thread(() =>
            {
                // Everything related to ThreeDControl happens here
                threedview = new ThreeDControl();
                threedview.SetComp(objectPlacement);
                threedview.SetView(objectPlacement.cont);

                // Signal WPF thread that threedview is ready
                _glReady.Set();

                // Blocks this thread for the lifetime of the GL window
                threedview.Run(60.0);
            })
            {
                IsBackground = true,    // killed automatically when the app exits
                Name = "OpenGL-Thread"
            };
            glThread.SetApartmentState(ApartmentState.STA);
            glThread.Start();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Don't set Windows startup location to center screen in XAML to avoid issues with DPI scaling on multi-monitor setups.
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Process command line arguments
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 1) return;

            for (int i = 1; i < args.Length; i++)
            {
                string file = args[i];
                if (File.Exists(file))
                    LoadGCodeOrSTL(file);
            }
            // <>
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
       ///  RegMemory.StoreWindowPos("mainWindow", this, true, true);
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
            ///if (threedview?.gl == null) return;

            // Convert WinForms screen point to WPF logical units
            //var screenPoint = threedview.gl.PointToScreen(System.Drawing.Point.Empty);
            //threedview.ui.Left = (double)screenPoint.X / dpiX * 96;
            //threedview.ui.Top  = (double)screenPoint.Y / dpiY * 96;
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
