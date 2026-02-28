using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

            // Translator
            trans = new Trans(AppDomain.CurrentDomain.BaseDirectory
                              + Path.DirectorySeparatorChar + "Resources");

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

            // ThreeDSettings
            threeDSettings = new ThreeDSettings();
            threeDSettings.Hide();

            // STLComposer
            objectPlacement = new View3D.view.wpf.STLComposer();
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(objectPlacement);
            objectPlacement.Hide();

            InitializeComponent();
            UI();

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
                pos[6] = stl.Scale.x; pos[7] = stl.Scale.y; pos[8] = stl.Scale.z;
            }
            catch { }
        }

        public bool killed = false;
        public string olddata;
        public DateTime newDate;
        public Dictionary<string, string> dict;
        public static string temp_buffer = "";
        public string SN = "";
        public float layerNow;
        public int layerTotal;
        private bool sliceToolEnabled = false;

        public void UI()
        {
            VisualStateManager.GoToState(UI_view, "State2", true);
            VisualStateManager.GoToState(UI_move, "State2", true);
            VisualStateManager.GoToState(UI_rotate, "State2", true);
            VisualStateManager.GoToState(UI_resize_advance, "State2", true);
            VisualStateManager.GoToState(UI_object_information, "State2", true);

            UI_resize_advance.btn_Scale.FontSize = 12;
            UI_resize_advance.button_mmtoinch.FontSize = 12;
            UI_resize_advance.button_inchtomm.FontSize = 12;
            UI_resize_advance.lbl_Size.FontSize = 12;

            move_toggleButton.FontSize = 12;
            import_button.FontSize = 12;

            translate();
            languageChanged += translate;
        }

        private void translate()
        {
            view_toggleButton.ToolTip = Trans.T("B_VIEW");
            move_toggleButton.ToolTip = Trans.T("B_MOVE");
            rotate_toggleButton.ToolTip = Trans.T("B_ROTATE");
            resize_toggleButton.ToolTip = Trans.T("B_SCALE");
            info_toggleButton.ToolTip = Trans.T("B_INFO");
            remove_toggleButton.ToolTip = Trans.T("B_REMOVE");
            import_button.ToolTip = Trans.T("B_IMPORT");
            about_button.ToolTip = Trans.T("B_ABOUT");

            view_toggleButton.Content = Trans.T("B_VIEW");
            move_toggleButton.Content = Trans.T("B_MOVE");
            rotate_toggleButton.Content = Trans.T("B_ROTATE");
            resize_toggleButton.Content = Trans.T("B_SCALE");
            info_toggleButton.Content = Trans.T("B_INFO");
            remove_toggleButton.Content = Trans.T("B_REMOVE");
            import_button.Content = Trans.T("B_IMPORT");
            about_button.Content = Trans.T("B_ABOUT");
        }

        public void setbuttonVisable(bool flag)
        {
            if (flag == true)
            {
                view_toggleButton.Visibility = Visibility.Visible;
                move_toggleButton.Visibility = Visibility.Visible;
                rotate_toggleButton.Visibility = Visibility.Visible;
                resize_toggleButton.Visibility = Visibility.Visible;
                info_toggleButton.Visibility = Visibility.Visible;
            }
            else
            {
                move_toggleButton.Visibility = Visibility.Hidden;
                rotate_toggleButton.Visibility = Visibility.Hidden;
                resize_toggleButton.Visibility = Visibility.Hidden;
                info_toggleButton.Visibility = Visibility.Hidden;
            }

            if (MainWindow.main.objectPlacement.listObjects.SelectedItems.Count == 0)
                remove_toggleButton.Visibility = Visibility.Hidden;
            else
                remove_toggleButton.Visibility = Visibility.Visible;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Close();
        }

        private void view_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_view, "State1", true);

            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;
        }

        private void view_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_view, "State2", true);
            MainWindow.main.Focus();
        }

        public void move_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            //Fix the bug for scenarios where move reset has been invoked in between support generation
            if (!move_toggleButton.IsEnabled)
                return;

            VisualStateManager.GoToState(UI_move, "State1", true);
            view_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            PrintModel stl = MainWindow.main.objectPlacement.SingleSelectedModel;
            if (stl == null) return;

            UI_move.slider_moveX.Maximum = 1000;
            UI_move.slider_moveX.Minimum = -1000;
            UI_move.slider_moveY.Maximum = 1000;
            UI_move.slider_moveY.Minimum = -1000;
            UI_move.slider_moveZ.Maximum = 1000;
            UI_move.slider_moveZ.Minimum = -1000;
            UI_move.slider_moveX.Value = stl.Position.x;
            UI_move.slider_moveY.Value = stl.Position.y;
            UI_move.slider_moveZ.Value = stl.Position.z;

            double moveMax, moveMin;

            moveMax = (int)Math.Floor((MainWindow.main.PrintAreaWidth - (stl.BoundingBox.xMax - stl.Position.x)) * 100) * 0.01;
            moveMin = (int)Math.Ceiling((stl.Position.x - stl.BoundingBox.xMin) * 100) * 0.01;

            double a = MainWindow.main.PrintAreaWidth - (stl.BoundingBox.xMax - stl.Position.x);
            double b = stl.Position.x - stl.BoundingBox.xMin;

            a += 0;
            b += 0;

            //module is out of bound,it cannot move. 
            if (moveMin > moveMax)
                UI_move.slider_moveX.Value = (float)(moveMin + moveMax) / 2;
            else if (moveMin <= stl.Position.x && stl.Position.x <= moveMax)//module is in of bound.
                UI_move.slider_moveX.Value = stl.Position.x;
            else if (stl.Position.x > moveMax)//model is out of bound(too big), but it can move.
                UI_move.slider_moveX.Value = moveMax;
            else // (moveMin > stl.Position.x)//model is out of bound(too small), but it can move.
                UI_move.slider_moveX.Value = moveMin;

            if (moveMin > moveMax)
            {
                UI_move.slider_moveX.Maximum = (float)(moveMin + moveMax) / 2;
                UI_move.slider_moveX.Minimum = (float)(moveMin + moveMax) / 2;
            }
            else
            {
                UI_move.slider_moveX.Maximum = moveMax;
                UI_move.slider_moveX.Minimum = moveMin;
            }


            moveMax = (int)Math.Floor((MainWindow.main.PrintAreaDepth - (stl.BoundingBox.yMax - stl.Position.y)) * 100) * 0.01;
            moveMin = (int)Math.Ceiling((stl.Position.y - stl.BoundingBox.yMin) * 100) * 0.01;

            //module is out of bound,it can not move. 
            if (moveMin > moveMax)
                UI_move.slider_moveY.Value = (float)(moveMin + moveMax) / 2;
            else if (moveMin <= stl.Position.y && stl.Position.y <= moveMax)//module is in of bound.
                UI_move.slider_moveY.Value = stl.Position.y;
            else if (stl.Position.y > moveMax)//model is out of bound(too big), but it can move.
                UI_move.slider_moveY.Value = moveMax;
            else // (moveMin > stl.Position.y)//model is out of bound(too small), but it can move.
                UI_move.slider_moveY.Value = moveMin;

            if (moveMin > moveMax)
            {
                UI_move.slider_moveY.Maximum = (float)(moveMin + moveMax) / 2;
                UI_move.slider_moveY.Minimum = (float)(moveMin + moveMax) / 2;
            }
            else
            {
                UI_move.slider_moveY.Maximum = moveMax;
                UI_move.slider_moveY.Minimum = moveMin;
            }

            moveMax = MainWindow.main.PrintAreaHeight - (stl.BoundingBoxWOSupport.zMax - stl.Position.z);
            moveMin = stl.Position.z - stl.BoundingBoxWOSupport.zMin;
            if (moveMin > moveMax)
                moveMin = moveMax;
            if (moveMin <= stl.Position.z && moveMax >= stl.Position.z)
                UI_move.slider_moveZ.Value = stl.Position.z;
            else if (moveMax < stl.Position.z)
                UI_move.slider_moveZ.Value = moveMax;
            else // (moveMin > stl.Position.z)
                UI_move.slider_moveZ.Value = moveMin;
            UI_move.slider_moveZ.Maximum = moveMax;
            UI_move.slider_moveZ.Minimum = moveMin;

            UI_move.moveZ_textbox.Text = (Math.Round(MainWindow.main.UI_move.slider_moveZ.Value - MainWindow.main.UI_move.slider_moveZ.Minimum, 3)).ToString();
        }

        public void move_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_move, "State2", true);
            MainWindow.main.Focus();
        }

        private void import_button_Click(object sender, RoutedEventArgs e)
        {
            if (false == confirmSupportEditModeBreak())
            {
                return;
            }
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Title = "Select a File";
            openFileDialog.Filter = "STL Files (*.stl)|*.stl";

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string filePath = openFileDialog.FileName;

                string fileLow = filePath.ToLower();
                if (fileLow.EndsWith(".stl"))
                    MainWindow.main.objectPlacement.openAndAddObject(filePath);

                if (MainWindow.main.objectPlacement.listObjects.Items.Count > 0)
                {
                    MainWindow.main.threedview.viewSilhouette = false;
                    MainWindow.main.threedview.clipDownward = true;
                    MainWindow.main.threedview.setclipLayerHeight = (double)0.1;
                }

                MainWindow.main.Focus();
                MainWindow.main.threedview.UpdateChanges();
            }
        }

        public void IsTabStopAllToggleButton(object control, bool pIsTabStop)
        {
            if (control is ToggleButton)
                ((ToggleButton)control).IsTabStop = pIsTabStop;
            else
                if (control is Grid)
                    foreach (UIElement child in ((Grid)control).Children)
                        IsTabStopAllToggleButton(child, pIsTabStop);
            if (control is StackPanel)
                foreach (UIElement child in ((StackPanel)control).Children)
                    IsTabStopAllToggleButton(child, pIsTabStop);
        }

        private void about_button_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;
        }

        private void rotate_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            UI_rotate.sliderX.Value = Convert.ToDouble(MainWindow.main.objectPlacement.textRotX.Text);
            UI_rotate.sliderY.Value = Convert.ToDouble(MainWindow.main.objectPlacement.textRotY.Text);
            UI_rotate.sliderZ.Value = Convert.ToDouble(MainWindow.main.objectPlacement.textRotZ.Text);
            VisualStateManager.GoToState(UI_rotate, "State1", true);
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;
        }

        // Scale
        public void resize_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_resize_advance, "State1", true);
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            PrintModel stl = MainWindow.main.objectPlacement.SingleSelectedModel;
            if (stl == null) return;
            UI_move.slider_moveZ.Maximum = 1000;

            model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;
            UI_resize_advance.bboxnow = bbox.Size.x / Convert.ToDouble(MainWindow.main.objectPlacement.textScaleX.Text);
            UI_resize_advance.bboynow = bbox.Size.y / Convert.ToDouble(MainWindow.main.objectPlacement.textScaleY.Text);
            UI_resize_advance.bboznow = bbox.Size.z / Convert.ToDouble(MainWindow.main.objectPlacement.textScaleZ.Text);

            UI_resize_advance.gIsShow = true;
            UI_resize_advance.dimX = bbox.Size.x;
            UI_resize_advance.updateTxt(Enums.Axis.X);
            UI_resize_advance.dimY = bbox.Size.y;
            UI_resize_advance.updateTxt(Enums.Axis.Y);
            UI_resize_advance.dimZ = bbox.Size.z;
            UI_resize_advance.updateTxt(Enums.Axis.Z);

            if (Convert.ToDouble(MainWindow.main.objectPlacement.textRotX.Text) != 0 ||
                Convert.ToDouble(MainWindow.main.objectPlacement.textRotY.Text) != 0 ||
                Convert.ToDouble(MainWindow.main.objectPlacement.textRotZ.Text) != 0)
            {
                UI_resize_advance.chk_Uniform.IsChecked = true;
                UI_resize_advance.chk_Uniform.IsEnabled = false;
            }
            else
            {
                UI_resize_advance.chk_Uniform.IsEnabled = true;
            }

            if (UI_resize_advance.chk_Uniform.IsChecked == true)
            {
                UI_resize_advance.chk_Uniform_Checked(null, null);
            }

            UI_resize_advance.gIsShow = false;
            UI_resize_advance.button_mmtoinch.IsEnabled = true;
            UI_resize_advance.button_inchtomm.IsEnabled = false;
            UI_resize_advance.lbl_XUnits.Content = Trans.T("L_MM");
            UI_resize_advance.lbl_YUnits.Content = Trans.T("L_MM");
            UI_resize_advance.lbl_ZUnits.Content = Trans.T("L_MM");

            UI_resize_advance.txt_Scale.Text = "";
            UI_resize_advance.button_Reset.ToolTip = Trans.T("B_RESET");
            UI_resize_advance.button_Reset.Content = Trans.T("B_RESET");
            UI_resize_advance.lbl_Uniform.Content = Trans.T("L_UNIFORM");
            UI_resize_advance.lbl_Size.Content = Trans.T("L_SIZE");
            UI_resize_advance.btn_Scale.Content = Trans.T("B_APPLY");
            UI_resize_advance.button_mmtoinch.Content = Trans.T("B_SCALE_UP") + " (" + Trans.T("L_MM") + "→" + Trans.T("L_INCH") + ")";
            UI_resize_advance.button_inchtomm.Content = Trans.T("B_SCALE_DOWN") + " (" + Trans.T("L_INCH") + "→" + Trans.T("L_MM") + ")";
        }

        private void rotate_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_rotate, "State2", true);
            MainWindow.main.Focus();
        }

        private void resize_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_resize_advance, "State2", true);
            MainWindow.main.Focus();
        }

        private bool confirmSupportEditModeBreak()
        {
            return true;
        }

        private void info_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_object_information, "State1", true);
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
        }

        private void info_toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(UI_object_information, "State2", true);
            MainWindow.main.Focus();
        }

        public void remove_toggleButton_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            UI_move.slider_moveX.Minimum = -1000;
            UI_move.slider_moveX.Maximum = 1000;
            UI_move.slider_moveY.Minimum = -1000;
            UI_move.slider_moveY.Maximum = 1000;

            OutofBound.Visibility = System.Windows.Visibility.Hidden;
            MainWindow.main.threedview.clipviewEnabled = false;
            MainWindow.main.threedview.button_remove_Click(null, null);
            if (MainWindow.main.objectPlacement.listObjects.Items.Count > 0)
                MainWindow.main.objectPlacement.updateSTLState(MainWindow.main.objectPlacement.SingleSelectedModel);
            MainWindow.main.Focus();
        }

        private void zoomin_toggleButton_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            MainWindow.main.threedview.button_zoomIn_Click(null, null);
            MainWindow.main.Focus();
        }

        private void zoomout_toggleButton_Click(object sender, RoutedEventArgs e)
        {
            view_toggleButton.IsChecked = false;
            move_toggleButton.IsChecked = false;
            rotate_toggleButton.IsChecked = false;
            resize_toggleButton.IsChecked = false;
            info_toggleButton.IsChecked = false;

            threedview.button_zoomOut_Click(null, null);
            Focus();
        }

        private void remove_toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            remove_toggleButton.IsChecked = false;
        }

        public static ManualResetEvent allDone = new ManualResetEvent(false);
    }


    public static class MouseDownHelper
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled",
        typeof(bool), typeof(MouseDownHelper), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnNotifyPropertyChanged)));

        private static void OnNotifyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as UIElement;
            if (element != null && e.NewValue != null)
            {
                if ((bool)e.NewValue)
                {
                    Register(element);
                }
                else
                {
                    UnRegister(element);
                }
            }
        }

        private static void Register(UIElement element)
        {
            element.MouseDown += element_MouseDown;
            element.MouseLeftButtonDown += element_MouseLeftButtonDown;
            element.MouseLeave += element_MouseLeave;
            element.MouseUp += element_MouseUp;
        }

        private static void UnRegister(UIElement element)
        {
            element.MouseDown -= element_MouseDown;
            element.MouseLeftButtonDown -= element_MouseLeftButtonDown;
            element.MouseLeave -= element_MouseLeave;
            element.MouseUp -= element_MouseUp;
        }

        private static void element_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null)
            {
                SetIsMouseDown(element, true);
            }
        }

        private static void element_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null)
            {
                SetIsMouseLeftButtonDown(element, true);
            }
        }

        private static void element_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null)
            {
                SetIsMouseDown(element, false);
                SetIsMouseLeftButtonDown(element, false);
            }
        }

        private static void element_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = e.Source as UIElement;
            if (element != null)
            {
                SetIsMouseDown(element, false);
                SetIsMouseLeftButtonDown(element, false);
            }
        }

        internal static readonly DependencyPropertyKey IsMouseDownPropertyKey = DependencyProperty.RegisterAttachedReadOnly("IsMouseDown",
        typeof(bool), typeof(MouseDownHelper), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty IsMouseDownProperty = IsMouseDownPropertyKey.DependencyProperty;

        internal static void SetIsMouseDown(UIElement element, bool value)
        {
            element.SetValue(IsMouseDownPropertyKey, value);
        }

        internal static readonly DependencyPropertyKey IsMouseLeftButtonDownPropertyKey = DependencyProperty.RegisterAttachedReadOnly("IsMouseLeftButtonDown",
        typeof(bool), typeof(MouseDownHelper), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty IsMouseLeftButtonDownProperty = IsMouseLeftButtonDownPropertyKey.DependencyProperty;

        internal static void SetIsMouseLeftButtonDown(UIElement element, bool value)
        {
            element.SetValue(IsMouseLeftButtonDownPropertyKey, value);
        }
    }
}
