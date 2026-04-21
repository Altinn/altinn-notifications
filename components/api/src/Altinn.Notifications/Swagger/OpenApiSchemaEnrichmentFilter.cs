using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Notifications.Swagger;

/// <summary>
/// Schema filter that enriches OpenAPI schemas with pattern constraints and default values.
/// </summary>
[ExcludeFromCodeCoverage]
public class OpenApiSchemaEnrichmentFilter : ISchemaFilter
{
    /// <summary>
    /// Applies pattern constraints and default value handling to the schema.
    /// </summary>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.MemberInfo == null)
        {
            return;
        }

        // Cast to concrete OpenApiSchema to access settable properties
        if (schema is not OpenApiSchema openApiSchema)
        {
            return;
        }

        var patternAttr = GetAttribute<OpenApiPatternAttribute>(context);

        if (patternAttr != null)
        {
            openApiSchema.Pattern = patternAttr.Pattern;
        }

        var defaultValue = GetAttribute<DefaultValueAttribute>(context);

        if (defaultValue == null || schema.Default != null)
        {
            return;
        }

        if (defaultValue.Value is Enum)
        {
            openApiSchema.Default = Convert.ToString(defaultValue.Value);
        }
        else if (defaultValue.Value is int intValue)
        {
            openApiSchema.Default = intValue;
        }
        else if (defaultValue.Value is bool boolValue)
        {
            openApiSchema.Default = boolValue;
        }
        else if (defaultValue.Value is string stringValue)
        {
            openApiSchema.Default = stringValue;
        }
    }

    private static T? GetAttribute<T>(SchemaFilterContext context)
    {
        return context.MemberInfo.GetCustomAttributes(typeof(T), true)
            .OfType<T>()
            .FirstOrDefault();
    }
}
