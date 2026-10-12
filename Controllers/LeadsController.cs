using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers;

[Authorize]
public class LeadsController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : Controller
{
    // GET: Leads
    public async Task<IActionResult> Index(string? search, string? status, string? priority, string? source, int page = 1)
    {
        var query = authService.ApplyScope(context.Leads.Include(l => l.AssignedUser).AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(s) ||
                l.Email.ToLower().Contains(s) ||
                l.Phone.Contains(s) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(l => l.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(l => l.Source == source);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.Source = source;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(items);
    }

    // GET: Leads/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var lead = await context.Leads
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(m => m.LeadId == id);

        if (lead == null) return NotFound();

        if (!authService.CanAccessLead(User, lead))
        {
            return Forbid();
        }

        return View(lead);
    }

    // GET: Leads/Create
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdownAsync();
        return View(new Lead
        {
            LeadCode = "LEAD-" + (await context.Leads.CountAsync() + 1001),
            Status = LeadStatus.New,
            Priority = "Medium"
        });
    }

    // POST: Leads/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        // Business Validation: Status Validity
        if (!LeadStatus.All.Contains(lead.Status))
        {
            ModelState.AddModelError("Status", "Invalid lead status provided.");
        }

        // Business Validation: Expected Value >= 0
        if (lead.ExpectedValue < 0)
        {
            ModelState.AddModelError("ExpectedValue", "Expected Value must be 0 or greater.");
        }

        if (ModelState.IsValid)
        {
            var currentUser = await userManager.GetUserAsync(User);
            lead.CreatedDate = DateTime.UtcNow;

            if (authService.IsSalesExecutive(User) || string.IsNullOrEmpty(lead.AssignedTo))
            {
                lead.AssignedTo = currentUser?.Id;
            }

            context.Leads.Add(lead);
            await context.SaveChangesAsync();

            await auditService.LogAsync("Create Lead", "Lead", lead.LeadId.ToString(), null, lead.LeadName, "Success", $"Created lead {lead.LeadName} ({lead.LeadCode}) with status {lead.Status}");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdownAsync();
        return View(lead);
    }

    // GET: Leads/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var lead = await context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (!authService.CanAccessLead(User, lead))
        {
            return Forbid();
        }

