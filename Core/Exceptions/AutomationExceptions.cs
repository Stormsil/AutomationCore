// Core/Exceptions/AutomationExceptions.cs
using System;
using AutomationCore.Core.Models;

namespace AutomationCore.Core.Exceptions
{
    /// <summary>
    /// Базовое исключение для всех ошибок автоматизации
    /// </summary>
    public abstract class AutomationException : Exception
    {
        protected AutomationException(string message) : base(message) { }
        protected AutomationException(string message, Exception innerException) : base(message, innerException) { }
    }

    #region Capture Exceptions

    /// <summary>
    /// Исключения захвата экрана
    /// </summary>
    public class CaptureException : AutomationException
    {
        public CaptureTarget? Target { get; }

        public CaptureException(string message, CaptureTarget? target = null) : base(message)
        {
            Target = target;
        }

        public CaptureException(string message, Exception innerException, CaptureTarget? target = null)
            : base(message, innerException)
        {
            Target = target;
        }
    }

    /// <summary>
    /// Захват не поддерживается на данной системе
    /// </summary>
    public class CaptureNotSupportedException : CaptureException
    {
        public CaptureNotSupportedException(string reason)
            : base($"Screen capture is not supported: {reason}") { }
    }

    /// <summary>
    /// Таймаут операции захвата
    /// </summary>
    public class CaptureTimeoutException : CaptureException
    {
        public TimeSpan Timeout { get; }

        public CaptureTimeoutException(TimeSpan timeout, CaptureTarget? target = null)
            : base($"Capture operation timed out after {timeout.TotalMilliseconds}ms", target)
        {
            Timeout = timeout;
        }
    }

    /// <summary>
    /// Устройство захвата недоступно
    /// </summary>
    public class CaptureDeviceException : CaptureException
    {
        public string DeviceName { get; }

        public CaptureDeviceException(string deviceName, string message)
            : base($"Capture device '{deviceName}': {message}")
        {
            DeviceName = deviceName;
        }

        public CaptureDeviceException(string deviceName, string message, Exception innerException)
            : base($"Capture device '{deviceName}': {message}", innerException)
        {
            DeviceName = deviceName;
        }
    }

    #endregion

    #region Window Exceptions

    /// <summary>
    /// Исключения работы с окнами
    /// </summary>
    public class WindowException : AutomationException
    {
        public WindowHandle? Handle { get; }

        public WindowException(string message, WindowHandle? handle = null) : base(message)
        {
            Handle = handle;
        }

        public WindowException(string message, Exception innerException, WindowHandle? handle = null)
            : base(message, innerException)
        {
            Handle = handle;
        }
    }

    /// <summary>
    /// Окно не найдено
    /// </summary>
    public class WindowNotFoundException : WindowException
    {
        public WindowSearchCriteria? SearchCriteria { get; }

        public WindowNotFoundException(string message, WindowSearchCriteria? criteria = null)
            : base(message)
        {
            SearchCriteria = criteria;
        }

        public WindowNotFoundException(WindowHandle handle)
            : base($"Window with handle 0x{handle.Value:X} was not found", handle) { }
    }

    /// <summary>
    /// Окно недоступно для операций
    /// </summary>
    public class WindowNotAccessibleException : WindowException
    {
        public WindowOperation? AttemptedOperation { get; }

        public WindowNotAccessibleException(WindowHandle handle, WindowOperation? operation = null)
            : base($"Window 0x{handle.Value:X} is not accessible for operations", handle)
        {
            AttemptedOperation = operation;
        }
    }

    #endregion

    #region Template Matching Exceptions

    /// <summary>
    /// Исключения поиска шаблонов
    /// </summary>
    public class TemplateMatchingException : AutomationException
    {
        public string? TemplateKey { get; }

        public TemplateMatchingException(string message, string? templateKey = null) : base(message)
        {
            TemplateKey = templateKey;
        }

        public TemplateMatchingException(string message, Exception innerException, string? templateKey = null)
            : base(message, innerException)
        {
            TemplateKey = templateKey;
        }
    }

    /// <summary>
    /// Шаблон не найден
    /// </summary>
    public class TemplateNotFoundException : TemplateMatchingException
    {
        public TemplateNotFoundException(string templateKey)
            : base($"Template '{templateKey}' was not found", templateKey) { }
    }

