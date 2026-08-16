using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MyApi.Configuration
{
    public static class TokenHelper
    {
        public static string GenerateDevelopmentToken(IConfiguration configuration)
        {
            var jwtKey = configuration["Jwt:Key"] ?? "YourSuperSecretKeyHere12345";
            var issuer = configuration["Jwt:Issuer"] ?? "MyApi";
            var audience = configuration["Jwt:Audience"] ?? "MyApiClients";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("UserId", "1"),
                new Claim(ClaimTypes.Email, "dev@flowservice.com"),
                new Claim(ClaimTypes.Name, "Development User"),
                new Claim("role", "admin"),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24), // 24 hour expiry for dev token
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string GeneratePermanentTestToken(IConfiguration configuration, string userId = "999", string email = "test@flowservice.com")
        {
            var jwtKey = configuration["Jwt:Key"] ?? "YourSuperSecretKeyHere12345";
            var issuer = configuration["Jwt:Issuer"] ?? "MyApi";
            var audience = configuration["Jwt:Audience"] ?? "MyApiClients";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("UserId", userId),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim("role", "admin"),
                new Claim("test_user", "true"),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddYears(1), // 1 year expiry for permanent test token
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
