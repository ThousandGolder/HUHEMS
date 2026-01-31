using CsvHelper;
using HEMS.Data;
using HEMS.Models;
using System.Globalization;
using System.IO.Compression;

namespace HEMS.Services
{
    // Matches the Manifest Mapping Logic in your design [cite: 25, 26]
    public class QuestionMap
    {
        public string QuestionText { get; set; }
        public string HasImage { get; set; }
        public string? ImageFileName { get; set; }
        public string ChoiceA { get; set; }
        public string ChoiceB { get; set; }
        public string ChoiceC { get; set; }
        public string ChoiceD { get; set; }
        public string CorrectAnswer { get; set; }
    }

    public interface IExamImportService
    {
        Task ProcessZipAsync(Stream zipStream, int examId);
    }

    public class ExamImportService : IExamImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ExamImportService(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task ProcessZipAsync(Stream zipStream, int examId)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            using var archive = new ZipArchive(zipStream);
            archive.ExtractToDirectory(tempPath); // [cite: 33]

            var csvPath = Path.Combine(tempPath, "manifest.csv"); // [cite: 23]

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<QuestionMap>().ToList();

            using var transaction = await _context.Database.BeginTransactionAsync(); // [cite: 35]
            try
            {
                foreach (var row in records)
                {
                    var question = new Question
                    {
                        ExamId = examId,
                        QuestionText = row.QuestionText,
                        MarkWeight = 1.0m // Default weight
                    };

                    // Image Persistence Logic [cite: 34]
                    if (row.HasImage == "Yes" && !string.IsNullOrEmpty(row.ImageFileName))
                    {
                        var sourceImg = Path.Combine(tempPath, "images", row.ImageFileName);
                        if (File.Exists(sourceImg))
                        {
                            var uniqueName = $"{Guid.NewGuid()}_{row.ImageFileName}";
                            var storagePath = Path.Combine(_env.WebRootPath, "uploads/exam-images", uniqueName);

                            Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
                            File.Copy(sourceImg, storagePath);
                            question.ImagePath = $"/uploads/exam-images/{uniqueName}"; // [cite: 14]
                        }
                    }

                    _context.Questions.Add(question);
                    await _context.SaveChangesAsync();

                    // Create 4 Choice records [cite: 29, 30]
                    var choices = new List<Choice>
                    {
                        new Choice { QuestionId = question.QuestionId, ChoiceText = row.ChoiceA, IsAnswer = row.CorrectAnswer == "A" },
                        new Choice { QuestionId = question.QuestionId, ChoiceText = row.ChoiceB, IsAnswer = row.CorrectAnswer == "B" },
                        new Choice { QuestionId = question.QuestionId, ChoiceText = row.ChoiceC, IsAnswer = row.CorrectAnswer == "C" },
                        new Choice { QuestionId = question.QuestionId, ChoiceText = row.ChoiceD, IsAnswer = row.CorrectAnswer == "D" }
                    };
                    _context.Choices.AddRange(choices);
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync(); // [cite: 36]
                throw;
            }
            finally
            {
                Directory.Delete(tempPath, true); // [cite: 37]
            }
        }
    }
}