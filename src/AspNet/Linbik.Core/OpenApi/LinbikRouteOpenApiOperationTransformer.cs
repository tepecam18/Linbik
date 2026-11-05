using Linbik.Core.Attributes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.OpenApi;

namespace Linbik.Core.OpenApi;

/// <summary>Preserves the type of string-bound slice path values described by route constraints.</summary>
public sealed class LinbikRouteOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<LinbikRoutePatternMetadata>().LastOrDefault();
        if (metadata is null) return Task.CompletedTask;
        var pattern = RoutePatternFactory.Parse(metadata.Pattern);
        foreach (var parameter in operation.Parameters ?? [])
        {
            if (parameter.In != ParameterLocation.Path || parameter.Schema is not OpenApiSchema schema)
                continue;
            var route = pattern.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
            foreach (var policy in route?.ParameterPolicies ?? [])
            {
                var type = ConstraintType(policy.Content);
                if (type is null) continue;
                // ASP.NET already infers some numeric constraints, but maps decimal
                // constraints to double even when the delegate binds a string.
                if ((schema.Type == JsonSchemaType.String && string.IsNullOrEmpty(schema.Format)) ||
                    (type.Value.Format == "decimal" && schema.Type == JsonSchemaType.Number && schema.Format == "double"))
                    (schema.Type, schema.Format) = type.Value;
                break;
            }
        }
        return Task.CompletedTask;
    }

    private static (JsonSchemaType Type, string? Format)? ConstraintType(string? constraint) =>
        constraint?.ToLowerInvariant() switch
        {
            "guid" => (JsonSchemaType.String, "uuid"),
            "int" => (JsonSchemaType.Integer, "int32"),
            "long" => (JsonSchemaType.Integer, "int64"),
            "bool" => (JsonSchemaType.Boolean, null),
            "decimal" => (JsonSchemaType.Number, "decimal"),
            "double" => (JsonSchemaType.Number, "double"),
            "float" => (JsonSchemaType.Number, "float"),
            "datetime" => (JsonSchemaType.String, "date-time"),
            _ => null
        };
}
