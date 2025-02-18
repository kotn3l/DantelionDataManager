using Serilog;

namespace DantelionDataManager.Log
{
    public class NoLog : ALogWrapper
    {
        public NoLog() : base()
        {
        }
        public override ILogger LogDebug(object sender, object id, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override Task<ILogger> LogDebugAsync(object sender, object id, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override ILogger LogError(object sender, object id, Exception e, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override Task<ILogger> LogErrorAsync(object sender, object id, Exception e, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override ILogger LogFatal(object sender, object id, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override Task<ILogger> LogFatalAsync(object sender, object id, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override ILogger LogInfo(object sender, object id, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override Task<ILogger> LogInfoAsync(object sender, object id, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override ILogger LogWarning(object sender, object id, string template, params object?[]? propertyValues)
        {
            return null;
        }

        public override Task<ILogger> LogWarningAsync(object sender, object id, string template, params object?[]? propertyValues)
        {
            return null;
        }
    }
}
