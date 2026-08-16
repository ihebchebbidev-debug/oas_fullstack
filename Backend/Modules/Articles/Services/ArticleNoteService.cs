using MyApi.Data;
using MyApi.Modules.Articles.DTOs;
using MyApi.Modules.Articles.Models;
using Microsoft.EntityFrameworkCore;

namespace MyApi.Modules.Articles.Services
{
    public class ArticleNoteService : IArticleNoteService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ArticleNoteService> _logger;

        public ArticleNoteService(ApplicationDbContext context, ILogger<ArticleNoteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ArticleNoteListResponseDto> GetNotesByArticleIdAsync(int articleId)
        {
            var notes = await _context.Set<ArticleNote>()
                .AsNoTracking()
                .Where(n => n.ArticleId == articleId)
                .OrderByDescending(n => n.CreatedDate)
                .ToListAsync();

            var noteDtos = notes.Select(MapToDto).ToList();
            return new ArticleNoteListResponseDto
            {
                Notes = noteDtos,
                TotalCount = noteDtos.Count
            };
        }

        public async Task<ArticleNoteDto?> GetNoteByIdAsync(int id)
        {
            var note = await _context.Set<ArticleNote>()
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == id);
            return note != null ? MapToDto(note) : null;
        }

        public async Task<ArticleNoteDto> CreateNoteAsync(CreateArticleNoteRequestDto createDto, string createdByUser)
        {
            var note = new ArticleNote
            {
                ArticleId = createDto.ArticleId,
                Note = createDto.Note,
                CreatedBy = createdByUser,
                CreatedDate = DateTime.UtcNow
            };

            _context.Set<ArticleNote>().Add(note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Article note created with ID {NoteId}", note.Id);
            return MapToDto(note);
        }

        public async Task<ArticleNoteDto?> UpdateNoteAsync(int id, UpdateArticleNoteRequestDto updateDto)
        {
            var note = await _context.Set<ArticleNote>().FirstOrDefaultAsync(n => n.Id == id);
            if (note == null) return null;

            note.Note = updateDto.Note;
            await _context.SaveChangesAsync();
            return MapToDto(note);
        }

        public async Task<bool> DeleteNoteAsync(int id)
        {
            var note = await _context.Set<ArticleNote>().FirstOrDefaultAsync(n => n.Id == id);
            if (note == null) return false;

            _context.Set<ArticleNote>().Remove(note);
            await _context.SaveChangesAsync();
            return true;
        }

        private static ArticleNoteDto MapToDto(ArticleNote note) => new ArticleNoteDto
        {
            Id = note.Id,
            ArticleId = note.ArticleId,
            Note = note.Note,
            CreatedDate = note.CreatedDate,
            CreatedBy = note.CreatedBy
        };
    }
}