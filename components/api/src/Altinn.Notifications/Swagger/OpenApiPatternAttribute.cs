using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Swagger;

/// <summary>
/// A non-validating attribute that communicates a pattern constraint  
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Property)]
public class OpenApiPatternAttribute(string pattern) : Attribute
{
    /// <summary>
    /// The property's pattern attribute
    /// </summary>
    public string Pattern { get; } = pattern;
}
