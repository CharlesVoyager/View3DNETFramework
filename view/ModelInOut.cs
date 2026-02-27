using System;
using System.IO;
using View3D.model;
using View3D.model.geom;
using View3D.MeshInOut;
using System.Windows.Forms;

namespace View3D.view
{
    public class ModelInOut
    {
        public bool TwoStageUpdateProcess { get; set; }
        public event EventHandler AbortTask;
        private bool saveTaskAbort;
        private int mergedCount, mergeCountTotal;

        public ModelInOut()
        {
            TwoStageUpdateProcess = false;
        }
        public void Load(string file, ModelData model)
        {
            EnableBusyWindowNoCancleButton();

            string lname = model.FileName.ToLower();
            if (lname.EndsWith(".stl"))
            {
                MeshIOStl fileMesh = new MeshIOStl();
                AbortTask += fileMesh.TaskAbort;

                try
                {
                    fileMesh.Load(model.FileName, model.originalModel, OnProcessUpdate);
                }
                catch
                {
                    MessageBox.Show(Trans.T("M_LOAD_STL_FILE_ERROR"), Trans.T("W_LOAD_STL_FILE_ERROR"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                AbortTask -= fileMesh.TaskAbort;
            }

            DisableBusyWindow();

            FileInfo info = new FileInfo(file);
            model.name = info.Name;
        }

        public void LoadWOCatch(string file, ModelData model)
        {
            EnableBusyWindow();

            string lname = model.FileName.ToLower();

            IMeshInOut fileMesh;
            Action<int> updateRateFunc;
            if (lname.EndsWith(".stl"))
            {
                fileMesh = new MeshIOStl();
                updateRateFunc = OnProcessUpdate;
            }
            else
            {
                fileMesh = new MeshIOBase();
                updateRateFunc = OnProcessUpdate;
            }
            AbortTask += fileMesh.TaskAbort;
            fileMesh.LoadWOCatch(model.FileName, model.originalModel, updateRateFunc);
            AbortTask -= fileMesh.TaskAbort;

            DisableBusyWindow();

            FileInfo info = new FileInfo(file);
            model.name = info.Name;
        }
        public void Save(string filename, TopoModel model, Setting outSetting)
        { 
        }

        #region EvenHandler
        private void EnableBusyWindow()
        {
            // BusyWindow start
            MainWindow.main.threedview.ui.BusyWindow.labelBusyMessage.Text = Trans.T("L_MODELING");
            MainWindow.main.threedview.ui.BusyWindow.killed = false;
            MainWindow.main.threedview.ui.BusyWindow.Visibility = System.Windows.Visibility.Visible;
            MainWindow.main.threedview.ui.BusyWindow.buttonCancel.Visibility = System.Windows.Visibility.Visible;
            MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.IsIndeterminate = false;
            MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Maximum = 100;
            MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = 0; 
            MainWindow.main.threedview.ui.BusyWindow.AbortTask += OnUIAbort;
            MainWindow.main.threedview.ui.BusyWindow.StartTimer();
            System.Windows.Forms.Application.DoEvents();
        }

        private void EnableBusyWindowNoCancleButton()
        {
            // BusyWindow start
            MainWindow.main.threedview.ui.BusyWindow.labelBusyMessage.Text = Trans.T("L_MODELING");
            MainWindow.main.threedview.ui.BusyWindow.killed = false;
            MainWindow.main.threedview.ui.BusyWindow.Visibility = System.Windows.Visibility.Visible;
            MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.IsIndeterminate = false;
            MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Maximum = 100;
            MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = 0; 
            MainWindow.main.threedview.ui.BusyWindow.AbortTask += OnUIAbort;
            MainWindow.main.threedview.ui.BusyWindow.StartTimer();
            System.Windows.Forms.Application.DoEvents();
        }

        private void DisableBusyWindow()
        {
            MainWindow.main.threedview.ui.BusyWindow.AbortTask -= OnUIAbort;
            MainWindow.main.threedview.ui.BusyWindow.Visibility = System.Windows.Visibility.Hidden;
        }

        public void OnProcessUpdate(int rate)
        {
            if (MainWindow.main.threedview.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible)
            {
                MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = rate; 
            }
            Application.DoEvents();
        }

        public void OnProcessUpdate3wsLoadStageLoadStl(int rate)
        {
            if (MainWindow.main.threedview.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible)
            {
                MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = rate / 2; 
            }
            Application.DoEvents();
        }

        public void OnProcessUpdateSaveStageMerge(double value)
        {
            if (MainWindow.main.threedview.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                MainWindow.main.threedview.ui.BusyWindow.increment != 0.0)
            {
                if (MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value < MainWindow.main.threedview.ui.BusyWindow.firstStagePercent)
                {
                    MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value = ((value + mergedCount * 100) / mergeCountTotal) * MainWindow.main.threedview.ui.BusyWindow.firstStagePercent / 100;
                }
                Application.DoEvents();
            }
        }

        public void OnProcessUpdateSaveStage2nd(int rate)
        {
            if (MainWindow.main.threedview.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                MainWindow.main.threedview.ui.BusyWindow.increment != 0.0)
            {
                MainWindow.main.threedview.ui.BusyWindow.busyProgressbar.Value =
                        (rate) * (100.0 - MainWindow.main.threedview.ui.BusyWindow.firstStagePercent)
                        + MainWindow.main.threedview.ui.BusyWindow.firstStagePercent;
            }
            Application.DoEvents();
        }

        public void OnUIAbort(object sender, EventArgs e)
        {
            saveTaskAbort = true;

            if (AbortTask != null)
                AbortTask(sender, e);
        }
        #endregion

    }
}
