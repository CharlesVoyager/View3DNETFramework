using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using View3D.MeshInOut;
using View3D.model;
using View3D.model.geom;
using View3D.view.wpf;

namespace View3D.view
{
    public delegate void ObjectModelRemovedEvent(PrintModel model);

    public partial class STLComposer : UserControl
    {
        public class BBoxInfo
        {
            public double MaxX;
            public double MinX;
            public double MaxY;
            public double MinY;
            public double MaxZ;
            public double MinZ;
            public double NewScaleX;
            public double NewScaleY;
            public double OffsetX;
            public double OffsetY;
        }

        public BBoxInfo BBoxOfAllObjects = new BBoxInfo();
        public ThreeDView cont;
        private Dictionary<ListViewItem, Button> delButtonList = new Dictionary<ListViewItem, Button>();
        public event ObjectModelRemovedEvent objectModelRemovedEvent = null;
        public List<PrintModel> models = new List<PrintModel>();
        public List<ModelData> modelDatas = new List<ModelData>();

        IDraw modelDrawer = new ModelGLDraw();
        int mergedCount;
        private List<PrintModel> cloneModels = new List<PrintModel>();

        public double inchtommX = 0, inchtommY = 0, inchtommZ = 0;
        public double mmtoinchX = 0, mmtoinchY = 0, mmtoinchZ = 0;

        public STLComposer()
        {
            InitializeComponent();
            try
            {
                cont = new ThreeDView();
                cont.SetEditor(true);
                cont.objectsSelected = false;
                cont.eventObjectMoved += objectMoved;
                cont.eventObjectSelected += objectSelected;
                cont.autoupdateable = true;
                updateEnabled();

                modelDrawer.GetColorSetting = Main.main.GetColorSetting;

                if (Main.main != null)
                {
                    Main.main.languageChanged += translate;
                    translate();
                }
            }
            catch { }
        }

        public void translate()
        {
        }

        private void AddObject(PrintModel model)
        {
            ListViewItem item = new ListViewItem(model.name);
            item.Tag = model;
            Button button = new Button();
            button.ImageList = imageList16;
            button.ImageIndex = 4;
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Width = 16;
            button.Height = 16;
            button.TextImageRelation = TextImageRelation.Overlay;
            button.Text = "";
            button.Tag = model;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Click += buttonRemoveObject_Click;
            button.Visible = false;
            delButtonList.Add(item, button);
            item.SubItems.Add("");
            item.SubItems.Add("");
            item.SubItems.Add("");
            listObjects.Controls.Add(button);
            listObjects.Items.Add(item);
            SetObjectSelected(model, true);
        }

        private bool CloneObject(PrintModel model)
        {
            PrintModel newModel = (PrintModel)model.cloneWithModel();// (iSameName);
            for (int i = 0; i < modelDatas.Count; i++)
            {
                if (modelDatas[i].originalModel == model.originalModel)
                {
                    newModel.originalModel = modelDatas[i].CloneModel();
                }
            }
            newModel.UpdateBoundingBox();
            newModel.mid = models.Count;
            newModel.serNum = models.Count;

            models.Add(newModel);
            ListViewItem item = new ListViewItem(newModel.name);
            item.Tag = newModel;
            Button button = new Button();
            button.ImageList = imageList16;
            button.ImageIndex = 4;
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Width = 16;
            button.Height = 16;
            button.TextImageRelation = TextImageRelation.Overlay;
            button.Text = "";
            button.Tag = newModel;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Click += buttonRemoveObject_Click;
            button.Visible = false;
            delButtonList.Add(item, button);
            item.SubItems.Add("");
            item.SubItems.Add("");
            item.SubItems.Add("");
            listObjects.Controls.Add(button);
            listObjects.Items.Add(item);
            SetObjectSelected(newModel, true);
            cont.models.AddLast(newModel);
            updateSTLState(newModel);
            Autoposition();
            updateSTLState(newModel);
            return true;
        }

        public void RemoveObject(PrintModel model)
        {
            ListViewItem item = null;
            resetTotalTime(model.serNum);
            foreach (ListViewItem test in listObjects.Items)
            {
                if (test.Tag == model)
                {
                    item = test;
                    break;
                }
            }
            if (item == null) return;
            Button trash = delButtonList[item];
            if (trash != null)
            {
                listObjects.Controls.Remove(trash);
                delButtonList.Remove(item);
            }
            foreach (Button b in delButtonList.Values)
                b.Visible = false;
            listObjects.Items.Remove(item);
            if (objectModelRemovedEvent != null)
                objectModelRemovedEvent(model);
            GC.Collect();
        }

        public LinkedList<PrintModel> ListObjects(bool selected)
        {
            LinkedList<PrintModel> list = new LinkedList<PrintModel>();
            if (selected)
            {
                foreach (ListViewItem item in listObjects.SelectedItems)
                    list.AddLast((PrintModel)item.Tag);
            }
            else
            {
                foreach (ListViewItem item in listObjects.Items)
                    list.AddLast((PrintModel)item.Tag);
            }
            return list;
        }

        public PrintModel SingleSelectedModel
        {
            get
            {
                if (listObjects.SelectedItems.Count != 1) return null;
                return (PrintModel)listObjects.SelectedItems[0].Tag;
            }
        }

        private void resetTotalTime(int serNum)
        {
        }

        public void Update3D()
        {
            Main.main.threedview.UpdateChanges();
        }

        private void float_Validating(object sender, CancelEventArgs e)
        {
            TextBox box = (TextBox)sender;
            try
            {
                float.Parse(box.Text, NumberStyles.Float, GCode.format);
                errorProvider.SetError(box, "");
            }
            catch
            {
                errorProvider.SetError(box, "Not a number.");
            }
        }

        public void UpdateAnalyserData()
        {
            PrintModel model = SingleSelectedModel;
            if (model == null) return;
            labelVertices.Text = model.ActiveModel.vertices.Count.ToString();
            labelEdges.Text = model.ActiveModel.edges.Count.ToString();
            labelFaces.Text = model.ActiveModel.triangles.Count.ToString();
            labelShells.Text = model.ActiveModel.shells.ToString();
            labelIntersectingTriangles.Text = model.ActiveModel.intersectingTriangles.Count.ToString();
            labelIntersectingTriangles.ForeColor = (model.ActiveModel.intersectingTriangles.Count == 0 ? Color.Black : Color.Red);
            labelLoopEdges.Text = model.ActiveModel.loopEdges.ToString();
            labelLoopEdges.ForeColor = (model.ActiveModel.loopEdges == 0 ? Color.Black : Color.Red);
            labelHighConnected.Text = model.ActiveModel.manyShardEdges.ToString();
            labelHighConnected.ForeColor = (model.ActiveModel.manyShardEdges == 0 ? Color.Black : Color.Red);
        }

