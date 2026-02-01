using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HEMS.Data;
using HEMS.Models;
using Microsoft.AspNetCore.Authorization;

namespace HEMS.Controllers
{
    [Authorize(Roles = "Coordinator")]
    public class QuestionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuestionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Create(int examId)
        {
            var exam = await _context.Exams.FindAsync(examId);
            if (exam == null) return NotFound();

            ViewBag.ExamId = examId;
            ViewBag.DefaultMark = exam.DefaultMark;

            // Ensure the strongly-typed view receives a non-null model so TagHelpers
            // and expressions that reference Model.* do not trigger a NullReferenceException.
            var model = new Question
            {
                ExamId = examId,
                MarkWeight = exam.DefaultMark
            };

            return View(model);
        }

        // Bulk upload UI for Questions (GET)
        public IActionResult Bulk()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("QuestionText,ExamId")] Question question)
        {
            // DUPLICATE CHECK: Question in Exam
            bool questionExists = await _context.Questions.AnyAsync(q =>
                q.ExamId == question.ExamId &&
                q.QuestionText.Trim().ToLower() == question.QuestionText.Trim().ToLower());

            if (questionExists)
            {
                TempData["ErrorMessage"] = "Duplicate Question: This text already exists in this exam.";
                var currentExam = await _context.Exams.FindAsync(question.ExamId);
                ViewBag.ExamId = question.ExamId;
                ViewBag.DefaultMark = currentExam?.DefaultMark ?? 0;
                return View(question);
            }

            var exam = await _context.Exams.FindAsync(question.ExamId);
            if (exam != null) question.MarkWeight = exam.DefaultMark;

            ModelState.Remove("Choices");
            ModelState.Remove("Exam");

            if (ModelState.IsValid)
            {
                _context.Add(question);
                await _context.SaveChangesAsync();
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, redirectUrl = Url.Action("ManageChoices", new { questionId = question.QuestionId }) });
                }
                return RedirectToAction("ManageChoices", new { questionId = question.QuestionId });
            }

            ViewBag.ExamId = question.ExamId;
            ViewBag.DefaultMark = exam?.DefaultMark ?? 0;
            return View(question);
        }

        public async Task<IActionResult> ManageChoices(int questionId)
        {
            var question = await _context.Questions
                .Include(q => q.Choices)
                .Include(q => q.Exam)
                .FirstOrDefaultAsync(q => q.QuestionId == questionId);

            if (question == null) return NotFound();
            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddChoice(int questionId, string choiceText, bool isAnswer)
        {
            if (string.IsNullOrEmpty(choiceText)) return RedirectToAction(nameof(ManageChoices), new { questionId });

            // DUPLICATE CHECK: Choice in Question
            bool choiceExists = await _context.Choices.AnyAsync(c =>
                c.QuestionId == questionId &&
                c.ChoiceText.Trim().ToLower() == choiceText.Trim().ToLower());

            if (choiceExists)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { duplicate = true, message = "Duplicate Choice: This option already exists for this question." });
                }

                TempData["ErrorMessage"] = "Duplicate Choice: This option already exists for this question.";
                return RedirectToAction(nameof(ManageChoices), new { questionId });
            }

            var choice = new Choice
            {
                QuestionId = questionId,
                ChoiceText = choiceText,
                IsAnswer = isAnswer
            };
            _context.Choices.Add(choice);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ManageChoices), new { questionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChoice(int choiceId, string returnUrl = "ManageChoices")
        {
            var choice = await _context.Choices.FindAsync(choiceId);
            if (choice != null)
            {
                int qId = choice.QuestionId;
                _context.Choices.Remove(choice);
                await _context.SaveChangesAsync();

                if (returnUrl == "Edit") return RedirectToAction("Edit", new { id = qId });
                return RedirectToAction("ManageChoices", new { questionId = qId });
            }
            return RedirectToAction("Index", "Exams");
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var question = await _context.Questions
                .Include(q => q.Choices)
                .FirstOrDefaultAsync(q => q.QuestionId == id);

            if (question == null) return NotFound();

            ViewBag.ExamId = question.ExamId;
            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("QuestionId,QuestionText,MarkWeight,ExamId")] Question question)
        {
            if (id != question.QuestionId) return NotFound();

            // DUPLICATE CHECK: Question in Exam (Excluding itself)
            bool questionExists = await _context.Questions.AnyAsync(q =>
                q.ExamId == question.ExamId &&
                q.QuestionId != question.QuestionId &&
                q.QuestionText.Trim().ToLower() == question.QuestionText.Trim().ToLower());

            if (questionExists)
            {
                TempData["ErrorMessage"] = "Duplicate Question: Another question with this text already exists.";
                return View(question);
            }

            ModelState.Remove("Choices");
            ModelState.Remove("Exam");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(question);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Questions.Any(e => e.QuestionId == question.QuestionId)) return NotFound();
                    else throw;
                }
                return RedirectToAction("Details", "Exams", new { id = question.ExamId });
            }
            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question != null)
            {
                int examId = question.ExamId;
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", "Exams", new { id = examId });
            }
            return RedirectToAction("Index", "Exams");
        }
    }
}