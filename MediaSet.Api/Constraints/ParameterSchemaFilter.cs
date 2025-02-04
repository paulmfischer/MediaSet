using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MediaSet.Api.Bindings;

public class ParameterSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
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
            
            schema.Type = "string";
            schema.Enum = names.OfType<string>().Select(x => new OpenApiString(x)).ToList<IOpenApiAny>();
        }
    }
}