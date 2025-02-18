using Serilog;

namespace DantelionDataManager.Log
{
    public abstract class ALogWrapper
    {
        public static ALogWrapper Instance { get; private set; }
        public ALogWrapper()
        {
            Instance = this;
        }
        public static ALogWrapper Get()
        {
            return Instance;
        }
        public abstract Task<ILogger> LogInfoAsync(object sender, object id, string template, params object?[]? propertyValues);
        public abstract Task<ILogger> LogWarningAsync(object sender, object id, string template, params object?[]? propertyValues);
        public abstract Task<ILogger> LogErrorAsync(object sender, object id, Exception e, string template, params object?[]? propertyValues);
        public abstract Task<ILogger> LogFatalAsync(object sender, object id, string template, params object?[]? propertyValues);
        public abstract Task<ILogger> LogDebugAsync(object sender, object id, string template, params object?[]? propertyValues);

        public abstract ILogger LogInfo(object sender, object id, string template, params object?[]? propertyValues);
        public abstract ILogger LogWarning(object sender, object id, string template, params object?[]? propertyValues);
        public abstract ILogger LogError(object sender, object id, Exception e, string template, params object?[]? propertyValues);
        public abstract ILogger LogFatal(object sender, object id, string template, params object?[]? propertyValues);
        public abstract ILogger LogDebug(object sender, object id, string template, params object?[]? propertyValues);
    }
}
