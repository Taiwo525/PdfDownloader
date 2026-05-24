using Microsoft.Extensions.Configuration;
using PdfDownloader.Core;
using PdfDownloader.Infrastructure;
using PdfDownloader.Models;
using Serilog;

Log.Logger = LoggerFactory.Create();

try
{
    Log.Information("=== PDF Downloader Application Started ===");
    
    // Build configuration from appsettings.json and environment variables
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
   
        .Build();

    // Bind configuration sections
    var appSettings = new AppSettings();
    configuration.GetSection("AppSettings").Bind(appSettings);
    
    var httpSettings = new HttpSettings();
    configuration.GetSection("HttpSettings").Bind(httpSettings);
   
    
    Log.Information("HTTP Configuration - Timeout: {Timeout}s, Max Retries: {MaxRetries}, Retry Delay: {RetryDelay}s",
        httpSettings.TimeoutSeconds,
        httpSettings.MaxRetryAttempts,
        httpSettings.RetryDelaySeconds);

    // Create options (command-line args override config file)
    var options = AppOptions.Create(appSettings, args, Log.Logger);

    // Create and run downloader
    var downloader = new FlyerDownloader(
        new HttpClientFactory(httpSettings),
        new ResiliencePipelineFactory(httpSettings, Log.Logger),
        new FileStorageService(Log.Logger),
        Log.Logger,
        new SystemTimeProvider());

    await downloader.DownloadAsync(options, CancellationToken.None);
    
    Log.Information("=== PDF Downloader Application Completed Successfully ===");
    return 0;
}
catch (FlyerDownloadException ex)
{
    Log.Error(ex, "Flyer download failed: {Message}", ex.Message);
    Log.Information("=== PDF Downloader Application Failed ===");
    return 2;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unexpected error occurred");
    Log.Information("=== PDF Downloader Application Failed ===");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
