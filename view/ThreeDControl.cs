using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using View3D.model;
using View3D.model.geom;
using View3D.ModelObjectTool;
using View3D.view.utils;

// NOTE: ThreeDControl_Designer.cs is no longer needed and should be removed from the project.
// The ContextMenu is now implemented via a WPF ContextMenu defined in the UI layer (ui.ContextMenu),
// or you can add a System.Windows.Controls.ContextMenu to the WPF UI.xaml that hosts this window.

namespace View3D.view
{
    /// <summary>
    /// OpenTK GameWindow replacing the WinForms UserControl + RHOpenGL child.
    /// Rendering, input, and camera logic are unchanged; only the hosting mechanism differs.
    /// </summary>
    public class ThreeDControl : GameWindow
    {
        // ── Public static / shared ────────────────────────────────────────────
        public static double GLversion;

        // ── Camera & scene state ──────────────────────────────────────────────
        public ThreeDCamera cam;
        float bedRadius;
        bool loaded = false;
        float xDown, yDown;
        float xPos, yPos;
        float speedX, speedY;
        float lastX, lastY;
        readonly Stopwatch sw = new Stopwatch();
        readonly Stopwatch fpsTimer = new Stopwatch();
        int mode = 0;
        int slowCounter = 0;
        public float zoom = 1.0f;
        public Matrix4 lookAt, persp, modelView;
        public float nearDist, farDist, aspectRatio, nearHeight, midHeight;

        int keyX = -1;
        int keyY = -1;

        public ThreeDView view = null;
        public STLComposer stlComp = null;

        // Clip / silhouette
        public double setclipLayerHeight = 0.1;
        public bool clipDownward;
        public bool clipviewEnabled;
        public bool viewSilhouette;

        // Geometry helpers (pick ray)
        public Geom3DLine pickLine = null;
        public Geom3DVector pickPoint = new Geom3DVector(0, 0, 0);

