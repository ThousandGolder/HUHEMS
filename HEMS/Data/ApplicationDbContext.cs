using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HEMS.Models;

namespace HEMS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Choice> Choices { get; set; }
        public DbSet<ExamAttempt> ExamAttempts { get; set; }
        public DbSet<StudentExam> StudentExams { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
           // This ensures MarkWeight supports decimals (e.g., 1.5 marks)
            builder.Entity<Question>()
                .Property(q => q.MarkWeight)
                .HasColumnType("decimal(18,2)");
            builder.Entity<Exam>()
               .Property(e => e.DefaultMark) // Change 'DefaultMark' to whatever your property name is
               .HasColumnType("decimal(18,2)");

            base.OnModelCreating(builder);

            // 1. One-to-One: ApplicationUser <-> Student
            builder.Entity<Student>()
                .HasOne(s => s.User)
                .WithOne(u => u.Student)
                .HasForeignKey<Student>(s => s.UserId);

            // 2. One-to-Many: Exam -> Questions
            builder.Entity<Question>()
                .HasOne(q => q.Exam)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            // 3. One-to-Many: Question -> Choices
            builder.Entity<Choice>()
                .HasOne(c => c.Question)
                .WithMany(q => q.Choices)
                .HasForeignKey(c => c.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // 4. StudentExam Mapping (Many-to-Many Link Table)
            builder.Entity<StudentExam>()
                .HasOne(se => se.Student)
                .WithMany(s => s.StudentExams)
                .HasForeignKey(se => se.StudentId);

            builder.Entity<StudentExam>()
                .HasOne(se => se.Exam)
                .WithMany(e => e.StudentExams)
                .HasForeignKey(se => se.ExamId);

            // 5. FIXED: ExamAttempt Mapping
            // Explicitly linking both sides of the relationship to stop "StudentId1"
            builder.Entity<ExamAttempt>()
                .HasOne(ea => ea.Student)
                .WithMany(s => s.ExamAttempts) // This MUST match the collection in Student.cs
                .HasForeignKey(ea => ea.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ExamAttempt>()
                .HasOne(ea => ea.Exam)
                .WithMany() // Exams don't necessarily need a collection of all attempts
                .HasForeignKey(ea => ea.ExamId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}