namespace PdfDownloader.Models;

public sealed class AppOptions
{
    public required string Source { get; init; }
    public required Uri PdfUrl { get; init; }
    public required string OutputDirectory { get; init; }

    public static AppOptions Parse(string[] args)
    {
        var values = args
            .Select(a => a.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(parts => parts[0][2..], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var source = values.TryGetValue("source", out var sourceValue) ? sourceValue : "toom";
        var urlString = values.TryGetValue("url", out var urlValue)
            ? urlValue
            : "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf";
        var output = values.TryGetValue("output", out var outputValue)
            ? outputValue
            : Path.Combine(AppContext.BaseDirectory, "downloads");

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new FlyerDownloadException("Source cannot be empty.");
        }

        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new FlyerDownloadException("Invalid URL. Provide an absolute HTTP/HTTPS URL using --url=<url>.");
        }

        var sanitizedSource = new string(source.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitizedSource))
        {
            throw new FlyerDownloadException("Source must contain at least one letter or digit after sanitization.");
        }

        return new AppOptions
        {
            Source = sanitizedSource.ToLowerInvariant(),
            PdfUrl = uri,
            OutputDirectory = output
        };
    }
}
