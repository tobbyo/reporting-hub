using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ReportingHub.Api.Contracts;

/// <summary>
/// Multipart form for merging Excel files.
/// </summary>
public sealed class MergeForm
{
    /// <summary>Upload one or more .xlsx files</summary>
    [Required]
    public List<IFormFile> Files { get; set; } = new();

    /// <summary>
    /// Optional JSON to control sheet naming. Example:
    /// {"mode":"pattern","pattern":"{file}_{sheet}","collision":"dedupe"}
    /// {"mode":"pattern","pattern":"{sheet}","collision":"dedupe"}
    /// </summary>
    [DefaultValue("{ \"mode\": \"pattern\", \"pattern\": \"{sheet}\", \"collision\": \"dedupe\" }")]
    public string? Names { get; set; }
}
