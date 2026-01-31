namespace HEMS.Models;
using HEMS.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Student
{
    [Key]
    public int StudentId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Gender { get; set; } = string.Empty;

    // --- NEW ACADEMIC FIELDS ---
    [Required]
    public string IdNumber { get; set; } = string.Empty;

    public string AcademicYear { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    // --- IDENTITY LINK ---
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public virtual ApplicationUser? User { get; set; }

    public virtual ICollection<StudentExam> StudentExams { get; set; } = new List<StudentExam>();

    [InverseProperty("Student")]
    public virtual ICollection<ExamAttempt> ExamAttempts { get; set; } = new List<ExamAttempt>();
}