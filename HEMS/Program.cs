using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HEMS.Data;
using HEMS.Models;
using HEMS.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddScoped<IExamImportService, ExamImportService>();

// 2. Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// 3. Automated Database and Role Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        context.Database.Migrate();

        string[] roleNames = { "Coordinator", "Student" };
        foreach (var roleName in roleNames)
        {
            var roleExist = Task.Run(() => roleManager.RoleExistsAsync(roleName)).Result;
            if (!roleExist)
            {
                Task.Run(() => roleManager.CreateAsync(new IdentityRole(roleName))).Wait();
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// 4. Middleware Configuration
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// FIX: Handle 404 - Page Not Found for all invalid routes
// This sends the user to the NotFound action in HomeController
app.UseStatusCodePagesWithReExecute("/Home/NotFound/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 5. Routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapRazorPages();

// Optional: Catch-all fallback for routes that don't match anything
app.MapFallbackToController("NotFound", "Home");

app.Run();