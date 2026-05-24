using PdfDownloader.Core;
using Serilog;

namespace PdfDownloader.Models;

/// <summary>
/// Runtime application options created by merging AppSettings with command-line arguments.
/// This is the final, validated configuration used during execution.
/// Command-line arguments take precedence over appsettings.json values.
/// 
/// SEALED CLASS: Prevents inheritance to maintain immutability and predictable behavior.
/// Properties are immutable (init) to ensure thread-safety and prevent accidental modification.
/// </summary>
public sealed class AppOptions
{
    public required string Source { get; init; }

    public required Uri PdfUrl { get; init; }

    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Creates AppOptions from configuration and command-line arguments.
    /// </summary>
    
    public static AppOptions Create(AppSettings settings, string[] args, ILogger logger)
    {
        logger.Information("Loading configuration from appsettings.json and command-line arguments");

        // Parse command-line arguments into a dictionary
        // Format: --key=value
        var cmdArgs = args
            .Select(a => a.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(parts => parts[0][2..], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        // Command-line arguments override configuration file values
        var source = cmdArgs.TryGetValue("source", out var sourceValue) ? sourceValue : settings.Source;
        var urlString = cmdArgs.TryGetValue("url", out var urlValue) ? urlValue : settings.PdfUrl;
        var output = cmdArgs.TryGetValue("output", out var outputValue) ? outputValue : settings.OutputDirectory;

        logger.Information("Configuration source: {ConfigSource}",
            cmdArgs.Count > 0 ? "Command-line arguments + appsettings.json" : "appsettings.json");
        logger.Information("Source: '{Source}'", source);
        logger.Information("URL: {Url}", urlString);
        logger.Information("Output directory: {OutputDirectory}", output);

        // Validate inputs
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new FlyerDownloadException("Source cannot be empty. Set it in appsettings.json or use --source=<name>.");
        }

        if (string.IsNullOrWhiteSpace(urlString))
        {
            throw new FlyerDownloadException("PDF URL cannot be empty. Set it in appsettings.json or use --url=<url>.");
        }

        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new FlyerDownloadException($"Invalid URL: '{urlString}'. Provide an absolute HTTP/HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new FlyerDownloadException("Output directory cannot be empty. Set it in appsettings.json or use --output=<path>.");
        }

        // Sanitize source name: keep only alphanumeric, hyphens, and underscores
        var sanitizedSource = new string(source.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitizedSource))
        {
            throw new FlyerDownloadException("Source must contain at least one letter or digit after sanitization.");
        }

        if (sanitizedSource != source)
        {
            logger.Warning("Source name was sanitized from '{OriginalSource}' to '{SanitizedSource}'", source, sanitizedSource);
        }

        // Normalize to lowercase for consistency
        var normalizedSource = sanitizedSource.ToLowerInvariant();
        logger.Information("Final source identifier: '{Source}'", normalizedSource);

        return new AppOptions
        {
            Source = normalizedSource,
            PdfUrl = uri,
            OutputDirectory = output
        };
    }
}
