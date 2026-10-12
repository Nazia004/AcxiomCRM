using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/followups")]
[Authorize]
public class FollowUpsApiController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = authService.ApplyScope(context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.AssignedUser)
            .AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(f => f.FollowUpDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FollowUpDto
            {
                FollowUpId = f.FollowUpId,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                LeadId = f.LeadId,
                LeadName = f.Lead != null ? f.Lead.LeadName : null,
                FollowUpDate = f.FollowUpDate,
                FollowUpType = f.FollowUpType,
                Subject = f.Subject,
                Remarks = f.Remarks,
                Status = f.Status,
                AssignedUserName = f.AssignedUser != null ? f.AssignedUser.FullName : null
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = items });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFollowUpDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (dto.Status == FollowUpStatus.Planned && dto.FollowUpDate.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Follow-up date cannot be earlier than today." });
        }

        var currentUser = await userManager.GetUserAsync(User);

        var followUp = new FollowUp
        {
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            FollowUpDate = dto.FollowUpDate,
            FollowUpType = dto.FollowUpType,
            Subject = dto.Subject,
            Remarks = dto.Remarks,
            Status = dto.Status,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = currentUser?.Id
        };

        context.FollowUps.Add(followUp);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Create FollowUp", "FollowUp", followUp.FollowUpId.ToString(), null, followUp.Subject, "Success", "Created via API");

        var responseDto = new FollowUpDto
        {
            FollowUpId = followUp.FollowUpId,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            FollowUpDate = followUp.FollowUpDate,
            FollowUpType = followUp.FollowUpType,
            Subject = followUp.Subject,
            Remarks = followUp.Remarks,
            Status = followUp.Status,
            AssignedUserName = currentUser?.FullName
        };

        return CreatedAtAction(nameof(GetAll), new { id = followUp.FollowUpId }, responseDto);
    }
}
