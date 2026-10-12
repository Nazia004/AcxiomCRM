using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;

namespace AcxiomCRM.Services;

public class AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuditService> logger) : IAuditService
{
    public async Task LogAsync(
        string action,
        string entityName,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        string result = "Success",
        string? details = null,
        string? customUserId = null,
        string? customUserName = null)
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            var userId = customUserId ?? httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = customUserName ?? httpContext?.User.Identity?.Name ?? "System";
            var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();

            var log = new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = oldValue,
                NewValue = newValue,
                CreatedDate = DateTime.UtcNow,
                IpAddress = ipAddress,
                Result = result,
                Details = details
            };

            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write audit log for Action={Action}, Entity={EntityName}", action, entityName);
        }
    }
}
