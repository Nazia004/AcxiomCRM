using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuditService auditService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            await auditService.LogAsync("API Failed Login", "AuthAPI", null, null, null, "Failed", $"Invalid API login attempt for {request.Email}");
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!user.IsActive)
        {
            await auditService.LogAsync("API Deactivated Login Attempt", "AuthAPI", user.Id, null, null, "Failed", $"Deactivated user {request.Email} attempted API login");
            return StatusCode(403, new { message = "Account is deactivated." });
        }

        var result = await signInManager.PasswordSignInAsync(user.UserName!, request.Password, false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var roles = await userManager.GetRolesAsync(user);
            await auditService.LogAsync("API Login", "AuthAPI", user.Id, null, null, "Success", $"API login for {user.Email}");

            return Ok(new AuthResponseDto
            {
                Success = true,
                Message = "Authentication successful.",
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = roles.FirstOrDefault() ?? "None"
            });
        }

        if (result.IsLockedOut)
        {
            await auditService.LogAsync("API Account Lockout", "AuthAPI", user.Id, null, null, "Warning", $"API user {request.Email} locked out");
            return StatusCode(423, new { message = "Account is locked out due to multiple failed login attempts." });
        }

        await auditService.LogAsync("API Failed Login", "AuthAPI", user.Id, null, null, "Failed", $"Invalid password for {request.Email}");
        return Unauthorized(new { message = "Invalid email or password." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok(new { message = "Logged out successfully." });
    }
}
