using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyApi.Infrastructure
{
    /// <summary>
    /// Simple API-key gate. Reads X-Api-Key and compares (constant-time) against
    /// the value of an environment variable. 401s on mismatch/missing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ApiKeyAuthAttribute : Attribute, IAuthorizationFilter
    {
        public const string HeaderName = "X-Api-Key";
        private readonly string _envVarName;

        public ApiKeyAuthAttribute(string envVarName = "PUBLIC_TICKETS_API_KEY")
        {
            _envVarName = envVarName;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var expected = Environment.GetEnvironmentVariable(_envVarName);
            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Result = new ObjectResult(new { error = "API key is not configured on the server." })
                {
                    StatusCode = 503
                };
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) ||
                string.IsNullOrWhiteSpace(provided))
            {
                Reject(context, "Missing X-Api-Key header.");
                return;
            }

            if (!FixedTimeEquals(provided.ToString(), expected))
            {
                Reject(context, "Invalid API key.");
                return;
            }
        }

        private static void Reject(AuthorizationFilterContext context, string message)
        {
            var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var logger = context.HttpContext.RequestServices
                .GetService(typeof(ILogger<ApiKeyAuthAttribute>)) as ILogger<ApiKeyAuthAttribute>;
            logger?.LogWarning("Public API 401 from {Ip}: {Message}", ip, message);
            context.Result = new UnauthorizedObjectResult(new { error = message });
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var ba = Encoding.UTF8.GetBytes(a);
            var bb = Encoding.UTF8.GetBytes(b);
            if (ba.Length != bb.Length) return false;
            return CryptographicOperations.FixedTimeEquals(ba, bb);
        }
    }
}
