using Microsoft.Extensions.Primitives;
using NModbus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ModbusCommunicator.ViewModels
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

            bool isChanged = false;
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
                        isChanged = true;
                    }
                }
                else
                {
                    // 设置错误（覆盖）
                    var newErrorList = new List<string> { errorMessage }; // 总是包含一个项的新列表
                    if (!hadErrorBefore || !_errors[propertyName].SequenceEqual(newErrorList)) // 检查是否与之前不同
                    {
                        _errors[propertyName] = newErrorList; // 赋值新列表
                        isChanged = true;
                    }
                }

                if (isChanged)
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


        /// <summary>
        /// 验证应用字符串输入,判断属性输入值是否合法。
        /// </summary>
        /// <param name="value">
        /// 属性值</param>
        /// <param name="modelInstance">
        /// Model或数据源的实例对象</param>
        /// <param name="propertyName">
        /// 属性名，切记传入的属性名要与Model或是数据源的属性名称保持一致！
        /// </param>
        protected bool ValidateAndApplyStringInput(string value,
            object modelInstance,
            [CallerMemberName] string? propertyName = null)
        {
            // 1. --- 基本参数验证 ---
            if (modelInstance == null)
            {
                Debug.WriteLine($"ValidateAndApplyStringInput 错误：属性 '{propertyName ?? "未知"}' 的 modelInstance 为 null。");
                return false;
            }
            if (string.IsNullOrEmpty(propertyName))
            {
                Debug.WriteLine("ValidateAndApplyStringInput 错误：propertyName 为 null 或空。");
                return false; // 没有属性名无法继续
            }

            PropertyInfo? modelPropertyInfo = modelInstance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (modelPropertyInfo == null)
            {
                SetError(propertyName, $"内部错误：在模型类型 '{modelInstance.GetType().Name}' 上找不到目标属性 '{propertyName}'。");
                return false;
            }

            Type targetType = modelPropertyInfo.PropertyType; // 获取目标属性的类型

            // 在尝试新的验证/转换之前清除旧错误至关重要
            ClearError(propertyName);

            object? parsedValue = null; // 用于存储转换后的值
            bool conversionSuccess = false; // 标记转换是否成功
            string formatErrorMessage = $"格式无效。请输入一个有效的 {GetFriendlyTypeName(targetType)}。"; // 默认的格式错误消息

            try
            {
                var converter = TypeDescriptor.GetConverter(targetType);

                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        bool isNullableType = !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;

                        if (isNullableType)
                        {
                            // 可空类型通常可以将 null/空字符串 接受为 null 或默认值
                            // 使用 ConvertFromInvariantString 可能对 null 处理更好，但 ConvertFromString 通常也有效
                            parsedValue = converter.ConvertFromInvariantString(value ?? ""); // 传入 "" 以防 stringValue 是 null
                            conversionSuccess = true;
                        }
                        else // 不可空的值类型（int, ushort, bool, DateTime 等）
                        {
                            // 空字符串对于不可空的值类型通常是无效的
                            formatErrorMessage = $"需要为 {GetFriendlyTypeName(targetType)} 提供一个值。";
                            conversionSuccess = false;
                        }
                    }
                    else
                    {
                        // 使用当前区域性设置（CultureInfo.CurrentCulture）来解析数字、日期等，以符合用户的操作系统设置
                        parsedValue = converter.ConvertFromString(null, CultureInfo.CurrentCulture, value);
                        conversionSuccess = true;
                    }
                }
                else
                {
                    // 对于大多数标准类型不应发生
                    formatErrorMessage = $"内部错误：无法将输入值转换为类型 '{targetType.Name}'。";
                    conversionSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"属性 '{propertyName}' 的转换错误：{ex.Message}");
                conversionSuccess = false;
            }

            if (!conversionSuccess)
            {
                SetError(propertyName, formatErrorMessage); // 设置格式错误
                return false; // 停止处理
            }

            var validationContext = new ValidationContext(modelInstance) { MemberName = propertyName };
            var validationResults = new List<ValidationResult>();

            bool isValid = Validator.TryValidateProperty(
                parsedValue,
                validationContext,
                validationResults
                );

            if (!isValid)
            {
                string? firstError = validationResults.FirstOrDefault()?.ErrorMessage;
                SetError(propertyName, firstError ?? "输入的值不满足验证规则。");
                return false; // 停止处理
            }

            try
            {
                modelPropertyInfo.SetValue(modelInstance, parsedValue);// 只有在转换和验证都通过后才设置值

                // ClearError(propertyName); // 这行通常是多余的，但能保证干净状态

                return true; // 表示成功完成
            }
            catch (Exception ex) // 捕获 SetValue 可能的异常（例如 TargetInvocationException）
            {
                Debug.WriteLine($"属性 '{propertyName}' 的 SetValue 错误：{ex.Message}");
                SetError(propertyName, $"内部错误：未能应用值。{ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 辅助方法，用于获取类型的更友好的名称，能处理 Nullable<T>。
        /// </summary>
        private string GetFriendlyTypeName(Type type)
        {
            Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;// 如果是 Nullable<T>，获取其基础类型，否则使用原类型

            // 根据需要自定义名称
            if (underlyingType == typeof(string)) return "文本";
            if (underlyingType == typeof(int)) return "整数";
            if (underlyingType == typeof(ushort)) return "正整数 (0-65535)";
            if (underlyingType == typeof(uint)) return "正整数";
            if (underlyingType == typeof(short)) return "整数 (-32768 到 32767)";
            if (underlyingType == typeof(byte)) return "字节 (0-255)";
            if (underlyingType == typeof(double)) return "数字";
            if (underlyingType == typeof(float)) return "数字";
            if (underlyingType == typeof(decimal)) return "小数";
            if (underlyingType == typeof(bool)) return "布尔值 (true/false)";
            if (underlyingType == typeof(DateTime)) return "日期/时间";
            if (underlyingType == typeof(TimeSpan)) return "时间段";

            // 默认回退为类型的原始名称
            return underlyingType.Name;
        }
    }
}
