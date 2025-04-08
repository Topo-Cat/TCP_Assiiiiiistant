using NModbus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PlcCommunicator.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        private readonly object _eventLock = new object();

        private event PropertyChangedEventHandler? _propertyChanged;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { lock (_eventLock) { _propertyChanged += value; } }
            remove { lock (_eventLock) { _propertyChanged -= value; } }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyname = null)
        {
            PropertyChangedEventHandler? handler;

            lock (_eventLock)
            {
                handler = _propertyChanged;
            }

            if (handler == null) return;

            if (Application.Current?.Dispatcher?.CheckAccess() ?? true)
            {
                handler(this, new PropertyChangedEventArgs(propertyname));
            }
            else
            {
                Application.Current?.Dispatcher?.Invoke(() => 
                handler(this, new PropertyChangedEventArgs(propertyname))
                );
            }
        }

        public bool SetProperty<T>(ref T filed, T value, [CallerMemberName] string? propertyname = null)
        {
            if (EqualityComparer<T>.Default.Equals(filed, value)) return false;

            filed = value;
            OnPropertyChanged(propertyname);
            return true;
        }
    }
}
