using PdfDownloader.Core;

namespace PdfDownloader.Infrastructure;

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
