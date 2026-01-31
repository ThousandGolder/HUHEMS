using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using HEMS.Models;
using HEMS.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace HEMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager; // Added RoleManager
        private readonly ApplicationDbContext _context;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login() => View();
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Username and Password are required.");
                return View();
            }

            // Attempt sign-in using UserName
            var result = await _signInManager.PasswordSignInAsync(username, password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByNameAsync(username);

                if (await _userManager.IsInRoleAsync(user, "Student"))
                    return RedirectToAction("Index", "Student");

                if (await _userManager.IsInRoleAsync(user, "Coordinator"))
                    return RedirectToAction("Index", "Exams");

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Invalid login. Please check your Student ID/Username.");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            // Redirects to the Login action of this controller
            return RedirectToAction("Login", "Account");
        }
    }
}