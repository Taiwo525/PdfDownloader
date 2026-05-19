using Serilog;

namespace PdfDownloader.Infrastructure;

public static class LoggerFactory
{
    public static ILogger Create()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();
    }
}
