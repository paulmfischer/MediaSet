using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using MiniValidation;

namespace MediaSet.API;

public static class ValidationFilterExtensions
{
    public static TBuilder WithParameterValidation<TBuilder>(this TBuilder builder, params Type[] typesToValidate) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder =>
        {
            var methodInfo = endpointBuilder.Metadata.OfType<MethodInfo>().FirstOrDefault();

            if (methodInfo is null)
            {
                return;
            }

            // Track the indices of validatable params
            List<int>? parameterIndexesToValidate = null;
            foreach (var param in methodInfo.GetParameters())
            {
                if (typesToValidate.Contains(param.ParameterType))
                {
                    parameterIndexesToValidate ??= new();
                    parameterIndexesToValidate.Add(param.Position);
                }
            }

            // nothing to validate so don't add the filter to the endpoint.
            if (parameterIndexesToValidate is null)
            {
                return;
            }

            endpointBuilder.Metadata.Add(new ProducesResponseTypeMetadata(typeof(HttpValidationProblemDetails), 400, "application/problem+json"));

            endpointBuilder.FilterFactories.Add((context, next) => 
            {
                return (filterFactoryContext) =>
                {
                    foreach (var index in parameterIndexesToValidate)
                    {
                        if (filterFactoryContext.Arguments[index] is { } arg && !MiniValidator.TryValidate(arg, out var errors))
                        {
                            return new ValueTask<object?>(Results.ValidationProblem(errors));
                        }
                    }

                    return next(filterFactoryContext);
                };
            });
        });

        return builder;
    }

    // Equivalent to the .Produces call to add metadata to endpoints
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