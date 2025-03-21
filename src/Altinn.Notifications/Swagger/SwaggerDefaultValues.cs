using System.ComponentModel;

using Microsoft.OpenApi.Models;

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
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
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

        if (defaultValue.Value is Enum)
        {
            schema.Default = new Microsoft.OpenApi.Any.OpenApiString(Convert.ToString(defaultValue.Value));
        }
        else if (defaultValue.Value is int intValue)
        {
            schema.Default = new Microsoft.OpenApi.Any.OpenApiInteger(intValue);
        }
        else if (defaultValue.Value is bool boolValue)
        {
            schema.Default = new Microsoft.OpenApi.Any.OpenApiBoolean(boolValue);
        }
        else if (defaultValue.Value is string stringValue)
        {
            schema.Default = new Microsoft.OpenApi.Any.OpenApiString(stringValue);
        }
    }
}
