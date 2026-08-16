using MyApi.Modules.Contacts.DTOs;
using MyApi.Modules.Contacts.Services;
using MyApi.Modules.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MyApi.Modules.Contacts.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ContactsController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly ISystemLogService _systemLogService;
        private readonly ILogger<ContactsController> _logger;

        public ContactsController(
            IContactService contactService, 
            ISystemLogService systemLogService,
            ILogger<ContactsController> logger)
        {
            _contactService = contactService;
            _systemLogService = systemLogService;
            _logger = logger;
        }

        /// <summary>
        /// Get all contacts with optional filtering and pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ContactListResponseDto>> GetAllContacts([FromQuery] ContactSearchRequestDto? searchRequest = null)
        {
            try
            {
                var result = await _contactService.GetAllContactsAsync(searchRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all contacts");
                await _systemLogService.LogErrorAsync("Failed to retrieve contacts", "Contacts", "read", GetCurrentUserId(), GetCurrentUser(), details: ex.Message);
                return StatusCode(500, "An error occurred while retrieving contacts");
            }
        }

        /// <summary>
        /// Get contact by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ContactResponseDto>> GetContact(int id)
        {
            try
            {
                var contact = await _contactService.GetContactByIdAsync(id);
                
                if (contact == null)
                {
                    return NotFound($"Contact with ID {id} not found");
                }

                return Ok(contact);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contact with ID {ContactId}", id);
                await _systemLogService.LogErrorAsync($"Failed to retrieve contact {id}", "Contacts", "read", GetCurrentUserId(), GetCurrentUser(), "Contact", id.ToString(), ex.Message);
                return StatusCode(500, "An error occurred while retrieving the contact");
            }
        }

        /// <summary>
        /// Create a new contact
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ContactResponseDto>> CreateContact([FromBody] CreateContactRequestDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var contact = await _contactService.CreateContactAsync(createDto, currentUser);

                await _systemLogService.LogSuccessAsync($"Contact created: {createDto.FirstName} {createDto.LastName}", "Contacts", "create", GetCurrentUserId(), currentUser, "Contact", contact.Id.ToString());

                return CreatedAtAction(nameof(GetContact), new { id = contact.Id }, contact);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while creating contact");
                await _systemLogService.LogWarningAsync($"Failed to create contact: {ex.Message}", "Contacts", "create", GetCurrentUserId(), GetCurrentUser(), "Contact", details: ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating contact");
                await _systemLogService.LogErrorAsync("Failed to create contact", "Contacts", "create", GetCurrentUserId(), GetCurrentUser(), "Contact", details: ex.Message);
                return StatusCode(500, "An error occurred while creating the contact");
            }
        }

        /// <summary>
        /// Update an existing contact
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ContactResponseDto>> UpdateContact(int id, [FromBody] UpdateContactRequestDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var contact = await _contactService.UpdateContactAsync(id, updateDto, currentUser);

                if (contact == null)
                {
                    return NotFound($"Contact with ID {id} not found");
                }

                await _systemLogService.LogSuccessAsync($"Contact updated: {contact.FirstName} {contact.LastName}", "Contacts", "update", GetCurrentUserId(), currentUser, "Contact", id.ToString());

                return Ok(contact);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while updating contact with ID {ContactId}", id);
                await _systemLogService.LogWarningAsync($"Failed to update contact {id}: {ex.Message}", "Contacts", "update", GetCurrentUserId(), GetCurrentUser(), "Contact", id.ToString(), ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact with ID {ContactId}", id);
                await _systemLogService.LogErrorAsync($"Failed to update contact {id}", "Contacts", "update", GetCurrentUserId(), GetCurrentUser(), "Contact", id.ToString(), ex.Message);
                return StatusCode(500, "An error occurred while updating the contact");
            }
        }

        /// <summary>
        /// Delete a contact (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteContact(int id)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var success = await _contactService.DeleteContactAsync(id, currentUser);

                if (!success)
                {
                    return NotFound($"Contact with ID {id} not found");
                }

                await _systemLogService.LogSuccessAsync($"Contact deleted: ID {id}", "Contacts", "delete", GetCurrentUserId(), currentUser, "Contact", id.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact with ID {ContactId}", id);
                await _systemLogService.LogErrorAsync($"Failed to delete contact {id}", "Contacts", "delete", GetCurrentUserId(), GetCurrentUser(), "Contact", id.ToString(), ex.Message);
                return StatusCode(500, "An error occurred while deleting the contact");
            }
        }

        /// <summary>
        /// Search contacts
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<ContactListResponseDto>> SearchContacts(
            [FromQuery] string searchTerm,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var result = await _contactService.SearchContactsAsync(searchTerm, pageNumber, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching contacts with term {SearchTerm}", searchTerm);
                return StatusCode(500, "An error occurred while searching contacts");
            }
        }

        /// <summary>
        /// Check if contact exists by email
        /// </summary>
        [HttpGet("exists/{email}")]
        public async Task<ActionResult<bool>> ContactExists(string email)
        {
            try
            {
                var exists = await _contactService.ContactExistsAsync(email);
                return Ok(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if contact exists with email {Email}", email);
                return StatusCode(500, "An error occurred while checking contact existence");
            }
        }

        /// <summary>
        /// Bulk import contacts
        /// </summary>
        [HttpPost("import")]
        public async Task<ActionResult<BulkImportResultDto>> BulkImportContacts([FromBody] BulkImportContactRequestDto importRequest)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUser = GetCurrentUser();
                var result = await _contactService.BulkImportContactsAsync(importRequest, currentUser);

                await _systemLogService.LogSuccessAsync($"Bulk imported {result.SuccessCount} contacts ({result.FailedCount} failures)", "Contacts", "import", GetCurrentUserId(), currentUser, "Contact");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk import of contacts");
                await _systemLogService.LogErrorAsync("Bulk import failed", "Contacts", "import", GetCurrentUserId(), GetCurrentUser(), "Contact", details: ex.Message);
                return StatusCode(500, "An error occurred during bulk import");
            }
        }

        /// <summary>
        /// Assign tag to contact
        /// </summary>
        [HttpPost("{contactId}/tags/{tagId}")]
        public async Task<ActionResult> AssignTagToContact(int contactId, int tagId)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var success = await _contactService.AssignTagToContactAsync(contactId, tagId, currentUser);

                if (!success)
                {
                    return BadRequest("Failed to assign tag to contact");
                }

                await _systemLogService.LogSuccessAsync($"Tag {tagId} assigned to contact {contactId}", "Contacts", "update", GetCurrentUserId(), currentUser, "Contact", contactId.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning tag {TagId} to contact {ContactId}", tagId, contactId);
                await _systemLogService.LogErrorAsync($"Failed to assign tag {tagId} to contact {contactId}", "Contacts", "update", GetCurrentUserId(), GetCurrentUser(), "Contact", contactId.ToString(), ex.Message);
                return StatusCode(500, "An error occurred while assigning the tag");
            }
        }

        /// <summary>
        /// Remove tag from contact
        /// </summary>
        [HttpDelete("{contactId}/tags/{tagId}")]
        public async Task<ActionResult> RemoveTagFromContact(int contactId, int tagId)
        {
            try
            {
                var success = await _contactService.RemoveTagFromContactAsync(contactId, tagId);

                if (!success)
                {
                    return NotFound("Tag assignment not found");
                }

                await _systemLogService.LogSuccessAsync($"Tag {tagId} removed from contact {contactId}", "Contacts", "update", GetCurrentUserId(), GetCurrentUser(), "Contact", contactId.ToString());

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing tag {TagId} from contact {ContactId}", tagId, contactId);
                await _systemLogService.LogErrorAsync($"Failed to remove tag {tagId} from contact {contactId}", "Contacts", "update", GetCurrentUserId(), GetCurrentUser(), "Contact", contactId.ToString(), ex.Message);
                return StatusCode(500, "An error occurred while removing the tag");
            }
        }

        /// <summary>
        /// Assign a user group to a contact (idempotent)
        /// </summary>
        [HttpPost("{contactId}/user-groups/{groupId}")]
        public async Task<ActionResult> AssignUserGroupToContact(int contactId, int groupId)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var success = await _contactService.AssignUserGroupToContactAsync(contactId, groupId, currentUser);

                if (!success)
                {
                    return NotFound("Contact or user group not found");
                }

                var contact = await _contactService.GetContactByIdAsync(contactId);
                return Ok(new { userGroups = contact?.UserGroups ?? new List<MyApi.Modules.Contacts.DTOs.ContactUserGroupDto>() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning user group {GroupId} to contact {ContactId}", groupId, contactId);
                return StatusCode(500, "An error occurred while assigning the user group");
            }
        }

        /// <summary>
        /// Remove a user group from a contact (idempotent)
        /// </summary>
        [HttpDelete("{contactId}/user-groups/{groupId}")]
        public async Task<ActionResult> RemoveUserGroupFromContact(int contactId, int groupId)
        {
            try
            {
                await _contactService.RemoveUserGroupFromContactAsync(contactId, groupId);

                var contact = await _contactService.GetContactByIdAsync(contactId);
                return Ok(new { userGroups = contact?.UserGroups ?? new List<MyApi.Modules.Contacts.DTOs.ContactUserGroupDto>() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user group {GroupId} from contact {ContactId}", groupId, contactId);
                return StatusCode(500, "An error occurred while removing the user group");
            }
        }

        private string GetCurrentUser()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? 
                   User.FindFirst(ClaimTypes.Name)?.Value ?? 
                   User.FindFirst("email")?.Value ?? 
                   "system";
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
