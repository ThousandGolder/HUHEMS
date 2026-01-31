using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using HEMS.Data;
using HEMS.Models;
using HEMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace HEMS.Controllers
{
    [Authorize(Roles = "Coordinator")]
    public class ExamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Cloudinary _cloudinary;

        public ExamsController(ApplicationDbContext context)
        {
            _context = context;
            Account account = new Account(
                "di0eli4di",
                "113677216573493",
                "MuED4inIpYVYE0U8ItejDUO3as0"
            );
            _cloudinary = new Cloudinary(account);
        }

        // 1. List all exams
        public async Task<IActionResult> Index()
        {
            var exams = await _context.Exams
                .Include(e => e.Questions)
                .OrderByDescending(e => e.AcademicYear)
                .ToListAsync();
            return View(exams);
        }

        // 2. Create Exam (GET)
        public IActionResult Create()
        {
            var model = new Exam { AcademicYear = DateTime.Now.Year };
            return View(model);
        }

        // 3. Create Exam (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Exam exam)
        {
            ModelState.Remove("Questions");
            ModelState.Remove("StudentExams");

            if (ModelState.IsValid)
            {
                // If created directly as Published, generate a code
                if (exam.ExamStatus == "Published")
                {
                    exam.ExamCode = GenerateRandomCode();
                }

                _context.Add(exam);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(exam);
        }

        // 4. Edit Exam (GET)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var exam = await _context.Exams.FindAsync(id);
            if (exam == null) return NotFound();
            return View(exam);
        }

        // 5. Edit Exam (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Exam exam)
        {
            if (id != exam.ExamId) return NotFound();

            ModelState.Remove("Questions");
            ModelState.Remove("StudentExams");

            if (ModelState.IsValid)
            {
                try
                {
                    // Logic: If status is changed to Published and no code exists, generate one
                    if (exam.ExamStatus == "Published" && string.IsNullOrEmpty(exam.ExamCode))
                    {
                        exam.ExamCode = GenerateRandomCode();
                    }

                    _context.Update(exam);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Exams.Any(e => e.ExamId == exam.ExamId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(exam);
        }

        // NEW: 5b. Publish Toggle Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam == null) return NotFound();

            if (exam.ExamStatus != "Published")
            {
                exam.ExamStatus = "Published";
                // Generate code if it doesn't have one
                if (string.IsNullOrEmpty(exam.ExamCode))
                {
                    exam.ExamCode = GenerateRandomCode();
                }

                _context.Update(exam);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Exam '{exam.ExamTitle}' published successfully! Code: {exam.ExamCode}";
            }
            return RedirectToAction(nameof(Index));
        }

        // 6. View Exam Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams
                .Include(e => e.Questions!)
                .ThenInclude(q => q.Choices)
                .FirstOrDefaultAsync(m => m.ExamId == id);

            if (exam == null) return NotFound();
            return View(exam);
        }

        // 7. Reports
        public async Task<IActionResult> Reports(int? id)
        {
            if (id.HasValue)
            {
                var exam = await _context.Exams
                    .Include(e => e.Questions)
                    .FirstOrDefaultAsync(e => e.ExamId == id);

                if (exam == null) return NotFound();

                int totalQuestionsCount = exam.Questions?.Count ?? 0;

                var reportData = await _context.ExamAttempts
                    .Where(a => a.ExamId == id)
                    .Include(a => a.Student)
                    .GroupBy(a => new { a.StudentId, a.Student.FullName })
                    .Select(g => new ExamReportViewModel
                    {
                        StudentName = g.Key.FullName,
                        Score = g.Count(x => x.IsCorrect),
                        TotalQuestions = totalQuestionsCount,
                        DateTaken = g.Max(x => x.StartTime)
                    })
                    .ToListAsync();

                ViewBag.ExamTitle = exam.ExamTitle;
                ViewBag.IsGeneralReport = false;
                return View(reportData);
            }
            else
            {
                var generalReport = await _context.Exams
                    .Select(e => new ExamReportViewModel
                    {
                        ExamId = e.ExamId,
                        ExamTitle = e.ExamTitle,
                        TotalQuestions = e.Questions.Count,
                        StudentCount = _context.ExamAttempts
                            .Where(a => a.ExamId == e.ExamId)
                            .Select(a => a.StudentId)
                            .Distinct()
                            .Count(),
                        AverageScore = _context.ExamAttempts
                            .Where(a => a.ExamId == e.ExamId && a.IsCorrect)
                            .Count()
                    })
                    .ToListAsync();

                ViewBag.ExamTitle = "General Performance Report";
                ViewBag.IsGeneralReport = true;
                return View(generalReport);
            }
        }

        // 8. Delete Exam
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam != null)
            {
                _context.Exams.Remove(exam);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // 9. Bulk Upload
        [HttpPost]
        public async Task<IActionResult> BulkUpload(int id, IFormFile examZip)
        {
            if (examZip == null || examZip.Length == 0) return RedirectToAction("Details", new { id });

            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                using (var stream = examZip.OpenReadStream())
                using (var archive = new ZipArchive(stream))
                {
                    archive.ExtractToDirectory(tempFolder);
                }

                string manifestPath = Directory.GetFiles(tempFolder, "manifest.csv", SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrEmpty(manifestPath)) throw new Exception("manifest.csv missing from ZIP");

                using (var reader = new StreamReader(manifestPath))
                using (var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<dynamic>().ToList();

                    foreach (var row in records)
                    {
                        var question = new Question
                        {
                            ExamId = id,
                            QuestionText = row.QuestionText,
                            MarkWeight = 1.0m
                        };

                        string imageName = row.ImageName?.ToString();
                        if (!string.IsNullOrEmpty(imageName))
                        {
                            var imgPath = Directory.GetFiles(tempFolder, imageName, SearchOption.AllDirectories).FirstOrDefault();
                            if (imgPath != null)
                            {
                                var uploadParams = new ImageUploadParams()
                                {
                                    File = new FileDescription(imgPath),
                                    PublicId = $"hems_{Guid.NewGuid()}",
                                    Folder = "exam_questions"
                                };
                                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                                if (uploadResult?.SecureUrl == null) throw new Exception("Cloudinary upload failed.");
                                question.ImagePath = uploadResult.SecureUrl.ToString();
                            }
                        }

                        _context.Questions.Add(question);
                        await _context.SaveChangesAsync();

                        string choicesRaw = row.Choices.ToString();
                        string[] choiceArray = choicesRaw.Split('|');
                        int correctIdx = int.Parse(row.CorrectChoiceIndex.ToString());

                        for (int i = 0; i < choiceArray.Length; i++)
                        {
                            _context.Choices.Add(new Choice
                            {
                                QuestionId = question.QuestionId,
                                ChoiceText = choiceArray[i].Trim(),
                                IsAnswer = (i == correctIdx)
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["Success"] = "Bulk upload successful!";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"Error: {ex.Message}";
            }
            finally { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); }

            return RedirectToAction("Details", new { id });
        }

        // Helper to generate a clean numeric code
        private string GenerateRandomCode()
        {
            return new Random().Next(1000, 9999).ToString();
        }
    }

    // --- ENHANCED VIEWMODEL ---
    public class ExamReportViewModel
    {
        public string StudentName { get; set; } = string.Empty;
        public int Score { get; set; }
        public DateTime DateTaken { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int AverageScore { get; set; }
        public int TotalQuestions { get; set; }

        public double Percentage => TotalQuestions > 0
            ? Math.Round(((double)Score / TotalQuestions) * 100, 2) : 0;

        public double AvgPercentage
        {
            get
            {
                if (TotalQuestions == 0 || StudentCount == 0) return 0;
                double totalPossibleAnswers = TotalQuestions * StudentCount;
                return Math.Round((AverageScore / totalPossibleAnswers) * 100, 2);
            }
        }
    }
}