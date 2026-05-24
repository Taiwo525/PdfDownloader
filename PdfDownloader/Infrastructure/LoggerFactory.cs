using Serilog;
using Serilog.Events;

namespace PdfDownloader.Infrastructure;

/// <summary>
/// Factory for creating and configuring Serilog logger instances.
/// Uses Serilog.AspNetCore package which includes Console, File, and Debug sinks.
/// 
/// STATIC CLASS: Cannot be instantiated or inherited. Used for utility/factory methods.
/// </summary>
public static class LoggerFactory
{
    /// <summary>
    /// Creates a configured logger with console and file output.
    /// 
    /// Configuration includes:
    /// - Console sink with custom formatting
    /// - File sink with daily rolling (logs/pdfdownloader-{Date}.txt)
    /// - Structured logging with contextual enrichment
    /// - Minimum log level filtering
    /// </summary>
    public static ILogger Create()
    {
        return new LoggerConfiguration()
            // Set minimum log level to Information (filters out Debug and Verbose)
            .MinimumLevel.Information()
            
            // Override log levels for noisy Microsoft and System namespaces
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            
            // Enrich logs with contextual information
            .Enrich.FromLogContext()  // Adds properties from LogContext.PushProperty()
            .Enrich.WithProperty("Application", "PdfDownloader")  // Static property for all logs
            
            // Console sink with custom output template
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            
            // File sink with daily rolling (creates new file each day)
            .WriteTo.File(
                path: "logs/pdfdownloader-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,  // Keep last 30 days of logs
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            
            .CreateLogger();
    }
}

