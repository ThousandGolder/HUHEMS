using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HEMS.Models
{
    public class StudentExam
    {
        [Key]
        public int StudentToExamId { get; set; }

        public int StudentId { get; set; }
        public int ExamId { get; set; }

        public DateTime StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }

        public bool TakenExam { get; set; }

        public double Score { get; set; }

        // Nullable navigation properties (CORRECT)
        [ForeignKey(nameof(StudentId))]
        public virtual Student? Student { get; set; }

        [ForeignKey(nameof(ExamId))]
        public virtual Exam? Exam { get; set; }
    }
}
