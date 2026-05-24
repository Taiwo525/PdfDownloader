namespace PdfDownloader.Core;

/// <summary>
/// Validators for PDF content verification.
/// </summary>
public static class PdfValidators
{
    /// <summary>
    /// Validates if the content type indicates a PDF file.
    /// Accepts: application/pdf, application/x-pdf
    /// </summary>
    public static bool IsPdfContentType(string mediaType)
    {
        return mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/x-pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates if the stream starts with the PDF magic bytes (%PDF-).
    /// This is a basic validation that checks the first 5 bytes of the file.
    /// </summary>
    public static async Task<bool> HasPdfHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[5];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        return read == 5 && buffer[0] == '%' && buffer[1] == 'P' && buffer[2] == 'D' && buffer[3] == 'F' && buffer[4] == '-';
    }
}

