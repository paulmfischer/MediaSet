using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using MiniValidation;

namespace MediaSet.Api.Filters;

public static class ValidationFilterExtensions
{
    public static TBuilder WithPrameterValidation<TBuilder>(this TBuilder builder, params Type[] typesToValidate) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(eb =>
        {
            var methodInfo = eb.Metadata.OfType<MethodInfo>().FirstOrDefault();

            if (methodInfo is null)
            {
                return;
            }

            // track the indices of validatable parameters
            List<int>? parameterIndexesToValidate = null;
            foreach (var param in methodInfo.GetParameters())
            {
                if (typesToValidate.Contains(param.ParameterType))
                {
                    parameterIndexesToValidate ??= new();
                    parameterIndexesToValidate.Add(param.Position);
                }
            }

            // nothing to validate so don't add filter to endpoint
            if (parameterIndexesToValidate is null)
            {
                return;
            }

            // respond with problem details if validation error
            eb.Metadata.Add(new ProducesResponseTypeMetadata(typeof(HttpValidationProblemDetails), 400, "application/problem+json"));

            eb.FilterFactories.Add((content, next) =>
            {
                return efic =>
                {
                    foreach (var index in parameterIndexesToValidate)
                    {
                        if (efic.Arguments[index] is { } arg && !MiniValidator.TryValidate(arg, out var errors))
                        {
                            return new ValueTask<object?>(Results.ValidationProblem(errors));
                        }
                    }

                    return next(efic);
                };
            });
        });

        return builder;
    }

    private sealed class ProducesResponseTypeMetadata : IProducesResponseTypeMetadata
    {
         public ProducesResponseTypeMetadata(Type type, int statusCode, string contentType)
        {
            Type = type;
            StatusCode = statusCode;
            ContentTypes = new[] { contentType };
        }

        public Type Type { get; }
        public int StatusCode { get; }
        public IEnumerable<string> ContentTypes { get; }
    }
}