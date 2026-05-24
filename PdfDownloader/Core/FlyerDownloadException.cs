namespace PdfDownloader.Core;

/// <summary>
/// Exception thrown when a flyer download operation fails.
/// </summary>
public sealed class FlyerDownloadException : Exception
{
    public FlyerDownloadException(string message) : base(message)
    {
    }

    public FlyerDownloadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

