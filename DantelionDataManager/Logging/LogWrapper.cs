using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace DantelionDataManager.Log
{
    public class LogWrapper : ALogWrapper, IDisposable
    {
        private readonly ILogger _logger;
        public LogWrapper(string filename, ILogOutput output) : base()
        {
            _logger = output.GetLogger(filename);
            Serilog.Log.Logger = _logger;
        }
        private ILogger Log(LogEventLevel level, object sender, object id, string template, params object?[]? propertyValues)
        {
            using (LogContext.PushProperty("id", id))
            {
                var l = _logger.ForContext(sender?.GetType());
                l.Write(level, template, propertyValues);
                return l;
            }
        }
        public override Task<ILogger> LogInfoAsync(object sender, object id, string template, params object?[]? propertyValues)
        {
            return Task.Run(() => Log(LogEventLevel.Information, sender, id, template, propertyValues));
        }
        public override Task<ILogger> LogWarningAsync(object sender, object id, string template, params object?[]? propertyValues)
        {
            return Task.Run(() => Log(LogEventLevel.Warning, sender, id, AnsiColor.BrightYellow(template), propertyValues));
        }
        public override Task<ILogger> LogErrorAsync(object sender, object id, Exception e, string template, params object?[]? propertyValues)
        {
            return Task.Run(() => Log(LogEventLevel.Error, sender, id, AnsiColor.BrightRed(template + " Message: {m}\nStacktrace:\n{s}"), propertyValues, e.Message, e.StackTrace));
        }
        public override Task<ILogger> LogFatalAsync(object sender, object id, string template, params object?[]? propertyValues)
        {
            return Task.Run(() => Log(LogEventLevel.Fatal, sender, id, template, propertyValues));
        }
        public override Task<ILogger> LogDebugAsync(object sender, object id, string template, params object?[]? propertyValues)
        {
            return Task.Run(() => Log(LogEventLevel.Debug, sender, id, AnsiColor.BrightBlue(template), propertyValues));
        }

        public override ILogger LogInfo(object sender, object id, string template, params object?[]? propertyValues)
        {
            return Log(LogEventLevel.Information, sender, id, template, propertyValues);
        }
        public override ILogger LogWarning(object sender, object id, string template, params object?[]? propertyValues)
        {
            return Log(LogEventLevel.Warning, sender, id, AnsiColor.BrightYellow(template), propertyValues);
        }
        public override ILogger LogError(object sender, object id, Exception e, string template, params object?[]? propertyValues)
        {
            return Log(LogEventLevel.Error, sender, id, AnsiColor.BrightRed(template + "\nMessage: {m}\nStacktrace:\n{s}"), propertyValues, e.Message, e.StackTrace);
        }
        public override ILogger LogFatal(object sender, object id, string template, params object?[]? propertyValues)
        {
            return Log(LogEventLevel.Fatal, sender, id, template, propertyValues);
        }
        public override ILogger LogDebug(object sender, object id, string template, params object?[]? propertyValues)
        {
            return Log(LogEventLevel.Debug, sender, id, AnsiColor.BrightBlue(template), propertyValues);
        }

        public void Dispose()
        {
            Serilog.Log.CloseAndFlush();
        }
    }
}
