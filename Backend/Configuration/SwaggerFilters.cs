using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MyApi.Configuration
{
    public class SwaggerResponseExamplesFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var responseAttributes = context.MethodInfo.GetCustomAttributes<ProducesResponseTypeAttribute>();

            foreach (var attr in responseAttributes)
            {
                var statusCode = attr.StatusCode.ToString();
                if (operation.Responses.ContainsKey(statusCode))
                {
                    var response = operation.Responses[statusCode];
                    
                    // Add examples based on status codes
                    switch (attr.StatusCode)
                    {
                        case 200:
                            AddSuccessExample(response, attr.Type);
                            break;
                        case 400:
                            AddErrorExample(response, "Bad Request", "Invalid request data provided");
                            break;
                        case 401:
                            AddErrorExample(response, "Unauthorized", "Authentication required or token expired");
                            break;
                        case 403:
                            AddErrorExample(response, "Forbidden", "Access denied - insufficient permissions");
                            break;
                        case 404:
                            AddErrorExample(response, "Not Found", "The requested resource was not found");
                            break;
                        case 500:
                            AddErrorExample(response, "Internal Server Error", "An unexpected error occurred");
                            break;
                    }
                }
            }
        }

        private void AddSuccessExample(OpenApiResponse response, Type? returnType)
        {
            if (returnType == null) return;

            var example = CreateExampleForType(returnType);
            if (example != null)
            {
                response.Content ??= new Dictionary<string, OpenApiMediaType>();
                if (!response.Content.ContainsKey("application/json"))
                {
                    response.Content["application/json"] = new OpenApiMediaType();
                }
                
                response.Content["application/json"].Example = new Microsoft.OpenApi.Any.OpenApiString(
                    JsonSerializer.Serialize(example, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                    })
                );
            }
        }

        private void AddErrorExample(OpenApiResponse response, string title, string detail)
        {
            response.Content ??= new Dictionary<string, OpenApiMediaType>();
            if (!response.Content.ContainsKey("application/json"))
            {
                response.Content["application/json"] = new OpenApiMediaType();
            }

            var errorExample = new
            {
                title = title,
                detail = detail,
                status = GetStatusCodeFromTitle(title),
                timestamp = DateTime.UtcNow.ToString("O")
            };

            response.Content["application/json"].Example = new Microsoft.OpenApi.Any.OpenApiString(
                JsonSerializer.Serialize(errorExample, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                })
            );
        }

        private object? CreateExampleForType(Type type)
        {
            if (type.Name.Contains("AuthResponseDto"))
            {
                return new
                {
                    success = true,
                    message = "Operation completed successfully",
                    accessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                    refreshToken = "refresh_token_example",
                    expiresAt = DateTime.UtcNow.AddHours(1).ToString("O"),
                    user = new
                    {
                        id = 1,
                        email = "user@example.com",
                        firstName = "John",
                        lastName = "Doe",
                        company = "Example Corp",
                        isActive = true
                    }
                };
            }

            if (type.Name.Contains("ContactResponseDto"))
            {
                return new
                {
                    id = 1,
                    firstName = "Jane",
                    lastName = "Smith",
                    email = "jane.smith@example.com",
                    phone = "+1234567890",
                    company = "Tech Solutions Inc",
                    position = "Software Engineer",
                    tags = new[] { "client", "vip" },
                    createdAt = DateTime.UtcNow.AddDays(-30).ToString("O"),
                    updatedAt = DateTime.UtcNow.ToString("O")
                };
            }

            return null;
        }

        private int GetStatusCodeFromTitle(string title)
        {
            return title switch
            {
                "Bad Request" => 400,
                "Unauthorized" => 401,
                "Forbidden" => 403,
                "Not Found" => 404,
                "Internal Server Error" => 500,
                _ => 200
            };
        }
    }

    public class SwaggerSchemaExamplesFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.Name.Contains("LoginRequestDto"))
            {
                schema.Example = new Microsoft.OpenApi.Any.OpenApiString(@"{
                    ""email"": ""user@example.com"",
                    ""password"": ""SecurePassword123!""
                }");
            }
            else if (context.Type.Name.Contains("SignupRequestDto"))
            {
                schema.Example = new Microsoft.OpenApi.Any.OpenApiString(@"{
                    ""firstName"": ""John"",
                    ""lastName"": ""Doe"",
                    ""email"": ""john.doe@example.com"",
                    ""password"": ""SecurePassword123!"",
                    ""company"": ""Example Corp"",
                    ""position"": ""Software Developer"",
                    ""industry"": ""Technology"",
                    ""phone"": ""+1234567890""
                }");
            }
            else if (context.Type.Name.Contains("CreateContactRequestDto"))
            {
                schema.Example = new Microsoft.OpenApi.Any.OpenApiString(@"{
                    ""firstName"": ""Jane"",
                    ""lastName"": ""Smith"",
                    ""email"": ""jane.smith@example.com"",
                    ""phone"": ""+1234567890"",
                    ""company"": ""Tech Solutions Inc"",
                    ""position"": ""Software Engineer"",
                    ""notes"": ""Important client contact""
                }");
            }
        }
    }
}
