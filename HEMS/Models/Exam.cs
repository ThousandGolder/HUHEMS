using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HEMS.Models
{
    public class Exam
    {
        [Key]
        public int ExamId { get; set; }

        public string ExamTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a year")]
        [Display(Name = "Academic Year")]
        [Range(2018, 2100, ErrorMessage = "Please enter a valid year between 2020 and 2100")]
        public int AcademicYear { get; set; }

        public int DurationMinutes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultMark { get; set; } = 1.0m;

        public string? Description { get; set; }

        public string ExamStatus { get; set; } = "Draft";

        // New Attribute for the auto-generated code
        [Display(Name = "Authorization Code")]
        public string? ExamCode { get; set; }

        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
        public virtual ICollection<StudentExam> StudentExams { get; set; } = new List<StudentExam>();
    }
}