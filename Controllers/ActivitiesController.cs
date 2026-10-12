using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : Controller
{
    // GET: Activities
    public async Task<IActionResult> Index(string? type, string? status, int page = 1)
    {
        var query = authService.ApplyScope(context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(a => a.ActivityType == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.ActivityDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(items);
    }

    // GET: Activities/Create
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        await PopulateDropdownsAsync();
        var act = new Activity
        {
            ActivityDate = DateTime.UtcNow,
            ActivityType = ActivityTypes.Call,
            Status = "Completed"
        };
        if (customerId.HasValue) act.CustomerId = customerId.Value;
        if (leadId.HasValue) act.LeadId = leadId.Value;
        return View(act);
    }

    // POST: Activities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        if (ModelState.IsValid)
        {
            var currentUser = await userManager.GetUserAsync(User);
            activity.CreatedDate = DateTime.UtcNow;

            if (authService.IsSalesExecutive(User) || string.IsNullOrEmpty(activity.AssignedTo))
            {
                activity.AssignedTo = currentUser?.Id;
            }

            context.Activities.Add(activity);
            await context.SaveChangesAsync();

            await auditService.LogAsync("Create Activity", "Activity", activity.ActivityId.ToString(), null, activity.Subject, "Success", $"Logged {activity.ActivityType} activity: {activity.Subject}");

            TempData["SuccessMessage"] = "Activity logged successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(activity);
    }

    // GET: Activities/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var activity = await context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(m => m.ActivityId == id);

        if (activity == null) return NotFound();

        if (!authService.CanAccessActivity(User, activity))
        {
            return Forbid();
        }

        return View(activity);
    }

    // GET: Activities/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var activity = await context.Activities.FindAsync(id);
        if (activity == null) return NotFound();

        if (!authService.CanAccessActivity(User, activity))
        {
            return Forbid();
        }

        await PopulateDropdownsAsync();
        return View(activity);
    }

    // POST: Activities/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Activity activity)
    {
        if (id != activity.ActivityId) return NotFound();

        var existing = await context.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.ActivityId == id);
        if (existing == null) return NotFound();

        if (!authService.CanAccessActivity(User, existing))
        {
            return Forbid();
        }

        if (ModelState.IsValid)
        {
            context.Activities.Update(activity);
            await context.SaveChangesAsync();

            await auditService.LogAsync("Update Activity", "Activity", activity.ActivityId.ToString(), existing.Subject, activity.Subject, "Success", $"Updated {activity.ActivityType} activity");

            TempData["SuccessMessage"] = "Activity updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(activity);
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Customers = await context.Customers
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.CustomerName)
            .ToListAsync();

        ViewBag.Leads = await context.Leads
            .OrderBy(l => l.LeadName)
            .ToListAsync();

        ViewBag.Users = await userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }
}