    /// <summary>
    /// Шаблон поврежден или некорректен
    /// </summary>
    public class InvalidTemplateException : TemplateMatchingException
    {
        public InvalidTemplateException(string templateKey, string reason)
            : base($"Template '{templateKey}' is invalid: {reason}", templateKey) { }

        public InvalidTemplateException(string templateKey, Exception innerException)
            : base($"Template '{templateKey}' is invalid", innerException, templateKey) { }
    }

    /// <summary>
    /// Совпадение не найдено
    /// </summary>
    public class MatchNotFoundException : TemplateMatchingException
    {
        public double BestScore { get; }
        public double RequiredThreshold { get; }

        public MatchNotFoundException(string templateKey, double bestScore, double threshold)
            : base($"No match found for template '{templateKey}' (best score: {bestScore:F3}, required: {threshold:F3})", templateKey)
        {
            BestScore = bestScore;
            RequiredThreshold = threshold;
        }
    }

    #endregion

    #region Input Exceptions

    /// <summary>
    /// Исключения симуляции ввода
    /// </summary>
    public class InputSimulationException : AutomationException
    {
        public InputEventType? EventType { get; }

        public InputSimulationException(string message, InputEventType? eventType = null) : base(message)
        {
            EventType = eventType;
        }

        public InputSimulationException(string message, Exception innerException, InputEventType? eventType = null)
            : base(message, innerException)
        {
            EventType = eventType;
        }
    }

    /// <summary>
    /// Симуляция ввода заблокирована системой
    /// </summary>
    public class InputBlockedException : InputSimulationException
    {
        public InputBlockedException(string reason, InputEventType? eventType = null)
            : base($"Input simulation blocked by system: {reason}", eventType) { }
    }

    /// <summary>
    /// Недостаточно прав для симуляции ввода
    /// </summary>
    public class InsufficientPrivilegesException : InputSimulationException
    {
        public InsufficientPrivilegesException(string operation)
            : base($"Insufficient privileges for operation: {operation}") { }
    }

    #endregion

    #region Configuration Exceptions

    /// <summary>
    /// Исключения конфигурации
    /// </summary>
    public class ConfigurationException : AutomationException
    {
        public string? ParameterName { get; }

        public ConfigurationException(string message, string? parameterName = null) : base(message)
        {
            ParameterName = parameterName;
        }
    }

    /// <summary>
    /// Некорректное значение параметра
    /// </summary>
    public class InvalidConfigurationException : ConfigurationException
    {
        public object? ProvidedValue { get; }

        public InvalidConfigurationException(string parameterName, object? value, string reason)
            : base($"Invalid configuration parameter '{parameterName}': {reason}", parameterName)
        {
            ProvidedValue = value;
        }
    }

    #endregion

    #region Workflow Exceptions

    /// <summary>
    /// Исключения выполнения сценариев
    /// </summary>
    public class WorkflowException : AutomationException
    {
        public string? WorkflowName { get; }
        public string? FailedStep { get; }

        public WorkflowException(string message, string? workflowName = null, string? failedStep = null)
            : base(message)
        {
            WorkflowName = workflowName;
            FailedStep = failedStep;
        }

        public WorkflowException(string message, Exception innerException, string? workflowName = null, string? failedStep = null)
            : base(message, innerException)
        {
            WorkflowName = workflowName;
            FailedStep = failedStep;
        }
    }

    /// <summary>
    /// Таймаут выполнения сценария
    /// </summary>
    public class WorkflowTimeoutException : WorkflowException
    {
        public TimeSpan Timeout { get; }

        public WorkflowTimeoutException(string workflowName, TimeSpan timeout, string? failedStep = null)
            : base($"Workflow '{workflowName}' timed out after {timeout.TotalSeconds:F1}s", workflowName, failedStep)
        {
            Timeout = timeout;
        }
    }

    /// <summary>
    /// Сценарий был отменен
    /// </summary>
    public class WorkflowCancelledException : WorkflowException
    {
        public WorkflowCancelledException(string workflowName, string? failedStep = null)
            : base($"Workflow '{workflowName}' was cancelled", workflowName, failedStep) { }
    }

    #endregion
}