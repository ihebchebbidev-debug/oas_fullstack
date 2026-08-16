using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Reflection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MyApi.Configuration
{
    public static class SwaggerConfiguration
    {
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddEndpointsApiExplorer();
            
            services.AddSwaggerGen(options =>
            {
                try
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "Flow Service API",
                        Version = "v1",
                        Description = "Comprehensive API for managing sales, offers, installations, and service orders",
                        Contact = new OpenApiContact
                        {
                            Name = "Flow Service Support",
                            Email = "support@flowservice.com"
                        }
                    });

                    // Ensure schema IDs are unique across namespaces (avoid duplicate short names like "LocationDto")
                    options.CustomSchemaIds(type =>
                    {
                        // Prefer full name (namespace + type) to avoid collisions, but keep it readable
                        var full = type.FullName ?? type.Name;
                        // Replace invalid characters for schema Ids
                        return full.Replace("+", ".");
                    });

                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "JWT Authorization header using the Bearer scheme"
                    });

                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            new string[] { }
                        }
                    });

                    options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

                    try
                    {
                        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                        if (File.Exists(xmlPath))
                        {
                            options.IncludeXmlComments(xmlPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Swagger] Warning: Could not load XML comments: {ex.Message}");
                    }

                    options.OperationFilter<SwaggerOperationFilter>();
                    // Handle file uploads and form-data parameters (IFormFile / [FromForm])
                    options.OperationFilter<FileUploadOperationFilter>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Swagger] Error during configuration: {ex.Message}");
                    throw;
                }
            });

            return services;
        }

        public static WebApplication UseSwaggerDocumentation(this WebApplication app, IConfiguration configuration)
        {
            try
            {
                app.UseSwagger(options =>
                {
                    options.RouteTemplate = "swagger/{documentName}/swagger.json";
                });

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Flow Service API v1");
                    options.RoutePrefix = "swagger";
                    options.DefaultModelsExpandDepth(2);
                    options.DefaultModelExpandDepth(2);
                    options.EnableDeepLinking();
                });

                app.Logger.LogInformation("[Swagger] Swagger UI configured successfully");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "[Swagger] Error configuring Swagger UI: {Message}", ex.Message);
            }

            return app;
        }
    }

    public class SwaggerOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            try
            {
                // Add any custom operation logic here
                if (operation.Parameters == null)
                    operation.Parameters = new List<OpenApiParameter>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Swagger] Error in operation filter: {ex.Message}");
            }
        }
    }
}
