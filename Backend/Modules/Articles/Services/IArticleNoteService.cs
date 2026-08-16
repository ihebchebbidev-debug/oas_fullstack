using MyApi.Modules.Articles.DTOs;

namespace MyApi.Modules.Articles.Services
{
    public interface IArticleNoteService
    {
        Task<ArticleNoteListResponseDto> GetNotesByArticleIdAsync(int articleId);
        Task<ArticleNoteDto?> GetNoteByIdAsync(int id);
        Task<ArticleNoteDto> CreateNoteAsync(CreateArticleNoteRequestDto createDto, string createdByUser);
        Task<ArticleNoteDto?> UpdateNoteAsync(int id, UpdateArticleNoteRequestDto updateDto);
        Task<bool> DeleteNoteAsync(int id);
    }
}