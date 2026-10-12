namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal TotalWeightedPipelineValue { get; set; }
    public int UpcomingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }

    public string DateFilter { get; set; } = "All";

    // Chart Data
    public Dictionary<string, int> LeadStatusCounts { get; set; } = new();
    public Dictionary<string, decimal> StagePipelineAmounts { get; set; } = new();
    public List<MonthlySalesData> MonthlyTrends { get; set; } = new();

    // Recent lists
    public List<Models.FollowUp> RecentFollowUps { get; set; } = new();
    public List<Models.Opportunity> TopOpportunities { get; set; } = new();
}

public class MonthlySalesData
{
    public string Month { get; set; } = string.Empty;
    public decimal WonAmount { get; set; }
    public decimal PipelineAmount { get; set; }
}