        private void updateEnabled()
        {
            int n = listObjects.SelectedItems.Count;
            if (n != 1)
            {
                textRotX.Enabled = false;
                textRotY.Enabled = false;
                textRotZ.Enabled = false;
                textScaleX.Enabled = false;
                textScaleY.Enabled = false;
                textScaleZ.Enabled = false;
                buttonLockAspect.Enabled = false;
                textTransX.Enabled = false;
                textTransY.Enabled = false;
                textTransZ.Enabled = false;
                if (Main.main.threedview != null)
                    Main.main.threedview.SetObjectSelected(n > 0);
                panelAnalysis.Visible = false;
            }
            else
            {
                textRotX.Enabled = true;
                textRotY.Enabled = true;
                textRotZ.Enabled = true;
                textScaleX.Enabled = true;
                textScaleY.Enabled = !LockAspectRatio;
                textScaleZ.Enabled = !LockAspectRatio;
                buttonLockAspect.Enabled = true;
                textTransX.Enabled = true;
                textTransY.Enabled = true;
                textTransZ.Enabled = true;
                if (Main.main.threedview != null)
                    Main.main.threedview.SetObjectSelected(true);
                panelAnalysis.Visible = true;
                UpdateAnalyserData();
            }
        }

        public void liftModel(PrintModel model)
        {
            double liftHeight = 6.0;

            if (typeof(PrintModel) != model.GetType()) return;
            if (null == model.originalModel) return;
            if (model.BoundingBoxWOSupport.zMin < liftHeight)
            {
                model.Position.z += (float)Math.Min((liftHeight - model.BoundingBoxWOSupport.zMin), ((double)Main.main.PrintAreaHeight - model.BoundingBoxWOSupport.zMax));
            }
            model.UpdateBoundingBox();
        }

        public void landModel(PrintModel model)
        {
            if (typeof(PrintModel) != model.GetType()) return;
            if (null == model.originalModel) return;
            model.LandUpdateBB();
        }

