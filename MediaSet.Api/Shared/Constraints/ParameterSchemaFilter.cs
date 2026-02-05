using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace MediaSet.Api.Bindings;

public class ParameterSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;

        if (type == null)
        {
            return;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Parameter<>))
        {
            var enumType = type.GetGenericArguments().FirstOrDefault(x => x.IsEnum);

            if (enumType == null)
            {
                return;
            }

            var names = Enum.GetNames(enumType);

            if (names == null)
            {
                return;
            }

            if (schema is OpenApiSchema openApiSchema)
            {
                // The properties are only mutable on the concrete type
                openApiSchema.Type = JsonSchemaType.String;
                openApiSchema.Enum = names.Select(x => (JsonNode)x).ToList();
            }
        }
    }
}
