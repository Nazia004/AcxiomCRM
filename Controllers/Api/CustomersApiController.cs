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
[Route("api/customers")]
[Authorize]
public class CustomersApiController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ICrmAuthorizationService authService,
    IAuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = authService.ApplyScope(context.Customers.Include(c => c.Owner).AsNoTracking(), User);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.CustomerName.ToLower().Contains(s) || c.Email.ToLower().Contains(s) || c.Phone.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                OwnerName = c.Owner != null ? c.Owner.FullName : c.CreatedBy
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await context.Customers.Include(c => c.Owner).FirstOrDefaultAsync(c => c.CustomerId == id);
        if (c == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        if (!authService.CanAccessCustomer(User, c))
        {
            return Forbid();
        }

        return Ok(new CustomerDto
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            CreatedDate = c.CreatedDate,
            OwnerName = c.Owner != null ? c.Owner.FullName : c.CreatedBy
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Duplicate checks
        if (await context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
        {
            return Conflict(new { message = "A customer with this email already exists." });
        }

        if (await context.Customers.AnyAsync(c => c.Phone == dto.Phone))
        {
            return Conflict(new { message = "A customer with this phone number already exists." });
        }

        var currentUser = await userManager.GetUserAsync(User);
        var count = await context.Customers.CountAsync();

        var customer = new Customer
        {
            CustomerCode = $"CUST-{count + 1001}",
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = dto.Status,
            Notes = dto.Notes,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = currentUser?.UserName ?? "API",
            OwnerId = currentUser?.Id
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Create Customer", "Customer", customer.CustomerId.ToString(), null, customer.CustomerName, "Success", $"Created via API");

        var responseDto = new CustomerDto
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate,
            OwnerName = currentUser?.FullName ?? customer.CreatedBy
        };

        return CreatedAtAction(nameof(GetById), new { id = customer.CustomerId }, responseDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var customer = await context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        if (!authService.CanAccessCustomer(User, customer))
        {
            return Forbid();
        }

        if (await context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower() && c.CustomerId != id))
        {
            return Conflict(new { message = "Another customer with this email already exists." });
        }

        if (await context.Customers.AnyAsync(c => c.Phone == dto.Phone && c.CustomerId != id))
        {
            return Conflict(new { message = "Another customer with this phone number already exists." });
        }

        customer.CustomerName = dto.CustomerName;
        customer.Email = dto.Email;
        customer.Phone = dto.Phone;
        customer.CompanyName = dto.CompanyName;
        customer.Address = dto.Address;
        customer.City = dto.City;
        customer.State = dto.State;
        customer.Status = dto.Status;
        customer.Notes = dto.Notes;

        context.Customers.Update(customer);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Update Customer", "Customer", id.ToString(), null, customer.CustomerName, "Success", "Updated via API");

        return Ok(new { message = "Customer updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        if (!authService.CanAccessCustomer(User, customer))
        {
            return Forbid();
        }

        customer.Status = CustomerStatus.Inactive;
        context.Customers.Update(customer);
        await context.SaveChangesAsync();

        await auditService.LogAsync("API Deactivate Customer", "Customer", id.ToString(), "Active", "Inactive", "Success", "Deactivated via API");

        return Ok(new { message = "Customer deactivated successfully." });
    }
}
