using System;
using System.Windows.Input;

namespace PlcCommunicator.Commands
{
    /// <summary>
    /// 实现ICommand接口的命令类，用于MVVM模式中的命令绑定
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        /// <summary>
        /// 创建一个始终可以执行的命令
        /// </summary>
        /// <param name="execute">命令要执行的操作</param>
        public RelayCommand(Action execute) : this(execute, null)
        {
        }

        /// <summary>
        /// 创建一个可以根据条件执行的命令
        /// </summary>
        /// <param name="execute">命令要执行的操作</param>
        /// <param name="canExecute">决定命令是否可以执行的函数</param>
        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 当命令的可执行状态发生改变时触发
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        /// <param name="parameter">命令参数（本实现中未使用）</param>
        /// <returns>如果命令可以执行则返回true，否则返回false</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="parameter">命令参数（本实现中未使用）</param>
        public void Execute(object parameter)
        {
            _execute();
        }

        /// <summary>
        /// 手动触发命令可执行状态的重新评估
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 带参数的RelayCommand实现
    /// </summary>
    /// <typeparam name="T">命令参数的类型</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        /// <summary>
        /// 创建一个始终可以执行的带参数的命令
        /// </summary>
        /// <param name="execute">命令要执行的操作</param>
        public RelayCommand(Action<T> execute) : this(execute, null)
        {
        }

        /// <summary>
        /// 创建一个可以根据条件执行的带参数的命令
        /// </summary>
        /// <param name="execute">命令要执行的操作</param>
        /// <param name="canExecute">决定命令是否可以执行的函数</param>
        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 当命令的可执行状态发生改变时触发
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        /// <param name="parameter">命令参数</param>
        /// <returns>如果命令可以执行则返回true，否则返回false</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute((T)parameter);
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="parameter">命令参数</param>
        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        /// <summary>
        /// 手动触发命令可执行状态的重新评估
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}