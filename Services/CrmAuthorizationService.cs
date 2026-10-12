using System.Security.Claims;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class CrmAuthorizationService : ICrmAuthorizationService
{
    public bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(UserRoles.Admin);

    public bool IsManager(ClaimsPrincipal user) => user.IsInRole(UserRoles.Manager);

    public bool IsSalesExecutive(ClaimsPrincipal user) => user.IsInRole(UserRoles.SalesExecutive);

    public string? GetCurrentUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool CanAccessCustomer(ClaimsPrincipal user, Customer customer)
    {
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetCurrentUserId(user);
        var userName = user.Identity?.Name;
        return customer.OwnerId == userId || customer.CreatedBy == userName || customer.CreatedBy == userId;
    }

    public bool CanAccessLead(ClaimsPrincipal user, Lead lead)
    {
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetCurrentUserId(user);
        return lead.AssignedTo == userId;
    }

    public bool CanAccessOpportunity(ClaimsPrincipal user, Opportunity opportunity)
    {
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetCurrentUserId(user);
        return opportunity.AssignedTo == userId;
    }

    public bool CanAccessFollowUp(ClaimsPrincipal user, FollowUp followUp)
    {
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetCurrentUserId(user);
        return followUp.AssignedTo == userId;
    }

    public bool CanAccessActivity(ClaimsPrincipal user, Activity activity)
    {
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetCurrentUserId(user);
        return activity.AssignedTo == userId;
    }

    public IQueryable<Customer> ApplyScope(IQueryable<Customer> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetCurrentUserId(user);
        var userName = user.Identity?.Name;
        return query.Where(c => c.OwnerId == userId || c.CreatedBy == userName || c.CreatedBy == userId);
    }

    public IQueryable<Lead> ApplyScope(IQueryable<Lead> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetCurrentUserId(user);
        return query.Where(l => l.AssignedTo == userId);
    }

    public IQueryable<Opportunity> ApplyScope(IQueryable<Opportunity> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetCurrentUserId(user);
        return query.Where(o => o.AssignedTo == userId);
    }

    public IQueryable<FollowUp> ApplyScope(IQueryable<FollowUp> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetCurrentUserId(user);
        return query.Where(f => f.AssignedTo == userId);
    }

    public IQueryable<Activity> ApplyScope(IQueryable<Activity> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetCurrentUserId(user);
        return query.Where(a => a.AssignedTo == userId);
    }
}
