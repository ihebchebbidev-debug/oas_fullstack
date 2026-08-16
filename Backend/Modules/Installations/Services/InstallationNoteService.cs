using MyApi.Data;
using MyApi.Modules.Installations.DTOs;
using MyApi.Modules.Installations.Models;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.Installations.Services
{
    public class InstallationNoteService : IInstallationNoteService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InstallationNoteService> _logger;

        public InstallationNoteService(ApplicationDbContext context, ILogger<InstallationNoteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<InstallationNoteListResponseDto> GetNotesByInstallationIdAsync(int installationId)
        {
            try
            {
                // Hide notes belonging to soft-deleted installations so the
                // detail/related tabs match the list view.
                var installationVisible = await _context.Installations
                    .AnyAsync(i => i.Id == installationId && !i.IsDeleted);
                if (!installationVisible)
                {
                    return new InstallationNoteListResponseDto { Notes = new List<InstallationNoteDto>(), TotalCount = 0 };
                }

                var notes = await _context.InstallationNotes
                    .Where(n => n.InstallationId == installationId)
                    .OrderByDescending(n => n.CreatedDate)
                    .ToListAsync();

                var noteDtos = notes.Select(MapToNoteDto).ToList();

                return new InstallationNoteListResponseDto
                {
                    Notes = noteDtos,
                    TotalCount = noteDtos.Count()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notes for installation {InstallationId}", installationId);
                throw;
            }
        }

        public async Task<InstallationNoteDto?> GetNoteByIdAsync(int id)
        {
            try
            {
                var note = await _context.InstallationNotes
                    .Where(n => n.Id == id)
                    .FirstOrDefaultAsync();

                if (note == null || !await ParentVisibleAsync(note.InstallationId))
                {
                    return null;
                }

                return MapToNoteDto(note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting note by id {NoteId}", id);
                throw;
            }
        }

        public async Task<InstallationNoteDto> CreateNoteAsync(CreateInstallationNoteRequestDto createDto, string createdByUser)
        {
            try
            {
                // Verify installation exists
                var installationExists = await _context.Installations
                    .AnyAsync(i => i.Id == createDto.InstallationId && !i.IsDeleted);

                if (!installationExists)
                {
                    throw new InvalidOperationException("Installation not found");
                }

                var note = new InstallationNote
                {
                    InstallationId = createDto.InstallationId,
                    Note = createDto.Note,
                    CreatedBy = createdByUser,
                    CreatedDate = DateTime.UtcNow
                };

                _context.InstallationNotes.Add(note);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Installation note created successfully with ID {NoteId}", note.Id);
                return MapToNoteDto(note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating installation note");
                throw;
            }
        }

        public async Task<InstallationNoteDto?> UpdateNoteAsync(int id, UpdateInstallationNoteRequestDto updateDto)
        {
            try
            {
                var note = await _context.InstallationNotes
                    .Where(n => n.Id == id)
                    .FirstOrDefaultAsync();

                if (note == null || !await ParentVisibleAsync(note.InstallationId))
                {
                    return null;
                }

                note.Note = updateDto.Note;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Installation note updated successfully with ID {NoteId}", id);
                return MapToNoteDto(note);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating installation note with ID {NoteId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteNoteAsync(int id)
        {
            try
            {
                var note = await _context.InstallationNotes
                    .Where(n => n.Id == id)
                    .FirstOrDefaultAsync();

                if (note == null || !await ParentVisibleAsync(note.InstallationId))
                {
                    return false;
                }

                _context.InstallationNotes.Remove(note);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Installation note deleted successfully with ID {NoteId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting installation note with ID {NoteId}", id);
                throw;
            }
        }

        private static InstallationNoteDto MapToNoteDto(InstallationNote note)
        {
            return new InstallationNoteDto
            {
                Id = note.Id,
                InstallationId = note.InstallationId,
                Note = note.Note,
                CreatedDate = note.CreatedDate,
                CreatedBy = note.CreatedBy
            };
        }

        // Notes of soft-deleted installations must stay invisible/immutable,
        // matching the list view and create guard.
        private Task<bool> ParentVisibleAsync(int installationId)
        {
            return _context.Installations.AnyAsync(i => i.Id == installationId && !i.IsDeleted);
        }
    }
}
