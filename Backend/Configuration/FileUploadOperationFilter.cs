using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MyApi.Configuration
{
    /// <summary>
    /// Converts controller action parameters that come from form-data (including IFormFile) into
    /// a multipart/form-data request body in the generated OpenAPI document.
    /// </summary>
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            try
            {
                var formParameters = context.MethodInfo.GetParameters()
                    .Where(p => p.GetCustomAttributes().Any(a => a.GetType().Name == "FromFormAttribute")
                                || p.ParameterType == typeof(IFormFile)
                                || (p.ParameterType.IsGenericType && p.ParameterType.GetGenericArguments().Any(t => t == typeof(IFormFile))))
                    .ToArray();

                if (!formParameters.Any()) return;

                // Build schema properties for multipart/form-data
                var properties = new Dictionary<string, OpenApiSchema>();
                var required = new List<string>();

                foreach (var param in formParameters)
                {
                    var name = param.Name ?? "file";
                    var paramType = param.ParameterType;

                    // If the parameter itself is IFormFile or a collection of IFormFile, map directly
                    if (paramType == typeof(IFormFile))
                    {
                        properties[name] = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary"
                        };
                    }
                    else if (paramType.IsGenericType && paramType.GetGenericArguments().Any(t => t == typeof(IFormFile)))
                    {
                        properties[name] = new OpenApiSchema
                        {
                            Type = "array",
                            Items = new OpenApiSchema { Type = "string", Format = "binary" }
                        };
                    }
                    else if (paramType.IsClass && paramType != typeof(string))
                    {
                        // Complex type (DTO) marked with [FromForm] -> inspect its public properties
                        var props = paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in props)
                        {
                            var propName = prop.Name;
                            var propType = prop.PropertyType;

                            if (propType == typeof(IFormFile))
                            {
                                properties[propName] = new OpenApiSchema { Type = "string", Format = "binary" };
                            }
                            else if (propType.IsGenericType && propType.GetGenericArguments().Any(t => t == typeof(IFormFile)))
                            {
                                properties[propName] = new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "string", Format = "binary" } };
                            }
                            else
                            {
                                properties[propName] = MapClrTypeToOpenApiSchema(propType);
                            }

                            if (propType.IsValueType && Nullable.GetUnderlyingType(propType) == null)
                            {
                                required.Add(propName);
                            }
                        }
                    }
                    else
                    {
                        // Map common CLR types to simple OpenAPI types
                        properties[name] = MapClrTypeToOpenApiSchema(paramType);

                        // Consider value types (non-nullable) as required
                        if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null)
                        {
                            required.Add(name);
                        }
                    }
                }

                // Remove the parameters that were previously added to operation.Parameters
                if (operation.Parameters != null && operation.Parameters.Any())
                {
                    var propertyNames = properties.Keys.Select(k => k.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    operation.Parameters = operation.Parameters
                        .Where(p => !propertyNames.Contains(p.Name))
                        .ToList();
                }

                operation.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["multipart/form-data"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = properties,
                                Required = required.Any() ? new SortedSet<string>(required) : null
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                // Don't break generation; log to console for diagnostics
                Console.WriteLine($"[Swagger] FileUploadOperationFilter error: {ex.Message}");
            }
        }

        private OpenApiSchema MapClrTypeToOpenApiSchema(Type clrType)
        {
            var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (underlying == typeof(string)) return new OpenApiSchema { Type = "string" };
            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short)) return new OpenApiSchema { Type = "integer", Format = "int32" };
            if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal)) return new OpenApiSchema { Type = "number", Format = "double" };
            if (underlying == typeof(bool)) return new OpenApiSchema { Type = "boolean" };
            if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)) return new OpenApiSchema { Type = "string", Format = "date-time" };

            // Fallback to string
            return new OpenApiSchema { Type = "string" };
        }
    }
}