        await PopulateUsersDropdownAsync();
        return View(lead);
    }

    // POST: Leads/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.LeadId) return NotFound();

        var existing = await context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == id);
        if (existing == null) return NotFound();

        if (!authService.CanAccessLead(User, existing))
        {
            return Forbid();
        }

        if (!LeadStatus.All.Contains(lead.Status))
        {
            ModelState.AddModelError("Status", "Invalid lead status provided.");
        }

        if (lead.ExpectedValue < 0)
        {
            ModelState.AddModelError("ExpectedValue", "Expected Value must be 0 or greater.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var oldSnapshot = $"{existing.LeadName}, Status:{existing.Status}, Assigned:{existing.AssignedTo}";
                var newSnapshot = $"{lead.LeadName}, Status:{lead.Status}, Assigned:{lead.AssignedTo}";

                context.Update(lead);
                await context.SaveChangesAsync();

                await auditService.LogAsync("Update Lead", "Lead", lead.LeadId.ToString(), oldSnapshot, newSnapshot, "Success", $"Updated lead {lead.LeadName}");

                TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await context.Leads.AnyAsync(e => e.LeadId == lead.LeadId))
                    return NotFound();
                else
                    throw;
            }
        }

        await PopulateUsersDropdownAsync();
        return View(lead);
    }

    // GET: Leads/Convert/5
    public async Task<IActionResult> Convert(int? id)
    {
        if (id == null) return NotFound();

        var lead = await context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (!authService.CanAccessLead(User, lead))
        {
            return Forbid();
        }

        if (lead.Status == LeadStatus.Converted)
        {
            TempData["ErrorMessage"] = $"Lead '{lead.LeadName}' is already converted. Duplicate conversion is prevented.";
            return RedirectToAction(nameof(Details), new { id = lead.LeadId });
        }

        var model = new ConvertLeadViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            OpportunityName = $"{lead.CompanyName ?? lead.LeadName} Opportunity",
            Amount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 10000m,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        };

        return View(model);
    }

    // POST: Leads/Convert/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(ConvertLeadViewModel model)
    {
        var lead = await context.Leads.FindAsync(model.LeadId);
        if (lead == null) return NotFound();

        if (!authService.CanAccessLead(User, lead))
        {
            return Forbid();
        }

        // Prevent duplicate conversion
        if (lead.Status == LeadStatus.Converted)
        {
            TempData["ErrorMessage"] = $"Lead '{lead.LeadName}' has already been converted. Duplicate conversion is prevented.";
            return RedirectToAction(nameof(Details), new { id = lead.LeadId });
        }

        if (model.CreateOpportunity && model.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0 for active opportunities.");
        }

        if (ModelState.IsValid)
        {
            var currentUser = await userManager.GetUserAsync(User);
            var customerCount = await context.Customers.CountAsync();

            // 1. Create Customer or find existing by email
            var customer = await context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == model.Email.ToLower());
            if (customer == null)
            {
                customer = new Customer
                {
                    CustomerCode = $"CUST-{customerCount + 1001}",
                    CustomerName = model.LeadName,
                    Email = model.Email,
                    Phone = model.Phone,
                    CompanyName = model.CompanyName,
                    Address = model.Address,
                    City = model.City,
                    State = model.State,
                    Status = CustomerStatus.Active,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = currentUser?.UserName ?? "LeadConversion",
                    OwnerId = lead.AssignedTo ?? currentUser?.Id,
                    Notes = $"Converted from lead {lead.LeadCode}."
                };
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
            }

            Opportunity? opp = null;
            if (model.CreateOpportunity)
            {
                opp = new Opportunity
                {
                    OpportunityName = string.IsNullOrWhiteSpace(model.OpportunityName) ? $"{customer.CustomerName} Deal" : model.OpportunityName,
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.Amount,
                    Stage = OpportunityStage.Qualification,
                    Probability = 20,
                    ExpectedCloseDate = model.ExpectedCloseDate,
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow,
                    AssignedTo = lead.AssignedTo ?? currentUser?.Id,
                    Notes = $"Created via conversion of lead {lead.LeadCode}."
                };
                context.Opportunities.Add(opp);
                await context.SaveChangesAsync();
            }

            // 2. Mark Lead as Converted
            lead.Status = LeadStatus.Converted;
            lead.ConvertedCustomerId = customer.CustomerId;
            lead.ConvertedOpportunityId = opp?.OpportunityId;
            lead.ConvertedDate = DateTime.UtcNow;
            context.Leads.Update(lead);
            await context.SaveChangesAsync();

            // 3. Audit conversion
            await auditService.LogAsync(
                "Lead Conversion",
                "Lead",
                lead.LeadId.ToString(),
                $"Status: {LeadStatus.Qualified}",
                $"Status: {LeadStatus.Converted}, CustomerId: {customer.CustomerId}, OppId: {opp?.OpportunityId}",
                "Success",
                $"Lead '{lead.LeadName}' successfully converted to Customer '{customer.CustomerName}' (Code: {customer.CustomerCode}) and Opportunity '{opp?.OpportunityName}'");

            TempData["SuccessMessage"] = $"Lead successfully converted! Customer '{customer.CustomerName}' created.";
            return RedirectToAction(nameof(Details), new { id = lead.LeadId });
        }

        return View(model);
    }

    // GET: Leads/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var lead = await context.Leads
            .Include(l => l.AssignedUser)
            .FirstOrDefaultAsync(m => m.LeadId == id);

        if (lead == null) return NotFound();

        if (!authService.CanAccessLead(User, lead))
        {
            return Forbid();
        }

        return View(lead);
    }

    // POST: Leads/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lead = await context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (!authService.CanAccessLead(User, lead))
        {
            return Forbid();
        }

        var name = lead.LeadName;
        context.Leads.Remove(lead);
        await context.SaveChangesAsync();

        await auditService.LogAsync("Delete Lead", "Lead", id.ToString(), name, null, "Success", $"Deleted lead {name}");

        TempData["SuccessMessage"] = $"Lead '{name}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUsersDropdownAsync()
    {
        var users = await userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
        ViewBag.Users = users;
    }
}
