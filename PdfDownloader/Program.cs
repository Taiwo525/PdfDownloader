using PdfDownloader.Core;
using PdfDownloader.Infrastructure;
using PdfDownloader.Models;
using Serilog;

Log.Logger = LoggerFactory.Create();

try
{
    var options = AppOptions.Parse(args);

    var downloader = new FlyerDownloader(
        new HttpClientFactory(),
        new ResiliencePipelineFactory(),
        new FileStorageService(),
        Log.Logger);

    await downloader.DownloadAsync(options, CancellationToken.None);
    return 0;
}
catch (FlyerDownloadException ex)
{
    Log.Error(ex, "Flyer download failed: {Message}", ex.Message);
    return 2;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unexpected error");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
