using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Articles.DTOs
{
    public class ArticleNoteDto
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class CreateArticleNoteRequestDto
    {
        [Required]
        public int ArticleId { get; set; }

        [Required]
        public string Note { get; set; } = string.Empty;
    }

    public class UpdateArticleNoteRequestDto
    {
        [Required]
        public string Note { get; set; } = string.Empty;
    }

    public class ArticleNoteListResponseDto
    {
        public List<ArticleNoteDto> Notes { get; set; } = new List<ArticleNoteDto>();
        public int TotalCount { get; set; }
    }
}