        public void openAndAddObject(string file)
        {
            listObjects.SelectedItems.Clear();
            modelDatas.Add(new ModelData(file));
            models.Add(new PrintModel(modelDrawer, modelDatas[modelDatas.Count - 1].originalModel));
            FileInfo f = new FileInfo(file);
            bool modelToLand = true;

            ModelInOut modelIO = new ModelInOut();

            try
            {
                modelIO.LoadWOCatch(file, modelDatas[modelDatas.Count - 1]);
                models[models.Count - 1].name = modelDatas[modelDatas.Count - 1].name;
            }
            catch (System.OutOfMemoryException)
            {
                models[models.Count - 1].Clear();
                models.RemoveAt(models.Count - 1);
                Main.main.threedview.ui.BusyWindow.Visibility = System.Windows.Visibility.Hidden;
                GC.Collect();
                MessageBox.Show("Error(" + (short)Protocol.ErrorCode.LOAD_FILE_FAIL + "): " + Trans.T("M_LOAD_FILE_FAIL"));	// Please downsize the model
                return;
            }
            catch
            {
                MessageBox.Show(Trans.T("M_LOAD_STL_FILE_ERROR"), Trans.T("W_LOAD_STL_FILE_ERROR"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            if (Main.main.threedview.ui.BusyWindow.killed)
            {
                models[models.Count - 1].Clear();
                models.RemoveAt(models.Count - 1);
                return;
            }
            models[models.Count - 1].mid = models.Count - 1;
            models[models.Count - 1].serNum = models.Count - 1;
            models[models.Count - 1].ListviewGetModels += ListObjects;

            if (modelToLand)   
            {
                models[models.Count - 1].Center(Main.main.PrintAreaWidth / 2, Main.main.PrintAreaDepth / 2);  // PrintAreaWidth: 128, PrintAreaDepth: 128

                if (models[models.Count - 1].BoundingBox.Center.x != 0 || models[models.Count - 1].BoundingBox.Center.y != 0 || models[models.Count - 1].BoundingBox.Center.z != 0)
                {
                    models[models.Count - 1].ResetVertexPosToBBox();
                }
                models[models.Count - 1].Land();
            }
            else
            {
                models[models.Count - 1].ResetVertexPosToBBox();
                models[models.Count - 1].Position.x = (float)models[models.Count - 1].originalModel.boundingBox.Center.x;
                models[models.Count - 1].Position.y = (float)models[models.Count - 1].originalModel.boundingBox.Center.y;
                models[models.Count - 1].Position.z = (float)models[models.Count - 1].originalModel.boundingBox.Center.z;
                models[models.Count - 1].UpdateBoundingBox();
            }

            if (models[models.Count - 1].ActiveModel.triangles.Count > 0)
            {
                AddObject(models[models.Count - 1]);
                cont.models.AddLast(models[models.Count - 1]);

                if (modelToLand)
                    Autoposition();
                
                updateSTLState(models[models.Count - 1]);
            }

            double xxx = models[models.Count - 1].BoundingBox.Size.x * models[models.Count - 1].BoundingBox.Size.y
                * models[models.Count - 1].BoundingBox.Size.z * 0.001;
            double yyy = 20 / xxx;
            double zzz = Math.Pow(yyy, 0.3);

            if (true)//issue .3wn too small, can not query change scale.
            {
                double modelsZ = 0; 
                modelsZ = (Math.Floor(models[models.Count - 1].BoundingBox.Size.z * 1000)) / 1000;
                double epsilon = 1e-4; // 0.0001

                if (xxx < 0.1)
                {
                    View3D.view.wpf.ObjectResizeDialog tResizeMSG = new view.wpf.ObjectResizeDialog(models[models.Count - 1].BoundingBox.Size.x, models[models.Count - 1].BoundingBox.Size.y, models[models.Count - 1].BoundingBox.Size.z);
                    if (Main.main.threedview.ui.Visibility == System.Windows.Visibility.Visible)
                    {
                        tResizeMSG.Owner = Main.main.threedview.ui;
                    }

                    tResizeMSG.ShowDialog();
                    if (tResizeMSG.gIsScale)
                    {
                        DoInchOrScale(models[models.Count - 1], false);
                    }
                    else if (tResizeMSG.gIsInch)
                    {
                        DoInchScale(models[models.Count - 1]);
                        //DoInchOrScale(models[models.Count - 1], true);
                    }
                }
                else if (models[models.Count - 1].BoundingBox.Size.x - epsilon > Convert.ToDouble(Main.main.PrintAreaWidth) ||
                         models[models.Count - 1].BoundingBox.Size.y - epsilon > Convert.ToDouble(Main.main.PrintAreaDepth) ||
                         modelsZ > Convert.ToDouble(Main.main.PrintAreaHeight))
                {
                    #region object too large
                    double tXBound = models[models.Count - 1].BoundingBox.Size.x / Convert.ToDouble(Main.main.PrintAreaWidth);
                    double tYBound = models[models.Count - 1].BoundingBox.Size.y / Convert.ToDouble(Main.main.PrintAreaDepth);
                    double tZBound = models[models.Count - 1].BoundingBox.Size.z / Convert.ToDouble(Main.main.PrintAreaHeight);
                    double tMax = Math.Max(Math.Max(tXBound, tYBound), Math.Max(tYBound, tZBound));
                    float scaleValue = 0;

                    if (tMax == tXBound)
                    {
                        scaleValue = (float)(Convert.ToDouble(Main.main.PrintAreaWidth) / models[models.Count - 1].BoundingBox.Size.x) * 100;
                    }
                    else if (tMax == tYBound)
                    {
                        scaleValue = (float)(Convert.ToDouble(Main.main.PrintAreaDepth) / models[models.Count - 1].BoundingBox.Size.y) * 100;
                    }
                    else if (tMax == tZBound)
                    {
                        scaleValue = (float)(Convert.ToDouble(Main.main.PrintAreaHeight) / models[models.Count - 1].BoundingBox.Size.z) * 100;
                    }

                    if (MessageBox.Show(Trans.T("M_OBJ_SCALE_DOWN") + " " + (int)scaleValue + "%", Trans.T("W_OBJ_TOO_LARGE"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            tXBound = models[models.Count - 1].BoundingBox.Size.x / Convert.ToDouble(Main.main.PrintAreaWidth);
                            tYBound = models[models.Count - 1].BoundingBox.Size.y / Convert.ToDouble(Main.main.PrintAreaDepth);
                            tZBound = models[models.Count - 1].BoundingBox.Size.z / Convert.ToDouble(Main.main.PrintAreaHeight);

                            tMax = Math.Max(Math.Max(tXBound, tYBound), Math.Max(tYBound, tZBound));
                            if (tMax == tXBound)
                            {
                                models[models.Count - 1].Scale.x = (float)(Convert.ToDouble(Main.main.PrintAreaWidth) / models[models.Count - 1].BoundingBox.Size.x);
                                models[models.Count - 1].Scale.y = models[models.Count - 1].Scale.x;
                                models[models.Count - 1].Scale.z = models[models.Count - 1].Scale.x;

                                Main.main.threedview.ui.UI_move.button_land_Click(null, null);
                                Autoposition();
                            }
                            else if (tMax == tYBound)
                            {
                                models[models.Count - 1].Scale.y = (float)(Convert.ToDouble(Main.main.PrintAreaDepth) / models[models.Count - 1].BoundingBox.Size.y);
                                models[models.Count - 1].Scale.x = models[models.Count - 1].Scale.y;
                                models[models.Count - 1].Scale.z = models[models.Count - 1].Scale.y;
                                Main.main.threedview.ui.UI_move.button_land_Click(null, null);
                                Autoposition();
                            }
                            else if (tMax == tZBound)
                            {
                                models[models.Count - 1].Scale.z = (float)(Convert.ToDouble(Main.main.PrintAreaHeight) / models[models.Count - 1].BoundingBox.Size.z);
                                models[models.Count - 1].Scale.x = models[models.Count - 1].Scale.z;
                                models[models.Count - 1].Scale.y = models[models.Count - 1].Scale.z;
                                Main.main.threedview.ui.UI_move.button_land_Click(null, null);
                                Autoposition();
                            }
                        }
                        catch { }
                    }
                    #endregion
                }
            }

            #region remeber ini position
            for (int i = 0; i < models.Count; i++)
            {
                models[i].Position.inix = models[i].Position.x;
                models[i].Position.iniy = models[i].Position.y;
                models[i].Position.iniz = models[i].Position.z;
            }
            #endregion
        }

        public void check_stl_size_too_small()
        {
            PrintModel model = SingleSelectedModel;
            if (model == null) return;

            double xxx = model.BoundingBox.Size.x * model.BoundingBox.Size.y
                * model.BoundingBox.Size.z * 0.001;
            double yyy = 20 / xxx;
            double zzz = Math.Pow(yyy, 0.3);
            if (xxx < 0.1)
            {
                View3D.view.wpf.ObjectResizeDialog tResizeMSG = new view.wpf.ObjectResizeDialog(models[models.Count - 1].BoundingBox.Size.x, models[models.Count - 1].BoundingBox.Size.y, models[models.Count - 1].BoundingBox.Size.z);
                if (Main.main.threedview.ui.Visibility == System.Windows.Visibility.Visible)
                {
                    tResizeMSG.Owner = Main.main.threedview.ui;
                }
                tResizeMSG.ShowDialog();
                if (tResizeMSG.gIsScale)
                {
                    DoInchOrScale(model, false);
                }
                else if (tResizeMSG.gIsInch)
                {
                    DoInchScale(model);
                    //DoInchOrScale(model, true);
                }
            }
        }

        /// <summary>
        /// Checks the state of the object.
        /// If it is outside print are it starts pulsing
        /// </summary>
        public void updateSTLState(PrintModel stl2)
        {
            bool dataChanged = false;
                  
            stl2.UpdateBoundingBox();

            LinkedList<PrintModel> testList = ListObjects(false);
            foreach (PrintModel pm in testList)
            {
                pm.oldOutside = pm.outside;
                pm.outside = false;
            }
            Boolean showButton = true;
            foreach (PrintModel stl in testList)
            {
                float xMin = stl.xMin, xMax = stl.xMax;

                if ( stl.convexHull3DVtxOrg != null)
                {
                    double radiusSqrt = Math.Pow(Main.main.PrintAreaWidth / 2, 2);

                    Main.main.threedview.TransConvexHull3D(stl);

                    for (int i = 0; i < stl.convexHull2DVtx.Length / 2; i++)
                    {
                        if ((Math.Pow(stl.convexHull2DVtx[i * 2] - (Main.main.PrintAreaWidth / 2), 2) +
                            Math.Pow(stl.convexHull2DVtx[i * 2 + 1] - (Main.main.PrintAreaWidth / 2), 2))
                            > radiusSqrt)
                        {
                            stl.outside = true;
                            showButton = showButton & !stl.outside;
                            break;
                        }
                    }
                }
                else
                {
                    if (!Main.main.PointInside(xMin, stl.yMin, stl.zMin) ||
                       !Main.main.PointInside(xMax, stl.yMin, stl.zMin) ||
                       !Main.main.PointInside(xMin, stl.yMax, stl.zMin) ||
                       !Main.main.PointInside(xMax, stl.yMax, stl.zMin) ||
                       !Main.main.PointInside(xMin, stl.yMin, stl.zMax) ||
                       !Main.main.PointInside(xMax, stl.yMin, stl.zMax) ||
                       !Main.main.PointInside(xMin, stl.yMax, stl.zMax) ||
                       !Main.main.PointInside(xMax, stl.yMax, stl.zMax))
                    {
                        stl.outside = true;
                        showButton = showButton & !stl.outside;
                    }
                }
            }

            if (showButton == false)
            {
                Main.main.threedview.ui.OutofBound.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                Main.main.threedview.ui.OutofBound.Visibility = System.Windows.Visibility.Collapsed;
            }

            foreach (PrintModel pm in testList)
            {
                if (pm.oldOutside != pm.outside)
                {
                    dataChanged = true;
                    pm.ForceViewRegeneration();
                }
            }

            if (dataChanged)
            {
                listObjects.Refresh();
            }
        }

        public void updateOutside()
        {
            bool dataChanged = false;
            LinkedList<PrintModel> testList = ListObjects(false);

            foreach (PrintModel pm in testList)
            {
                pm.oldOutside = pm.outside;
                pm.outside = false;
            }
            Boolean showButton = true;
            foreach (PrintModel stl in testList)
            {
                float xMin = stl.xMin, xMax = stl.xMax;
                if (!Main.main.PointInside(xMin, stl.yMin, stl.zMin) ||
                    !Main.main.PointInside(xMax, stl.yMin, stl.zMin) ||
                    !Main.main.PointInside(xMin, stl.yMax, stl.zMin) ||
                    !Main.main.PointInside(xMax, stl.yMax, stl.zMin) ||
                    !Main.main.PointInside(xMin, stl.yMin, stl.zMax) ||
                    !Main.main.PointInside(xMax, stl.yMin, stl.zMax) ||
                    !Main.main.PointInside(xMin, stl.yMax, stl.zMax) ||
                    !Main.main.PointInside(xMax, stl.yMax, stl.zMax))
                {
                    showButton = showButton & !stl.outside;
                }
            }

            if (showButton == false)
            {
                Main.main.threedview.ui.OutofBound.Visibility = System.Windows.Visibility.Visible;
            }

            foreach (PrintModel pm in testList)
            {
                if (pm.oldOutside != pm.outside)
                {
                    dataChanged = true;
                    pm.ForceViewRegeneration();
                }
            }

            if (dataChanged)
            {
                listObjects.Refresh();
            }
        }

        public void listSTLObjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            updateEnabled();
            LinkedList<PrintModel> list = ListObjects(false);
            LinkedList<PrintModel> sellist = ListObjects(true);
            PrintModel stl = (sellist.Count == 1 ? sellist.First.Value : null);
            foreach (PrintModel s in list)
            {
                s.Selected = sellist.Contains(s);
            }
            if (stl != null)
            {
                textRotX.Text = stl.Rotation.x.ToString(GCode.format);
                textRotY.Text = stl.Rotation.y.ToString(GCode.format);
                textRotZ.Text = stl.Rotation.z.ToString(GCode.format);
                LockAspectRatio = (stl.Scale.x == stl.Scale.y && stl.Scale.x == stl.Scale.z);
                textScaleX.Text = stl.Scale.x.ToString(GCode.format);
                textScaleY.Text = stl.Scale.y.ToString(GCode.format);
                textScaleZ.Text = stl.Scale.z.ToString(GCode.format);
                textTransX.Text = (stl.Position.x).ToString(GCode.format);
                textTransY.Text = (stl.Position.y).ToString(GCode.format);
                textTransZ.Text = (stl.Position.z).ToString(GCode.format);
                Main.main.threedview.ui.UI_object_information.Analyse(stl);
            }
            Main.main.threedview.UpdateChanges();
        }

        public bool LockAspectRatio
        {
            get
            {
                return buttonLockAspect.ImageIndex == 1;
            }
            set
            {
                buttonLockAspect.ImageIndex = value ? 1 : 0;
                textScaleY.Enabled = !value;
                textScaleZ.Enabled = !value;
            }
        }

        private void textTransX_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldPositionX = stl.Position.x;
            float.TryParse(textTransX.Text, NumberStyles.Float, GCode.format, out stl.Position.x);

            if (Math.Abs(oldPositionX - stl.Position.x) < 0.001) return;

  
            if (typeof(PrintModel) == stl.GetType())
            {
                // model is not a support model, and model has no support
                updateSTLState(stl);
            }

            Main.main.threedview.UpdateChanges();
        }

        private void textTransY_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldPositionY = stl.Position.y;
            float.TryParse(textTransY.Text, NumberStyles.Float, GCode.format, out stl.Position.y);

            if (Math.Abs(oldPositionY - stl.Position.y) < 0.001) return;

            if (typeof(PrintModel) == stl.GetType())
            {
                // model is not a support model, and model has no support
                updateSTLState(stl);
            }

            Main.main.threedview.UpdateChanges();
        }

        private void textTransZ_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldPositionZ = stl.Position.z;
            float.TryParse(textTransZ.Text, NumberStyles.Float, GCode.format, out stl.Position.z);

            if (Math.Abs(oldPositionZ - stl.Position.z) < 0.001) return;

            updateSTLState(stl);
            Main.main.threedview.UpdateChanges();
        }

