using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers;

public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuditService auditService,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            await auditService.LogAsync("Failed Login", "Authentication", null, null, null, "Failed", $"Attempted login for unknown email: {model.Email}", null, model.Email);
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        if (!user.IsActive)
        {
            await auditService.LogAsync("Deactivated Login Attempt", "Authentication", user.Id, null, null, "Failed", $"Deactivated user {model.Email} attempted login", user.Id, user.UserName);
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact your system administrator.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await auditService.LogAsync("Login", "Authentication", user.Id, null, null, "Success", $"User {user.Email} successfully logged in", user.Id, user.UserName);
            logger.LogInformation("User {Email} logged in successfully.", model.Email);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            await auditService.LogAsync("Account Lockout", "Authentication", user.Id, null, null, "Warning", $"User account {model.Email} is locked out due to repeated failed logins", user.Id, user.UserName);
            logger.LogWarning("User {Email} account locked out.", model.Email);
            return View("Lockout");
        }

        await auditService.LogAsync("Failed Login", "Authentication", user.Id, null, null, "Failed", $"Invalid password attempt for {model.Email}", user.Id, user.UserName);
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            // Default role is always SalesExecutive for public self-registration
            await userManager.AddToRoleAsync(user, UserRoles.SalesExecutive);

            await auditService.LogAsync("User Registered", "Authentication", user.Id, null, null, "Success", $"Public user registration: {user.Email} as {UserRoles.SalesExecutive}", user.Id, user.UserName);

            await signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userName = User.Identity?.Name;
        var userId = userManager.GetUserId(User);

        await signInManager.SignOutAsync();
        await auditService.LogAsync("Logout", "Authentication", userId, null, null, "Success", $"User {userName} logged out", userId, userName);

        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