        // Object-move tracking
        Geom3DPlane movePlane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1));
        Geom3DVector moveStart = new Geom3DVector(0, 0, 0);
        Geom3DVector moveLast  = new Geom3DVector(0, 0, 0);
        Geom3DVector movePos   = new Geom3DVector(0, 0, 0);

        // ── Constructor ───────────────────────────────────────────────────────
        /// <summary>
        /// Creates the GameWindow with an OpenGL 2.x compatibility context.
        /// Pass width/height matching your panel or leave as defaults; the window
        /// is later embedded via WindowsFormsHost in MainWindow.xaml.
        /// </summary>
        public ThreeDControl(int width = 830, int height = 624)
            : base(width, height,
                   new GraphicsMode(32, 24, 8, 4),   // colour, depth, stencil, MSAA
                   "OpenGL 3D Viewer",
                   GameWindowFlags.Default,
                   DisplayDevice.Default,
                   2, 0,                             // OpenGL 2.0 minimum
                   GraphicsContextFlags.Default)
        {
            VSync = VSyncMode.Off;

            // Camera
            cam = new ThreeDCamera(this);
            SetCameraDefaults();
            cam.OrientIsometric();

            // Language hook
            translate();
            MainWindow.main.languageChanged += translate;
        }

        // ── Public wiring ─────────────────────────────────────────────────────
        public void SetComp(STLComposer comp)  => stlComp = comp;
        public void SetView(ThreeDView v) { view = v; UpdateChanges(); }

        private volatile bool _isDirty = true;
        private void Invalidate() => _isDirty = true;

        public void UpdateChanges() => Invalidate();

        public void SetObjectSelected(bool sel)
        {
            MainWindow.main.setbuttonVisable(stlComp.listObjects.SelectedItems.Count == 1 && sel);
            view.objectsSelected = sel;
        }

        // ── Translations ──────────────────────────────────────────────────────
        // Context-menu item text is still kept here so the strings stay in sync
        // with whatever WPF ContextMenu is wired up from the UI layer.
        private void translate()
        {
            // These string keys mirror the original WinForms menu items.
            // Apply them to the WPF ContextMenu items exposed by ui if needed.
        }

        private const int MinWidth = 800;
        private const int MinHeight = 600;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int GWLP_WNDPROC = -4;

        private delegate IntPtr WndProcDelegate(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // Keep a reference — prevents the delegate from being GC'd
        private WndProcDelegate _wndProcDelegate;
        private IntPtr _originalWndProc;

        [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);
        [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved, ptMaxSize, ptMaxPosition,
                         ptMinTrackSize, ptMaxTrackSize;
        }

        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(
                    lParam, typeof(MINMAXINFO));

                mmi.ptMinTrackSize.x = MinWidth;
                mmi.ptMinTrackSize.y = MinHeight;

                Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }

            return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }


        // ── GameWindow overrides ──────────────────────────────────────────────
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Subclass the native window to intercept Win32 messages
            _wndProcDelegate = CustomWndProc;
            IntPtr hwnd = this.WindowInfo.Handle;
            _originalWndProc = SetWindowLongPtr(
                hwnd, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

            // WindowInfo.Handle is the native HWND of the GameWindow.
            // Must be done here — handle does not exist before OnLoad.
            IntPtr gameWindowHandle = this.WindowInfo.Handle;

            // Marshal to WPF thread to set ui's owner
            MainWindow.main.Dispatcher.InvokeAsync(() =>
            {
                WindowInteropHelper helper = new WindowInteropHelper(MainWindow.main);
                helper.Owner = gameWindowHandle;

                MainWindow.main.Show();

                // Position
                MainWindow.main.UpdateLocation(X, Y);

                // Size
                MainWindow.main.UpdateSize(Width, Height);
            });

            // Detect OpenGL version & capabilities (runs once)
            try
            {
                string sv = GL.GetString(StringName.Version).Trim();
                int p = sv.IndexOf(' ');
                if (p > 0) sv = sv.Substring(0, p);
                p = sv.IndexOf('.');
                if (p > 0)
                {
                    p = sv.IndexOf('.', p + 1);
                    if (p > 0) sv = sv.Substring(0, p);
                    GLversion = Convert.ToDouble(sv, CultureInfo.InvariantCulture);
                }
                try
                {
                    float val;
                    float.TryParse(sv, NumberStyles.Float, GCode.format, out val);
                    MainWindow.main.threeDSettings.openGLVersion = val;
                }
                catch { MainWindow.main.threeDSettings.openGLVersion = 1.1f; }

                MainWindow.main.threeDSettings.useVBOs =
                    GL.GetString(StringName.Extensions).Contains("GL_ARB_vertex_buffer_object");
            }
            catch { }

            loaded = true;
            SetupViewport();
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);

            // GameWindow.X and GameWindow.Y are the new position
            int newX = this.X;
            int newY = this.Y;

            MainWindow.main.Dispatcher.Invoke(() =>
            {
                if (MainWindow.main != null)
                    MainWindow.main.UpdateLocation(newX, newY);
            });
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!loaded) return;

            int newWidth = this.Width;
            int newHeight = this.Height;

            MainWindow.main.Dispatcher.Invoke(() =>
            {
                if (MainWindow.main!= null)
                    MainWindow.main.UpdateSize(newWidth, newHeight);
            });

            SetupViewport();
            Invalidate();
        }

        // ThreeDControl.cs
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Clean up GL resources here — still on the GL thread
            // e.g. delete VBOs, textures, etc.

            // Notify WPF thread if needed
            MainWindow.main.Dispatcher.InvokeAsync(() =>
            {
                // e.g. shut down the WPF app when the GL window closes
                System.Windows.Application.Current.Shutdown();
            });
        }

        // Thread-safe queue for GL objects that need to be deleted on the GL thread
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _glDeleteQueue
            = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        /// <summary>
        /// Schedules GL resource deletion to run safely on the GL thread.
        /// Call this from ANY thread instead of calling GL.Delete* directly.
        /// </summary>
        public void ScheduleGLDelete(Action deleteAction)
            => _glDeleteQueue.Enqueue(deleteAction);

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            // Drain all pending GL deletions first — safe because we are on the GL thread
            while (_glDeleteQueue.TryDequeue(out Action deleteAction))
                deleteAction();

            if (!_isDirty) return;
            _isDirty = false;
            gl_Paint();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            Application_Idle();
        }

        // ── Mouse input ───────────────────────────────────────────────────────
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

#if false
            MainWindow.main.view_toggleButton.IsChecked   = false;
            MainWindow.main.move_toggleButton.IsChecked   = false;
            MainWindow.main.resize_toggleButton.IsChecked = false;
