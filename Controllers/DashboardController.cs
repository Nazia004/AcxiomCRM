using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController(
    ApplicationDbContext context,
    ICrmAuthorizationService authService) : Controller
{
    public async Task<IActionResult> Index(string dateFilter = "All")
    {
        var now = DateTime.UtcNow;
        DateTime? startDate = dateFilter switch
        {
            "Today" => now.Date,
            "ThisWeek" => now.AddDays(-7),
            "ThisMonth" => now.AddDays(-30),
            _ => null
        };

        // Scoped Queries
        var customersQuery = authService.ApplyScope(context.Customers.AsNoTracking(), User);
        var leadsQuery = authService.ApplyScope(context.Leads.AsNoTracking(), User);
        var oppsQuery = authService.ApplyScope(context.Opportunities.Include(o => o.Customer).AsNoTracking(), User);
        var followUpsQuery = authService.ApplyScope(context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).AsNoTracking(), User);

        if (startDate.HasValue)
        {
            customersQuery = customersQuery.Where(c => c.CreatedDate >= startDate.Value);
            leadsQuery = leadsQuery.Where(l => l.CreatedDate >= startDate.Value);
            oppsQuery = oppsQuery.Where(o => o.CreatedDate >= startDate.Value);
            followUpsQuery = followUpsQuery.Where(f => f.CreatedDate >= startDate.Value);
        }

        var customersCount = await customersQuery.CountAsync();
        var leads = await leadsQuery.ToListAsync();
        var opps = await oppsQuery.ToListAsync();
        var followUps = await followUpsQuery.ToListAsync();

        var openLeadsCount = leads.Count(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost);
        var openOpps = opps.Where(o => o.Status == "Open").ToList();
        var wonOpps = opps.Where(o => o.Status == "Won" || o.Stage == OpportunityStage.Won).ToList();
        var lostOpps = opps.Where(o => o.Status == "Lost" || o.Stage == OpportunityStage.Lost).ToList();

        var totalPipeline = openOpps.Sum(o => o.Amount);
        var weightedPipeline = openOpps.Sum(o => o.WeightedPipeline);

        var upcomingFollowUps = followUps.Count(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= now);
        var overdueFollowUps = followUps.Count(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate < now);

        // Lead Status Chart Data
        var leadStatuses = new[] { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Lost, LeadStatus.Converted };
        var leadStatusCounts = new Dictionary<string, int>();
        foreach (var status in leadStatuses)
        {
            leadStatusCounts[status] = leads.Count(l => l.Status == status);
        }

        // Opportunity Pipeline Stages Chart Data
        var stages = new[] { OpportunityStage.Qualification, OpportunityStage.Proposal, OpportunityStage.Negotiation, OpportunityStage.Won, OpportunityStage.Lost };
        var stageAmounts = new Dictionary<string, decimal>();
        foreach (var stg in stages)
        {
            stageAmounts[stg] = opps.Where(o => o.Stage == stg).Sum(o => o.Amount);
        }

        // Monthly Trends (Past 6 months)
        var monthlyTrends = new List<MonthlySalesData>();
        for (int i = 5; i >= 0; i--)
        {
            var monthDate = now.AddMonths(-i);
            var monthName = monthDate.ToString("MMM yyyy");
            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var wonInMonth = opps
                .Where(o => (o.Status == "Won" || o.Stage == OpportunityStage.Won) && o.CreatedDate >= monthStart && o.CreatedDate < monthEnd)
                .Sum(o => o.Amount);

            var pipelineInMonth = opps
                .Where(o => o.Status == "Open" && o.CreatedDate >= monthStart && o.CreatedDate < monthEnd)
                .Sum(o => o.Amount);

            monthlyTrends.Add(new MonthlySalesData
            {
                Month = monthName,
                WonAmount = wonInMonth,
                PipelineAmount = pipelineInMonth
            });
        }

        var model = new DashboardViewModel
        {
            TotalCustomers = customersCount,
            TotalLeads = leads.Count,
            OpenLeads = openLeadsCount,
            TotalOpportunities = opps.Count,
            OpenOpportunities = openOpps.Count,
            WonOpportunities = wonOpps.Count,
            LostOpportunities = lostOpps.Count,
            TotalPipelineValue = totalPipeline,
            TotalWeightedPipelineValue = weightedPipeline,
            UpcomingFollowUps = upcomingFollowUps,
            OverdueFollowUps = overdueFollowUps,
            DateFilter = dateFilter,
            LeadStatusCounts = leadStatusCounts,
            StagePipelineAmounts = stageAmounts,
            MonthlyTrends = monthlyTrends,
            RecentFollowUps = followUps
                .Where(f => f.Status == FollowUpStatus.Planned)
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToList(),
            TopOpportunities = openOpps
                .OrderByDescending(o => o.Amount)
                .Take(5)
                .ToList()
        };

        return View(model);
    }
}
