using HEMS.Data;
using HEMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HEMS.Controllers
{
    [Authorize(Roles = "Student")] // Only logged-in students can enter
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public StudentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        }

        // 1. Dashboard - Show available exams
        public async Task<IActionResult> Index()
        {
            var studentId = await GetStudentIdAsync();
            if (studentId == 0) return RedirectToAction("Index", "Home");

            var takenExamIds = await _context.StudentExams
                .Where(se => se.StudentId == studentId && se.TakenExam)
                .Select(se => se.ExamId)
                .ToListAsync();

            var exams = await _context.Exams
                .Where(e => e.ExamStatus == "Published" && !takenExamIds.Contains(e.ExamId))
                .ToListAsync();

            // Determine if the current user must change their default password
            var currentUser = await _userManager.GetUserAsync(User);
            bool mustChange = false;
            if (currentUser != null)
            {
                var claims = await _userManager.GetClaimsAsync(currentUser);
                mustChange = claims.Any(c => c.Type == "MustChangePassword" && c.Value == "true");
            }
            ViewBag.MustChangePassword = mustChange;

            return View(exams);
        }

        // 1b. Verify Exam Code
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCode(int examId, string? enteredCode)
        {
            var exam = await _context.Exams.FindAsync(examId);
            if (exam == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(enteredCode) && exam.ExamCode == enteredCode.Trim())
            {
                return RedirectToAction(nameof(TakeExam), new { examId });
            }

            TempData["Error"] = "Invalid Authorization Code. Please check with your invigilator.";
            return RedirectToAction(nameof(Index));
        }

        // 2. Security - Change Password Logic
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "The new password and confirmation do not match.");
                return View();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);

            if (result.Succeeded)
            {
                // Remove the MustChangePassword claim so the security tip no longer appears
                try
                {
                    await _userManager.RemoveClaimAsync(user, new Claim("MustChangePassword", "true"));
                }
                catch
                {
                    // ignore failures removing the claim
                }

                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Your password has been updated successfully!";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        // 3. Exam Taking Logic
        public async Task<IActionResult> TakeExam(int examId, int index = 0)
        {
            var studentId = await GetStudentIdAsync();
            if (studentId == 0) return RedirectToAction(nameof(Index));

            var alreadyTaken = await _context.StudentExams
                .AnyAsync(se => se.StudentId == studentId && se.ExamId == examId && se.TakenExam);

            if (alreadyTaken) return RedirectToAction(nameof(ViewResult), new { examId });

            var exam = await _context.Exams
                .Include(e => e.Questions)
                    .ThenInclude(q => q.Choices)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            if (exam == null || exam.Questions == null) return NotFound();

            var questions = exam.Questions.OrderBy(q => q.QuestionId).ToList();
            if (index < 0 || index >= questions.Count) return RedirectToAction(nameof(ViewResult), new { examId });

            var currentQuestion = questions[index];

            var attempts = await _context.ExamAttempts
                .Where(a => a.StudentId == studentId && a.ExamId == examId)
                .ToListAsync();

            var existingAttempt = attempts.FirstOrDefault(a => a.QuestionId == currentQuestion.QuestionId);

            ViewBag.SelectedChoiceId = existingAttempt?.ChoiceId;
            ViewBag.AnsweredIndices = attempts.Where(a => a.ChoiceId != 0)
                .Select(a => questions.FindIndex(q => q.QuestionId == a.QuestionId)).ToList();
            ViewBag.FlaggedIndices = attempts.Where(a => a.IsFlagged)
                .Select(a => questions.FindIndex(q => q.QuestionId == a.QuestionId)).ToList();

            ViewBag.Index = index;
            ViewBag.Total = questions.Count;
            ViewBag.ExamId = examId;
            ViewBag.DurationMinutes = exam.DurationMinutes;

            return View(currentQuestion);
        }

        // 4. Submit Answer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAnswer(int qId, int choiceId, bool flagged, int examId, int nextIdx)
        {
            var studentId = await GetStudentIdAsync();
            if (studentId == 0) return Unauthorized();

            var choice = await _context.Choices.FirstOrDefaultAsync(c => c.ChoiceId == choiceId);
            var correct = choice != null && choice.IsAnswer;

            var attempt = await _context.ExamAttempts
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.QuestionId == qId && a.ExamId == examId);

            if (attempt == null)
            {
                _context.ExamAttempts.Add(new ExamAttempt
                {
                    StudentId = studentId,
                    ExamId = examId,
                    QuestionId = qId,
                    ChoiceId = choiceId,
                    IsCorrect = correct,
                    IsFlagged = flagged,
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    StartTime = DateTime.UtcNow
                });
            }
            else
            {
                attempt.ChoiceId = choiceId;
                attempt.IsCorrect = correct;
                attempt.IsFlagged = flagged;
                _context.Update(attempt);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(TakeExam), new { examId = examId, index = nextIdx });
        }

        // 5. View Result
        [HttpGet]
        public async Task<IActionResult> ViewResult(int? examId)
        {
            var studentId = await GetStudentIdAsync();
            if (studentId == 0) return RedirectToAction(nameof(Index));

            // If no examId provided, show history list
            if (!examId.HasValue || examId.Value == 0)
            {
                var history = await _context.StudentExams
                    .Include(se => se.Exam)
                    .Where(se => se.StudentId == studentId && se.TakenExam)
                    .OrderByDescending(se => se.EndDateTime)
                    .ToListAsync();

                return View(history);
            }

            var exam = await _context.Exams.Include(e => e.Questions).FirstOrDefaultAsync(e => e.ExamId == examId.Value);
            if (exam == null) return NotFound();

            var score = await _context.ExamAttempts
                .CountAsync(a => a.StudentId == studentId && a.ExamId == examId.Value && a.IsCorrect);

            var studentExam = await _context.StudentExams
                .FirstOrDefaultAsync(se => se.StudentId == studentId && se.ExamId == examId.Value);

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
                _context.Entry(studentExam).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();

            var result = await _context.StudentExams
                .Include(se => se.Exam)
                    .ThenInclude(e => e.Questions)
                .FirstOrDefaultAsync(se => se.StudentToExamId == studentExam.StudentToExamId);

            return View(result);
        }

        // Helper
        private async Task<int> GetStudentIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return 0;

            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            return student?.StudentId ?? 0;
        }
    }
}
