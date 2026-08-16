using System.Text.Json.Serialization;

namespace MyApi.Modules.Auth.DTOs
{
    /// <summary>
    /// Dedicated DTO for profile picture URL updates.
    /// Kept minimal to avoid issues with general update DTOs.
    /// </summary>
    public class UpdateProfilePictureRequestDto
    {
        [JsonPropertyName("profilePictureUrl")]
        public string? ProfilePictureUrl { get; set; }
    }
}
