using NModbus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PlcCommunicator.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly object _eventLock = new object();
        private readonly object _errorLock = new object();

        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        public bool HasErrors
        {
            get => GetHasErrors();
        }

        private bool GetHasErrors() // 辅助方法确保使用锁
        {
            lock (_errorLock)
            {
                return _errors.Any(kvp => kvp.Value != null && kvp.Value.Count > 0);
            }
        }

        private event EventHandler<DataErrorsChangedEventArgs>? _errorsChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
        {
            add { lock (_errorLock) { _errorsChanged += value; } }
            remove { lock (_errorLock) { _errorsChanged -= value; } }
        }

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


        public void OnErrorsChanged(string propertyName)
        {
            EventHandler<DataErrorsChangedEventArgs>? errorsHandler;

            lock (_errorLock)
            {
                errorsHandler = _errorsChanged;
            }

            if (errorsHandler == null) return;

            if (Application.Current?.Dispatcher?.CheckAccess() ?? true)
            {
                errorsHandler(this, new DataErrorsChangedEventArgs(propertyName));
            }
            else
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                errorsHandler(this, new DataErrorsChangedEventArgs(propertyName))
                );
            }
        }

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return Enumerable.Empty<string>();
            }

            lock (_errorLock)
            {
                return _errors.TryGetValue(propertyName!, out var errorsList) && errorsList != null
                       ? errorsList
                       : Enumerable.Empty<string>();
            }
        }

        protected virtual void SetError(string propertyName, string errorMessage)
        {

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                Debug.WriteLine("SetError函数使用了null或空字符串的属性名！");
                return;
            }

            bool changed = false;
            bool originalHasErrors = GetHasErrors(); // 在加锁前检查整体状态

            lock (_errorLock)
            {
                bool hadErrorBefore = _errors.ContainsKey(propertyName);

                if (string.IsNullOrEmpty(errorMessage))
                {
                    // 清除错误
                    if (hadErrorBefore)
                    {
                        _errors.Remove(propertyName);
                        changed = true;
                    }
                }
                else
                {
                    // 设置错误（覆盖）
                    var newErrorList = new List<string> { errorMessage }; // 总是包含一个项的新列表
                    if (!hadErrorBefore || !_errors[propertyName].SequenceEqual(newErrorList)) // 检查是否与之前不同
                    {
                        _errors[propertyName] = newErrorList; // 赋值新列表
                        changed = true;
                    }
                }

                if (changed)
                {
                    OnErrorsChanged(propertyName); // 通知此属性的错误已更改
                    bool currentHasErrors = GetHasErrors(); // 更改后检查整体状态
                    if (originalHasErrors != currentHasErrors)
                    {
                        OnPropertyChanged(nameof(HasErrors)); // 如果整体 HasErrors 状态改变，则发出通知
                    }
                }
            }
        }

        protected virtual void ClearError(string propertyName)
        {
            SetError(propertyName, null); // 使用 SetError 的清除逻辑
        }
    }
}
