using Microsoft.AspNetCore.Mvc;

namespace HEMS.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // SAFE FIX: Added null-checks (?) to prevent 'Dereference of a possibly null reference'
            if (User?.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Student"))
                {
                    return RedirectToAction("Index", "Student");
                }
                else if (User.IsInRole("Coordinator"))
                {
                    return RedirectToAction("Index", "Exams");
                }
            }
            // If not authenticated, redirect to Login since Account is the default route
            return RedirectToAction("Login", "Account");
        }

        // NEW: Action to handle 404 - Page Not Found
        [Route("Home/NotFound/{statusCode?}")]
        public IActionResult NotFound(int? statusCode)
        {
            // Even if statusCode isn't 404, we treat it as Page Not Found for this view
            ViewData["StatusCode"] = statusCode;
            return View();
        }

        // Standard Error Action
        public IActionResult Error()
        {
            return View();
        }
    }
}