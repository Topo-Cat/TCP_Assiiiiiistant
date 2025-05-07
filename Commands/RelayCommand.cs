using ModbusCommunicator.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace ModbusCommunicator.Commands
{
    /// <summary>
    /// 实现ICommand接口的命令类，用于MVVM模式中的命令绑定
    /// </summary>
    public class RelayCommand : ICommand, IAsyncCommand, INotifyPropertyChanged
    {
        private readonly object _syncLock = new();
        private readonly object _eventLock = new();
        private volatile bool _isExecuting;
        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                bool isChanged = false;

                lock (_syncLock)
                {
                    if (_isExecuting != value)
                    {
                        _isExecuting = value;
                        isChanged = true;
                    }
                }

                if (isChanged)
                    OnPropertyChanged();
                RaiseCanExecuteChangedInternal();
            }
        }

        private readonly Action<object?>? _executeSync;

        private readonly Func<object?, Task>? _executeAsync;
        private readonly Func<object?, CancellationToken, Task>? _executeAsyncWithToken;

        private readonly Func<object?, bool>? _canExecute;

        private event PropertyChangedEventHandler? _propertyChanged;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { lock (_eventLock) { _propertyChanged += value; } }
            remove { lock (_eventLock) { _propertyChanged -= value; } }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyname = null)
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
                Application.Current?.Dispatcher?.Invoke(() => handler(this, new PropertyChangedEventArgs(propertyname)));
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute, Func<bool>? canExecute = null) : this(WrapSyncAction(execute), WrapCanExecute(canExecute)) { }

        public RelayCommand(Action<object?>? execute, Func<bool>? canExecute = null) : this(execute, WrapCanExecute(canExecute)) { }

        public RelayCommand(Action execute, Func<object?, bool>? canExecute) : this(WrapSyncAction(execute), canExecute) { }

        public RelayCommand(Action<object?>? execute, Func<object?, bool>? canExecute = null)
        {
            _executeSync = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            ValidateConstructor();
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null) : this(WrapAsyncFunc(executeAsync), WrapCanExecute(canExecute)) { }

        public RelayCommand(Func<object?, Task> executeAsync, Func<bool>? canExecute = null) : this(executeAsync, WrapCanExecute(canExecute)) { }

        public RelayCommand(Func<Task> executeAsync, Func<object?, bool>? canExecute) : this(WrapAsyncFunc(executeAsync), canExecute) { }

        public RelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            ValidateConstructor();
        }

        public RelayCommand(Func<CancellationToken, Task> executeAsync, Func<bool>? canExecute = null) : this(WrapAsyncFuncWithToken(executeAsync), WrapCanExecute(canExecute)) { }

        public RelayCommand(Func<object?, CancellationToken, Task> executeAsync, Func<bool>? canExecute = null) : this(executeAsync, WrapCanExecute(canExecute)) { }

        public RelayCommand(Func<CancellationToken, Task> executeAsync, Func<object?, bool>? canExecute = null) : this(WrapAsyncFuncWithToken(executeAsync), canExecute) { }

        public RelayCommand(Func<object?, CancellationToken, Task> executeAsync, Func<object?, bool>? canExecute = null)
        {
            _executeAsyncWithToken = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            ValidateConstructor();
        }

        private async Task ExecuteAsyncInternal(object? parameter, CancellationToken cancellationToken, bool forceFireAndForget)
        {
            bool acquiredExecution = false;
            lock (_syncLock)
            {
                if (!_isExecuting && CanExecute(parameter))
                {
                    _isExecuting = true;
                    acquiredExecution = true;
                }
            }

            if (!acquiredExecution)
            {
                return;
            }

            try
            {
                Task? executionTask = null;

                if (_executeSync != null)
                {
                    _executeSync(parameter);
                    executionTask = Task.CompletedTask;
                }
                else if (_executeAsyncWithToken != null)
                {
                    executionTask = _executeAsyncWithToken(parameter, cancellationToken);
                }
                else if (_executeAsync != null)
                {
                    executionTask = _executeAsync(parameter);
                }
                else
                {
                    throw new InvalidOperationException("未找到执行委托。");
                }

                if (!forceFireAndForget && executionTask != null)
                {
                    await executionTask;
                }
                else if (forceFireAndForget && executionTask != null)
                {
                    _ = executionTask.ContinueWith(t =>
                    {
                        if (t.IsFaulted && t.Exception != null)
                        {
                            Debug.WriteLine($"[RelayCommand 即发即忘错误]: {t.Exception}");
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[RelayCommand]: 操作已取消。");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RelayCommand 执行错误]: {ex}");

                if (_executeSync != null || !forceFireAndForget)
                {
                    throw;
                }
            }
            finally
            {
                if (acquiredExecution)// 仅当成功获取执行权时才释放执行标志位
                {
                    _isExecuting = false;
                }
            }
        }

        public void RaiseCanExecuteChanged()// 公开的 RaiseCanExecuteChanged 方法，用于外部调用
        {
            RaiseCanExecuteChangedInternal();
        }

        private void RaiseCanExecuteChangedInternal()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private static Action<object?> WrapSyncAction(Action action)// 用于构造函数的execute辅助包装器
        {
            ArgumentNullException.ThrowIfNull(action);
            return _ => action();// 忽略参数
        }

        private static Func<object?, bool>? WrapCanExecute(Func<bool>? canExecute)// 用于构造函数的canexecute辅助包装器
        {
            if (canExecute == null) return null;
            return _ => canExecute(); // 忽略参数
        }

        private static Func<object?, Task> WrapAsyncFunc(Func<Task> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return _ => func();
        }

        private static Func<object?, CancellationToken, Task> WrapAsyncFuncWithToken(Func<CancellationToken, Task> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return (_, token) => func(token);
        }

        private void ValidateConstructor() // 构造函数验证，确保只有一个执行委托被提供
        {
            int delegateCount = (_executeSync != null ? 1 : 0) +
                                (_executeAsync != null ? 1 : 0) +
                                (_executeAsyncWithToken != null ? 1 : 0);
            if (delegateCount != 1)
            {
                throw new InvalidOperationException("RelayCommand 必须有且只有一个执行委托。");
            }
        }

        public void Execute(object? parameter)
        {
            _ = ExecuteAsyncInternal(parameter, CancellationToken.None, true);
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting)
            {
                return false;
            }
            try
            {
                return _canExecute?.Invoke(parameter) ?? true;
            }
            catch
            {
                Debug.WriteLine("Canexecute执行出错！");
                return false;
            }
        }

        public Task ExecuteAsync(object? parameter)
        {
            return ExecuteAsyncInternal(parameter, CancellationToken.None, false);
        }

        public Task ExecuteAsync(object? parameter, CancellationToken cancellationToken)
        {
            return ExecuteAsyncInternal(parameter, cancellationToken, false);
        }
    }
}