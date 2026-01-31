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
        // FK Relationship to Exam
        public int ExamId { get; set; }

        [ForeignKey("ExamId")]
        public virtual Exam? Exam { get; set; }

        public string? ImagePath { get; set; } // Nullable string

        // Initializing the collection avoids null reference issues and build errors
        public virtual ICollection<Choice> Choices { get; set; } = new List<Choice>();
    }
}