using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class OpportunitiesController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : Controller
{
    // GET: Opportunities
    public async Task<IActionResult> Index(string? search, string? stage, string? status, int page = 1)
    {
        var query = authService.ApplyScope(context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.AssignedUser)
            .AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(s) ||
                (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Stage = stage;
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        // Pipeline summary stats for current view
        ViewBag.TotalPipeline = items.Sum(o => o.Amount);
        ViewBag.WeightedPipeline = items.Sum(o => o.WeightedPipeline);

        return View(items);
    }

    // GET: Opportunities/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .FirstOrDefaultAsync(m => m.OpportunityId == id);

        if (opportunity == null) return NotFound();

        if (!authService.CanAccessOpportunity(User, opportunity))
        {
            return Forbid();
        }

        return View(opportunity);
    }

    // GET: Opportunities/Create
    public async Task<IActionResult> Create(int? customerId = null)
    {
        await PopulateDropdownsAsync();
        var opp = new Opportunity
        {
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            Stage = OpportunityStage.Qualification,
            Probability = 20,
            Status = "Open"
        };
        if (customerId.HasValue) opp.CustomerId = customerId.Value;
        return View(opp);
    }

    // POST: Opportunities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        // Business Validation: Amount > 0
        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        }

        // Business Validation: Probability 0 - 100
        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        }

        // Business Validation: Expected Close Date cannot be in the past for an active Opportunity
        if (opportunity.Status != "Won" && opportunity.Status != "Lost" && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        }

        // Validate Customer foreign key
        if (!await context.Customers.AnyAsync(c => c.CustomerId == opportunity.CustomerId))
        {
            ModelState.AddModelError("CustomerId", "The selected Customer does not exist.");
        }

        if (ModelState.IsValid)
        {
            var currentUser = await userManager.GetUserAsync(User);
            opportunity.CreatedDate = DateTime.UtcNow;

            // Sync status with stage
            if (opportunity.Stage == OpportunityStage.Won)
            {
                opportunity.Status = "Won";
                opportunity.Probability = 100;
            }
            else if (opportunity.Stage == OpportunityStage.Lost)
            {
                opportunity.Status = "Lost";
                opportunity.Probability = 0;
            }
            else
            {
                opportunity.Status = "Open";
            }

            if (authService.IsSalesExecutive(User) || string.IsNullOrEmpty(opportunity.AssignedTo))
            {
                opportunity.AssignedTo = currentUser?.Id;
            }

            context.Opportunities.Add(opportunity);
            await context.SaveChangesAsync();

            await auditService.LogAsync("Create Opportunity", "Opportunity", opportunity.OpportunityId.ToString(), null, opportunity.OpportunityName, "Success", $"Created opportunity {opportunity.OpportunityName} with Amount {opportunity.Amount:C} and Stage {opportunity.Stage}");

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(opportunity);
    }

    // GET: Opportunities/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await context.Opportunities.FindAsync(id);
        if (opportunity == null) return NotFound();

        if (!authService.CanAccessOpportunity(User, opportunity))
        {
            return Forbid();
        }

        await PopulateDropdownsAsync();
        return View(opportunity);
    }

    // POST: Opportunities/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity opportunity)
    {
        if (id != opportunity.OpportunityId) return NotFound();

        var existing = await context.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (existing == null) return NotFound();

        if (!authService.CanAccessOpportunity(User, existing))
        {
            return Forbid();
        }

        // Business Validations
        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        }

        if (opportunity.Stage != OpportunityStage.Won && opportunity.Stage != OpportunityStage.Lost && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Sync status with stage
                if (opportunity.Stage == OpportunityStage.Won)
                {
                    opportunity.Status = "Won";
                    opportunity.Probability = 100;
                }
                else if (opportunity.Stage == OpportunityStage.Lost)
                {
                    opportunity.Status = "Lost";
                    opportunity.Probability = 0;
                }
                else
                {
                    opportunity.Status = "Open";
                }

                var oldSnapshot = $"{existing.OpportunityName}, Stage:{existing.Stage}, Amount:{existing.Amount}, Prob:{existing.Probability}";
                var newSnapshot = $"{opportunity.OpportunityName}, Stage:{opportunity.Stage}, Amount:{opportunity.Amount}, Prob:{opportunity.Probability}";

                context.Update(opportunity);
                await context.SaveChangesAsync();

                await auditService.LogAsync("Update Opportunity", "Opportunity", opportunity.OpportunityId.ToString(), oldSnapshot, newSnapshot, "Success", $"Updated opportunity {opportunity.OpportunityName}");

                TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await context.Opportunities.AnyAsync(e => e.OpportunityId == opportunity.OpportunityId))
                    return NotFound();
                else
                    throw;
            }
        }

        await PopulateDropdownsAsync();
        return View(opportunity);
    }

    // GET: Opportunities/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.AssignedUser)
            .FirstOrDefaultAsync(m => m.OpportunityId == id);

        if (opportunity == null) return NotFound();

        if (!authService.CanAccessOpportunity(User, opportunity))
        {
            return Forbid();
        }

        return View(opportunity);
    }

    // POST: Opportunities/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var opportunity = await context.Opportunities.FindAsync(id);
        if (opportunity == null) return NotFound();

        if (!authService.CanAccessOpportunity(User, opportunity))
        {
            return Forbid();
        }

        var name = opportunity.OpportunityName;
        context.Opportunities.Remove(opportunity);
        await context.SaveChangesAsync();

        await auditService.LogAsync("Delete Opportunity", "Opportunity", id.ToString(), name, null, "Success", $"Deleted opportunity {name}");

        TempData["SuccessMessage"] = $"Opportunity '{name}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Customers = await context.Customers
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.CustomerName)
            .ToListAsync();

        ViewBag.Users = await userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }
}
