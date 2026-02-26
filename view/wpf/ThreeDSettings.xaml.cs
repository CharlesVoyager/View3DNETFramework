using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// Note: WPF's built-in ColorDialog doesn't exist; use a NuGet package such as
// "ColorPickerWPF" or "Ookii.Dialogs.Wpf", or the Windows Forms one via interop.
// This file uses a simple Windows Forms ColorDialog via interop (add reference to
// System.Windows.Forms and WindowsFormsIntegration in your .csproj).
using WinForms = System.Windows.Forms;

namespace View3D.view
{
    public partial class ThreeDSettings : Window
    {
        // ── Constructor ──────────────────────────────────────────────────────────
        public ThreeDSettings()
        {
            InitializeComponent();
            comboDrawMethod.SelectedIndex = 0;
        }

        // ── Helper: open color picker for a Border swatch ───────────────────────
        private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
                PickColor(border);
        }

        private void PickColor(Border border)
        {
            var dialog = new WinForms.ColorDialog();

            // Pre-select the current swatch color
            if (border.Background is SolidColorBrush scb)
            {
                var c = scb.Color;
                dialog.Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                var dc = dialog.Color;
                border.Background = new SolidColorBrush(Color.FromArgb(dc.A, dc.R, dc.G, dc.B));
                OnColorChanged(border);
            }
        }

        // Called after any color swatch changes — override / hook as needed.
        protected virtual void OnColorChanged(Border changedSwatch)
        {
            // Consumers can subscribe to per-swatch changes here or override this method.
        }

        // ── Tab: General ─────────────────────────────────────────────────────────
        private void showPrintbed_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Mirror: WinForms showEdges_CheckedChanged called for printbed too
            OnSettingsChanged();
        }

        private void comboDrawMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Mirror: WinForms comboDrawMethod_SelectedIndexChanged
            OnSettingsChanged();
        }

        // ── Tab: Model ───────────────────────────────────────────────────────────
        private void showEdges_CheckedChanged(object sender, RoutedEventArgs e)
        {
            OnSettingsChanged();
        }

        private void showFaces_CheckedChanged(object sender, RoutedEventArgs e)
        {
            OnSettingsChanged();
        }

        // ── Tab: Lights ──────────────────────────────────────────────────────────
        private void enableLight_CheckedChanged(object sender, RoutedEventArgs e)
        {
            OnSettingsChanged();
        }

        private void light_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Mirror: WinForms light_TextChanged
            OnSettingsChanged();
        }

        /// <summary>
        /// Validates that the TextBox contains a valid float; highlights in red on failure.
        /// Mirrors: WinForms float_Validating / ErrorProvider pattern.
        /// </summary>
        private void float_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                bool valid = float.TryParse(tb.Text, NumberStyles.Float,
                                            CultureInfo.InvariantCulture, out _);
                tb.BorderBrush = valid
                    ? SystemColors.ControlDarkBrush
                    : Brushes.Red;
                tb.ToolTip = valid ? null : "Please enter a valid floating-point number.";
            }
        }

        // ── Window closing ───────────────────────────────────────────────────────
        private void ThreeDSettings_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Mirror: WinForms ThreeDSettings_FormClosing
            // Add any save / cancel logic here.
        }

        // ── Shared notification hook ─────────────────────────────────────────────
        protected virtual void OnSettingsChanged()
        {
            // Raise an event or call a ViewModel method here as needed.
        }

        // ── Public accessors (mirror the public WinForms fields) ─────────────────

        public SolidColorBrush FacesColor       => (SolidColorBrush)faces.Background;
        public SolidColorBrush SelectedFacesColor => (SolidColorBrush)selectedFaces.Background;
        public SolidColorBrush EdgesColor        => (SolidColorBrush)edges.Background;
        public SolidColorBrush InsideFacesColor  => (SolidColorBrush)insideFaces.Background;
        public SolidColorBrush CutFacesColor     => (SolidColorBrush)cutFaces.Background;
        public SolidColorBrush SelectionBoxColor => (SolidColorBrush)selectionBox.Background;
        public SolidColorBrush OutsidePrintbedColor => (SolidColorBrush)outsidePrintbed.Background;
        public SolidColorBrush ErrorModelColor   => (SolidColorBrush)errorModel.Background;
        public SolidColorBrush ErrorModelEdgeColor => (SolidColorBrush)errorModelEdge.Background;

        public SolidColorBrush BackgroundTopColor    => (SolidColorBrush)backgroundTop.Background;
        public SolidColorBrush BackgroundBottomColor => (SolidColorBrush)backgroundBottom.Background;
        public SolidColorBrush PrinterBaseColor      => (SolidColorBrush)printerBase.Background;
        public SolidColorBrush PrinterFrameColor     => (SolidColorBrush)printerFrame.Background;

        public bool ShowPrintbed => showPrintbed.IsChecked == true;
        public bool ShowEdges    => showEdges.IsChecked == true;
        public bool ShowFaces    => showFaces.IsChecked == true;

        public bool EnableLight1 => enableLight1.IsChecked == true;
        public bool EnableLight2 => enableLight2.IsChecked == true;
        public bool EnableLight3 => enableLight3.IsChecked == true;
        public bool EnableLight4 => enableLight4.IsChecked == true;

        public int DrawMethodIndex => comboDrawMethod.SelectedIndex;

        // Light direction accessors
        public string XDir1 => xdir1.Text;
        public string XDir2 => xdir2.Text;
        public string XDir3 => xdir3.Text;
        public string XDir4 => xdir4.Text;
        public string YDir1 => ydir1.Text;
        public string YDir2 => ydir2.Text;
        public string YDir3 => ydir3.Text;
        public string YDir4 => ydir4.Text;
        public string ZDir1 => zdir1.Text;
        public string ZDir2 => zdir2.Text;
        public string ZDir3 => zdir3.Text;
        public string ZDir4 => zdir4.Text;

        // Light color accessors
        public SolidColorBrush Ambient1Color  => (SolidColorBrush)ambient1.Background;
        public SolidColorBrush Ambient2Color  => (SolidColorBrush)ambient2.Background;
        public SolidColorBrush Ambient3Color  => (SolidColorBrush)ambient3.Background;
        public SolidColorBrush Ambient4Color  => (SolidColorBrush)ambient4.Background;
        public SolidColorBrush Diffuse1Color  => (SolidColorBrush)diffuse1.Background;
        public SolidColorBrush Diffuse2Color  => (SolidColorBrush)diffuse2.Background;
        public SolidColorBrush Diffuse3Color  => (SolidColorBrush)diffuse3.Background;
        public SolidColorBrush Diffuse4Color  => (SolidColorBrush)diffuse4.Background;
        public SolidColorBrush Specular1Color => (SolidColorBrush)specular1.Background;
        public SolidColorBrush Specular2Color => (SolidColorBrush)specular2.Background;
        public SolidColorBrush Specular3Color => (SolidColorBrush)specular3.Background;
        public SolidColorBrush Specular4Color => (SolidColorBrush)specular4.Background;
    }
}
