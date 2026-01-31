using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HEMS.Data;
using HEMS.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace HEMS.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // 1. Dashboard - Show available exams
        public async Task<IActionResult> Index()
        {
            int studentId = await GetStudentIdAsync();
            if (studentId == 0)
                return RedirectToAction("Index", "Home");

            List<int> takenExamIds = await _context.StudentExams
                .Where(se => se.StudentId == studentId && se.TakenExam)
                .Select(se => se.ExamId)
                .ToListAsync();

            List<Exam> exams = await _context.Exams
                .Where(e => e.ExamStatus == "Published" && !takenExamIds.Contains(e.ExamId))
                .ToListAsync();

            return View(exams);
        }

        // 1b. Verify Exam Code
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCode(int examId, string? enteredCode)
        {
            Exam? exam = await _context.Exams.FindAsync(examId);
            if (exam == null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(enteredCode) &&
                exam.ExamCode == enteredCode.Trim())
            {
                return RedirectToAction(nameof(TakeExam), new { examId });
            }

            TempData["Error"] = "Invalid Authorization Code. Please check with your invigilator.";
            return RedirectToAction(nameof(Index));
        }

        // 2. Take Exam
        public async Task<IActionResult> TakeExam(int examId, int index = 0)
        {
            int studentId = await GetStudentIdAsync();
            if (studentId == 0)
                return RedirectToAction(nameof(Index));

            bool alreadyTaken = await _context.StudentExams
                .AnyAsync(se => se.StudentId == studentId && se.ExamId == examId && se.TakenExam);

            if (alreadyTaken)
                return RedirectToAction(nameof(ViewResult), new { examId });

            Exam? exam = await _context.Exams
                .Include(e => e.Questions)
                    .ThenInclude(q => q.Choices)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            if (exam == null || exam.Questions == null)
                return NotFound();

            List<Question> questions = exam.Questions
                .OrderBy(q => q.QuestionId)
                .ToList();

            if (index < 0 || index >= questions.Count)
                return RedirectToAction(nameof(ViewResult), new { examId });

            Question currentQuestion = questions[index];

            List<ExamAttempt> attempts = await _context.ExamAttempts
                .Where(a => a.StudentId == studentId && a.ExamId == examId)
                .ToListAsync();

            ExamAttempt? existingAttempt =
                attempts.FirstOrDefault(a => a.QuestionId == currentQuestion.QuestionId);

            ViewBag.SelectedChoiceId = existingAttempt?.ChoiceId;

            ViewBag.AnsweredIndices = attempts
                .Where(a => a.ChoiceId != 0)
                .Select(a => questions.FindIndex(q => q.QuestionId == a.QuestionId))
                .Where(i => i >= 0)
                .ToList();

            ViewBag.FlaggedIndices = attempts
                .Where(a => a.IsFlagged)
                .Select(a => questions.FindIndex(q => q.QuestionId == a.QuestionId))
                .Where(i => i >= 0)
                .ToList();

            ViewBag.Index = index;
            ViewBag.Total = questions.Count;
            ViewBag.ExamId = examId;
            ViewBag.DurationMinutes = exam.DurationMinutes;

            return View(currentQuestion);
        }

        // 3. Submit Answer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAnswer(
            int qId,
            int choiceId,
            bool flagged,
            int examId,
            int nextIdx)
        {
            int studentId = await GetStudentIdAsync();
            if (studentId == 0)
                return Unauthorized();

            Choice? choice = await _context.Choices
                .FirstOrDefaultAsync(c => c.ChoiceId == choiceId);

            bool isCorrect = choice?.IsAnswer ?? false;

            ExamAttempt? attempt = await _context.ExamAttempts
                .FirstOrDefaultAsync(a =>
                    a.StudentId == studentId &&
                    a.ExamId == examId &&
                    a.QuestionId == qId);

            if (attempt == null)
            {
                _context.ExamAttempts.Add(new ExamAttempt
                {
                    StudentId = studentId,
                    ExamId = examId,
                    QuestionId = qId,
                    ChoiceId = choiceId,
                    IsCorrect = isCorrect,
                    IsFlagged = flagged,
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    StartTime = DateTime.UtcNow
                });
            }
            else
            {
                attempt.ChoiceId = choiceId;
                attempt.IsCorrect = isCorrect;
                attempt.IsFlagged = flagged;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(TakeExam), new { examId, index = nextIdx });
        }

        // 4. View Result
        [HttpGet]
        public async Task<IActionResult> ViewResult(int? examId)
        {
            int studentId = await GetStudentIdAsync();
            if (studentId == 0)
                return RedirectToAction(nameof(Index));

            // History view
            if (!examId.HasValue || examId.Value == 0)
            {
                var history = await _context.StudentExams
                    .Include(se => se.Exam)
                    .Where(se => se.StudentId == studentId && se.TakenExam)
                    .OrderByDescending(se => se.EndDateTime)
                    .ToListAsync();

                return View(history);
            }

            Exam? exam = await _context.Exams
                .Include(e => e.Questions)
                .FirstOrDefaultAsync(e => e.ExamId == examId.Value);

            if (exam == null)
                return NotFound();

            int score = await _context.ExamAttempts
                .CountAsync(a =>
                    a.StudentId == studentId &&
                    a.ExamId == examId.Value &&
                    a.IsCorrect);

            StudentExam? studentExam = await _context.StudentExams
                .FirstOrDefaultAsync(se =>
                    se.StudentId == studentId &&
                    se.ExamId == examId.Value);

            if (studentExam == null)
            {
                studentExam = new StudentExam
                {
                    StudentId = studentId,
                    ExamId = examId.Value,
                    Score = score,
                    TakenExam = true,
                    StartDateTime = DateTime.UtcNow,
                    EndDateTime = DateTime.UtcNow
                };

                _context.StudentExams.Add(studentExam);
            }
            else
            {
                studentExam.Score = score;
                studentExam.TakenExam = true;
                studentExam.EndDateTime = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            StudentExam? result = await _context.StudentExams
                .Include(se => se.Exam)
                    .ThenInclude(e => e.Questions)
                .FirstOrDefaultAsync(se => se.StudentToExamId == studentExam.StudentToExamId);

            return View(result);
        }

        // Helper
        private async Task<int> GetStudentIdAsync()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return 0;

            Student? student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            return student?.StudentId ?? 0;
        }
    }
}
