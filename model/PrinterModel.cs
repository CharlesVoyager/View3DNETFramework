using Microsoft.Win32;
using System.ComponentModel;

namespace View3D.model
{
    public class PrinterModel : INotifyPropertyChanged
    {
        public PrinterModel()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }
    }
}
