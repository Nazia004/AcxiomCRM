using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Manager}")]
public class AuditLogsController(ApplicationDbContext context) : Controller
{
    // GET: AuditLogs
    public async Task<IActionResult> Index(string? entityFilter, string? actionFilter, string? userFilter, string? timeframe, int page = 1)
    {
        var query = context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityFilter))
        {
            query = query.Where(a => a.EntityName == entityFilter);
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action.Contains(actionFilter));
        }

        if (!string.IsNullOrWhiteSpace(userFilter))
        {
            query = query.Where(a => (a.UserName != null && a.UserName.Contains(userFilter)) || (a.UserId != null && a.UserId.Contains(userFilter)));
        }

        var now = DateTime.UtcNow;
        if (timeframe == "Today")
        {
            var today = now.Date;
            query = query.Where(a => a.CreatedDate >= today);
        }
        else if (timeframe == "ThisWeek")
        {
            var weekAgo = now.AddDays(-7);
            query = query.Where(a => a.CreatedDate >= weekAgo);
        }
        else if (timeframe == "ThisMonth")
        {
            var monthAgo = now.AddDays(-30);
            query = query.Where(a => a.CreatedDate >= monthAgo);
        }

        const int pageSize = 20;
        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.EntityFilter = entityFilter;
        ViewBag.ActionFilter = actionFilter;
        ViewBag.UserFilter = userFilter;
        ViewBag.Timeframe = timeframe;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(items);
    }
}