#endif

            keyX = e.X; keyY = e.Y;
            cam.PreparePanZoomRot();
            lastX = xDown = e.X;
            lastY = yDown = e.Y;
            movePlane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1));
            moveStart = moveLast = new Geom3DVector(0, 0, 0);
            UpdatePickLine(e.X, e.Y);
            movePlane.intersectLine(pickLine, moveStart);
            Invalidate();
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);

            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(Key.AltLeft) ||
                OpenTK.Input.Keyboard.GetState().IsKeyDown(Key.AltRight))
                return;

            var mouse = OpenTK.Input.Mouse.GetState();
            bool anyButton = mouse.LeftButton  == ButtonState.Pressed ||
                             mouse.RightButton == ButtonState.Pressed ||
                             mouse.MiddleButton == ButtonState.Pressed;

            if (!anyButton)
            {
                speedX = speedY = 0;
                Invalidate();
                return;
            }

            xPos = e.X;
            yPos = e.Y;
            UpdatePickLine(e.X, e.Y);
            movePos = new Geom3DVector(0, 0, 0);
            movePlane.intersectLine(pickLine, movePos);
            float d = Math.Min(Width, Height) / 3f;
            speedX = Math.Max(-1, Math.Min(1, (xPos - xDown) / d));
            speedY = Math.Max(-1, Math.Min(1, (yPos - yDown) / d));
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            ThreeDModel sel = null;

            if (e.Button == MouseButton.Left)
            {
                sel = Picktest(e.X, e.Y);
                if (sel != null)
                {
                    movePlane  = new Geom3DPlane(pickPoint, new Geom3DVector(0, 0, 1));
                    moveStart  = moveLast = new Geom3DVector(pickPoint);
                }
                if (sel != null && view.eventObjectMoved != null)
                {
                    MainWindow.main.Dispatcher.InvokeAsync(() =>
                    {
                        view.eventObjectSelected(sel);
                    });
                }
                else if (keyX == e.X && keyY == e.Y)
                {
                    MainWindow.main.Dispatcher.InvokeAsync(() =>
                    {
                        stlComp.listObjects.SelectedItems.Clear();
                    });
                }
            }

            if (e.Button == MouseButton.Right)
            {
                sel = Picktest(e.X, e.Y);
                if (sel != null)
                {
                    movePlane  = new Geom3DPlane(pickPoint, new Geom3DVector(0, 0, 1));
                    moveStart  = moveLast = new Geom3DVector(pickPoint);
                }
                if (sel != null && view.eventObjectMoved != null)
                {
                    MainWindow.main.Dispatcher.InvokeAsync(() =>
                    {
                        view.eventObjectSelected(sel);
                        ShowContextMenu();
                    });
                }
                else if (keyX == e.X && keyY == e.Y)
                    ShowContextMenu();
            }

            speedX = speedY = 0;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta != 0)
            {
                cam.PreparePanZoomRot();
                cam.Zoom(1f - e.Delta / 6000f);
                zoom *= 1f - e.Delta / 2000f;
                if (zoom < 0.002f) zoom = 0.002f;
                if (zoom > 5.9f)   zoom = 5.9f;
                Invalidate();
            }
        }

        // ── Keyboard input ────────────────────────────────────────────────────
        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);
            ThreeDControl_KeyDown(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            ThreeDControl_KeyPress(e);
        }

        // Public so MainWindow can forward WPF KeyDown events into here
        public void ThreeDControl_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            // Extend with key handling as needed.
        }

        private void ThreeDControl_KeyDown(KeyboardKeyEventArgs e)
        {
            // Extend with key handling as needed.
        }

        private void ThreeDControl_KeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar == '-')
            {
                zoom *= 1.05f;
                if (zoom > 10) zoom = 10;
                Invalidate();
            }
            if (e.KeyChar == '+')
            {
                zoom *= 0.95f;
                if (zoom < 0.01f) zoom = 0.01f;
                Invalidate();
            }
        }

        // ── Context menu (WPF) ────────────────────────────────────────────────
        /// <summary>
        /// Shows the WPF context menu exposed by the UI overlay.
        /// Wire up menu items in ui.ContextMenu in the WPF UI layer.
        /// </summary>
        private void ShowContextMenu()
        {
            if (MainWindow.main?.ContextMenuItems == null) return;

            PrintModel model = stlComp.SingleSelectedModel;
            bool hasModel = model != null;

            // Must marshal to WPF thread
            MainWindow.main.Dispatcher.InvokeAsync(() =>
            {
                MainWindow.main.ShowContextMenu(hasModel);
            });
        }

        // Context-menu action handlers (called by the WPF UI ContextMenu)
        public void ContextMenu_LandObject()   => MainWindow.main.UI_move.button_land_Click(null, null);

        public void ContextMenu_ResetObject()
        {
            MainWindow.main.UI_resize_advance.button_Reset_Click(null, null);
            MainWindow.main.UI_rotate.button_rotate_reset_Click(null, null);
            MainWindow.main.UI_move.button_move_reset_Click(null, null);
        }

        public void ContextMenu_RemoveObject() => MainWindow.main.remove_toggleButton_Click(null, null);

        public void ContextMenu_MmToInch()
        {
            PrintModel m = stlComp.SingleSelectedModel;
            if (m != null) stlComp.DoInchOrScale(m, true);
        }

        public void ContextMenu_InchToMm()
        {
            PrintModel m = stlComp.SingleSelectedModel;
            if (m != null) stlComp.DoInchtomm(m);
        }

        public void ContextMenu_Clone() => stlComp.CloneObject();

        // ── Rendering ─────────────────────────────────────────────────────────
        private void gl_Paint()
        {
            if (view == null || !loaded) return;
            try
            {
                fpsTimer.Reset();
                fpsTimer.Start();

                MakeCurrent();
                GL.Enable(EnableCap.Multisample);
                GL.ClearColor(MainWindow.main.threeDSettings.BackgroundTopBackgroundColor());
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

                // Gradient background
                GL.MatrixMode(MatrixMode.Projection); GL.LoadIdentity();
                GL.MatrixMode(MatrixMode.Modelview);  GL.LoadIdentity();
                GL.Disable(EnableCap.DepthTest);
                GL.Disable(EnableCap.Lighting);
                GL.Begin(PrimitiveType.Quads);
                GL.Color4(convertColor(MainWindow.main.threeDSettings.BackgroundBottomBackgroundColor()));
                GL.Vertex2(-1.0, -1.0); GL.Vertex2(1.0, -1.0);
                GL.Color4(convertColor(MainWindow.main.threeDSettings.BackgroundTopBackgroundColor()));
                GL.Vertex2(1.0, 1.0);   GL.Vertex2(-1.0, 1.0);
                GL.End();

                GL.Enable(EnableCap.DepthTest);
                SetupViewport();
                Vector3 camPos = cam.CameraPosition;
                lookAt = Matrix4.LookAt(camPos.X, camPos.Y, camPos.Z,
                    cam.viewCenter.X, cam.viewCenter.Y, cam.viewCenter.Z, 0, 0, 1.0f);
                GL.MatrixMode(MatrixMode.Modelview);
                GL.LoadMatrix(ref lookAt);
                GL.ShadeModel(ShadingModel.Smooth);
                GL.Enable(EnableCap.LineSmooth);
                AddLights();
                GL.Enable(EnableCap.CullFace);
                GL.Enable(EnableCap.PolygonSmooth);
                GL.LineWidth(2f);
                GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
                GL.GetFloat(GetPName.ModelviewMatrix, out modelView);
                GL.Material(MaterialFace.Front, MaterialParameter.Specular,
                    new Color4(255, 255, 255, 255));

                GL.Disable(EnableCap.DepthTest);
                DrawPrintbedBase();
                GL.Enable(EnableCap.DepthTest);
                DrawPrintbedFrame();

                try { DrawModels(); }
                catch (OutOfMemoryException)
                {
                    if (view.models.Count > 0)
                    {
                        stlComp.RemoveLastModel();
                        UpdateChanges();
                    }
                    MainWindow.main.BusyWindow.Visibility = Visibility.Hidden;
                    System.Windows.MessageBox.Show(
                        "Error(" + (short)View3D.Protocol.ErrorCode.LOAD_FILE_FAIL + "): " +
                        Trans.T("M_LOAD_FILE_FAIL"));
                    GC.Collect();
                }

                SwapBuffers();
                fpsTimer.Stop();

                double fps = 1.0 / fpsTimer.Elapsed.TotalSeconds;
                if (fps < 30)
                {
                    if (++slowCounter >= 10)
                    {
                        slowCounter = 0;
                        foreach (ThreeDModel m in view.models) m.ReduceQuality();
                    }
                }
                else if (slowCounter > 0) slowCounter--;
            }
            catch { }
        }

        // ── Viewport / projection ─────────────────────────────────────────────
        public void SetupViewport()
        {
            try
            {
                int w = Width, h = Height;
                bedRadius = (float)(1.5 * Math.Sqrt(
                    (MainWindow.main.PrintAreaDepth  * MainWindow.main.PrintAreaDepth  +
                     MainWindow.main.PrintAreaHeight * MainWindow.main.PrintAreaHeight +
                     MainWindow.main.PrintAreaWidth  * MainWindow.main.PrintAreaWidth) * 0.25));

                GL.Viewport(0, 0, w, h);
                GL.MatrixMode(MatrixMode.Projection);
                if (cam == null) return;

                Vector3 camPos = cam.CameraPosition;
                float dist = (float)cam.distance;
                nearDist    = Math.Max(1, dist - bedRadius);
                farDist     = Math.Max(bedRadius * 2, dist + bedRadius);
                midHeight   = 2.0f * (float)Math.Tan(cam.angle) * dist;
                nearHeight  = 2.0f * (float)Math.Tan(cam.angle) * nearDist;
                aspectRatio = (float)w / h;
                float angle = (float)cam.angle * 2.0f;
                persp = Matrix4.CreatePerspectiveFieldOfView(angle, aspectRatio, nearDist, farDist);
                GL.LoadMatrix(ref persp);
            }
            catch { }
        }

        // ── Lights ────────────────────────────────────────────────────────────
        private void AddLights()
        {
            GL.Light(LightName.Light0, LightParameter.Ambient,  new float[] { 0.2f, 0.2f, 0.2f, 1f });
            GL.Light(LightName.Light0, LightParameter.Diffuse,  new float[] { 0, 0, 0, 0 });
            GL.Light(LightName.Light0, LightParameter.Specular, new float[] { 0, 0, 0, 0 });
            GL.Enable(EnableCap.Light0);

            var s = MainWindow.main.threeDSettings;

            SetLight(LightName.Light1, s.EnableLight1(), s.Ambient1(), s.Diffuse1(), s.Specular1(), s.Dir1(), false);
            SetLight(LightName.Light2, s.EnableLight2(), s.Ambient2(), s.Diffuse2(), s.Specular2(), s.Dir2(), true);
            SetLight(LightName.Light3, s.EnableLight3(), s.Ambient3(), s.Diffuse3(), s.Specular3(), s.Dir3(), true);
            SetLight(LightName.Light4, s.EnableLight4(), s.Ambient4(), s.Diffuse4(), s.Specular4(), s.Dir4(), true);
            GL.Enable(EnableCap.Lighting);
        }

        private void SetLight(LightName name,
                              bool enable,
                              float[] amb, float[] diff, float[] spec, float[] pos,
                              bool setExponent)
        {
            if (enable)
            {
                GL.Light(name, LightParameter.Ambient,  amb);
                GL.Light(name, LightParameter.Diffuse,  diff);
                GL.Light(name, LightParameter.Specular, spec);
                GL.Light(name, LightParameter.Position, pos);
                if (setExponent)
                    GL.Light(name, LightParameter.SpotExponent, new float[] { 1f, 1f, 1f, 1f });
                GL.Enable((EnableCap)name);
            }
            else GL.Disable((EnableCap)name);
        }

        // ── Draw helpers ──────────────────────────────────────────────────────
        private void DrawModels(bool showBbox = true)
        {
            GL.Enable(EnableCap.PolygonSmooth);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.CullFace);

            foreach (ThreeDModel model in view.models)
            {
                if (!model.reset)
                    model.curPos = model.curPos2;
                else { model.curPos = Matrix4.Identity; model.reset = false; }

                Matrix4 curPosT = model.curPos;
                curPosT.Transpose();
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                model.AnimationBefore();
                GL.Translate(model.Position.x, model.Position.y, model.Position.z);
                GL.MultMatrix(ref curPosT);
                GL.Scale(model.Scale.x, model.Scale.y, model.Scale.z);
                model.Paint();
                model.AnimationAfter();
                GL.PopMatrix();

                if (model.Selected && showBbox)
                {
                    Color col = MainWindow.main.threeDSettings.SelectionBoxBackgroundColor();
                    GL.PushMatrix();
                    model.AnimationBefore();
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new Color4(0, 0, 0, 255));
                    GL.Material(MaterialFace.Front, MaterialParameter.Emission,
                        new Color4(col.R, col.G, col.B, col.A));
                    GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0f, 0f, 0f, 1f });
                    GL.Begin(PrimitiveType.Lines);
                    DrawBBoxLines(model);
                    GL.End();
                    model.AnimationAfter();
                    GL.PopMatrix();
                }
            }
        }

        private static void DrawBBoxLines(ThreeDModel m)
        {
            GL.Vertex3(m.xMin, m.yMin, m.zMin); GL.Vertex3(m.xMax, m.yMin, m.zMin);
            GL.Vertex3(m.xMin, m.yMin, m.zMin); GL.Vertex3(m.xMin, m.yMax, m.zMin);
            GL.Vertex3(m.xMin, m.yMin, m.zMin); GL.Vertex3(m.xMin, m.yMin, m.zMax);
            GL.Vertex3(m.xMax, m.yMax, m.zMax); GL.Vertex3(m.xMin, m.yMax, m.zMax);
            GL.Vertex3(m.xMax, m.yMax, m.zMax); GL.Vertex3(m.xMax, m.yMin, m.zMax);
            GL.Vertex3(m.xMax, m.yMax, m.zMax); GL.Vertex3(m.xMax, m.yMax, m.zMin);
            GL.Vertex3(m.xMin, m.yMax, m.zMax); GL.Vertex3(m.xMin, m.yMax, m.zMin);
            GL.Vertex3(m.xMin, m.yMax, m.zMax); GL.Vertex3(m.xMin, m.yMin, m.zMax);
            GL.Vertex3(m.xMax, m.yMax, m.zMin); GL.Vertex3(m.xMax, m.yMin, m.zMin);
            GL.Vertex3(m.xMax, m.yMax, m.zMin); GL.Vertex3(m.xMin, m.yMax, m.zMin);
            GL.Vertex3(m.xMax, m.yMin, m.zMax); GL.Vertex3(m.xMin, m.yMin, m.zMax);
            GL.Vertex3(m.xMax, m.yMin, m.zMax); GL.Vertex3(m.xMax, m.yMin, m.zMin);
        }

        private void DrawPrintbedFrame()
        {
            var  main = MainWindow.main;
            GL.LineWidth(2f);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            GL.BlendFunc((BlendingFactor)BlendingFactorSrc.SrcAlpha,
                         (BlendingFactor)BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);

            Color doorcol = Color.Red;
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new Color4(0, 0, 0, 180));
            GL.Material(MaterialFace.Front, MaterialParameter.Emission,  new Color4(0, 0, 0, 0));
            GL.Material(MaterialFace.Front, MaterialParameter.Specular,  new float[] { 0f, 0f, 0f, 1f });
            GL.Enable(EnableCap.LineSmooth);
            GL.Material(MaterialFace.Front, MaterialParameter.Emission,
                new Color4(doorcol.R, doorcol.G, doorcol.B, doorcol.A));

            int pad = 2, tri = 10;
            GL.Begin(PrimitiveType.LineStrip);
            GL.Vertex3(-pad,                               main.PrintAreaDepth + pad,          -pad);
            GL.Vertex3(-pad,                               -pad,                               -pad);
            GL.Vertex3(main.PrintAreaWidth + pad,          -pad,                               -pad);
            GL.Vertex3(main.PrintAreaWidth + pad,          main.PrintAreaDepth + pad,          -pad);
            GL.Vertex3(main.PrintAreaWidth / 2f + tri,     main.PrintAreaDepth + pad,          -pad);
            GL.Vertex3(main.PrintAreaWidth / 2f,           main.PrintAreaDepth + pad + tri,    -pad);
            GL.Vertex3(main.PrintAreaWidth / 2f - tri,     main.PrintAreaDepth + pad,          -pad);
            GL.Vertex3(-pad,                               main.PrintAreaDepth + pad,          -pad);
            GL.End();

            if (main.threeDSettings.IsPrintbed() != true) return;

            GL.LineWidth(1f);
            Color col = main.threeDSettings.PrinterFrameBackgroundColor();
            GL.Material(MaterialFace.Front, MaterialParameter.Emission,
                new Color4(col.R, col.G, col.B, col.A));

            GL.Disable(EnableCap.CullFace);
            GL.Begin(PrimitiveType.Lines);

            // Vertical cage edges
            GL.Vertex3(0,                        0,                        0); GL.Vertex3(0,                        0,                        main.PrintAreaHeight);
            GL.Vertex3(main.PrintAreaWidth,       0,                        0); GL.Vertex3(main.PrintAreaWidth,       0,                        main.PrintAreaHeight);
            GL.Vertex3(0,                        main.PrintAreaDepth,       0); GL.Vertex3(0,                        main.PrintAreaDepth,       main.PrintAreaHeight);
            GL.Vertex3(main.PrintAreaWidth,       main.PrintAreaDepth,       0); GL.Vertex3(main.PrintAreaWidth,       main.PrintAreaDepth,       main.PrintAreaHeight);
            // Top face
            GL.Vertex3(0,                        0,                        main.PrintAreaHeight); GL.Vertex3(main.PrintAreaWidth, 0,                  main.PrintAreaHeight);
            GL.Vertex3(main.PrintAreaWidth,       0,                        main.PrintAreaHeight); GL.Vertex3(main.PrintAreaWidth, main.PrintAreaDepth, main.PrintAreaHeight);
            GL.Vertex3(main.PrintAreaWidth,       main.PrintAreaDepth,       main.PrintAreaHeight); GL.Vertex3(0,                  main.PrintAreaDepth, main.PrintAreaHeight);
            GL.Vertex3(0,                        main.PrintAreaDepth,       main.PrintAreaHeight); GL.Vertex3(0,                  0,                  main.PrintAreaHeight);

            // Grid lines
            float dx = 10, dy = 10;
            for (int i = 0; i < 200; i++)
            {
                float x = Math.Min(i * dx, main.PrintAreaWidth);
                GL.Vertex3(x, 0,                  0); GL.Vertex3(x, main.PrintAreaDepth, 0);
                if (x >= main.PrintAreaWidth) break;
            }
            for (int i = 0; i < 200; i++)
            {
                float y = Math.Min(i * dy, main.PrintAreaDepth);
                GL.Vertex3(0,                 y, 0); GL.Vertex3(main.PrintAreaWidth, y, 0);
                if (y >= main.PrintAreaDepth) break;
            }
            GL.End();
            GL.DepthFunc(DepthFunction.Less);
        }

        private void DrawPrintbedBase()
        {
            if (MainWindow.main.threeDSettings.IsPrintbed() != true) return;

            var main = MainWindow.main;
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc((BlendingFactor)BlendingFactorSrc.SrcAlpha,
                         (BlendingFactor)BlendingFactorDest.OneMinusSrcAlpha);
            GL.DepthFunc(DepthFunction.Less);

            Color col = main.threeDSettings.PrinterBaseBackgroundColor();
            float[] transblack = { 0, 0, 0, 0 };
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse,
                new Color4(col.R, col.G, col.B, 130));
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, transblack);
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Emission, transblack);

            GL.PushMatrix();
            GL.Translate(0, 0, cam.phi < Math.PI / 2 ? -0.04 : +0.04);
            GL.Begin(PrimitiveType.Quads);
            GL.Normal3(0, 0, 1);
            GL.Vertex3(0,                  0,                  0);
            GL.Vertex3(main.PrintAreaWidth, 0,                  0);
            GL.Vertex3(main.PrintAreaWidth, main.PrintAreaDepth, 0);
            GL.Vertex3(0,                  main.PrintAreaDepth, 0);
            GL.End();
            GL.PopMatrix();
            GL.Disable(EnableCap.Blend);
        }

        // ── Pick / ray-cast ───────────────────────────────────────────────────
        public void UpdatePickLine(int x, int y)
        {
            if (view == null) return;
            int window_y   = (Height - y) - Height / 2;
            double norm_y  = (double)window_y / (Height / 2.0);
            int window_x   = x - Width / 2;
            double norm_x  = (double)window_x / (Width / 2.0);
            float fpy = (float)(nearHeight * 0.5 * norm_y);
            float fpx = (float)(nearHeight * 0.5 * aspectRatio * norm_x);

            Vector4 dirN = new Vector4(fpx, fpy, -nearDist, 0);
            Vector3 camPos = cam.CameraPosition;
            Matrix4 ntrans = Matrix4.LookAt(camPos.X, camPos.Y, camPos.Z,
                cam.viewCenter.X, cam.viewCenter.Y, cam.viewCenter.Z, 0, 0, 1.0f);
            ntrans = Matrix4.Invert(ntrans);
            Vector4 frontPoint = ntrans.Row3;
            Vector4 dirVec     = Vector4.Transform(dirN, ntrans);
            pickLine = new Geom3DLine(
                new Geom3DVector(frontPoint.X / frontPoint.W, frontPoint.Y / frontPoint.W, frontPoint.Z / frontPoint.W),
                new Geom3DVector(dirVec.X, dirVec.Y, dirVec.Z), true);
            pickLine.dir.normalize();
        }

        private ThreeDModel Picktest(int x, int y)
        {
            Vector3 near, far;
            Ray ray = RayCasting.GenerateRay(x, y, out near, out far);
            float length = float.MaxValue;
            ThreeDModel nearestModel = null;

            foreach (PrintModel model in stlComp.models)
            {
                if (!RayCasting.RaycastAABB(ray, model)) continue;

                float[] rayPos = { ray.Position.X, ray.Position.Y, ray.Position.Z };
                float[] rayNor = { ray.Normal.X,   ray.Normal.Y,   ray.Normal.Z };
                ModelMatrix mtx = ModelObjectToolHelper.ToModelMatrix(model.trans);

                int id; float output;
                if (ModelObjectToolWrapper.Instance.Tool.RayIntersectTriangle(
                        mtx, model.submesh.glVertices, rayPos, rayNor, out id, out output))
                {
                    Vector3 hitP = ray.Position + ray.Normal * output;
                    float lineLen = new Line(near, hitP).Length;
                    if (lineLen <= length)
                    {
                        length = lineLen;
                        nearestModel = model;
                    }
                }
                GC.Collect();
            }
            return nearestModel;
        }

        // ── Idle / animation update ───────────────────────────────────────────
        private void Application_Idle()
        {
            if (!loaded || (speedX == 0 && speedY == 0)) return;

            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            sw.Reset(); sw.Start();

            var kb = OpenTK.Input.Keyboard.GetState();
            var mb = OpenTK.Input.Mouse.GetState();

            int emode = mode;
            if (kb.IsKeyDown(Key.ShiftLeft) || kb.IsKeyDown(Key.ShiftRight) ||
                mb.MiddleButton == ButtonState.Pressed) emode = 2;
            if (kb.IsKeyDown(Key.ControlLeft) || kb.IsKeyDown(Key.ControlRight)) emode = 0;
            if (kb.IsKeyDown(Key.AltLeft)     || kb.IsKeyDown(Key.AltRight))     emode = 4;

            float d = Math.Min(Width, Height) / 3f;

            switch (emode)
            {
                case 0: // Rotate
                    speedX = (xPos - xDown) / d;
                    speedY = (yPos - yDown) / d;
                    cam.Rotate(-speedX * 0.9, speedY * 0.9);
                    Invalidate();
                    break;

                case 1: // Pan (slow)
                case 2: // Pan (fast)
                {
                    speedX = (xPos - xDown) / Width;
                    speedY = (yPos - yDown) / Height;
                    Vector3 planeVec = Vector3.Subtract(
                        new Vector3(moveStart.x, moveStart.y, moveStart.z), cam.CameraPosition);
                    float dot = Vector3.Dot(planeVec, cam.ViewDirection());
                    double len = dot > 0 ? planeVec.Length : -1;
                    float scale = emode == 1 ? 200f : 1f;
                    cam.Pan(speedX * scale * (emode == 2 ? -1 : 1),
                            speedY * scale * (emode == 2 ? -1 : 1), len);
                    Invalidate();
                    break;
                }

                case 3: // Zoom
                    cam.Zoom(1 - speedY / 3f);
                    Invalidate();
                    break;

                case 4: // Move objects
                {
                    Geom3DVector diff = movePos.sub(moveLast);
                    moveLast = movePos;
                    speedX = (xPos - lastX) * 200 * zoom / Width;
                    speedY = (yPos - lastY) * 200 * zoom / Height;

                    if (view.eventObjectMoved != null)
                    {
                        var selModels = new List<PrintModel>();
                        var prevX     = new List<float>();
                        var prevY     = new List<float>();

                        foreach (PrintModel stl in stlComp.ListObjects(true))
                        {
                            selModels.Add(stl);
                            prevX.Add(stl.Position.x);
                            prevY.Add(stl.Position.y);
                        }
                        view.eventObjectMoved(diff.x, diff.y);
                    }
                    lastX = xPos; lastY = yPos;
                    Invalidate();
                    break;
                }
            }
        }

        // ── Camera helpers ────────────────────────────────────────────────────
        public void SetMode(int m) => mode = m;

        private void SetCameraDefaults()
        {
            cam.viewCenter = new Vector3(0, 0, 0);
            cam.defaultDistance = 1.6f * (float)Math.Sqrt(
                MainWindow.main.PrintAreaDepth  * MainWindow.main.PrintAreaDepth  +
                MainWindow.main.PrintAreaWidth  * MainWindow.main.PrintAreaWidth  +
                MainWindow.main.PrintAreaHeight * MainWindow.main.PrintAreaHeight);
            cam.minDistance = 0.001 * cam.defaultDistance;
        }

        public void frontView()    { SetCameraDefaults(); cam.OrientFront();     Invalidate(); }
        public void backView()     { SetCameraDefaults(); cam.OrientBack();      Invalidate(); }
        public void leftView()     { SetCameraDefaults(); cam.OrientLeft();      Invalidate(); }
        public void rightView()    { SetCameraDefaults(); cam.OrientRight();     Invalidate(); }
        public void topView()      { SetCameraDefaults(); cam.OrientTop();       Invalidate(); }
        public void bottomView()   { SetCameraDefaults(); cam.OrientBottom();    Invalidate(); }
        public void isometricView(){ SetCameraDefaults(); cam.OrientIsometric(); Invalidate(); }

        // ── Zoom button handlers (called from UI overlay) ─────────────────────
        public void button_zoomIn_Click(object sender, EventArgs e)
        {
            cam.PreparePanZoomRot(); cam.Zoom(0.9);
            zoom = Math.Max(0.002f, Math.Min(5.9f, zoom));
            Invalidate();
        }

        public void button_zoomOut_Click(object sender, EventArgs e)
        {
            cam.PreparePanZoomRot(); cam.Zoom(1.1);
            zoom = Math.Max(0.002f, Math.Min(5.9f, zoom));
            Invalidate();
        }

        public void button_remove_Click(object sender, EventArgs e)
        {
            if (view.editor) stlComp.buttonRemoveSTL_Click(null, null);
            foreach (ThreeDModel m in view.models) m.Clear();
            Invalidate();
            stlComp.updateOutside();
        }

        // ── Utility ───────────────────────────────────────────────────────────
        public OpenTK.Graphics.Color4 convertColor(Color col) =>
            new Color4(col.R, col.G, col.B, col.A);
    }
}
