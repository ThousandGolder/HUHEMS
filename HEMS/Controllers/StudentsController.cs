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

            // Generate credentials similar to bulk upload
            string last4 = student.IdNumber != null && student.IdNumber.Length >= 4 ? student.IdNumber.Substring(student.IdNumber.Length - 4) : student.IdNumber ?? "0000";
            string cleanName = student.FullName.Replace(" ", "");
            string generatedUsername = $"{cleanName}{last4.Replace("/", "")}";
            string generatedPassword = $"{generatedUsername}@HUHEMS";

            var user = new ApplicationUser
            {
                UserName = generatedUsername,
                Email = $"{generatedUsername}@huhems.edu",
                EmailConfirmed = true
            };

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
                using (var reader = new StreamReader(studentFile.OpenReadStream()))
                using (var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<dynamic>().ToList();

                    // Minimal processing here: you can implement actual creation logic as needed
                    foreach (var rec in records)
                    {
                        // Example: attempt to extract fields if present; skip if missing
                        try
                        {
                            var dict = rec as IDictionary<string, object>;
                            string fullName = dict != null && dict.ContainsKey("FullName") ? (dict["FullName"]?.ToString() ?? string.Empty) : string.Empty;
                            string idNumber = dict != null && dict.ContainsKey("IdNumber") ? (dict["IdNumber"]?.ToString() ?? string.Empty) : string.Empty;

                            if (string.IsNullOrWhiteSpace(fullName))
                                continue;

                            // Create student entity
                            var student = new Student
                            {
                                FullName = fullName,
                                IdNumber = idNumber,
                                Gender = "",
                                AcademicYear = "",
                                Department = ""
                            };

                            // Generate username/password similar to single create
                            string last4 = student.IdNumber != null && student.IdNumber.Length >= 4 ? student.IdNumber.Substring(student.IdNumber.Length - 4) : student.IdNumber ?? "0000";
                            string cleanName = student.FullName.Replace(" ", "");
                            string generatedUsername = $"{cleanName}{last4.Replace("/", "")}";
                            string generatedPassword = $"{generatedUsername}@HUHEMS";

                            var user = new ApplicationUser
                            {
                                UserName = generatedUsername,
                                Email = $"{generatedUsername}@huhems.edu",
                                EmailConfirmed = true
                            };

                            var result = await _userManager.CreateAsync(user, generatedPassword);
                            if (result.Succeeded)
                            {
                                await _userManager.AddToRoleAsync(user, "Student");
                                // mark new user to change default password on first login
                                await _userManager.AddClaimAsync(user, new Claim("MustChangePassword", "true"));
                                student.UserId = user.Id;
                                _context.Students.Add(student);
                            }
                            // If user creation failed, skip and continue; optionally collect errors
                        }
                        catch
                        {
                            // skip malformed record
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                TempData["Success"] = "Bulk upload completed.";
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