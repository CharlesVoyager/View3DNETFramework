using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using View3D.model.geom;
using System.Diagnostics;

namespace View3D.model
{
    public class ModelGLDraw : IDraw
    {
        #region Draw Function
        public void Draw(Submesh mesh, int method, Vector3 edgetrans, bool forceFaces = false)
        {
            GL.LightModel(LightModelParameter.LightModelTwoSide, 1);
            GL.Material(MaterialFace.Back, MaterialParameter.AmbientAndDiffuse, mesh.convertColor(MainWindow.main.threeDSettings.InsideFacesBackgroundColor()));
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Emission, new OpenTK.Graphics.Color4(0, 0, 0, 0));
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, 50f);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Normalize);
            GL.LineWidth(1f);
            GL.DepthFunc(DepthFunction.Less);
            if (method == 2)
            {
                if (mesh.glBuffer == null)
                {
                    mesh.glBuffer = new int[6];
                    GL.GenBuffers(6, mesh.glBuffer);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, mesh.glBuffer[0]);
                    GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(mesh.glVertices.Length * sizeof(float)), mesh.glVertices, BufferUsageHint.StaticDraw);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, mesh.glBuffer[1]);
                    GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(mesh.glNormals.Length * sizeof(float)), mesh.glNormals, BufferUsageHint.StaticDraw);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, mesh.glBuffer[2]);
                    GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(mesh.glColors.Length * sizeof(int)), mesh.glColors, BufferUsageHint.StaticDraw);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.glBuffer[3]);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(mesh.glTriangles.Length * sizeof(int)), mesh.glTriangles, BufferUsageHint.StaticDraw);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.glBuffer[4]);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(mesh.glEdges.Length * sizeof(int)), mesh.glEdges, BufferUsageHint.StaticDraw);
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.glBuffer[5]);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(mesh.glTrianglesError.Length * sizeof(int)), mesh.glTrianglesError, BufferUsageHint.StaticDraw);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, mesh.glBuffer[0]);
                GL.VertexPointer(3, VertexPointerType.Float, 0, 0);
                GL.EnableClientState(ArrayCap.VertexArray);
                GL.BindBuffer(BufferTarget.ArrayBuffer, mesh.glBuffer[1]);
                GL.NormalPointer(NormalPointerType.Float, 0, 0);
                GL.EnableClientState(ArrayCap.NormalArray);
                GL.BindBuffer(BufferTarget.ArrayBuffer, mesh.glBuffer[2]);
                GL.ColorMaterial(MaterialFace.Front, ColorMaterialParameter.AmbientAndDiffuse);
                GL.ColorPointer(4, ColorPointerType.UnsignedByte, sizeof(int), IntPtr.Zero);
                GL.EnableClientState(ArrayCap.ColorArray);
                GL.Enable(EnableCap.ColorMaterial);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.glBuffer[3]);

                if (MainWindow.main.threeDSettings.ShowFaces || forceFaces)
                   GL.DrawElements(PrimitiveType.Triangles, mesh.glTriangles.Length, DrawElementsType.UnsignedInt, 0);

                GL.LightModel(LightModelParameter.LightModelTwoSide, 0);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.glBuffer[5]);
                GL.DrawElements(PrimitiveType.Triangles, mesh.glTrianglesError.Length, DrawElementsType.UnsignedInt, 0);
                GL.Disable(EnableCap.ColorMaterial);
                GL.DisableClientState(ArrayCap.NormalArray);
                GL.Disable(EnableCap.Lighting);
                GL.DepthFunc(DepthFunction.Lequal);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.glBuffer[4]);
                GL.PushMatrix();
                GL.Translate(edgetrans);
                GL.DrawElements(PrimitiveType.Lines, mesh.glEdges.Length, DrawElementsType.UnsignedInt, 0);
                GL.PopMatrix();
                GL.Enable(EnableCap.Lighting);
                GL.DisableClientState(ArrayCap.ColorArray);
                GL.DisableClientState(ArrayCap.VertexArray);
                GL.DisableClientState(ArrayCap.ColorArray);
                GL.Disable(EnableCap.ColorMaterial);
            }
            else if (method == 1)
            {
                GL.EnableClientState(ArrayCap.VertexArray);
                GL.VertexPointer(3, VertexPointerType.Float, 0, mesh.glVertices);
                GL.EnableClientState(ArrayCap.NormalArray);
                GL.NormalPointer(NormalPointerType.Float, 0, mesh.glNormals);
                GL.EnableClientState(ArrayCap.ColorArray);
                GL.Enable(EnableCap.ColorMaterial);
                GL.ColorMaterial(MaterialFace.Front, ColorMaterialParameter.AmbientAndDiffuse);
                GL.ColorPointer(4, ColorPointerType.UnsignedByte, 0, mesh.glColors);
                GL.EnableClientState(ArrayCap.ColorArray);
                if (MainWindow.main.threeDSettings.ShowFaces || forceFaces)
                    GL.DrawElements(PrimitiveType.Triangles, mesh.glTriangles.Length, DrawElementsType.UnsignedInt, mesh.glTriangles);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, mesh.convertColor(MainWindow.main.threeDSettings.ErrorModelBackgroundColor()));
                GL.LightModel(LightModelParameter.LightModelTwoSide, 0);
                GL.DrawElements(PrimitiveType.Triangles, mesh.glTrianglesError.Length, DrawElementsType.UnsignedInt, mesh.glTrianglesError);
                GL.DepthFunc(DepthFunction.Lequal);
                GL.Disable(EnableCap.Lighting);
                GL.PushMatrix();
                GL.Translate(edgetrans);
                GL.DrawElements(PrimitiveType.Lines, mesh.glEdges.Length, DrawElementsType.UnsignedInt, mesh.glEdges);
                GL.PopMatrix();
                GL.Enable(EnableCap.Lighting);
                GL.Disable(EnableCap.ColorMaterial);
                GL.DisableClientState(ArrayCap.ColorArray);
                GL.DisableClientState(ArrayCap.VertexArray);
                GL.DisableClientState(ArrayCap.NormalArray);

            }
            else if (method == 0)
            {
                int n;
                GL.ColorMaterial(MaterialFace.Front, ColorMaterialParameter.AmbientAndDiffuse);
                GL.Enable(EnableCap.ColorMaterial);
                if (MainWindow.main.threeDSettings.ShowFaces || forceFaces)
                {
                    GL.Begin(PrimitiveType.Triangles);
                    n = mesh.glTriangles.Length;
                    for (int i = 0; i < n; i++)
                    {
                        int p = mesh.glTriangles[i] * 3;
                        int col = mesh.glColors[mesh.glTriangles[i]];
                        GL.Color4(new OpenTK.Graphics.Color4((byte)(col & 255), (byte)((col >> 8) & 255), (byte)((col >> 16) & 255), (byte)(col >> 24)));
                        GL.Normal3(mesh.glNormals[p], mesh.glNormals[p + 1], mesh.glNormals[p + 2]);
                        GL.Vertex3(mesh.glVertices[p], mesh.glVertices[p + 1], mesh.glVertices[p + 2]);
                    }
                    GL.End();
                }
                GL.LightModel(LightModelParameter.LightModelTwoSide, 0);
                n = mesh.glTrianglesError.Length;
                if (n > 0)
                {
                    GL.Begin(PrimitiveType.Triangles);
                    for (int i = 0; i < n; i++)
                    {
                        int p = mesh.glTrianglesError[i] * 3;
                        int col = mesh.glColors[mesh.glTrianglesError[i]];
                        GL.Color4(new OpenTK.Graphics.Color4((byte)(col & 255), (byte)((col >> 8) & 255), (byte)((col >> 16) & 255), (byte)(col >> 24)));
                        GL.Normal3(mesh.glNormals[p], mesh.glNormals[p + 1], mesh.glNormals[p + 2]);
                        GL.Vertex3(mesh.glVertices[p], mesh.glVertices[p + 1], mesh.glVertices[p + 2]);
                    }
                    GL.End();
                }

                GL.Disable(EnableCap.Lighting);
                n = mesh.glEdges.Length;
                GL.PushMatrix();
                GL.Translate(edgetrans);

                if (n > 0)
                {
                    GL.Begin(PrimitiveType.Lines);
                    for (int i = 0; i < n; i++)
                    {
                        int p = mesh.glEdges[i] * 3;
                        int col = mesh.glColors[mesh.glEdges[i]];
                        GL.Color4(new OpenTK.Graphics.Color4((byte)(col & 255), (byte)((col >> 8) & 255), (byte)((col >> 16) & 255), (byte)(col >> 24)));
                        GL.Normal3(mesh.glNormals[p], mesh.glNormals[p + 1], mesh.glNormals[p + 2]);
                        GL.Vertex3(mesh.glVertices[p], mesh.glVertices[p + 1], mesh.glVertices[p + 2]);
                    }
                    GL.End();
                }
                GL.PopMatrix();
                GL.Enable(EnableCap.Lighting);
                GL.Disable(EnableCap.ColorMaterial);
            }
            GL.LightModel(LightModelParameter.LightModelTwoSide, 0);
            GL.Disable(EnableCap.Normalize);
        }

        public Vector3 GetTranslateVector()
        {
            return MainWindow.main.threedview.cam.EdgeTranslation();
        }
        #endregion

        #region Color Setting
        public GetColorSettingHandler GetColorSetting { get; set; }

        public virtual int GetColorRGBA(Submesh.MeshColor colorCode, Color frontBackColor)
        {
            int idx = (int)colorCode;

            if (idx >= 0)
                return 255 << 24 | idx;

            Color color;
            if (GetColorSetting != null)
                color = GetColorSetting(colorCode, frontBackColor);
            else
                color = Color.Wheat;

            return ColorToRgba32(color);
        }

        private int ColorToRgba32(Color c)
        {
            return (int)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
        }
        #endregion

        #region Cut Face
        public bool IsCutFaceEnabled()
        {
            return false;
        }

        public bool IsEdgeShowEnabled()
        {
            return MainWindow.main.threeDSettings.ShowEdges;
        }

        public RHVector3 GetCutPosition()
        {
            return new RHVector3(0, 0, 0);
        }

        public RHVector3 GetCutDirection()
        {
            return new RHVector3(0, 0, 0);
        }

        public bool IsCutFaceUpdated()
        {
            return false;
        }
        #endregion

        #region Cut Edge
        //Added by RCGREY for STL Slice Previewer 
        private void BeginCutEdgeConfig(Submesh mesh)
        {
            double[] planeEqDown = mesh.clipPlaneEq;
            double[] planeEqUP = { mesh.clipPlaneEq[0], mesh.clipPlaneEq[1], -mesh.clipPlaneEq[2], mesh.clipPlaneEq[3] };
            //double[] planeEqUP = { 0, 0, 1.0, cut_height + 0.5 };
            AttribMask flag = 0;
            flag = flag | AttribMask.EnableBit;
            flag = flag | AttribMask.PolygonBit;
            flag = flag | AttribMask.CurrentBit;
            flag = flag | AttribMask.DepthBufferBit;
            flag = flag | AttribMask.TransformBit;
            GL.PushAttrib(flag);

            GL.Enable(EnableCap.ClipPlane0);
            GL.Enable(EnableCap.ClipPlane1);
            GL.ClipPlane(ClipPlaneName.ClipPlane0, planeEqDown);
            GL.ClipPlane(ClipPlaneName.ClipPlane1, planeEqUP);


            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
        }

        private void EndCutEdgeConfig()
        {
            GL.Disable(EnableCap.ClipPlane0);
            GL.Disable(EnableCap.ClipPlane1);
            GL.PopAttrib();
        }

        #endregion
    }
}
