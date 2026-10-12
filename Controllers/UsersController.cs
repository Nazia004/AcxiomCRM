using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IAuditService auditService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, string? roleFilter, string? statusFilter)
    {
        var users = await userManager.Users.OrderByDescending(u => u.CreatedDate).ToListAsync();
        var userViewModels = new List<UserListViewModel>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "None";
            var isLocked = await userManager.IsLockedOutAsync(user);

            // Filtering
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                if (!user.FullName.ToLower().Contains(term) && !user.Email!.ToLower().Contains(term))
                {
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(roleFilter) && primaryRole != roleFilter)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (statusFilter == "Active" && !user.IsActive) continue;
                if (statusFilter == "Inactive" && user.IsActive) continue;
                if (statusFilter == "Locked" && !isLocked) continue;
            }

            userViewModels.Add(new UserListViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = primaryRole,
                IsActive = user.IsActive,
                IsLockedOut = isLocked,
                LockoutEnd = user.LockoutEnd,
                CreatedDate = user.CreatedDate
            });
        }

        ViewBag.SearchTerm = searchTerm;
        ViewBag.RoleFilter = roleFilter;
        ViewBag.StatusFilter = statusFilter;

        return View(userViewModels);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "A user with this email address already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            IsActive = model.IsActive,
            CreatedDate = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            if (await roleManager.RoleExistsAsync(model.Role))
            {
                await userManager.AddToRoleAsync(user, model.Role);
            }

            await auditService.LogAsync("Create User", "UserManagement", user.Id, null, $"{user.Email} ({model.Role})", "Success", $"Administrator created user account for {user.Email} with role {model.Role}");
            TempData["SuccessMessage"] = $"User {user.Email} created successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? UserRoles.SalesExecutive;

        var model = new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = primaryRole,
            IsActive = user.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        var currentRoles = await userManager.GetRolesAsync(user);
        var oldRole = currentRoles.FirstOrDefault() ?? "None";
        var oldStatus = user.IsActive ? "Active" : "Inactive";

        user.FullName = model.FullName;
        user.IsActive = model.IsActive;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        if (oldRole != model.Role)
        {
            if (currentRoles.Any())
            {
                await userManager.RemoveFromRolesAsync(user, currentRoles);
            }
            if (await roleManager.RoleExistsAsync(model.Role))
            {
                await userManager.AddToRoleAsync(user, model.Role);
            }

            await auditService.LogAsync("Role Change", "UserManagement", user.Id, oldRole, model.Role, "Success", $"Admin modified role for {user.Email} from {oldRole} to {model.Role}");
        }

        var newStatus = user.IsActive ? "Active" : "Inactive";
        if (oldStatus != newStatus)
        {
            await auditService.LogAsync("Status Change", "UserManagement", user.Id, oldStatus, newStatus, "Success", $"Admin changed status for {user.Email} to {newStatus}");
        }

        TempData["SuccessMessage"] = $"User {user.Email} updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var currentUserId = userManager.GetUserId(User);
        if (user.Id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own administrative account.";
            return RedirectToAction(nameof(Index));
        }

        var oldStatus = user.IsActive ? "Active" : "Inactive";
        user.IsActive = !user.IsActive;
        var newStatus = user.IsActive ? "Active" : "Inactive";

        await userManager.UpdateAsync(user);
        await auditService.LogAsync("Toggle Status", "UserManagement", user.Id, oldStatus, newStatus, "Success", $"Admin toggled active status for {user.Email} to {newStatus}");

        TempData["SuccessMessage"] = $"User {user.Email} is now {newStatus}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        await auditService.LogAsync("Unlock Account", "UserManagement", user.Id, "Locked", "Unlocked", "Success", $"Admin unlocked account for {user.Email}");

        TempData["SuccessMessage"] = $"Account {user.Email} unlocked successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        return View(new ResetPasswordViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByIdAsync(model.UserId);
        if (user == null) return NotFound();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (result.Succeeded)
        {
            await auditService.LogAsync("Password Reset", "UserManagement", user.Id, null, null, "Success", $"Admin reset password for user {user.Email}");
            TempData["SuccessMessage"] = $"Password for {user.Email} has been reset successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }
}
