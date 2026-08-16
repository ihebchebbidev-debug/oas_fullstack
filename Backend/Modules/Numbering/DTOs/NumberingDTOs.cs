namespace MyApi.Modules.Numbering.DTOs
{
    public class NumberingSettingsDto
    {
        public int Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string Template { get; set; } = string.Empty;
        public string Strategy { get; set; } = "atomic_counter";
        public string ResetFrequency { get; set; } = "yearly";
        public int StartValue { get; set; } = 1;
        public int Padding { get; set; } = 6;
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateNumberingSettingsRequest
    {
        public bool IsEnabled { get; set; }
        public string Template { get; set; } = string.Empty;
        public string Strategy { get; set; } = "atomic_counter";
        public string ResetFrequency { get; set; } = "yearly";
        public int StartValue { get; set; } = 1;
        public int Padding { get; set; } = 6;
    }

    public class NumberingPreviewRequest
    {
        public string Entity { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
        public string Strategy { get; set; } = "atomic_counter";
        public string ResetFrequency { get; set; } = "yearly";
        public int StartValue { get; set; } = 1;
        public int Padding { get; set; } = 6;
        public int Count { get; set; } = 5;
    }

    public class NumberingResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    public class NumberingListResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<NumberingSettingsDto>? Data { get; set; }
    }

    public class NumberingPreviewResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string>? Preview { get; set; }
        public List<string>? Warnings { get; set; }
    }
}
