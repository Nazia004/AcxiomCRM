using System.Security.Claims;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public interface ICrmAuthorizationService
{
    bool IsAdmin(ClaimsPrincipal user);
    bool IsManager(ClaimsPrincipal user);
    bool IsSalesExecutive(ClaimsPrincipal user);
    string? GetCurrentUserId(ClaimsPrincipal user);

    bool CanAccessCustomer(ClaimsPrincipal user, Customer customer);
    bool CanAccessLead(ClaimsPrincipal user, Lead lead);
    bool CanAccessOpportunity(ClaimsPrincipal user, Opportunity opportunity);
    bool CanAccessFollowUp(ClaimsPrincipal user, FollowUp followUp);
    bool CanAccessActivity(ClaimsPrincipal user, Activity activity);

    IQueryable<Customer> ApplyScope(IQueryable<Customer> query, ClaimsPrincipal user);
    IQueryable<Lead> ApplyScope(IQueryable<Lead> query, ClaimsPrincipal user);
    IQueryable<Opportunity> ApplyScope(IQueryable<Opportunity> query, ClaimsPrincipal user);
    IQueryable<FollowUp> ApplyScope(IQueryable<FollowUp> query, ClaimsPrincipal user);
    IQueryable<Activity> ApplyScope(IQueryable<Activity> query, ClaimsPrincipal user);
}
