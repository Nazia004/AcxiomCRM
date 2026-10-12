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
[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? stage, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = authService.ApplyScope(context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                WeightedPipeline = o.WeightedPipeline,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                CreatedDate = o.CreatedDate,
                AssignedUserName = o.AssignedUser != null ? o.AssignedUser.FullName : null
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var o = await context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.AssignedUser)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (o == null) return NotFound(new { message = $"Opportunity with ID {id} not found." });

        if (!authService.CanAccessOpportunity(User, o)) return Forbid();

        return Ok(new OpportunityDto
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            WeightedPipeline = o.WeightedPipeline,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Status = o.Status,
            CreatedDate = o.CreatedDate,
            AssignedUserName = o.AssignedUser != null ? o.AssignedUser.FullName : null
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (dto.Amount <= 0)
        {
            return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
        }

        if (dto.Probability < 0 || dto.Probability > 100)
        {
            return BadRequest(new { message = "Probability must be between 0 and 100." });
        }

        if (dto.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Expected Close Date cannot be in the past." });
        }

        if (!await context.Customers.AnyAsync(c => c.CustomerId == dto.CustomerId))
        {
            return BadRequest(new { message = "Customer not found." });
        }

        var currentUser = await userManager.GetUserAsync(User);

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = "Open",
            Notes = dto.Notes,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = currentUser?.Id
        };

        if (opp.Stage == OpportunityStage.Won)
        {
            opp.Status = "Won";
            opp.Probability = 100;
        }
        else if (opp.Stage == OpportunityStage.Lost)
        {
            opp.Status = "Lost";
            opp.Probability = 0;
        }

        context.Opportunities.Add(opp);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Create Opportunity", "Opportunity", opp.OpportunityId.ToString(), null, opp.OpportunityName, "Success", "Created via API");

        var responseDto = new OpportunityDto
        {
            OpportunityId = opp.OpportunityId,
            OpportunityName = opp.OpportunityName,
            CustomerId = opp.CustomerId,
            Amount = opp.Amount,
            Stage = opp.Stage,
            Probability = opp.Probability,
            WeightedPipeline = opp.WeightedPipeline,
            ExpectedCloseDate = opp.ExpectedCloseDate,
            Status = opp.Status,
            CreatedDate = opp.CreatedDate,
            AssignedUserName = currentUser?.FullName
        };

        return CreatedAtAction(nameof(GetById), new { id = opp.OpportunityId }, responseDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateOpportunityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var opp = await context.Opportunities.FindAsync(id);
        if (opp == null) return NotFound(new { message = $"Opportunity with ID {id} not found." });

        if (!authService.CanAccessOpportunity(User, opp)) return Forbid();

        if (dto.Amount <= 0) return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
        if (dto.Probability < 0 || dto.Probability > 100) return BadRequest(new { message = "Probability must be between 0 and 100." });

        opp.OpportunityName = dto.OpportunityName;
        opp.CustomerId = dto.CustomerId;
        opp.LeadId = dto.LeadId;
        opp.Amount = dto.Amount;
        opp.Stage = dto.Stage;
        opp.Probability = dto.Probability;
        opp.ExpectedCloseDate = dto.ExpectedCloseDate;
        opp.Notes = dto.Notes;

        if (opp.Stage == OpportunityStage.Won) { opp.Status = "Won"; opp.Probability = 100; }
        else if (opp.Stage == OpportunityStage.Lost) { opp.Status = "Lost"; opp.Probability = 0; }

        context.Opportunities.Update(opp);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Update Opportunity", "Opportunity", id.ToString(), null, opp.OpportunityName, "Success", "Updated via API");

        return Ok(new { message = "Opportunity updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var opp = await context.Opportunities.FindAsync(id);
        if (opp == null) return NotFound(new { message = $"Opportunity with ID {id} not found." });

        if (!authService.CanAccessOpportunity(User, opp)) return Forbid();

        context.Opportunities.Remove(opp);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Delete Opportunity", "Opportunity", id.ToString(), opp.OpportunityName, null, "Success", "Deleted via API");

        return Ok(new { message = "Opportunity deleted successfully." });
    }
}
