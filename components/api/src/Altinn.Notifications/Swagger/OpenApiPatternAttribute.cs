using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Swagger;

/// <summary>
/// A non-validating attribute that communicates the pattern constraint for a OpenAPI schema property
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Property)]
public class OpenApiPatternAttribute(string pattern) : Attribute
{
    /// <summary>
    /// The regex pattern of the property
    /// </summary>
    public string Pattern { get; } = pattern;
}
