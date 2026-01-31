using HEMS.Data;
using HEMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // 1. Dashboard - Show available exams
        public async Task<IActionResult> Index()
        {
            var studentId = await GetStudentId();
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

            // Validates old password and updates to new password
            var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);

            if (result.Succeeded)
            {
                // Remove the MustChangePassword claim so the security tip no longer appears
                try
                {
                    await _userManager.RemoveClaimAsync(user, new System.Security.Claims.Claim("MustChangePassword", "true"));
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
            var studentId = await GetStudentId();
            if (studentId == 0) return RedirectToAction("Index");

            var isTaken = await _context.StudentExams
                .AnyAsync(se => se.StudentId == studentId && se.ExamId == examId && se.TakenExam);

            if (isTaken) return RedirectToAction("ViewResult", new { examId = examId });

            var exam = await _context.Exams
                .Include(e => e.Questions)
                .ThenInclude(q => q.Choices)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            if (exam == null) return NotFound();

            var questionsList = exam.Questions.OrderBy(q => q.QuestionId).ToList();

            if (index >= questionsList.Count) return RedirectToAction("ViewResult", new { examId = examId });

            var currentQuestion = questionsList.ElementAt(index);
            var allAttempts = await _context.ExamAttempts
                .Where(a => a.StudentId == studentId && a.ExamId == examId)
                .ToListAsync();

            var existingAttempt = allAttempts.FirstOrDefault(a => a.QuestionId == currentQuestion.QuestionId);

            ViewBag.SelectedChoiceId = existingAttempt?.ChoiceId;
            ViewBag.AnsweredIndices = allAttempts.Where(a => a.ChoiceId != 0)
                .Select(a => questionsList.FindIndex(q => q.QuestionId == a.QuestionId)).ToList();
            ViewBag.FlaggedIndices = allAttempts.Where(a => a.IsFlagged)
                .Select(a => questionsList.FindIndex(q => q.QuestionId == a.QuestionId)).ToList();

            ViewBag.Index = index;
            ViewBag.Total = questionsList.Count;
            ViewBag.ExamId = examId;
            ViewBag.durationMinutes = exam.DurationMinutes;

            return View(currentQuestion);
        }

        // 4. Submit Answer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAnswer(int qId, int choiceId, bool flagged, int examId, int nextIdx)
        {
            var studentId = await GetStudentId();
            if (studentId == 0) return Unauthorized();

            var choice = await _context.Choices.FirstOrDefaultAsync(c => c.ChoiceId == choiceId);
            bool correct = (choice != null && choice.IsAnswer);

            var attempt = await _context.ExamAttempts
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.QuestionId == qId);

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
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    StartTime = DateTime.Now
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
            return RedirectToAction("TakeExam", new { examId = examId, index = nextIdx });
        }

        // 5. View Result
        [HttpGet]
        public async Task<IActionResult> ViewResult(int? examId)
        {
            var studentId = await GetStudentId();
            if (studentId == 0) return RedirectToAction("Index");

            if (examId == null || examId == 0)
            {
                var latestRecord = await _context.StudentExams
                    .Where(se => se.StudentId == studentId)
                    .OrderByDescending(se => se.StartDateTime)
                    .FirstOrDefaultAsync();

                if (latestRecord != null) examId = latestRecord.ExamId;
                else
                {
                    var latestAttempt = await _context.ExamAttempts
                        .Where(a => a.StudentId == studentId)
                        .OrderByDescending(a => a.StartTime)
                        .FirstOrDefaultAsync();

                    if (latestAttempt == null)
                    {
                        TempData["ErrorMessage"] = "You haven't taken any exams yet.";
                        return RedirectToAction("Index");
                    }
                    examId = latestAttempt.ExamId;
                }
            }

            var exam = await _context.Exams.FindAsync(examId);
            if (exam == null) return NotFound();

            var finalScore = await _context.ExamAttempts
                .CountAsync(a => a.StudentId == studentId && a.ExamId == examId && a.IsCorrect);

            var studentExam = await _context.StudentExams
                .FirstOrDefaultAsync(se => se.StudentId == studentId && se.ExamId == examId);

            if (studentExam == null)
            {
                studentExam = new StudentExam
                {
                    StudentId = studentId,
                    ExamId = examId.Value,
                    Score = (double)finalScore,
                    TakenExam = true,
                    StartDateTime = DateTime.UtcNow,
                    EndDateTime = DateTime.UtcNow
                };
                _context.StudentExams.Add(studentExam);
            }
            else
            {
                studentExam.Score = (double)finalScore;
                studentExam.TakenExam = true;
                studentExam.EndDateTime = DateTime.UtcNow;
                _context.Entry(studentExam).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();

            ViewBag.Total = await _context.Questions.CountAsync(q => q.ExamId == examId);
            ViewBag.ExamTitle = exam.ExamTitle;
            ViewBag.ExamId = examId;

            return View(finalScore);
        }

        private async Task<int> GetStudentId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return 0;
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
            return student?.StudentId ?? 0;
        }
    }
}