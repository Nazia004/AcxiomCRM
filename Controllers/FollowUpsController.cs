using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : Controller
{
    // GET: FollowUps
    public async Task<IActionResult> Index(string? status, string? type, string? timeframe, int page = 1)
    {
        var query = authService.ApplyScope(context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.AssignedUser)
            .AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(f => f.FollowUpType == type);
        }

        var now = DateTime.UtcNow;
        if (timeframe == "Upcoming")
        {
            query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= now);
        }
        else if (timeframe == "Overdue")
        {
            query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < now);
        }
        else if (timeframe == "Today")
        {
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            query = query.Where(f => f.FollowUpDate >= today && f.FollowUpDate < tomorrow);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var items = await query
            .OrderBy(f => f.FollowUpDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Type = type;
        ViewBag.Timeframe = timeframe;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(items);
    }

    // GET: FollowUps/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var followUp = await context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.AssignedUser)
            .FirstOrDefaultAsync(m => m.FollowUpId == id);

        if (followUp == null) return NotFound();

        if (!authService.CanAccessFollowUp(User, followUp))
        {
            return Forbid();
        }

        return View(followUp);
    }

    // GET: FollowUps/Create
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        await PopulateDropdownsAsync();
        var followUp = new FollowUp
        {
            FollowUpDate = DateTime.UtcNow.AddDays(1).Date.AddHours(10),
            Status = FollowUpStatus.Planned,
            FollowUpType = "Call"
        };
        if (customerId.HasValue) followUp.CustomerId = customerId.Value;
        if (leadId.HasValue) followUp.LeadId = leadId.Value;
        return View(followUp);
    }

    // POST: FollowUps/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        // Business Validation: Follow-up date cannot be earlier than today for new/planned follow-up
        if (followUp.Status == FollowUpStatus.Planned && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
        }

        if (ModelState.IsValid)
        {
            var currentUser = await userManager.GetUserAsync(User);
            followUp.CreatedDate = DateTime.UtcNow;

            if (authService.IsSalesExecutive(User) || string.IsNullOrEmpty(followUp.AssignedTo))
            {
                followUp.AssignedTo = currentUser?.Id;
            }

            context.FollowUps.Add(followUp);
            await context.SaveChangesAsync();

            await auditService.LogAsync("Create FollowUp", "FollowUp", followUp.FollowUpId.ToString(), null, followUp.Subject, "Success", $"Created {followUp.FollowUpType} follow-up scheduled for {followUp.FollowUpDate:g}");

            TempData["SuccessMessage"] = "Follow-up scheduled successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(followUp);
    }

    // GET: FollowUps/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var followUp = await context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        if (!authService.CanAccessFollowUp(User, followUp))
        {
            return Forbid();
        }

        await PopulateDropdownsAsync();
        return View(followUp);
    }

    // POST: FollowUps/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUp followUp)
    {
        if (id != followUp.FollowUpId) return NotFound();

        var existing = await context.FollowUps.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing == null) return NotFound();

        if (!authService.CanAccessFollowUp(User, existing))
        {
            return Forbid();
        }

        // Validate follow-up date if changing planned date to the past
        if (followUp.Status == FollowUpStatus.Planned && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var oldSnapshot = $"{existing.Subject}, Date:{existing.FollowUpDate:g}, Status:{existing.Status}";
                var newSnapshot = $"{followUp.Subject}, Date:{followUp.FollowUpDate:g}, Status:{followUp.Status}";

                context.Update(followUp);
                await context.SaveChangesAsync();

                var isRescheduled = existing.FollowUpDate != followUp.FollowUpDate;
                var action = isRescheduled ? "Reschedule FollowUp" : "Update FollowUp";
                await auditService.LogAsync(action, "FollowUp", followUp.FollowUpId.ToString(), oldSnapshot, newSnapshot, "Success", $"Updated follow-up '{followUp.Subject}'");

                TempData["SuccessMessage"] = "Follow-up updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await context.FollowUps.AnyAsync(e => e.FollowUpId == followUp.FollowUpId))
                    return NotFound();
                else
                    throw;
            }
        }

        await PopulateDropdownsAsync();
        return View(followUp);
    }

    // POST: FollowUps/Complete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? completionRemarks)
    {
        var followUp = await context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        if (!authService.CanAccessFollowUp(User, followUp))
        {
            return Forbid();
        }

        var oldStatus = followUp.Status;
        followUp.Status = FollowUpStatus.Completed;
        if (!string.IsNullOrWhiteSpace(completionRemarks))
        {
            followUp.Remarks = (followUp.Remarks + "\n[Completed]: " + completionRemarks).Trim();
        }

        // Also add an Activity for the completed follow-up
        var activity = new Activity
        {
            ActivityType = followUp.FollowUpType,
            Subject = $"Completed Follow-Up: {followUp.Subject}",
            Description = followUp.Remarks,
            ActivityDate = DateTime.UtcNow,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            AssignedTo = followUp.AssignedTo,
            Status = "Completed",
            CreatedDate = DateTime.UtcNow
        };
        context.Activities.Add(activity);

        context.FollowUps.Update(followUp);
        await context.SaveChangesAsync();

        await auditService.LogAsync("Complete FollowUp", "FollowUp", id.ToString(), oldStatus, FollowUpStatus.Completed, "Success", $"Completed follow-up '{followUp.Subject}'");

        TempData["SuccessMessage"] = "Follow-up marked as Completed and logged to activities.";
        return RedirectToAction(nameof(Index));
    }

    // POST: FollowUps/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var followUp = await context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        if (!authService.CanAccessFollowUp(User, followUp))
        {
            return Forbid();
        }

        var oldStatus = followUp.Status;
        followUp.Status = FollowUpStatus.Cancelled;
        context.FollowUps.Update(followUp);
        await context.SaveChangesAsync();

        await auditService.LogAsync("Cancel FollowUp", "FollowUp", id.ToString(), oldStatus, FollowUpStatus.Cancelled, "Success", $"Cancelled follow-up '{followUp.Subject}'");

        TempData["SuccessMessage"] = "Follow-up marked as Cancelled.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Customers = await context.Customers
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.CustomerName)
            .ToListAsync();

        ViewBag.Leads = await context.Leads
            .Where(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost)
            .OrderBy(l => l.LeadName)
            .ToListAsync();

        ViewBag.Users = await userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }
}