        private void objectMoved(float dx, float dy)
        {
            //STL stl = (STL)listSTLObjects.SelectedItem;
            //if (stl == null) return;
            foreach (PrintModel stl in ListObjects(true))
            {
                if (stl.Position.x + dx < Main.main.PrintAreaWidth * 1.2 && stl.Position.x + dx > -Main.main.PrintAreaWidth * 0.2)
                    stl.Position.x += dx;
                if (stl.Position.y + dy < Main.main.PrintAreaDepth * 1.2 && stl.Position.y + dy > -Main.main.PrintAreaDepth * 0.2)
                    stl.Position.y += dy;
                if (listObjects.SelectedItems.Count == 1)
                {
                    textTransX.Text = stl.Position.x.ToString(GCode.format);
                    textTransY.Text = stl.Position.y.ToString(GCode.format);
                }
                updateSTLState(stl);
            }
            Main.main.threedview.UpdateChanges();
        }

        /* vic add for delete all stl object */
        public void RemoveAllObject()
        {
            foreach (ListViewItem item in listObjects.Items)
            {
                PrintModel p = (PrintModel)item.Tag;
                if (p.GetType() == typeof(PrintModel))
                    item.Selected = true;
            }
            buttonRemoveSTL_Click(null, null);
        }

        public void SetObjectSelected(PrintModel model, bool select)
        {
            foreach (ListViewItem item in listObjects.Items)
            {
                if (item.Tag == model)
                    item.Selected = select;
            }
        }

