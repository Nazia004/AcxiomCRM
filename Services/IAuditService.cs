namespace AcxiomCRM.Services;

public interface IAuditService
{
    Task LogAsync(
        string action, 
        string entityName, 
        string? recordId = null, 
        string? oldValue = null, 
        string? newValue = null, 
        string result = "Success", 
        string? details = null,
        string? customUserId = null,
        string? customUserName = null);
}
