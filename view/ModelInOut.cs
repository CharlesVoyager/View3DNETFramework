using System;
using System.IO;
using View3D.model;
using View3D.model.geom;
using View3D.MeshInOut;

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
                    ///MessageBox.Show(Trans.T("M_LOAD_STL_FILE_ERROR"), Trans.T("W_LOAD_STL_FILE_ERROR"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            if (MainWindow.main == null) return;
            if (MainWindow.main.threedview == null) return;

            // BusyWindow start
            MainWindow.main.ui.BusyWindow.labelBusyMessage.Text = Trans.T("L_MODELING");
            MainWindow.main.ui.BusyWindow.killed = false;
            MainWindow.main.ui.BusyWindow.Visibility = System.Windows.Visibility.Visible;
            MainWindow.main.ui.BusyWindow.buttonCancel.Visibility = System.Windows.Visibility.Visible;
            MainWindow.main.ui.BusyWindow.busyProgressbar.IsIndeterminate = false;
            MainWindow.main.ui.BusyWindow.busyProgressbar.Maximum = 100;
            MainWindow.main.ui.BusyWindow.busyProgressbar.Value = 0; 
            MainWindow.main.ui.BusyWindow.AbortTask += OnUIAbort;
            MainWindow.main.ui.BusyWindow.StartTimer();
        }

        private void EnableBusyWindowNoCancleButton()
        {
            if (MainWindow.main == null) return;
            if (MainWindow.main.threedview == null) return;

            // BusyWindow start
            MainWindow.main.ui.BusyWindow.labelBusyMessage.Text = Trans.T("L_MODELING");
            MainWindow.main.ui.BusyWindow.killed = false;
            MainWindow.main.ui.BusyWindow.Visibility = System.Windows.Visibility.Visible;
            MainWindow.main.ui.BusyWindow.busyProgressbar.IsIndeterminate = false;
            MainWindow.main.ui.BusyWindow.busyProgressbar.Maximum = 100;
            MainWindow.main.ui.BusyWindow.busyProgressbar.Value = 0; 
            MainWindow.main.ui.BusyWindow.AbortTask += OnUIAbort;
            MainWindow.main.ui.BusyWindow.StartTimer();
        }

        private void DisableBusyWindow()
        {
            if (MainWindow.main == null) return;
            if (MainWindow.main.threedview == null) return;
            MainWindow.main.ui.BusyWindow.AbortTask -= OnUIAbort;
            MainWindow.main.ui.BusyWindow.Visibility = System.Windows.Visibility.Hidden;
        }

        public void OnProcessUpdate(int rate)
        {
            if (MainWindow.main.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible)
            {
                MainWindow.main.ui.BusyWindow.busyProgressbar.Value = rate; 
            }
        }

        public void OnProcessUpdate3wsLoadStageLoadStl(int rate)
        {
            if (MainWindow.main.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible)
            {
                MainWindow.main.ui.BusyWindow.busyProgressbar.Value = rate / 2; 
            }
        }

        public void OnProcessUpdateSaveStageMerge(double value)
        {
            if (MainWindow.main.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                MainWindow.main.ui.BusyWindow.increment != 0.0)
            {
                if (MainWindow.main.ui.BusyWindow.busyProgressbar.Value < MainWindow.main.ui.BusyWindow.firstStagePercent)
                {
                    MainWindow.main.ui.BusyWindow.busyProgressbar.Value = ((value + mergedCount * 100) / mergeCountTotal) * MainWindow.main.ui.BusyWindow.firstStagePercent / 100;
                }
            }
        }

        public void OnProcessUpdateSaveStage2nd(int rate)
        {
            if (MainWindow.main.ui.BusyWindow.Visibility == System.Windows.Visibility.Visible &&
                MainWindow.main.ui.BusyWindow.increment != 0.0)
            {
                MainWindow.main.ui.BusyWindow.busyProgressbar.Value =
                        (rate) * (100.0 - MainWindow.main.ui.BusyWindow.firstStagePercent)
                        + MainWindow.main.ui.BusyWindow.firstStagePercent;
            }
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
