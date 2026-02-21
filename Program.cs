using System;
using System.Threading;
using System.Windows.Forms;

namespace View3D
{
    static class Program
    {
        /// <summary>main
        /// Der Haupteinstiegspunkt fur die Anwendung.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Boolean bCreatedNew;
            string MutexName = "";

            MutexName = "View3D_Mutex";
            //Create a new mutex using specific mutex name
            using (Mutex m = new Mutex(false, MutexName, out bCreatedNew))
            {
                GC.Collect();

                if (bCreatedNew)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Main());
                }
                else
                {
                }
            }
        }
    }
}