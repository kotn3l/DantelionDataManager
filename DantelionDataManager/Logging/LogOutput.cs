using Serilog;

namespace DantelionDataManager.Log
{
    public abstract class ILogOutput
    {
        protected static readonly string _outTemplate = "{Timestamp:yy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{id}] {Message:lj}{NewLine}{Exception}";
        protected static readonly string _outTemplateImgui = "[{id}] {Message:lj}{NewLine}{Exception}";
        //public ImguiSink _config;
        protected static LoggerConfiguration DefaultLogConfig()
        {
#if DEBUG
            return new LoggerConfiguration().Enrich.FromLogContext().MinimumLevel.Verbose();
#endif
            return new LoggerConfiguration().Enrich.FromLogContext().MinimumLevel.Debug();
        }
        public abstract ILogger GetLogger(string filename);
    }

    public class ConsoleOutput : ILogOutput
    {
        public override ILogger GetLogger(string filename)
        {
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            return DefaultLogConfig().WriteTo.Async(x => x.Console(outputTemplate: _outTemplate)).CreateLogger();
        }
    }

    public class FileOutput : ILogOutput
    {
        public override ILogger GetLogger(string filename)
        {
            var rformatter = new AnsiColorRemoveTextFormatter(_outTemplate);
            return DefaultLogConfig().WriteTo.Async(x => x.File(rformatter, filename)).CreateLogger();
        }
    }

    public class ConsoleAndFileOutput : ILogOutput
    {
        public override ILogger GetLogger(string filename)
        {
            var rformatter = new AnsiColorRemoveTextFormatter(_outTemplate);
            return DefaultLogConfig().WriteTo.Async(x => x.File(rformatter, filename))
                                     .WriteTo.Async(x => x.Console(outputTemplate: _outTemplate)).CreateLogger();
        }
    }

    public class ConsoleAndSeparateFileOutput : ILogOutput
    {
        public override ILogger GetLogger(string directory)
        {
            string timestamp = DateTime.Now.ToString("yyMMdd_HHmmss");
            return DefaultLogConfig().WriteTo.Async(x => x.Console(outputTemplate: _outTemplate))
                                      .WriteTo.Map(
                                            keyPropertyName: "id",
                                            configure: (idValue, writeTo) =>
                                            {
                                                var sanitizedId = idValue.ToString()?.Replace(":", "_").Replace("/", "_");
                                                var rformatter = new AnsiColorRemoveTextFormatter(_outTemplate);
                                                writeTo.File(rformatter, $"{directory}\\{sanitizedId}_{timestamp}.log");
                                            },
                                            defaultKey: "default"
                                      ).CreateLogger();
        }
    }
}
