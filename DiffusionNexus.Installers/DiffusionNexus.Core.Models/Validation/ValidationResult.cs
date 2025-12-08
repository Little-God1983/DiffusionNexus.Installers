namespace DiffusionNexus.Core.Models.Validation;

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
