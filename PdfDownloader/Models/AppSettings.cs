namespace PdfDownloader.Models;

/// <summary>
/// Application settings loaded from appsettings.json.
/// These are the default values that can be overridden by command-line arguments.
/// 
/// This is a Data Transfer Object (DTO) for configuration binding.
/// Properties are mutable (set) because the configuration binder needs to populate them.
/// </summary>
public sealed class AppSettings
{
    public string Source { get; set; } = "W3C dummy PDF";
    public string PdfUrl { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
}
