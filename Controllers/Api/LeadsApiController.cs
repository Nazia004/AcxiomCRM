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
[Route("api/leads")]
[Authorize]
public class LeadsApiController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = authService.ApplyScope(context.Leads.Include(l => l.AssignedUser).AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l => l.LeadName.ToLower().Contains(s) || l.Email.ToLower().Contains(s) || l.Phone.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LeadDto
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                CreatedDate = l.CreatedDate,
                AssignedUserName = l.AssignedUser != null ? l.AssignedUser.FullName : null
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var l = await context.Leads.Include(l => l.AssignedUser).FirstOrDefaultAsync(l => l.LeadId == id);
        if (l == null) return NotFound(new { message = $"Lead with ID {id} not found." });

        if (!authService.CanAccessLead(User, l))
        {
            return Forbid();
        }

        return Ok(new LeadDto
        {
            LeadId = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            CreatedDate = l.CreatedDate,
            AssignedUserName = l.AssignedUser != null ? l.AssignedUser.FullName : null
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!LeadStatus.All.Contains(dto.Status))
        {
            return BadRequest(new { message = "Invalid lead status." });
        }

        var currentUser = await userManager.GetUserAsync(User);
        var count = await context.Leads.CountAsync();

        var lead = new Lead
        {
            LeadCode = $"LEAD-{count + 1001}",
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source,
            Status = dto.Status,
            Priority = dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            Notes = dto.Notes,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = currentUser?.Id
        };

        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Create Lead", "Lead", lead.LeadId.ToString(), null, lead.LeadName, "Success", "Created via API");

        var responseDto = new LeadDto
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            CreatedDate = lead.CreatedDate,
            AssignedUserName = currentUser?.FullName
        };

        return CreatedAtAction(nameof(GetById), new { id = lead.LeadId }, responseDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var lead = await context.Leads.FindAsync(id);
        if (lead == null) return NotFound(new { message = $"Lead with ID {id} not found." });

        if (!authService.CanAccessLead(User, lead)) return Forbid();

        lead.LeadName = dto.LeadName;
        lead.Email = dto.Email;
        lead.Phone = dto.Phone;
        lead.CompanyName = dto.CompanyName;
        lead.Source = dto.Source;
        lead.Status = dto.Status;
        lead.Priority = dto.Priority;
        lead.ExpectedValue = dto.ExpectedValue;
        lead.Notes = dto.Notes;

        context.Leads.Update(lead);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Update Lead", "Lead", id.ToString(), null, lead.LeadName, "Success", "Updated via API");

        return Ok(new { message = "Lead updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await context.Leads.FindAsync(id);
        if (lead == null) return NotFound(new { message = $"Lead with ID {id} not found." });

        if (!authService.CanAccessLead(User, lead)) return Forbid();

        context.Leads.Remove(lead);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Delete Lead", "Lead", id.ToString(), lead.LeadName, null, "Success", "Deleted via API");

        return Ok(new { message = "Lead deleted successfully." });
    }
}
