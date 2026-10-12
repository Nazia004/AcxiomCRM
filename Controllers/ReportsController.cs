using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController(
    ApplicationDbContext context,
    ICrmAuthorizationService authService) : Controller
{
    // GET: Reports Overview
    public IActionResult Index()
    {
        return View();
    }

    // 1. Customer Report
    public async Task<IActionResult> Customers(string? status, string? format)
    {
        var query = authService.ApplyScope(context.Customers.Include(c => c.Owner).AsNoTracking(), User);
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var customers = await query.OrderByDescending(c => c.CreatedDate).ToListAsync();

        if (format == "csv")
        {
            var sb = new StringBuilder();
            sb.AppendLine("Code,Customer Name,Email,Phone,Company,Status,Owner,Created Date");
            foreach (var c in customers)
            {
                sb.AppendLine($"\"{c.CustomerCode}\",\"{c.CustomerName}\",\"{c.Email}\",\"{c.Phone}\",\"{c.CompanyName}\",\"{c.Status}\",\"{c.Owner?.FullName ?? c.CreatedBy}\",\"{c.CreatedDate:yyyy-MM-dd}\"");
            }
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"CustomerReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        ViewBag.Status = status;
        return View(customers);
    }

    // 2. Lead Report
    public async Task<IActionResult> Leads(string? status, string? source, string? format)
    {
        var query = authService.ApplyScope(context.Leads.Include(l => l.AssignedUser).Include(l => l.ConvertedCustomer).AsNoTracking(), User);
        if (!string.IsNullOrEmpty(status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrEmpty(source)) query = query.Where(l => l.Source == source);

        var leads = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();

        if (format == "csv")
        {
            var sb = new StringBuilder();
            sb.AppendLine("Code,Lead Name,Email,Phone,Company,Source,Status,Priority,Expected Value,Assigned To,Converted");
            foreach (var l in leads)
            {
                sb.AppendLine($"\"{l.LeadCode}\",\"{l.LeadName}\",\"{l.Email}\",\"{l.Phone}\",\"{l.CompanyName}\",\"{l.Source}\",\"{l.Status}\",\"{l.Priority}\",\"{l.ExpectedValue}\",\"{l.AssignedUser?.FullName}\",\"{l.ConvertedDate != null}\"");
            }
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"LeadReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        ViewBag.Status = status;
        ViewBag.Source = source;
        return View(leads);
    }

    // 3. Opportunity & Pipeline Report
    public async Task<IActionResult> Pipeline(string? stage, string? format)
    {
        var query = authService.ApplyScope(context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsNoTracking(), User);
        if (!string.IsNullOrEmpty(stage)) query = query.Where(o => o.Stage == stage);

        var opps = await query.OrderByDescending(o => o.Amount).ToListAsync();

        if (format == "csv")
        {
            var sb = new StringBuilder();
            sb.AppendLine("Opportunity Name,Customer,Amount,Stage,Probability,Weighted Pipeline,Close Date,Owner");
            foreach (var o in opps)
            {
                sb.AppendLine($"\"{o.OpportunityName}\",\"{o.Customer?.CustomerName}\",\"{o.Amount}\",\"{o.Stage}\",\"{o.Probability}%\",\"{o.WeightedPipeline}\",\"{o.ExpectedCloseDate:yyyy-MM-dd}\",\"{o.AssignedUser?.FullName}\"");
            }
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"PipelineReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        ViewBag.Stage = stage;
        ViewBag.TotalAmount = opps.Sum(o => o.Amount);
        ViewBag.TotalWeighted = opps.Sum(o => o.WeightedPipeline);
        return View(opps);
    }

    // 4. Follow-Up Report
    public async Task<IActionResult> FollowUps(string? status, string? timeframe)
    {
        var query = authService.ApplyScope(context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).Include(f => f.AssignedUser).AsNoTracking(), User);
        if (!string.IsNullOrEmpty(status)) query = query.Where(f => f.Status == status);

        var now = DateTime.UtcNow;
        if (timeframe == "Upcoming") query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= now);
        if (timeframe == "Overdue") query = query.Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < now);

        var followUps = await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        ViewBag.Status = status;
        ViewBag.Timeframe = timeframe;
        return View(followUps);
    }

    // 5. Sales & Conversion Report
    public async Task<IActionResult> Conversions()
    {
        var leadsQuery = authService.ApplyScope(context.Leads.AsNoTracking(), User);
        var oppsQuery = authService.ApplyScope(context.Opportunities.AsNoTracking(), User);

        var totalLeads = await leadsQuery.CountAsync();
        var convertedLeads = await leadsQuery.CountAsync(l => l.Status == LeadStatus.Converted);
        var lostLeads = await leadsQuery.CountAsync(l => l.Status == LeadStatus.Lost);
        var conversionRate = totalLeads > 0 ? (convertedLeads * 100.0 / totalLeads) : 0;

        var wonOpps = await oppsQuery.Where(o => o.Status == "Won" || o.Stage == OpportunityStage.Won).ToListAsync();
        var lostOpps = await oppsQuery.Where(o => o.Status == "Lost" || o.Stage == OpportunityStage.Lost).ToListAsync();
        var wonRevenue = wonOpps.Sum(o => o.Amount);

        ViewBag.TotalLeads = totalLeads;
        ViewBag.ConvertedLeads = convertedLeads;
        ViewBag.LostLeads = lostLeads;
        ViewBag.ConversionRate = Math.Round(conversionRate, 1);
        ViewBag.WonOppsCount = wonOpps.Count;
        ViewBag.LostOppsCount = lostOpps.Count;
        ViewBag.WonRevenue = wonRevenue;

        return View();
    }

    // 6. User Activity Report (Admin/Manager only)
    [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Manager}")]
    public async Task<IActionResult> UserActivity()
    {
        var users = await context.Users.Where(u => u.IsActive).ToListAsync();
        var activityCounts = new List<dynamic>();

        foreach (var u in users)
        {
            var custCount = await context.Customers.CountAsync(c => c.OwnerId == u.Id || c.CreatedBy == u.UserName);
            var leadCount = await context.Leads.CountAsync(l => l.AssignedTo == u.Id);
            var oppCount = await context.Opportunities.CountAsync(o => o.AssignedTo == u.Id);
            var followUpCount = await context.FollowUps.CountAsync(f => f.AssignedTo == u.Id);
            var actCount = await context.Activities.CountAsync(a => a.AssignedTo == u.Id);

            activityCounts.Add(new
            {
                User = u,
                Customers = custCount,
                Leads = leadCount,
                Opportunities = oppCount,
                FollowUps = followUpCount,
                Activities = actCount,
                Total = custCount + leadCount + oppCount + followUpCount + actCount
            });
        }

        return View(activityCounts);
    }
}
