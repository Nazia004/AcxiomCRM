using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsApiController(
    ApplicationDbContext context,
    ICrmAuthorizationService authService) : ControllerBase
{
    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipelineReport()
    {
        var oppsQuery = authService.ApplyScope(context.Opportunities.AsNoTracking(), User);
        var opps = await oppsQuery.ToListAsync();

        var openOpps = opps.Where(o => o.Status == "Open").ToList();
        var stages = OpportunityStage.All;

        var stageSummaries = stages.Select(stg =>
        {
            var stageOpps = opps.Where(o => o.Stage == stg).ToList();
            return new StageSummaryDto
            {
                Stage = stg,
                Count = stageOpps.Count,
                Amount = stageOpps.Sum(o => o.Amount),
                WeightedAmount = stageOpps.Sum(o => o.WeightedPipeline)
            };
        }).ToList();

        var report = new PipelineReportDto
        {
            TotalPipelineValue = openOpps.Sum(o => o.Amount),
            TotalWeightedPipelineValue = openOpps.Sum(o => o.WeightedPipeline),
            Stages = stageSummaries
        };

        return Ok(report);
    }
}
