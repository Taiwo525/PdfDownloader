namespace PdfDownloader.Core;

public static class PdfValidators
{
    public static bool IsPdfContentType(string mediaType)
    {
        return mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/x-pdf", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> HasPdfHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[5];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        return read == 5 && buffer[0] == '%' && buffer[1] == 'P' && buffer[2] == 'D' && buffer[3] == 'F' && buffer[4] == '-';
    }
}
