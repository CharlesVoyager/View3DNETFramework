using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Windows.Interop;
using View3D.model;
using View3D.model.geom;
using View3D.view.utils;
using View3D.ModelObjectTool;

namespace View3D.view
{
    public partial class ThreeDControl : UserControl
    {
        public static double GLversion; // EX: 4.6

        public ThreeDCamera cam;
        float bedRadius;
        bool loaded = false;
        float xDown, yDown;
        float xPos, yPos;
        float speedX, speedY;
        float lastX, lastY;
        Stopwatch sw = new Stopwatch();
        Stopwatch fpsTimer = new Stopwatch();
        int mode = 0;
        int slowCounter = 0; // Indicates slow framerates
        public float zoom = 1.0f;
        public Matrix4 lookAt, persp, modelView;
        public float nearDist, farDist, aspectRatio, nearHeight, midHeight;

        int keyX = -1;
        int keyY = -1;

        public ThreeDView view = null;
        public View3D.view.wpf.STLComposer stlComp = null;

        //STL Slice Previewer
        public double setclipLayerHeight = 0.1;
        public bool clipDownward;
        public bool clipviewEnabled;
        public bool viewSilhouette;

        public View3D.view.wpf.UI ui = null;

        public ThreeDControl()
        {
            InitializeComponent();

            // Integrate UI
            ui = new View3D.view.wpf.UI();
            WindowInteropHelper helper = new WindowInteropHelper(ui);
            helper.Owner = this.Handle; // Make the UI be top of this control, but not top of other windows.
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(ui);

            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            // Initialize camera
            cam = new ThreeDCamera(this);
            SetCameraDefaults();
            cam.OrientIsometric();
            timer.Start();

            // Languages
            translate();
            MainWindow.main.languageChanged += translate;
        }

        private void translate()
        {
            landObjectToolStripMenuItem.Text = Trans.T("B_LAND");
            resetObjectToolStripMenuItem.Text = Trans.T("B_RESET");
            removeObjectToolStripMenuItem.Text = Trans.T("B_REMOVE");
            mminchToolStripMenuItem.Text = Trans.T("B_SCALE_UP") + " (" + Trans.T("L_MM") + ">" + Trans.T("L_INCH") + ")";
            inchmmToolStripMenuItem.Text = Trans.T("B_SCALE_DOWN") + " (" + Trans.T("L_INCH") + ">" + Trans.T("L_MM") + ")";
            cloneToolStripMenuItem.Text = Trans.T("B_CLONE_OBJECT");
        }

        public void SetComp(View3D.view.wpf.STLComposer comp)
        {
            this.stlComp = comp;
        }

        public void SetView(ThreeDView view)
        {
            this.view = view;
            UpdateChanges();
        }

        public void SetObjectSelected(bool sel)
        {
            if (stlComp.listObjects.SelectedItems.Count == 1)
            {
                ui.setbuttonVisable(sel);
            }
            else
            {
                ui.setbuttonVisable(false);
            }
            view.objectsSelected = sel;
        }

        public void UpdateChanges()
        {
            gl.Invalidate();
        }

        public void SetupViewport()
        {
            try
            {
                int w = gl.Width;
                int h = gl.Height;

                bedRadius = (float)(1.5 * Math.Sqrt((MainWindow.main.PrintAreaDepth * MainWindow.main.PrintAreaDepth + MainWindow.main.PrintAreaHeight * MainWindow.main.PrintAreaHeight + MainWindow.main.PrintAreaWidth * MainWindow.main.PrintAreaWidth) * 0.25));
                GL.Viewport(0, 0, w, h);
                
                GL.MatrixMode(MatrixMode.Projection);
                //GL.LoadIdentity();

                if (cam == null) return;

                float angle;
                Vector3 camPos = cam.CameraPosition;
                float dist = (float)cam.distance;
                nearDist = Math.Max(1, dist - bedRadius);
                farDist = Math.Max(bedRadius * 2, dist + bedRadius);
                midHeight = 2.0f * (float)Math.Tan(cam.angle) * dist;
                nearHeight = 2.0f * (float)Math.Tan(cam.angle) * nearDist;
                aspectRatio = (float)w / (float)h;
                angle = (float)(cam.angle) * 2.0f;
                persp = Matrix4.CreatePerspectiveFieldOfView((float)(angle), aspectRatio, nearDist, farDist);
                GL.LoadMatrix(ref persp);
                // GL.Ortho(0, w, 0, h, -1, 1); // Bottom-left corner pixel has coordinate (0, 0)
            }
            catch { }
        }

        public OpenTK.Graphics.Color4 convertColor(Color col)
        {
            return new OpenTK.Graphics.Color4(col.R, col.G, col.B, col.A);
        }