        private void objectSelected(ThreeDModel sel)
        {
  
            if (Control.ModifierKeys == Keys.Shift)
            {
                if (!sel.Selected)
                    SetObjectSelected((PrintModel)sel, true);
            }
            else
                if (Control.ModifierKeys == Keys.Control)
                {
                    if (sel.Selected)
                        SetObjectSelected((PrintModel)sel, false);
                    else
                        SetObjectSelected((PrintModel)sel, true);
                }
                else
                {
                    listObjects.SelectedItems.Clear();
                    SetObjectSelected((PrintModel)sel, true);
                }
        }

        private void textScaleX_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldScaleX = stl.Scale.x;
            float.TryParse(textScaleX.Text, NumberStyles.Float, GCode.format, out stl.Scale.x);
            updateSTLState(stl);
            Main.main.threedview.UpdateChanges();
        }

        private void textScaleY_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldScaleY = stl.Scale.y;
            float.TryParse(textScaleY.Text, NumberStyles.Float, GCode.format, out stl.Scale.y);

            updateSTLState(stl);
            Main.main.threedview.UpdateChanges();
        }

        private void textScaleZ_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldScaleZ = stl.Scale.z;
            float.TryParse(textScaleZ.Text, NumberStyles.Float, GCode.format, out stl.Scale.z);

            updateSTLState(stl);

            //Ben---20190116---Removed, Fixed a bug causes when loading 3ws the very first time, model will drop to ground causing support missing.
            //For detailed information, trace to "textScaleZ_TextChanged" event.
            if (oldScaleZ != stl.Scale.z)
            {
                stl.LandUpdateBBNoPreUpdate();
            }

            Main.main.threedview.UpdateChanges();
        }

        public void textRotX_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldRotX = stl.Rotation.x;
            float.TryParse(textRotX.Text, NumberStyles.Float, GCode.format, out stl.Rotation.x);
            stl.ForceViewRegeneration();

            if (Math.Abs(oldRotX - stl.Rotation.x) < 0.001) return;

            updateSTLState(stl);
            Main.main.threedview.UpdateChanges();
        }

        private void textRotY_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldRotY = stl.Rotation.y;
            float.TryParse(textRotY.Text, NumberStyles.Float, GCode.format, out stl.Rotation.y);
            stl.ForceViewRegeneration();

            if (Math.Abs(oldRotY - stl.Rotation.y) < 0.001) return;

            updateSTLState(stl);
            Main.main.threedview.UpdateChanges();
        }

        private void textRotZ_TextChanged(object sender, EventArgs e)
        {
            PrintModel stl = SingleSelectedModel;

            if (stl == null) return;

            float oldRotZ = stl.Rotation.z;
            float.TryParse(textRotZ.Text, NumberStyles.Float, GCode.format, out stl.Rotation.z);
            stl.ForceViewRegeneration();

            if (Math.Abs(oldRotZ - stl.Rotation.z) < 0.001) return;

            else if (typeof(PrintModel) == stl.GetType())
            {
                // model is not a support model, and model has no support
                updateSTLState(stl);
            }

            Main.main.threedview.UpdateChanges();
        }

        private void buttonRemoveObject_Click(object sender, EventArgs e)
        {
            PrintModel model = (PrintModel)((Button)sender).Tag;
            cont.models.Remove(model);
            RemoveObject(model);
            models[model.mid].Clear();
            Main.main.threedview.UpdateChanges();
        }

        private bool IsValidPrintModel(PrintModel model)
        {
            // [Todo] 如何只檢查有意義的stl?
            if (model.name == "Unknown" ||
                typeof(PrintModel) != model.GetType() ||
                null == model.originalModel)
                return false;

            return true;
        }

        public List<PrintModel> GetAllPrintModels()
        {
            List<PrintModel> printModels = new List<PrintModel>();
            foreach (PrintModel m in models)
            {
                if (IsValidPrintModel(m))
                {
                    printModels.Add(m);
                }
            }
            return printModels;
        }

        public List<PrintModel> GetSelectedPrintModels()
        {
            List<PrintModel> printModels = new List<PrintModel>();
            foreach (PrintModel m in models)
            {
                if (IsValidPrintModel(m) && m.Selected)
                {
                    printModels.Add(m);
                }
            }
            return printModels;
        }

        private void RemoveModel(PrintModel model)
        {
            cont.models.Remove(model);
            RemoveObject(model);
            for (int i = 0; i < models.Count; i++)
            {
                if (models[i] == model)
                {
                    models.RemoveAt(i);
                    break;
                }
            }
            
            for (int j = 0; j < modelDatas.Count; j++)
            {
                if (modelDatas[j].originalModel == model.originalModel)
                {
                    modelDatas[j].RemoveOne();
                    if (modelDatas[j].Used == 0)
                    {
                        modelDatas.RemoveAt(j);
                    }
                }
            }

            if (model.ListviewGetModels != null)
                model.ListviewGetModels -= ListObjects;

            model.Clear(); // release memory!!
        }

        public void RemoveLastModel()
        {
            if (0 == models.Count) return;

            int lastModelIdx = models.Count - 1;
            while (lastModelIdx >= 0)
            {
                if (typeof(PrintModel) == models[lastModelIdx].GetType() && null != models[lastModelIdx].originalModel)
                {
                    RemoveModel(models[lastModelIdx]);
                    return;
                }
                lastModelIdx--;
            }
        }

        private void RemoveAllSelectedModels()
        {
            foreach (PrintModel stl in ListObjects(true))
            {
                RemoveModel(stl);
            }

            if (Main.main.threedview.view.models.Count == 0)
            {
                Main.main.threedview.ui.OutofBound.Visibility = System.Windows.Visibility.Collapsed;
            }
            Main.main.threedview.UpdateChanges();
        }

        public void buttonRemoveSTL_Click(object sender, EventArgs e)
        {
            RemoveAllSelectedModels();
        }

        public bool Autoposition(bool inputByClone = false)
        {  
            if (listObjects.Items.Count == 1)
            {
                PrintModel stl = (PrintModel)listObjects.Items[0].Tag;

                stl.CenterWOLand(Main.main.PrintAreaWidth / 2, Main.main.PrintAreaDepth / 2);

                Main.main.threedview.UpdateChanges();
                return true;
            }

            RectPacker packer = new RectPacker(1, 1);
            OutRectPacker outPacker = new OutRectPacker(1000);

            int border = 1;//3

            float maxW = Main.main.PrintAreaWidth;
            float maxH = Main.main.PrintAreaDepth;
            float xOff = 0, yOff = 0;
            outPacker.SetPlatformSize(maxW, maxH);
            bool autosizeFailed = false;
            foreach (PrintModel stl in ListObjects(false))
            {
                if (typeof(PrintModel) != stl.GetType())
                {
                    continue;
                }
                //if (stl.modifiedR == false && stl.modifiedM == false && stl.modifiedS == false)       // Comment this to fix auto position incorrect issue, need to more test
                {
                    int w = 2 * border + (int)Math.Ceiling(stl.xMax - stl.xMin);
                    int h = 2 * border + (int)Math.Ceiling(stl.yMax - stl.yMin);
                    if (!packer.addAtEmptySpotAutoGrow(new PackerRect(0, 0, w, h, stl), (int)maxW, (int)maxH))
                    {
                        autosizeFailed = true;
                        outPacker.addOutsideSpotAutoGrow(new PackerRect(0, 0, w, h, stl));
                        //PlaceModelToOutside(stl);
                    }
                }
            }
            if (autosizeFailed)
            {
                float xCenter = (2000 - outPacker.w) / 2.0f;
                float yCenter = (2000 - outPacker.h) / 2.0f;
                float xOrigPos = xOff + xCenter + outPacker.vRects[0].x + border - 1000;
                float yOrigPos = yOff + yCenter + outPacker.vRects[0].y + border - 1000;
                for (int i = 1; i < outPacker.vRects.Count; i++)
                {
                    PrintModel s = (PrintModel)outPacker.vRects[i].obj;
                    float xPos = xOff + xCenter + outPacker.vRects[i].x + border - 1000 - xOrigPos;
                    float yPos = yOff + yCenter + outPacker.vRects[i].y + border - 1000 - yOrigPos;
                    float xMove = xPos - s.xMin;
                    float yMove = yPos - s.yMin;
                    s.Position.x += xMove;
                    s.Position.y += yMove;
                    s.UpdateBoundingBox();
                }
                MessageBox.Show(Trans.T("M_PRINTER_BED_FULL_TEXT"),
                Trans.T("W_PRINTER_BED_FULL"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            float xAdd = (maxW - packer.w) / 2.0f;
            float yAdd = (maxH - packer.h) / 2.0f;
            foreach (PackerRect rect in packer.vRects)
            {
                PrintModel s = (PrintModel)rect.obj;
                float xPos = xOff + xAdd + rect.x + border;
                float yPos = yOff + yAdd + rect.y + border;
                float xMove = xPos - s.xMin;
                float yMove = yPos - s.yMin;
                s.Position.x += xMove;
                s.Position.y += yMove;
                s.UpdateBoundingBox();
            }
            Main.main.threedview.UpdateChanges();
            return true;
        }

        static bool inRecheckFiles = false;

        public void listSTLObjects_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                //foreach (ListViewItem item in listObjects.Items)
                //    item.Selected = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Q || e.KeyCode == Keys.E)
            {
                if (listObjects.Items.Count != 0)
                {
                    if (listObjects.Items.Count == 1)
                    {
                        listObjects.Items[0].Selected = true;
                    }
                    else
                    {
                        if (ListObjects(true).Count > 1)
                        {
                            for (int i = 0; i < listObjects.Items.Count; i++)
                            {
                                listObjects.Items[i].Selected = false;
                            }
                        }
                        if (e.KeyCode == Keys.Q)
                        {
                            if (ListObjects(true).Count == 0)
                            {
                                listObjects.Items[0].Selected = true;
                            }
                            else
                            {
                                PrintModel act = SingleSelectedModel;
                                if (act != null)
                                {
                                    for (int i = 0; i < listObjects.Items.Count; i++)
                                    {
                                        if (act == (PrintModel)listObjects.Items[i].Tag)
                                        {
                                            if (i == 0)
                                            {
                                                listObjects.Items[0].Selected = true;
                                                break;
                                            }
                                            else
                                            {
                                                listObjects.Items[i].Selected = false;
                                                listObjects.Items[i - 1].Selected = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (e.KeyCode == Keys.E)
                        {
                            if (ListObjects(true).Count == 0)
                            {
                                listObjects.Items[listObjects.Items.Count - 1].Selected = true;
                            }
                            else
                            {
                                PrintModel act = SingleSelectedModel;
                                if (act != null)
                                {
                                    for (int i = 0; i < listObjects.Items.Count; i++)
                                    {
                                        if (act == (PrintModel)listObjects.Items[i].Tag)
                                        {
                                            if (i == listObjects.Items.Count - 1)
                                            {
                                                listObjects.Items[i].Selected = true;
                                                break;
                                            }
                                            else
                                            {
                                                listObjects.Items[i].Selected = false;
                                                listObjects.Items[i + 1].Selected = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.C)
            {
                cloneModels.Clear();
                cloneModels = GetSelectedPrintModels();
            }
            else if (e.KeyCode == Keys.V)
            {
                foreach (PrintModel pm in cloneModels)
                {
                    CloneObject(pm);
                    //if(!CloneObject(pm))
                    //{
                    //    return;
                    //}
                }
            }
            else if (e.KeyCode == Keys.Delete)
            {
                Main.main.threedview.ui.remove_toggleButton_Click(null, null);
                //buttonRemoveSTL_Click(sender, null);
                e.Handled = true;
            }
        }

        public void CloneObject()
        {
            cloneModels.Clear();
            cloneModels = GetSelectedPrintModels();
            foreach (PrintModel pm in cloneModels)
            {
                CloneObject(pm);
                //if(CloneObject(pm))
                //{
                //    return;
                //}
            }
        }

        private void listObjects_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (e.ColumnIndex == 1)
            {
                e.DrawDefault = false;
                //  if ((e.ItemState & ListViewItemStates.Selected) == 0)
                //      e.DrawBackground();

                Graphics g = e.Graphics;
                PrintModel model = (PrintModel)e.Item.Tag;
                g.DrawImage(imageList16.Images[model.Model.manifold ? 2 : 3], e.Bounds.Left + e.Bounds.Width / 2 - 8, e.Bounds.Top + e.Bounds.Height / 2 - 8);
            }
            else if (e.ColumnIndex == 2)
            {
                PrintModel model = (PrintModel)e.Item.Tag;
                e.DrawDefault = false;
                //  if ((e.ItemState & ListViewItemStates.Selected) == 0)
                //      e.DrawBackground();
                Graphics g = e.Graphics;
                int idx = model.outside ? 3 : 2;
                g.DrawImage(imageList16.Images[idx], e.Bounds.Left + e.Bounds.Width / 2 - 8, e.Bounds.Top + e.Bounds.Height / 2 - 8);
            }
            else if (e.ColumnIndex == 3) // Trash
            {
                e.DrawDefault = false;
                //if ((e.ItemState & ListViewItemStates.Selected) == 0)
                //    e.DrawBackground();
                Button b = delButtonList.ContainsKey(e.Item) ? delButtonList[e.Item] : null;
                if (b != null)
                {
                    b.Bounds = new Rectangle(e.Bounds.Left + 1, e.Bounds.Top + 1, e.Bounds.Width - 2, e.Bounds.Height - 2);
                    b.Visible = true;
                }
            }
            else
            {
                e.DrawDefault = true;
            }
        }

        private void listObjects_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void buttonLockAspect_Click(object sender, EventArgs e)
        {
            LockAspectRatio = !LockAspectRatio;
            if (LockAspectRatio)
                textScaleX_TextChanged(null, null);
        }

        private void listObjects_ClientSizeChanged(object sender, EventArgs e)
        {
            int nameWith = listObjects.Width - columnCollision.Width - columnMesh.Width - columnDelete.Width - 5;
            if (nameWith > 0)
                columnName.Width = nameWith;
        }


        public void DoInchScale(PrintModel stl)
        {
            try
            {
                Main.main.threedview.ui.UI_resize_advance.button_mmtoinch.IsEnabled = false;
                Main.main.threedview.ui.UI_resize_advance.button_inchtomm.IsEnabled = true;
                Main.main.threedview.ui.UI_resize_advance.chk_Uniform.IsChecked = true;
                model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;
                Main.main.threedview.ui.UI_resize_advance.bboxnow = bbox.Size.x / Convert.ToDouble(textScaleX.Text);
                Main.main.threedview.ui.UI_resize_advance.bboynow = bbox.Size.y / Convert.ToDouble(textScaleY.Text);
                Main.main.threedview.ui.UI_resize_advance.bboznow = bbox.Size.z / Convert.ToDouble(textScaleZ.Text);
                Double tempX = ObjectResizeDialog.scaleInchx;
                Double tempY = ObjectResizeDialog.scaleInchy;
                Double tempZ = ObjectResizeDialog.scaleInchz;
                textScaleX.Text = (tempX / Main.main.threedview.ui.UI_resize_advance.bboxnow).ToString("0.000");
                textScaleY.Text = (tempY / Main.main.threedview.ui.UI_resize_advance.bboynow).ToString("0.000");
                textScaleZ.Text = (tempZ / Main.main.threedview.ui.UI_resize_advance.bboznow).ToString("0.000");
                Main.main.threedview.ui.UI_resize_advance.gIsShow = true;
                Main.main.threedview.ui.UI_resize_advance.dimX = bbox.Size.x;
                Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.X);
                Main.main.threedview.ui.UI_resize_advance.dimY = bbox.Size.y;
                Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Y);
                Main.main.threedview.ui.UI_resize_advance.dimZ = bbox.Size.z;
                Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Z);
                Main.main.threedview.ui.UI_resize_advance.chk_Uniform_Checked(null, null);
                Main.main.threedview.ui.UI_resize_advance.gIsShow = false;

                updateSTLState(stl);

                //stl.LandUpdateBBNoPreUpdate();
                stl.LandToZ(0);
                Main.main.threedview.UpdateChanges();
            }
            catch { }
        }

        public void SetSliderBar(PrintModel stl)
        {
            try
            {
                model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;
                Main.main.threedview.ui.UI_resize_advance.bboxnow = bbox.Size.x / Convert.ToDouble(textScaleX.Text);
                Main.main.threedview.ui.UI_resize_advance.bboynow = bbox.Size.y / Convert.ToDouble(textScaleY.Text);
                Main.main.threedview.ui.UI_resize_advance.bboznow = bbox.Size.z / Convert.ToDouble(textScaleZ.Text);
                Main.main.threedview.ui.UI_resize_advance.chk_Uniform_Checked(null, null);
            }
            catch { }
        }
        private bool AskUserToChangeUnit()
        {
            StringBuilder strBuild = new StringBuilder(Trans.T("M_RESIZE_MODEL_TOO_BIG")).AppendLine();
            strBuild.Append(Trans.T("M_RESIZE_ASK_TO_SCALE_UP"));
            var result = System.Windows.MessageBox.Show(strBuild.ToString(), Trans.T("M_RESIZE_SCALE_UP_TITLE"), System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            return (result == System.Windows.MessageBoxResult.Yes) ? true : false;
        }
        public void DoInchOrScale(PrintModel stl, bool pIsInch)
        {
            try
            {
                string tDecision = "x";
                Main.main.threedview.ui.UI_resize_advance.chk_Uniform.IsChecked = true;
                model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;
                Main.main.threedview.ui.UI_resize_advance.bboxnow = bbox.Size.x / Convert.ToDouble(textScaleX.Text);
                Main.main.threedview.ui.UI_resize_advance.bboynow = bbox.Size.y / Convert.ToDouble(textScaleY.Text);
                Main.main.threedview.ui.UI_resize_advance.bboznow = bbox.Size.z / Convert.ToDouble(textScaleZ.Text);
                if ((stl.BoundingBox.Size.x >= stl.BoundingBox.Size.y) && (stl.BoundingBox.Size.x >= stl.BoundingBox.Size.z))
                {
                    tDecision = "x";
                }
                else if ((stl.BoundingBox.Size.y >= stl.BoundingBox.Size.x) && (stl.BoundingBox.Size.y >= stl.BoundingBox.Size.z))
                {
                    tDecision = "y";
                }
                else if ((stl.BoundingBox.Size.z >= stl.BoundingBox.Size.x) && (stl.BoundingBox.Size.z >= stl.BoundingBox.Size.y))
                {
                    tDecision = "z";
                }
                if (pIsInch == false)
                {
                    Main.main.threedview.ui.UI_resize_advance.button_mmtoinch.IsEnabled = true;
                    Main.main.threedview.ui.UI_resize_advance.button_inchtomm.IsEnabled = false;
                    Main.main.threedview.ui.UI_resize_advance.gIsShow = true;
                    Main.main.threedview.ui.UI_resize_advance.dimX = 24.000;
                    Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.X);
                    Main.main.threedview.ui.UI_resize_advance.dimY = 24.000;
                    Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Y);
                    Main.main.threedview.ui.UI_resize_advance.dimZ = 24.000;
                    Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Z);
                    Main.main.threedview.ui.UI_resize_advance.gIsShow = false;
                    switch (tDecision)
                    {
                        case "x":
                            Main.main.threedview.ui.UI_resize_advance.dimX = ObjectResizeDialog.scaleMMx;
                            Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.X);
                            break;
                        case "y":
                            Main.main.threedview.ui.UI_resize_advance.dimY = ObjectResizeDialog.scaleMMy;
                            Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Y);
                            break;
                        case "z":
                            Main.main.threedview.ui.UI_resize_advance.dimZ = ObjectResizeDialog.scaleMMz;
                            Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Z);
                            break;
                    }
                    SetSliderBar(stl);
                }
                else
                {
                    Main.main.threedview.ui.UI_resize_advance.chk_Uniform.IsChecked = true;
                    Double tempX = bbox.Size.x * 25.4;
                    Double tempY = bbox.Size.y * 25.4;
                    Double tempZ = bbox.Size.z * 25.4;

                    if ((tempX > Main.main.PrintAreaWidth) ||
                        (tempY > Main.main.PrintAreaDepth) ||
                        (tempZ > Main.main.PrintAreaHeight))
                    {
                        if (!AskUserToChangeUnit())
                        {
                            Main.main.threedview.ui.UI_resize_advance.button_mmtoinch.IsEnabled = true;
                            Main.main.threedview.ui.UI_resize_advance.button_inchtomm.IsEnabled = false;
                            return;
                        }
                    }

                    Main.main.threedview.ui.UI_resize_advance.gIsShow = true;
                    Main.main.threedview.ui.UI_resize_advance.dimX = bbox.Size.x;
                    Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.X, false);
                    Main.main.threedview.ui.UI_resize_advance.dimY = bbox.Size.y;
                    Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Y, false);
                    Main.main.threedview.ui.UI_resize_advance.dimZ = bbox.Size.z;
                    Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Z, false);
                    Main.main.threedview.ui.UI_resize_advance.chk_Uniform_Checked(null, null);
                    Main.main.threedview.ui.UI_resize_advance.gIsShow = false;
                    textScaleX.Text = (tempX / Main.main.threedview.ui.UI_resize_advance.bboxnow).ToString("0.000");
                    textScaleY.Text = (tempY / Main.main.threedview.ui.UI_resize_advance.bboynow).ToString("0.000");
                    textScaleZ.Text = (tempZ / Main.main.threedview.ui.UI_resize_advance.bboznow).ToString("0.000");
                    inchtommX = tempX; inchtommY = tempY; inchtommZ = tempZ;
                    updateSTLState(stl);
                    stl.LandUpdateBBNoPreUpdate();
                    Main.main.threedview.UpdateChanges();
                }
            }
            catch { }
        }

        public void DoInchtomm(PrintModel stl)
        {
            try
            {
                Main.main.threedview.ui.UI_resize_advance.chk_Uniform.IsChecked = true;
                model.geom.RHBoundingBox bbox = stl.BoundingBoxWOSupport;
                Main.main.threedview.ui.UI_resize_advance.chk_Uniform.IsChecked = true;
                Double tempX = bbox.Size.x / 25.4;
                Double tempY = bbox.Size.y / 25.4;
                Double tempZ = bbox.Size.z / 25.4;
                Main.main.threedview.ui.UI_resize_advance.gIsShow = true;
                Main.main.threedview.ui.UI_resize_advance.dimX = tempX;
                Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.X, false);
                Main.main.threedview.ui.UI_resize_advance.dimY = tempY;
                Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Y, false);
                Main.main.threedview.ui.UI_resize_advance.dimZ = tempZ;
                Main.main.threedview.ui.UI_resize_advance.updateTxt(Enums.Axis.Z, false);
                Main.main.threedview.ui.UI_resize_advance.chk_Uniform_Checked(null, null);
                Main.main.threedview.ui.UI_resize_advance.gIsShow = false;
                textScaleX.Text = (tempX / Main.main.threedview.ui.UI_resize_advance.bboxnow).ToString("0.000");
                textScaleY.Text = (tempY / Main.main.threedview.ui.UI_resize_advance.bboynow).ToString("0.000");
                textScaleZ.Text = (tempZ / Main.main.threedview.ui.UI_resize_advance.bboznow).ToString("0.000");
                updateSTLState(stl);
                stl.LandUpdateBBNoPreUpdate();
                Main.main.threedview.UpdateChanges();
            }
            catch { }
        }

        public void OnProcessUpdateForStlMerge(double value)
        {
            if (Main.main.threedview.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                Main.main.threedview.ui.BusyWindow.increment != 0.0)
            {
                if (Main.main.threedview.ui.BusyWindow.busyProgressbar.Value < Main.main.threedview.ui.BusyWindow.firstStagePercent)
                {
                    Main.main.threedview.ui.BusyWindow.busyProgressbar.Value = ((value + mergedCount * 100) / GetAllPrintModels().Count) * Main.main.threedview.ui.BusyWindow.firstStagePercent / 100;
                }
                Application.DoEvents();
            }
        }
    }
}
