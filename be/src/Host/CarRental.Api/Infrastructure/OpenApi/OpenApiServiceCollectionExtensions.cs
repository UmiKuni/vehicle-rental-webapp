using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace CarRental.Api.Infrastructure.OpenApi;

internal static class OpenApiServiceCollectionExtensions
{
    public static IServiceCollection AddCarRentalOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<CarRentalOpenApiDocumentTransformer>();
            options.AddOperationTransformer<BearerSecurityOperationTransformer>();
        });

        return services;
    }

    private sealed class CarRentalOpenApiDocumentTransformer : IOpenApiDocumentTransformer
    {
        private const int MaxExampleDepth = 5;

        private static readonly IReadOnlyDictionary<string, Func<IOpenApiAny>> CuratedExamples =
            new Dictionary<string, Func<IOpenApiAny>>(StringComparer.Ordinal)
            {
                ["LoginUserDto"] = static () => new OpenApiObject
                {
                    ["identifier"] = new OpenApiString("customer.seed@example.com"),
                    ["password"] = new OpenApiString("Password123!")
                },
                ["RegisterUserDto"] = static () => new OpenApiObject
                {
                    ["email"] = new OpenApiString("new.customer@example.com"),
                    ["phone"] = new OpenApiString("0901234567"),
                    ["password"] = new OpenApiString("Password123!")
                },
                ["RefreshTokenDto"] = static () => new OpenApiObject
                {
                    ["refreshToken"] = new OpenApiString("paste-refresh-token-here")
                }
            };

        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            document.Servers = new List<OpenApiServer>
            {
                new() { Url = "http://localhost:8080", Description = "Local Docker API" },
                new() { Url = "/", Description = "Relative Origin" }
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
            document.Components.Schemas ??= new Dictionary<string, OpenApiSchema>();

            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Paste the JWT accessToken returned by POST /api/auth/login (without the 'Bearer ' prefix)."
            };

            foreach (var (schemaId, exampleFactory) in CuratedExamples)
            {
                if (document.Components.Schemas.TryGetValue(schemaId, out var componentSchema) &&
                    componentSchema.Example is null)
                {
                    componentSchema.Example = exampleFactory();
                }
            }

            foreach (var pathItem in document.Paths.Values)
            {
                foreach (var operation in pathItem.Operations.Values)
                {
                    if (operation.RequestBody?.Content is null)
                        continue;

                    foreach (var (contentType, mediaType) in operation.RequestBody.Content)
                    {
                        if (!IsJsonMediaType(contentType) || mediaType.Example is not null || mediaType.Schema is null)
                            continue;

                        var example = BuildExample(
                            mediaType.Schema,
                            document.Components.Schemas,
                            new HashSet<string>(StringComparer.Ordinal),
                            propertyName: null,
                            depth: 0);

                        if (example is not null)
                        {
                            mediaType.Example = example;
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        private static bool IsJsonMediaType(string contentType)
        {
            var mediaTypeOnly = contentType.Split(';', 2)[0].Trim();
            return mediaTypeOnly.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                   mediaTypeOnly.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        }

        private static IOpenApiAny? BuildExample(
            OpenApiSchema schema,
            IDictionary<string, OpenApiSchema>? schemas,
            HashSet<string> visited,
            string? propertyName,
            int depth)
        {
            if (depth > MaxExampleDepth)
                return null;

            var schemaId = GetSchemaId(schema);
            if (!string.IsNullOrWhiteSpace(schemaId))
            {
                if (CuratedExamples.TryGetValue(schemaId, out var curatedFactory))
                    return curatedFactory();

                if (!visited.Add(schemaId))
                    return null;
            }

            try
            {
                var resolved = ResolveSchema(schema, schemaId, schemas);

                if (resolved.Example is not null)
                    return resolved.Example;

                if (resolved.Default is not null)
                    return resolved.Default;

                if (resolved.Enum is { Count: > 0 })
                    return resolved.Enum[0];

                if (resolved.AllOf is { Count: > 0 })
                {
                    return BuildAllOfExample(resolved, schemas, visited, propertyName, depth);
                }

                var variant = SelectNonNullVariant(resolved.OneOf) ?? SelectNonNullVariant(resolved.AnyOf);
                if (variant is not null)
                {
                    return BuildExample(variant, schemas, visited, propertyName, depth + 1);
                }

                if (string.Equals(resolved.Type, "object", StringComparison.OrdinalIgnoreCase) ||
                    resolved.Properties.Count > 0)
                {
                    var obj = new OpenApiObject();
                    foreach (var (propName, propSchema) in resolved.Properties)
                    {
                        var propExample = BuildExample(propSchema, schemas, visited, propName, depth + 1);
                        if (propExample is not null)
                        {
                            obj[propName] = propExample;
                        }
                    }

                    return obj;
                }

                if (string.Equals(resolved.Type, "array", StringComparison.OrdinalIgnoreCase) &&
                    resolved.Items is not null)
                {
                    var array = new OpenApiArray();
                    var itemExample = BuildExample(resolved.Items, schemas, visited, propertyName, depth + 1);
                    if (itemExample is not null)
                    {
                        array.Add(itemExample);
                    }

                    return array;
                }

                return BuildPrimitiveExample(resolved, propertyName);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(schemaId))
                {
                    visited.Remove(schemaId);
                }
            }
        }

        private static IOpenApiAny? BuildAllOfExample(
            OpenApiSchema schema,
            IDictionary<string, OpenApiSchema>? schemas,
            HashSet<string> visited,
            string? propertyName,
            int depth)
        {
            if (schema.AllOf.Count == 1 && schema.Properties.Count == 0)
            {
                return BuildExample(schema.AllOf[0], schemas, visited, propertyName, depth + 1);
            }

            var merged = new OpenApiObject();
            foreach (var part in schema.AllOf)
            {
                if (BuildExample(part, schemas, visited, propertyName, depth + 1) is OpenApiObject partObj)
                {
                    foreach (var (key, value) in partObj)
                    {
                        merged[key] = value;
                    }
                }
            }

            foreach (var (propName, propSchema) in schema.Properties)
            {
                var propExample = BuildExample(propSchema, schemas, visited, propName, depth + 1);
                if (propExample is not null)
                {
                    merged[propName] = propExample;
                }
            }

            return merged.Count > 0 ? merged : null;
        }

        private static OpenApiSchema ResolveSchema(
            OpenApiSchema schema,
            string? schemaId,
            IDictionary<string, OpenApiSchema>? schemas)
        {
            if (schemas is not null &&
                !string.IsNullOrWhiteSpace(schemaId) &&
                schema.Properties.Count == 0 &&
                schema.AllOf.Count == 0 &&
                schema.OneOf.Count == 0 &&
                schema.AnyOf.Count == 0 &&
                schema.Items is null &&
                string.IsNullOrEmpty(schema.Type) &&
                schemas.TryGetValue(schemaId, out var referenced))
            {
                return referenced;
            }

            return schema;
        }

        private static string? GetSchemaId(OpenApiSchema schema)
        {
            if (!string.IsNullOrWhiteSpace(schema.Reference?.Id))
                return schema.Reference.Id;

            if (schema.Annotations is not null &&
                schema.Annotations.TryGetValue("x-schema-id", out var rawSchemaId))
            {
                return rawSchemaId switch
                {
                    string id => id,
                    OpenApiString openApiString => openApiString.Value,
                    _ => rawSchemaId?.ToString()
                };
            }

            return null;
        }

        private static OpenApiSchema? SelectNonNullVariant(IList<OpenApiSchema>? variants)
        {
            if (variants is null || variants.Count == 0)
                return null;

            return variants.FirstOrDefault(v => !string.Equals(v.Type, "null", StringComparison.OrdinalIgnoreCase))
                ?? variants[0];
        }

        private static IOpenApiAny? BuildPrimitiveExample(OpenApiSchema schema, string? propertyName)
        {
            var type = schema.Type?.ToLowerInvariant();
            return type switch
            {
                "boolean" => new OpenApiBoolean(false),
                "integer" => new OpenApiInteger(1),
                "number" => new OpenApiDouble(IsMoneyProperty(propertyName) ? 100000 : 1),
                "string" => BuildStringExample(schema.Format, propertyName),
                _ => null
            };
        }

        private static IOpenApiAny BuildStringExample(string? format, string? propertyName)
        {
            if (string.Equals(format, "uuid", StringComparison.OrdinalIgnoreCase))
                return new OpenApiString("00000000-0000-0000-0000-000000000000");

            if (string.Equals(format, "date-time", StringComparison.OrdinalIgnoreCase))
                return new OpenApiString("2026-12-01T09:00:00Z");

            if (string.Equals(format, "date", StringComparison.OrdinalIgnoreCase))
                return new OpenApiString("2026-12-01");

            if (string.Equals(format, "email", StringComparison.OrdinalIgnoreCase) ||
                ContainsIgnoreCase(propertyName, "email"))
            {
                return new OpenApiString("customer.seed@example.com");
            }

            if (ContainsIgnoreCase(propertyName, "password"))
                return new OpenApiString("Password123!");

            if (ContainsIgnoreCase(propertyName, "phone"))
                return new OpenApiString("0901234567");

            if (ContainsIgnoreCase(propertyName, "url"))
                return new OpenApiString("https://example.com");

            return new OpenApiString("string");
        }

        private static bool IsMoneyProperty(string? propertyName) =>
            ContainsIgnoreCase(propertyName, "price") ||
            ContainsIgnoreCase(propertyName, "rate") ||
            ContainsIgnoreCase(propertyName, "amount") ||
            ContainsIgnoreCase(propertyName, "fee") ||
            ContainsIgnoreCase(propertyName, "deposit");

        private static bool ContainsIgnoreCase(string? value, string needle) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class BearerSecurityOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var hasAuthorize = metadata.OfType<IAuthorizeData>().Any();
            var hasAllowAnonymous = metadata.OfType<IAllowAnonymous>().Any();

            if (!hasAuthorize || hasAllowAnonymous)
                return Task.CompletedTask;

            operation.Security ??= [];
            var hasBearerRequirement = operation.Security.Any(requirement =>
                requirement.Keys.Any(scheme =>
                    string.Equals(scheme.Reference?.Id, "Bearer", StringComparison.Ordinal)));

            if (!hasBearerRequirement)
            {
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = new List<string>()
                });
            }

            return Task.CompletedTask;
        }
    }
}
