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
        private readonly Cloudinary _cloudinary;

        
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

        // 7. Reports: Handles both General (All Exams) and Specific (One Exam)
        public async Task<IActionResult> Reports(int? id)
        {
            if (id.HasValue)
            {
                // --- CASE A: Specific Exam Report ---
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
                // --- CASE B: General Performance Report (All Exams) ---
                var generalReport = await _context.Exams
                    .Select(e => new ExamReportViewModel
                    {
                        ExamId = e.ExamId,
                        ExamTitle = e.ExamTitle,
                        TotalQuestions = e.Questions.Count,
                        // Count unique students who attempted this exam
                        StudentCount = _context.ExamAttempts
                            .Where(a => a.ExamId == e.ExamId)
                            .Select(a => a.StudentId)
                            .Distinct()
                            .Count(),
                        // Total correct answers across all students for this exam
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

        // 9. Bulk Upload Questions via ZIP
    [HttpPost]
    public async Task<IActionResult> BulkUpload(int id, IFormFile examZip)
    {
        if (examZip == null || examZip.Length == 0) return RedirectToAction("Details", new { id });

        // Use the injected _cloudinary instance configured in the controller constructor

        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempFolder);

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            using (var stream = examZip.OpenReadStream())
            using (var archive = new System.IO.Compression.ZipArchive(stream))
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

                    // 2. Upload Image to Cloudinary if it exists
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
                            // Validate upload result before using SecureUrl
                            if (uploadResult == null || uploadResult.SecureUrl == null || uploadResult.Error != null)
                            {
                                var errMsg = uploadResult?.Error?.Message ?? "Cloudinary upload returned no URL.";
                                throw new Exception($"Cloudinary upload failed for image '{imageName}': {errMsg}");
                            }
                            question.ImagePath = uploadResult.SecureUrl.ToString(); // Save the Cloudinary URL
                        }
                    }

                    _context.Questions.Add(question);
                    await _context.SaveChangesAsync();

                    // 3. Dynamic Choice Handling (Splitting by |)
                    var choicesField = row.Choices;
                    if (choicesField == null) throw new Exception($"Choices field missing in manifest for question '{row.QuestionText}'.");
                    string choicesRaw = choicesField.ToString();
                    if (string.IsNullOrWhiteSpace(choicesRaw)) throw new Exception($"Choices field empty in manifest for question '{row.QuestionText}'.");
                    string[] choiceArray = choicesRaw.Split('|');

                    var correctField = row.CorrectChoiceIndex;
                    if (correctField == null) throw new Exception($"CorrectChoiceIndex missing in manifest for question '{row.QuestionText}'.");
                    if (!int.TryParse(correctField.ToString(), out int correctIdx)) throw new Exception($"CorrectChoiceIndex value invalid for question '{row.QuestionText}'.");

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
}

    // --- ENHANCED VIEWMODEL ---
    public class ExamReportViewModel
    {
        // Properties for Specific Report
        public string StudentName { get; set; } = string.Empty;
        public int Score { get; set; }
        public DateTime DateTaken { get; set; }

        // Properties for General Report
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int AverageScore { get; set; } // Used to store total correct answers for math

        // Shared Properties
        public int TotalQuestions { get; set; }

        // Calculated: Single Student Percentage
        public double Percentage => TotalQuestions > 0
            ? Math.Round(((double)Score / TotalQuestions) * 100, 2) : 0;

        // Calculated: General Average Percentage across all students
        public double AvgPercentage
        {
            get
            {
                if (TotalQuestions == 0 || StudentCount == 0) return 0;
                // Formula: (Total Correct Answers) / (Total Possible Answers across all students)
                double totalPossibleAnswers = TotalQuestions * StudentCount;
                return Math.Round((AverageScore / totalPossibleAnswers) * 100, 2);
            }
        }
    }
}