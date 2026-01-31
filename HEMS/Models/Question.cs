using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HEMS.Models
{
    public class Question
    {
        [Key]
        public int QuestionId { get; set; }

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        public decimal MarkWeight { get; set; } = 1.0m;

        // FK → Exam
        public int ExamId { get; set; }

        [ForeignKey(nameof(ExamId))]
        public virtual Exam? Exam { get; set; }

        public string? ImagePath { get; set; }

        // Initialized to avoid null reference issues
        public virtual ICollection<Choice> Choices { get; set; } = new List<Choice>();
    }
}