        private void AddLights()
        {
            //Enable lighting
            GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0.2f, 0.2f, 0.2f, 1f });
            GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { 0, 0, 0, 0 });
            GL.Light(LightName.Light0, LightParameter.Specular, new float[] { 0, 0, 0, 0 });
            GL.Enable(EnableCap.Light0);
            if (MainWindow.main.threeDSettings.enableLight1.IsChecked == true)
            {
                GL.Light(LightName.Light1, LightParameter.Ambient, MainWindow.main.threeDSettings.Ambient1());
                GL.Light(LightName.Light1, LightParameter.Diffuse, MainWindow.main.threeDSettings.Diffuse1());
                GL.Light(LightName.Light1, LightParameter.Specular, MainWindow.main.threeDSettings.Specular1());
                GL.Light(LightName.Light1, LightParameter.Position, MainWindow.main.threeDSettings.Dir1());
                //  GL.Light(LightName.Light1, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                GL.Enable(EnableCap.Light1);
            }
            else GL.Disable(EnableCap.Light1);
            if (MainWindow.main.threeDSettings.enableLight2.IsChecked == true)
            {
                GL.Light(LightName.Light2, LightParameter.Ambient, MainWindow.main.threeDSettings.Ambient2());
                GL.Light(LightName.Light2, LightParameter.Diffuse, MainWindow.main.threeDSettings.Diffuse2());
                GL.Light(LightName.Light2, LightParameter.Specular, MainWindow.main.threeDSettings.Specular2());
                GL.Light(LightName.Light2, LightParameter.Position, MainWindow.main.threeDSettings.Dir2());
                /*  GL.Light(LightName.Light2, LightParameter.Diffuse, new float[] { 0.7f, 0.7f, 0.7f, 1f });
                  GL.Light(LightName.Light2, LightParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                  GL.Light(LightName.Light2, LightParameter.Position, (new Vector4(100f, 200f, 300f, 0)));*/
                GL.Light(LightName.Light2, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                GL.Enable(EnableCap.Light2);
            }
            else GL.Disable(EnableCap.Light2);
            if (MainWindow.main.threeDSettings.enableLight3.IsChecked == true)
            {
                GL.Light(LightName.Light3, LightParameter.Ambient, MainWindow.main.threeDSettings.Ambient3());
                GL.Light(LightName.Light3, LightParameter.Diffuse, MainWindow.main.threeDSettings.Diffuse3());
                GL.Light(LightName.Light3, LightParameter.Specular, MainWindow.main.threeDSettings.Specular3());
                GL.Light(LightName.Light3, LightParameter.Position, MainWindow.main.threeDSettings.Dir3());
                /*  GL.Light(LightName.Light3, LightParameter.Diffuse, new float[] { 0.8f, 0.8f, 0.8f, 1f });
                  GL.Light(LightName.Light3, LightParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                  GL.Light(LightName.Light3, LightParameter.Position, (new Vector4(100f, -200f, 200f, 0)));*/
                GL.Light(LightName.Light3, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                GL.Enable(EnableCap.Light3);
            }
            else GL.Disable(EnableCap.Light3);
            if (MainWindow.main.threeDSettings.enableLight4.IsChecked == true)
            {
                GL.Light(LightName.Light4, LightParameter.Ambient, MainWindow.main.threeDSettings.Ambient4());
                GL.Light(LightName.Light4, LightParameter.Diffuse, MainWindow.main.threeDSettings.Diffuse4());
                GL.Light(LightName.Light4, LightParameter.Specular, MainWindow.main.threeDSettings.Specular4());
                GL.Light(LightName.Light4, LightParameter.Position, MainWindow.main.threeDSettings.Dir4());
                /* GL.Light(LightName.Light4, LightParameter.Diffuse, new float[] { 0.7f, 0.7f, 0.7f, 1f });
                 GL.Light(LightName.Light4, LightParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                 GL.Light(LightName.Light4, LightParameter.Position, (new Vector4(170f, -100f, -250f, 0)));*/
                GL.Light(LightName.Light4, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
                GL.Enable(EnableCap.Light4);
            }
            else GL.Disable(EnableCap.Light4);

            GL.Enable(EnableCap.Lighting);
        }

        private void DetectDrawingMethod()
        {
            // Check drawing method
            int om = MainWindow.main.threeDSettings.drawMethod;
            switch (MainWindow.main.threeDSettings.comboDrawMethod.SelectedIndex)
            {
                case 0: // Autodetect;
                    if (MainWindow.main.threeDSettings.useVBOs && MainWindow.main.threeDSettings.openGLVersion >= 1.499)
                        MainWindow.main.threeDSettings.drawMethod = 2;
                    else if (MainWindow.main.threeDSettings.openGLVersion >= 1.099)
                        MainWindow.main.threeDSettings.drawMethod = 1;
                    else
                        MainWindow.main.threeDSettings.drawMethod = 0;
                    break;
                case 1: // VBOs
                    MainWindow.main.threeDSettings.drawMethod = 2;
                    break;
                case 2: // drawElements
                    MainWindow.main.threeDSettings.drawMethod = 1;
                    break;
                case 3: // elements
                    MainWindow.main.threeDSettings.drawMethod = 0;
                    break;
            }
        }

        private void DrawModels(bool showBbox = true)
        {
            GL.Enable(EnableCap.PolygonSmooth);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.CullFace);

            foreach (ThreeDModel model in view.models)
            {
                if (!model.reset)
                    model.curPos = model.curPos2;
                else
                {
                    model.curPos = new Matrix4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
                    model.reset = false;
                }

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

                if ((model.Selected)&&(showBbox))
                {
                    GL.PushMatrix();
                    model.AnimationBefore();
                    Color col = MainWindow.main.threeDSettings.SelectionBoxBackgroundColor();
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(0, 0, 0, 255));
                    GL.Material(MaterialFace.Front, MaterialParameter.Emission, new OpenTK.Graphics.Color4(0, 0, 0, 0));
                    GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });
                    GL.Material(
                        MaterialFace.Front,
                        MaterialParameter.Emission,
                        new OpenTK.Graphics.Color4(col.R, col.G, col.B, col.A));
                    GL.Begin(PrimitiveType.Lines);
                    GL.Vertex3(model.xMin, model.yMin, model.zMin);
                    GL.Vertex3(model.xMax, model.yMin, model.zMin);

                    GL.Vertex3(model.xMin, model.yMin, model.zMin);
                    GL.Vertex3(model.xMin, model.yMax, model.zMin);

                    GL.Vertex3(model.xMin, model.yMin, model.zMin);
                    GL.Vertex3(model.xMin, model.yMin, model.zMax);

                    GL.Vertex3(model.xMax, model.yMax, model.zMax);
                    GL.Vertex3(model.xMin, model.yMax, model.zMax);

                    GL.Vertex3(model.xMax, model.yMax, model.zMax);
                    GL.Vertex3(model.xMax, model.yMin, model.zMax);

                    GL.Vertex3(model.xMax, model.yMax, model.zMax);
                    GL.Vertex3(model.xMax, model.yMax, model.zMin);

                    GL.Vertex3(model.xMin, model.yMax, model.zMax);
                    GL.Vertex3(model.xMin, model.yMax, model.zMin);

                    GL.Vertex3(model.xMin, model.yMax, model.zMax);
                    GL.Vertex3(model.xMin, model.yMin, model.zMax);

                    GL.Vertex3(model.xMax, model.yMax, model.zMin);
                    GL.Vertex3(model.xMax, model.yMin, model.zMin);

                    GL.Vertex3(model.xMax, model.yMax, model.zMin);
                    GL.Vertex3(model.xMin, model.yMax, model.zMin);

                    GL.Vertex3(model.xMax, model.yMin, model.zMax);
                    GL.Vertex3(model.xMin, model.yMin, model.zMax);

                    GL.Vertex3(model.xMax, model.yMin, model.zMax);
                    GL.Vertex3(model.xMax, model.yMin, model.zMin);

                    GL.End();
                    model.AnimationAfter();
                    GL.PopMatrix();
                }
            }
        }
 
        private void DrawPrintbedFrame()
        {
            float dx1 = 0;
            float dx2 = dx1 + 0;
            float dy1 = 0;
            float dy2 = dy1 + 0;
            GL.LineWidth(2f);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            GL.BlendFunc((BlendingFactor)BlendingFactorSrc.SrcAlpha, (BlendingFactor)BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);

            Color doorcol = Color.Red;
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(0, 0, 0, 180));
            GL.Material(MaterialFace.Front, MaterialParameter.Emission, new OpenTK.Graphics.Color4(0, 0, 0, 0));
            GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });
            GL.Enable(EnableCap.LineSmooth);
            GL.Material(
                MaterialFace.Front,
                MaterialParameter.Emission,
                new OpenTK.Graphics.Color4(doorcol.R, doorcol.G, doorcol.B, doorcol.A));
            {
                GL.Begin(PrimitiveType.LineStrip);
                int pad = 2;
                int tri = 10;
                GL.Vertex3(-pad, MainWindow.main.PrintAreaDepth + pad, -pad);
                GL.Vertex3(-pad, -pad, -pad);
                GL.Vertex3(MainWindow.main.PrintAreaWidth + pad, -pad, -pad);
                GL.Vertex3(MainWindow.main.PrintAreaWidth + pad, MainWindow.main.PrintAreaDepth + pad, -pad);
                GL.Vertex3(MainWindow.main.PrintAreaWidth / 2 + tri, MainWindow.main.PrintAreaDepth + pad, -pad);
                GL.Vertex3(MainWindow.main.PrintAreaWidth / 2, MainWindow.main.PrintAreaDepth + pad + tri, -pad);
                GL.Vertex3(MainWindow.main.PrintAreaWidth / 2 - tri, MainWindow.main.PrintAreaDepth + pad, -pad);
                GL.Vertex3(-pad, MainWindow.main.PrintAreaDepth + pad, -pad);
                GL.End();
            }

            GL.LineWidth(1f);
            Color col = MainWindow.main.threeDSettings.PrinterFrameBackgroundColor();
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(0, 0, 0, 255));
            GL.Material(MaterialFace.Front, MaterialParameter.Emission, new OpenTK.Graphics.Color4(0, 0, 0, 0));
            GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });
            GL.Material(
                MaterialFace.Front,
                MaterialParameter.Emission,
                new OpenTK.Graphics.Color4(col.R, col.G, col.B, col.A));
            GL.Enable(EnableCap.LineSmooth);
            if (MainWindow.main.threeDSettings.showPrintbed.IsChecked == true)
            {
                int i;
                // Draw origin
                GL.Disable(EnableCap.CullFace);
                GL.Begin(PrimitiveType.Triangles);
                GL.Normal3(0, 0, 1);

                GL.End();
                GL.Begin(PrimitiveType.Lines);

                {
                    // Original Print cube
                    GL.Vertex3(0, 0, 0);
                    GL.Vertex3(0, 0, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0, 0);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0, 0 + MainWindow.main.PrintAreaDepth, 0);
                    GL.Vertex3(0, 0 + MainWindow.main.PrintAreaDepth, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0 + MainWindow.main.PrintAreaDepth, 0);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0 + MainWindow.main.PrintAreaDepth, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0, 0, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0 + MainWindow.main.PrintAreaDepth, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0 + MainWindow.main.PrintAreaDepth, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0, 0 + MainWindow.main.PrintAreaDepth, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0, 0 + MainWindow.main.PrintAreaDepth, MainWindow.main.PrintAreaHeight);
                    GL.Vertex3(0, 0, MainWindow.main.PrintAreaHeight);

                    float dx = 10; // ps.PrintAreaWidth / 20f;
                    float dy = 10; // ps.PrintAreaDepth / 20f;
                    float x, y;

                    for (i = 0; i < 200; i++)
                    {
                        x = (float)i * dx;
                        if (x >= MainWindow.main.PrintAreaWidth)
                            x = MainWindow.main.PrintAreaWidth;

           
                            GL.Vertex3(0 + x, 0, 0);
                            GL.Vertex3(0 + x, 0 + MainWindow.main.PrintAreaDepth, 0);
              
                        if (x >= MainWindow.main.PrintAreaWidth) break;
                    }
                    for (i = 0; i < 200; i++)
                    {
                        y = (float)i * dy;
                        if (y > MainWindow.main.PrintAreaDepth)
                            y = MainWindow.main.PrintAreaDepth;
         
                            GL.Vertex3(0, 0 + y, 0);
                            GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0 + y, 0);
              
                        if (y >= MainWindow.main.PrintAreaDepth)
                            break;
                    }
                }
                GL.End();
                GL.DepthFunc(DepthFunction.Less);
            }
        }

        private void DrawPrintbedBase()
        {
            float dx1 = 0;
            float dx2 = dx1 + 0;
            float dy1 = 0;
            float dy2 = dy1 + 0;

            if (MainWindow.main.threeDSettings.showPrintbed.IsChecked == true)
            {
                GL.Disable(EnableCap.CullFace);
                GL.Enable(EnableCap.Blend);	// Turn Blending On
                GL.BlendFunc((BlendingFactor)BlendingFactorSrc.SrcAlpha, (BlendingFactor)BlendingFactorDest.OneMinusSrcAlpha);
                GL.DepthFunc(DepthFunction.Less);
                //GL.Disable(EnableCap.Lighting);

                // Draw bottom
                Color col = MainWindow.main.threeDSettings.PrinterBaseBackgroundColor();
                float[] transblack = new float[] { 0, 0, 0, 0 };
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, new OpenTK.Graphics.Color4(col.R, col.G, col.B, 130));
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, transblack);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Emission, transblack);
                GL.PushMatrix();
                if (cam.phi < Math.PI / 2)
                    GL.Translate(0, 0, -0.04);
                else
                    GL.Translate(0, 0, +0.04);

                {
                    GL.Begin(PrimitiveType.Quads);
                    GL.Normal3(0, 0, 1);

                    GL.Vertex3(0, 0, 0);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0, 0);
                    GL.Vertex3(0 + MainWindow.main.PrintAreaWidth, 0 + MainWindow.main.PrintAreaDepth, 0);
                    GL.Vertex3(0 + 0, 0 + MainWindow.main.PrintAreaDepth, 0);

                    GL.End();
                }
                GL.PopMatrix();
                GL.Disable(EnableCap.Blend);
            }
        }

        public void gl_Paint(object sender, PaintEventArgs e)
        {
            if (view == null) return;
            try
            {
                if (!loaded) return;

                Point location = gl.PointToScreen(Point.Empty);
                DetectDrawingMethod();
                fpsTimer.Reset();
                fpsTimer.Start();
                gl.MakeCurrent();
                GL.Enable(EnableCap.Multisample);
                GL.ClearColor(MainWindow.main.threeDSettings.BackgroundTopBackgroundColor());
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
                
                // Draw gradient background
                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadIdentity();
                GL.MatrixMode(MatrixMode.Modelview);
                GL.LoadIdentity();
                GL.Disable(EnableCap.DepthTest);
                GL.Disable(EnableCap.Lighting);

                GL.Begin(PrimitiveType.Quads);
                GL.Color4(convertColor(MainWindow.main.threeDSettings.BackgroundBottomBackgroundColor())); // Silver -> CornflowerBlue
                GL.Vertex2(-1.0, -1.0);
                GL.Vertex2(1.0, -1.0);
                GL.Color4(convertColor(MainWindow.main.threeDSettings.BackgroundTopBackgroundColor()));    // White -> WhiteSmoke   
                GL.Vertex2(1.0, 1.0);
                GL.Vertex2(-1.0, 1.0);
                GL.End();

                GL.Enable(EnableCap.DepthTest);
                SetupViewport();
                Vector3 camPos = cam.CameraPosition;
                lookAt = Matrix4.LookAt(camPos.X, camPos.Y, camPos.Z, cam.viewCenter.X, cam.viewCenter.Y, cam.viewCenter.Z, 0, 0, 1.0f);
                GL.MatrixMode(MatrixMode.Modelview);
                GL.LoadMatrix(ref lookAt);
                GL.ShadeModel(ShadingModel.Smooth);
                GL.Enable(EnableCap.LineSmooth);
                AddLights();
                //Enable Backfaceculling
                GL.Enable(EnableCap.CullFace);
                GL.Enable(EnableCap.PolygonSmooth);
                //GL.Enable(EnableCap.Blend);
                GL.LineWidth(2f);
                GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
                //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                Color col = MainWindow.main.threeDSettings.PrinterBaseBackgroundColor();
                //GL.Translate(-0-ps.PrintAreaWidth * 0.5f,-0 -ps.PrintAreaDepth * 0.5f, -0.5f * ps.PrintAreaHeight);
                GL.GetFloat(GetPName.ModelviewMatrix, out modelView);
                GL.Material(
                    MaterialFace.Front,
                    MaterialParameter.Specular,
                    new OpenTK.Graphics.Color4(255, 255, 255, 255));

                    GL.Disable(EnableCap.DepthTest);
                    DrawPrintbedBase();
                    GL.Enable(EnableCap.DepthTest);
                    DrawPrintbedFrame();

                try
                {
                    DrawModels();
                }
                catch (System.OutOfMemoryException)
                {
                    //Debug.WriteLine("Exception: "+exception + 
                    //" available memory is " + new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory.ToString("N0"));
                    if (view.models.Count > 0)
                    {
                        stlComp.RemoveLastModel();
                        UpdateChanges();
                    }
                    MainWindow.main.threedview.ui.BusyWindow.Visibility = System.Windows.Visibility.Hidden;
                    MessageBox.Show("Error(" + (short)View3D.Protocol.ErrorCode.LOAD_FILE_FAIL + "): " + Trans.T("M_LOAD_FILE_FAIL"));	// Please downsize the model
                    GC.Collect();
                }

                gl.SwapBuffers();
                fpsTimer.Stop();

                double framerate = 1.0 / fpsTimer.Elapsed.TotalSeconds;
                if (framerate < 30)
                {
                    slowCounter++;
                    if (slowCounter >= 10)
                    {
                        slowCounter = 0;
                        foreach (ThreeDModel model in view.models)
                        {
                            model.ReduceQuality();
                        }
                    }
                }
                else if (slowCounter > 0)
                    slowCounter--;
            }
            catch { }
        }

        static bool configureSettings = true;
        private void ThreeDControl_Load(object sender, EventArgs e)
        {
            if (configureSettings)
            {
                try
                {
                    string sv = GL.GetString(StringName.Version).Trim();

                    int p = sv.IndexOf(" ");
                    if (p > 0) sv = sv.Substring(0, p);
                    p = sv.IndexOf('.');
                    if (p > 0)
                    {
                        p = sv.IndexOf('.', p + 1);
                        if (p > 0)
                            sv = sv.Substring(0, p);
                        GLversion = Convert.ToDouble(sv, CultureInfo.InvariantCulture);
                    }
                    try
                    {
                        float val = 0;
                        float.TryParse(sv, NumberStyles.Float, GCode.format, out val);
                        MainWindow.main.threeDSettings.openGLVersion = val;
                    }
                    catch
                    {
                        MainWindow.main.threeDSettings.openGLVersion = 1.1f;
                    }

                    MainWindow.main.threeDSettings.useVBOs = GL.GetString(StringName.Extensions).Contains("GL_ARB_vertex_buffer_object");
                
                }
                catch { }
                configureSettings = false;
            }
            loaded = true;
            SetupViewport();
            ui.Show();
            Focus();
        }

        public Geom3DLine pickLine = null; // Last pick up line ray
        public Geom3DVector pickPoint = new Geom3DVector(0, 0, 0); // Koordinates of last pick

        public void UpdatePickLine(int x, int y)
        {
            if (view == null) return;
            // Intersection on bottom plane

            int window_y = (Height - y) - Height / 2;
            double norm_y = (double)window_y / (double)(Height / 2);
            int window_x = x - Width / 2;
            double norm_x = (double)window_x / (double)(Width / 2);
            float fpy = (float)(nearHeight * 0.5 * norm_y) * 1f;
            float fpx = (float)(nearHeight * 0.5 * aspectRatio * norm_x) * 1f;

            Vector4 frontPointN = new Vector4(0, 0, 0, 1);
            Vector4 dirN = new Vector4(fpx, fpy, -nearDist, 0);
            Matrix4 ntrans;
            Vector3 camPos = cam.CameraPosition;
            ntrans = Matrix4.LookAt(camPos.X, camPos.Y, camPos.Z, cam.viewCenter.X, cam.viewCenter.Y, cam.viewCenter.Z, 0, 0, 1.0f);
            ntrans = Matrix4.Invert(ntrans);
            Vector4 frontPoint = ntrans.Row3;
            Vector4 dirVec = Vector4.Transform(dirN, ntrans);
            pickLine = new Geom3DLine(new Geom3DVector(frontPoint.X / frontPoint.W, frontPoint.Y / frontPoint.W, frontPoint.Z / frontPoint.W),
                new Geom3DVector(dirVec.X, dirVec.Y, dirVec.Z), true);
            pickLine.dir.normalize();
        }

        private ThreeDModel Picktest(int x, int y)
        {
            Vector3 hitP = new Vector3();
            Vector3 near, far;
            Ray ray = RayCasting.GenerateRay(x, y, out near, out far);
            float length = float.MaxValue;
            Vector3[] triangle = new Vector3[3];
            Vector3[] curTri = new Vector3[3];
            ThreeDModel nearestModel = null;
            foreach (PrintModel model in stlComp.models)
            {
                if (RayCasting.RaycastAABB(ray, model))
                {
                    if (RayCasting.RaycastAABB(ray, model))
                    {
                        int id;
                        float output;
                        float[] rayPos = { ray.Position.X, ray.Position.Y, ray.Position.Z };
                        float[] rayNor = { ray.Normal.X, ray.Normal.Y, ray.Normal.Z };
                        ModelMatrix mtx = ModelObjectToolHelper.ToModelMatrix(model.trans);

                        if (ModelObjectToolWrapper.Instance.Tool.RayIntersectTriangle(mtx, model.submesh.glVertices,
                            rayPos, rayNor, out id, out output) == true)
                        {
                            hitP = ray.Position + ray.Normal * output;
                            Line line = new Line(near, hitP);
                            if (line.Length <= length)
                            {
                                length = line.Length;
                                nearestModel = model;
                            }
                        }
                        GC.Collect();
                    }
                }
            }
            return nearestModel;
        }

        public void gl_Resize(object sender, EventArgs e)
        {
            float dpiX, dpiY;
            Graphics graphics = this.CreateGraphics();
            dpiX = graphics.DpiX;
            dpiY = graphics.DpiY;

            ui.Width = gl.Size.Width / dpiX * 96;
            ui.Height = gl.Size.Height / dpiY * 96;

            SetupViewport();
            gl.Invalidate();
        }

        Geom3DPlane movePlane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1)); // Plane where object movement occurs
        Geom3DVector moveStart = new Geom3DVector(0, 0, 0), moveLast = new Geom3DVector(0, 0, 0), movePos = new Geom3DVector(0, 0, 0);

        private void gl_MouseDown(object sender, MouseEventArgs e)
        {
            ui.view_toggleButton.IsChecked = false;
            ui.move_toggleButton.IsChecked = false;
            ui.resize_toggleButton.IsChecked = false;

            keyX = e.X;
            keyY = e.Y;

            cam.PreparePanZoomRot();
            lastX = xDown = e.X;
            lastY = yDown = e.Y;
            movePlane = new Geom3DPlane(new Geom3DVector(0, 0, 0), new Geom3DVector(0, 0, 1));
            moveStart = moveLast = new Geom3DVector(0, 0, 0);
            UpdatePickLine(e.X, e.Y);
            movePlane.intersectLine(pickLine, moveStart);

            gl.Invalidate();
        }

        private void gl_MouseMove(object sender, MouseEventArgs e)
        {
            Keys k = Control.ModifierKeys;
            if ( k == Keys.Alt )
                return;

            double window_y = (gl.Height - e.Y) - gl.Height / 2;
            double window_x = e.X - gl.Width / 2;

            if (e.Button == MouseButtons.None)
            {
                speedX = speedY = 0;
                //ui.Show();
                //Focus();
                gl.Invalidate();
                return;
            }

            //ui.Hide();
            //Focus();

            xPos = e.X;
            yPos = e.Y;
            UpdatePickLine(e.X, e.Y);
            movePos = new Geom3DVector(0, 0, 0);
            movePlane.intersectLine(pickLine, movePos);
            float d = Math.Min(gl.Width, gl.Height) / 3;
            speedX = Math.Max(-1, Math.Min(1, (xPos - xDown) / d));
            speedY = Math.Max(-1, Math.Min(1, (yPos - yDown) / d));
        }

        private void gl_MouseUp(object sender, MouseEventArgs e)
        {
            ThreeDModel sel = null;

            // not in Support Editor mode, check if changing the 'selected' object. Otherwise, not change.
            if (true)
            {
                if (e.Button == MouseButtons.Left)
                {
                    sel = Picktest(e.X, e.Y);

                    if (sel != null)
                    {
                        movePlane = new Geom3DPlane(pickPoint, new Geom3DVector(0, 0, 1));
                        moveStart = moveLast = new Geom3DVector(pickPoint);
                    }
                    if (sel != null && view.eventObjectMoved != null)
                    {
                        view.eventObjectSelected(sel);
                    }
                    else if (keyX == e.X && keyY == e.Y)
                        stlComp.listObjects.SelectedItems.Clear();
                }
                if (e.Button == MouseButtons.Right)
                {
                    sel = Picktest(e.X, e.Y);

                    if (sel != null)
                    {
                        movePlane = new Geom3DPlane(pickPoint, new Geom3DVector(0, 0, 1));
                        moveStart = moveLast = new Geom3DVector(pickPoint);
                    }
                    if (sel != null && view.eventObjectMoved != null)
                    {
                        view.eventObjectSelected(sel);
                        contextMenu.Show(Cursor.Position);
                    }
                    else if (keyX == e.X && keyY == e.Y)
                    {
                        contextMenu.Show(Cursor.Position);
                    }
                }
            }

            speedX = speedY = 0;
            gl.Invalidate();
        }

        private void gl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta != 0)
            {
                cam.PreparePanZoomRot();
                cam.Zoom(1f - e.Delta / 6000f);
                zoom *= 1f - e.Delta / 2000f;

                if (zoom < 0.002) zoom = 0.002f;
                if (zoom > 5.9) zoom = 5.9f;
                //userPosition.Y += e.Delta;
                gl.Invalidate();
            }
        }

        void Application_Idle(object sender, EventArgs e)
        {
            if (!loaded || (speedX == 0 && speedY == 0)) return;
            // no guard needed -- we hooked into the event in Load handler

            sw.Stop(); // we've measured everything since last Idle run
            double milliseconds = sw.Elapsed.TotalMilliseconds;
            sw.Reset(); // reset stopwatch
            sw.Start(); // restart stopwatch
            Keys k = Control.ModifierKeys;
            int emode = mode;
            if (k == Keys.Shift || Control.MouseButtons == MouseButtons.Middle) emode = 2;
            if (k == Keys.Control) emode = 0;
            if (k == Keys.Alt) emode = 4;

            if (emode == 0) // Rotate
            {
                float d = Math.Min(gl.Width, gl.Height) / 3;
                speedX = (xPos - xDown) / d;
                speedY = (yPos - yDown) / d;
                cam.Rotate(-speedX * 0.9, speedY * 0.9);
                gl.Invalidate();
            }
            else if (emode == 1) // Pan
            {
                speedX = (xPos - xDown) / gl.Width;
                speedY = (yPos - yDown) / gl.Height;
                Vector3 planeVec = Vector3.Subtract(new Vector3(moveStart.x, moveStart.y, moveStart.z), cam.CameraPosition);
                float dot = Vector3.Dot(planeVec, cam.ViewDirection());
                double len = (dot > 0 ? planeVec.Length : -1);
                cam.Pan(speedX * 200, speedY * 200, len);
                gl.Invalidate();
            }
            else if (emode == 2)
            {
                speedX = (xPos - xDown) / gl.Width;
                speedY = (yPos - yDown) / gl.Height;
                Vector3 planeVec = Vector3.Subtract(new Vector3(moveStart.x, moveStart.y, moveStart.z), cam.CameraPosition);
                float dot = Vector3.Dot(planeVec, cam.ViewDirection());
                double len = (dot > 0 ? planeVec.Length : -1);
                cam.Pan(-speedX, -speedY, len);
                gl.Invalidate();
            }
            else if (emode == 3)
            {
                cam.Zoom(1 - speedY / 3f);
                gl.Invalidate();

            }
            else if (emode == 4)
            {
                Geom3DVector diff = movePos.sub(moveLast);
                moveLast = movePos;
                speedX = (xPos - lastX) * 200 * zoom / gl.Width;
                speedY = (yPos - lastY) * 200 * zoom / gl.Height;
                if (view.eventObjectMoved != null)
                {
                    List<PrintModel> selModels = new List<PrintModel>();
                    List<float> prevX = new List<float>();
                    List<float> prevY = new List<float>();
                    foreach (PrintModel stl in stlComp.ListObjects(true))
                    {
                        selModels.Add(stl);
                        prevX.Add(stl.Position.x);
                        prevY.Add(stl.Position.y);
                    }

                    view.eventObjectMoved(diff.x, diff.y);

                    for (int i = 0; i < selModels.Count; i++)
                    {
                        PrintModel stl = selModels[i];
                        float actualMoveX = stl.Position.x - prevX[i], actualMoveY = stl.Position.y - prevY[i];
                    }
                }
                lastX = xPos;
                lastY = yPos;
                gl.Invalidate();
            }
        }

        public void SetMode(int _mode)
        {
            mode = _mode;
        }

        private void SetCameraDefaults()
        {
            cam.viewCenter = new Vector3(0, 0, 0);
            cam.defaultDistance = 1.6f * (float)Math.Sqrt(MainWindow.main.PrintAreaDepth * MainWindow.main.PrintAreaDepth + MainWindow.main.PrintAreaWidth * MainWindow.main.PrintAreaWidth + MainWindow.main.PrintAreaHeight * MainWindow.main.PrintAreaHeight);
            cam.minDistance = 0.001 * cam.defaultDistance;
        }

        public void frontView()
        {
            SetCameraDefaults();
            cam.OrientFront();
            gl.Invalidate();
        }

        public void backView()
        {
            SetCameraDefaults();
            cam.OrientBack();
            gl.Invalidate();
        }

        public void leftView()
        {
            SetCameraDefaults();
            cam.OrientLeft();
            gl.Invalidate();
        }

        public void rightView()
        {
            SetCameraDefaults();
            cam.OrientRight();
            gl.Invalidate();
        }

        public void topView()
        {
            SetCameraDefaults();
            cam.OrientTop();
            gl.Invalidate();
        }

        public void bottomView()
        {
            SetCameraDefaults();
            cam.OrientBottom();
            gl.Invalidate();
        }

        public void isometricView()
        {
            SetCameraDefaults();
            cam.OrientIsometric();
            gl.Invalidate();
        }

        private void toolMoveViewpoint_Click(object sender, EventArgs e)
        {
            SetMode(2);
        }

        private void ThreeDControl_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (e.KeyChar == '-')
            {
                zoom *= 1.05f;
                if (zoom > 10) zoom = 10;
                gl.Invalidate();
                e.Handled = true;
            }
            if (e.KeyChar == '+')
            {
                zoom *= 0.95f;
                if (zoom < 0.01) zoom = 0.01f;
                gl.Invalidate();
                e.Handled = true;
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            Application_Idle(sender, e);
        }

        private void ThreeDControl_MouseEnter(object sender, EventArgs e)
        {
            // Focus();
        }

        public void ThreeDControl_KeyDown(object sender, KeyEventArgs e)
        {
#if false
            stlComp.listSTLObjects_KeyDown(sender, e);
#endif
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            cam.PreparePanZoomRot();
            cam.Zoom(1.1);
            if (zoom < 0.002) zoom = 0.002f;
            if (zoom > 5.9) zoom = 5.9f;
        
            gl.Invalidate();
            SetMode(3);
        }

        private void toolStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        { }

        public void button_zoomIn_Click(object sender, EventArgs e)
        {
            cam.PreparePanZoomRot();
            cam.Zoom(0.9);
            if (zoom < 0.002) zoom = 0.002f;
            if (zoom > 5.9) zoom = 5.9f;

            gl.Invalidate();
        }

        public void button_zoomOut_Click(object sender, EventArgs e)
        {
            cam.PreparePanZoomRot();
            cam.Zoom(1.1);
            if (zoom < 0.002) zoom = 0.002f;
            if (zoom > 5.9) zoom = 5.9f;
            gl.Invalidate();
        }

        public void button_remove_Click(object sender, EventArgs e)
        {
            if (view.editor)
            {
                stlComp.buttonRemoveSTL_Click(null, null);
            }
            foreach (ThreeDModel m in view.models)
            {
                m.Clear();
            }
            gl.Invalidate();
            stlComp.updateOutside();
        }

        private void contextMenu_Opening(object sender, CancelEventArgs e)
        {
            PrintModel model = stlComp.SingleSelectedModel;
            if (model == null)
            {
                landObjectToolStripMenuItem.Visible = false;
                toolStripSeparator3.Visible = false;
                resetObjectToolStripMenuItem.Visible = false;
                removeObjectToolStripMenuItem.Visible = false;
                mminchToolStripMenuItem.Visible = false;
                inchmmToolStripMenuItem.Visible = false;
                cloneToolStripMenuItem.Visible = false;
                return;
            }
            else
            {
                landObjectToolStripMenuItem.Visible = true;
                toolStripSeparator3.Visible = true;
                resetObjectToolStripMenuItem.Visible = true;
                removeObjectToolStripMenuItem.Visible = true;
                mminchToolStripMenuItem.Visible = true;
                inchmmToolStripMenuItem.Visible = true;
                cloneToolStripMenuItem.Visible = true;
             }
        }

        private void landObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ui.UI_move.button_land_Click(null, null);
        }

        private void resetObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ui.UI_resize_advance.button_Reset_Click(null, null);
            ui.UI_rotate.button_rotate_reset_Click(null, null);
            ui.UI_move.button_move_reset_Click(null, null);
        }

        private void removeObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainWindow.main.threedview.ui.remove_toggleButton_Click(null, null);
        }


        private void mminchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrintModel model = stlComp.SingleSelectedModel;
            if (model == null) return;
            stlComp.DoInchOrScale(model, true);
        }

        private void inchmmToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrintModel model = stlComp.SingleSelectedModel;
            if (model == null) return;
            stlComp.DoInchtomm(model);
        }

        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.stlComp.CloneObject();
        }
    }
}
