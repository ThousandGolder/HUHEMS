using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HEMS.Models;
using HEMS.Data;
using System.Formats.Asn1;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HEMS.Controllers
{
    [Authorize(Roles = "Coordinator")] // Only coordinators can access this
    public class StudentsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public StudentsController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // Action to show the student list and upload form
        public async Task<IActionResult> Index()
        {
            // Get all students with their user link, ordered alphabetically
            var students = await _context.Students
                .Include(s => s.User)
                .OrderBy(s => s.FullName)
                .ToListAsync();

            // Exclude any Student records that belong to users in the Coordinator role
            var coordinators = await _userManager.GetUsersInRoleAsync("Coordinator");
            var coordIds = coordinators.Select(u => u.Id).ToHashSet();

            var filtered = students.Where(s => s.User == null || !coordIds.Contains(s.UserId)).ToList();
            return View(filtered);
        }

        // GET: Students/Create
        public IActionResult Create()
        {
            return View(new Student());
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Student student)
        {
            if (!ModelState.IsValid) return View(student);

            // Prevent duplicate students by ID number
            if (!string.IsNullOrWhiteSpace(student.IdNumber))
            {
                var exists = await _context.Students.AnyAsync(s => s.IdNumber == student.IdNumber);
                if (exists)
                {
                    ModelState.AddModelError(string.Empty, "User already exists with this ID number.");
                    return View(student);
                }
            }

            // Generate credentials similar to bulk upload
            string last4 = student.IdNumber != null && student.IdNumber.Length >= 4 ? student.IdNumber.Substring(student.IdNumber.Length - 4) : student.IdNumber ?? "0000";
            // sanitize and generate a base username
            string cleanName = new string(student.FullName.Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "student";
            string baseUsername = $"{cleanName}{last4.Replace("/", "")}";
            string generatedUsername = baseUsername;
            int suffix = 1;
            // ensure username uniqueness
            while (await _userManager.FindByNameAsync(generatedUsername) != null)
            {
                generatedUsername = baseUsername + suffix.ToString();
                suffix++;
                if (suffix > 1000) break; // avoid infinite loop
            }
            string generatedPassword = $"{generatedUsername}@HUHEMS";

            var user = new ApplicationUser
            {
                UserName = generatedUsername,
                Email = $"{generatedUsername}@huhems.edu",
                EmailConfirmed = true
            };
            // Ensure email doesn't already exist
            var emailExists = await _userManager.FindByEmailAsync(user.Email);
            if (emailExists != null)
            {
                ModelState.AddModelError(string.Empty, "A user with the generated email already exists. Please modify the student's name or ID.");
                return View(student);
            }

            var result = await _userManager.CreateAsync(user, generatedPassword);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", string.Join("; ", result.Errors.Select(e => e.Description)));
                return View(student);
            }

            await _userManager.AddToRoleAsync(user, "Student");
            // Mark that this user must change the default password
            await _userManager.AddClaimAsync(user, new Claim("MustChangePassword", "true"));

            student.UserId = user.Id;
            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Student created. Username: {generatedUsername}, Password: {generatedPassword}";
            return RedirectToAction("Index");
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            return View(student);
        }

        // POST: Students/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Student model)
        {
            if (id != model.StudentId) return NotFound();
            if (!ModelState.IsValid) return View(model);

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            student.FullName = model.FullName;
            student.Gender = model.Gender;
            student.IdNumber = model.IdNumber;
            student.AcademicYear = model.AcademicYear;
            student.Department = model.Department;

            _context.Update(student);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Student updated successfully.";
            return RedirectToAction("Index");
        }

        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == id);
            if (student == null) return NotFound();
            return View(student);
        }

        // POST: Students/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int StudentId)
        {
            var student = await _context.Students
                .Include(s => s.User)
                .Include(s => s.ExamAttempts)
                .Include(s => s.StudentExams)
                .FirstOrDefaultAsync(s => s.StudentId == StudentId);
            if (student == null) return NotFound();

            if (student.ExamAttempts.Any() || student.StudentExams.Any())
            {
                TempData["Error"] = "Cannot delete a student with exam attempts or enrollments. Remove those records first.";
                return RedirectToAction("Index");
            }

            // Remove student first (won't cascade because we explicitly remove)
            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            if (student.User != null)
            {
                var appUser = await _userManager.FindByIdAsync(student.UserId);
                if (appUser != null)
                {
                    await _userManager.DeleteAsync(appUser);
                }
            }

            TempData["Success"] = "Student deleted successfully.";
            return RedirectToAction("Index");
        }

        // Support POST to /Students/Delete as some views/forms may post to that action name
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int StudentId)
        {
            return await DeleteConfirmed(StudentId);
        }

        // AJAX: Check if a student exists by ID number to prevent duplicates (used by Create form)
        [HttpGet]
        public async Task<IActionResult> CheckId(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber))
            {
                return Json(new { exists = false, message = string.Empty });
            }

            var exists = await _context.Students.AnyAsync(s => s.IdNumber == idNumber);
            return Json(new { exists = exists, message = exists ? "User already exists with this ID number." : string.Empty });
        }

        // AJAX: Check multiple IDs (bulk) - expects JSON array of idNumbers in request body
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIdsBulk([FromBody] List<string> idNumbers)
        {
            if (idNumbers == null || !idNumbers.Any()) return Json(new { existing = new List<string>() });

            var trimmed = idNumbers.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            var existing = await _context.Students.Where(s => trimmed.Contains(s.IdNumber)).Select(s => s.IdNumber).ToListAsync();
            return Json(new { existing = existing });
        }

        [HttpPost]
        public async Task<IActionResult> BulkUpload(IFormFile studentFile)
        {
            if (studentFile == null || studentFile.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV file.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var createdCount = 0;
                var skipped = new List<string>();
                using (var reader = new StreamReader(studentFile.OpenReadStream()))
                using (var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<dynamic>().ToList();

                    foreach (var rec in records)
                    {
                        try
                        {
                            var dict = rec as IDictionary<string, object>;

                            // helper to read fields case-insensitively and accept common header variants
                            string GetField(IDictionary<string, object> d, params string[] keys)
                            {
                                if (d == null) return string.Empty;
                                foreach (var k in keys)
                                {
                                    var match = d.Keys.FirstOrDefault(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
                                    if (match != null)
                                    {
                                        return d[match]?.ToString()?.Trim() ?? string.Empty;
                                    }
                                }
                                return string.Empty;
                            }

                            string fullName = GetField(dict, "FullName", "Full Name", "Name", "fullname");
                            string idNumber = GetField(dict, "IdNumber", "ID", "Identifier", "Id Number");

                            if (string.IsNullOrWhiteSpace(fullName))
                            {
                                skipped.Add("Missing FullName");
                                continue;
                            }

                            // Prevent duplicates by ID number
                            if (!string.IsNullOrWhiteSpace(idNumber) && await _context.Students.AnyAsync(s => s.IdNumber == idNumber))
                            {
                                skipped.Add($"{fullName} ({idNumber}): already exists");
                                continue;
                            }

                            // Prepare student entity
                            var student = new Student
                            {
                                FullName = fullName,
                                IdNumber = idNumber,
                                Gender = "",
                                AcademicYear = "",
                                Department = ""
                            };

                            // Generate a unique username
                            string last4 = student.IdNumber != null && student.IdNumber.Length >= 4 ? student.IdNumber.Substring(student.IdNumber.Length - 4) : student.IdNumber ?? "0000";
                            string cleanName = new string(student.FullName.Where(char.IsLetterOrDigit).ToArray());
                            if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "student";
                            string baseUsername = $"{cleanName}{last4.Replace("/", "")}";
                            string generatedUsername = baseUsername;
                            int suffix = 1;
                            while (await _userManager.FindByNameAsync(generatedUsername) != null)
                            {
                                generatedUsername = baseUsername + suffix.ToString();
                                suffix++;
                                if (suffix > 1000) break;
                            }

                            string generatedPassword = $"{generatedUsername}@HUHEMS";
                            string userEmail = $"{generatedUsername}@huhems.edu";

                            if (await _userManager.FindByEmailAsync(userEmail) != null)
                            {
                                skipped.Add($"{fullName} ({idNumber}): email {userEmail} exists");
                                continue;
                            }

                            var user = new ApplicationUser
                            {
                                UserName = generatedUsername,
                                Email = userEmail,
                                EmailConfirmed = true
                            };

                            var result = await _userManager.CreateAsync(user, generatedPassword);
                            if (result.Succeeded)
                            {
                                await _userManager.AddToRoleAsync(user, "Student");
                                await _userManager.AddClaimAsync(user, new Claim("MustChangePassword", "true"));
                                student.UserId = user.Id;
                                _context.Students.Add(student);
                                createdCount++;
                            }
                            else
                            {
                                skipped.Add($"{fullName} ({idNumber}): {string.Join(';', result.Errors.Select(e => e.Description))}");
                            }
                        }
                        catch (Exception ex)
                        {
                            skipped.Add("Malformed record: " + ex.Message);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                // Summarize results for user feedback
                var summary = $"Bulk upload completed. Created: {createdCount}. Skipped: {skipped.Count}.";
                if (skipped.Any())
                {
                    // include a few examples of skipped reasons
                    var examples = string.Join("; ", skipped.Take(5));
                    summary += " Examples: " + examples + (skipped.Count > 5 ? "..." : "");
                }
                TempData["Success"] = summary;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Error during bulk upload: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}