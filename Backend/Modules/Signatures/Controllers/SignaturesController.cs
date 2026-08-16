using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Signatures.DTOs;
using MyApi.Modules.Signatures.Services;
using System.Security.Claims;

namespace MyApi.Modules.Signatures.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SignaturesController : ControllerBase
    {
        private readonly ISignatureService _signatureService;

        public SignaturesController(ISignatureService signatureService)
        {
            _signatureService = signatureService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }
            return userId;
        }

        /// <summary>
        /// Get current user's saved signature
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMySignature()
        {
            try
            {
                var userId = GetCurrentUserId();
                var signatureUrl = await _signatureService.GetSignatureUrlAsync(userId);

                if (signatureUrl == null)
                {
                    return Ok(new SignatureResponseDto { SignatureUrl = "" });
                }

                return Ok(new SignatureResponseDto { SignatureUrl = signatureUrl });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Save or update current user's signature
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> SaveMySignature([FromBody] SaveSignatureDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.SignatureUrl))
                {
                    return BadRequest(new { message = "Signature URL is required" });
                }

                var userId = GetCurrentUserId();
                await _signatureService.SaveSignatureAsync(userId, dto.SignatureUrl);

                return Ok(new SignatureResponseDto { SignatureUrl = dto.SignatureUrl });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete current user's signature
        /// </summary>
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMySignature()
        {
            try
            {
                var userId = GetCurrentUserId();
                var deleted = await _signatureService.DeleteSignatureAsync(userId);

                if (!deleted)
                {
                    return NotFound(new { message = "No signature found to delete" });
                }

                return Ok(new { message = "Signature deleted successfully" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
