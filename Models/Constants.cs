namespace AcxiomCRM.Models;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";
}

public static class CustomerStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}

public static class LeadStatus
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string Unqualified = "Unqualified";
    public const string Converted = "Converted";
    public const string Lost = "Lost";

    public static readonly string[] All = [New, Contacted, Qualified, Unqualified, Converted, Lost];
}

public static class OpportunityStage
{
    public const string Qualification = "Qualification";
    public const string Proposal = "Proposal";
    public const string Negotiation = "Negotiation";
    public const string Won = "Won";
    public const string Lost = "Lost";

    public static readonly string[] All = [Qualification, Proposal, Negotiation, Won, Lost];
}

public static class FollowUpStatus
{
    public const string Planned = "Planned";
    public const string Completed = "Completed";
    public const string Missed = "Missed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Planned, Completed, Missed, Cancelled];
}

public static class ActivityTypes
{
    public const string Call = "Call";
    public const string Meeting = "Meeting";
    public const string Email = "Email";
    public const string Task = "Task";

    public static readonly string[] All = [Call, Meeting, Email, Task];
}
