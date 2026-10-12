using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : Controller
{
    // GET: Customers
    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        var query = authService.ApplyScope(context.Customers.Include(c => c.Owner).AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(s) ||
                c.Email.ToLower().Contains(s) ||
                c.Phone.Contains(s) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(items);
    }

    // GET: Customers/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var customer = await context.Customers
            .Include(c => c.Owner)
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(m => m.CustomerId == id);

        if (customer == null) return NotFound();

        if (!authService.CanAccessCustomer(User, customer))
        {
            return Forbid();
        }

        return View(customer);
    }

    // GET: Customers/Create
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdownAsync();
        return View(new Customer
        {
            CustomerCode = "CUST-" + (await context.Customers.CountAsync() + 1001),
            Status = CustomerStatus.Active
        });
    }

    // POST: Customers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        // Business Validation: Unique Email
        if (await context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower()))
        {
            ModelState.AddModelError("Email", "A customer with this email address already exists.");
        }

        // Business Validation: Unique Phone
        if (await context.Customers.AnyAsync(c => c.Phone == customer.Phone))
        {
            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
        }

        if (ModelState.IsValid)
        {
            var currentUser = await userManager.GetUserAsync(User);
            customer.CreatedDate = DateTime.UtcNow;
            customer.CreatedBy = currentUser?.UserName ?? "System";

            if (authService.IsSalesExecutive(User) || string.IsNullOrEmpty(customer.OwnerId))
            {
                customer.OwnerId = currentUser?.Id;
            }

            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            await auditService.LogAsync("Create Customer", "Customer", customer.CustomerId.ToString(), null, customer.CustomerName, "Success", $"Created customer {customer.CustomerName} ({customer.CustomerCode})");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdownAsync();
        return View(customer);
    }

    // GET: Customers/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var customer = await context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        if (!authService.CanAccessCustomer(User, customer))
        {
            return Forbid();
        }

        await PopulateUsersDropdownAsync();
        return View(customer);
    }

    // POST: Customers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.CustomerId) return NotFound();

        var existing = await context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
        if (existing == null) return NotFound();

        if (!authService.CanAccessCustomer(User, existing))
        {
            return Forbid();
        }

        // Business Validation: Unique Email
        if (await context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower() && c.CustomerId != id))
        {
            ModelState.AddModelError("Email", "Another customer with this email address already exists.");
        }

        // Business Validation: Unique Phone
        if (await context.Customers.AnyAsync(c => c.Phone == customer.Phone && c.CustomerId != id))
        {
            ModelState.AddModelError("Phone", "Another customer with this phone number already exists.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var oldSnapshot = $"{existing.CustomerName}, {existing.Email}, {existing.Phone}, Status:{existing.Status}";
                var newSnapshot = $"{customer.CustomerName}, {customer.Email}, {customer.Phone}, Status:{customer.Status}";

                context.Update(customer);
                await context.SaveChangesAsync();

                await auditService.LogAsync("Update Customer", "Customer", customer.CustomerId.ToString(), oldSnapshot, newSnapshot, "Success", $"Updated customer {customer.CustomerName}");

                TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await context.Customers.AnyAsync(e => e.CustomerId == customer.CustomerId))
                    return NotFound();
                else
                    throw;
            }
        }

        await PopulateUsersDropdownAsync();
        return View(customer);
    }

    // GET: Customers/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var customer = await context.Customers
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(m => m.CustomerId == id);

        if (customer == null) return NotFound();

        if (!authService.CanAccessCustomer(User, customer))
        {
            return Forbid();
        }

        return View(customer);
    }

    // POST: Customers/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        if (!authService.CanAccessCustomer(User, customer))
        {
            return Forbid();
        }

        // Safe soft deactivation or delete
        var name = customer.CustomerName;
        customer.Status = CustomerStatus.Inactive;
        context.Customers.Update(customer);
        await context.SaveChangesAsync();

        await auditService.LogAsync("Deactivate Customer", "Customer", id.ToString(), "Active", "Inactive", "Success", $"Deactivated customer {name}");

        TempData["SuccessMessage"] = $"Customer '{name}' deactivated successfully.";
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
