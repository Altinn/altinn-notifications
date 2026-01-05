using System.ComponentModel;

using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Notifications.Swagger;

/// <summary>
/// Schema filter to properly handle default values in Swagger.
/// </summary>
public class SwaggerDefaultValues : ISchemaFilter
{
    /// <summary>
    /// Applies default value handling to the schema
    /// </summary>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.MemberInfo == null)
        {
            return;
        }

        var defaultValue = context.MemberInfo
            .GetCustomAttributes(typeof(DefaultValueAttribute), true)
            .OfType<DefaultValueAttribute>()
            .FirstOrDefault();

        if (defaultValue == null || schema.Default != null)
        {
            return;
        }

        // Cast to concrete OpenApiSchema to access the settable Default property
        if (schema is not OpenApiSchema openApiSchema)
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
}
