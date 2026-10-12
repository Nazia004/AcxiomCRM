using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class ConvertLeadViewModel
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;

    [Display(Name = "Lead / Contact Name")]
    public string LeadName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Display(Name = "City")]
    public string? City { get; set; }

    [Display(Name = "State")]
    public string? State { get; set; }

    [Display(Name = "Create Related Opportunity")]
    public bool CreateOpportunity { get; set; } = true;

    [Display(Name = "Opportunity Name")]
    public string? OpportunityName { get; set; }

    [Display(Name = "Opportunity Amount ($)")]
    [Range(0, 100000000, ErrorMessage = "Opportunity Amount must be positive.")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);
}
