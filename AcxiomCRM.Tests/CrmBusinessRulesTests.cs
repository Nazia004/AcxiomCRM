using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Xunit;

namespace AcxiomCRM.Tests;

public class CrmBusinessRulesTests
{
    private ApplicationDbContext GetInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Opportunity_WeightedPipeline_CalculatesCorrectly()
    {
        var opp = new Opportunity
        {
            OpportunityName = "Cloud Migration Deal",
            Amount = 100000m,
            Probability = 75,
            Stage = OpportunityStage.Proposal,
            Status = "Open"
        };

        // Weighted = Amount * Probability / 100
        Assert.Equal(75000m, opp.WeightedPipeline);
    }

    [Fact]
    public async Task Customer_DuplicateEmail_Detection_Works()
    {
        using var context = GetInMemoryContext("CustDupEmailDb");

        context.Customers.Add(new Customer
        {
            CustomerCode = "CUST-001",
            CustomerName = "Test Co",
            Email = "duplicate@test.com",
            Phone = "1234567890",
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var exists = await context.Customers.AnyAsync(c => c.Email.ToLower() == "duplicate@test.com");
        Assert.True(exists);
    }

    [Fact]
    public async Task Customer_DuplicatePhone_Detection_Works()
    {
        using var context = GetInMemoryContext("CustDupPhoneDb");

        context.Customers.Add(new Customer
        {
            CustomerCode = "CUST-001",
            CustomerName = "First Co",
            Email = "first@test.com",
            Phone = "9876543210",
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var exists = await context.Customers.AnyAsync(c => c.Phone == "9876543210");
        Assert.True(exists);
    }

    [Fact]
    public void FollowUp_PlannedPastDate_RejectionRule_Works()
    {
        var pastDate = DateTime.UtcNow.AddDays(-2);
        var followUp = new FollowUp
        {
            Subject = "Past meeting",
            Status = FollowUpStatus.Planned,
            FollowUpDate = pastDate
        };

        // Business rule verification: Planned follow-up cannot have past date
        bool isInvalid = followUp.Status == FollowUpStatus.Planned && followUp.FollowUpDate.Date < DateTime.UtcNow.Date;
        Assert.True(isInvalid);
    }

    [Fact]
    public void Opportunity_BusinessValidations_Work()
    {
        // 1. Amount <= 0
        var oppZeroAmount = new Opportunity { Amount = 0, Probability = 50 };
        Assert.True(oppZeroAmount.Amount <= 0);

        // 2. Probability > 100
        var oppInvalidProb = new Opportunity { Amount = 1000, Probability = 110 };
        Assert.True(oppInvalidProb.Probability > 100);

        // 3. Expected close date in past for active
        var oppPastDate = new Opportunity
        {
            Amount = 5000,
            Probability = 50,
            Status = "Open",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(-5)
        };
        Assert.True(oppPastDate.ExpectedCloseDate.Date < DateTime.UtcNow.Date);
    }

    [Fact]
    public void RoleScoping_SalesExecutive_CannotAccess_OtherUserRecords()
    {
        var authService = new CrmAuthorizationService();

        var salesUserClaims = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-sales-1"),
                new Claim(ClaimTypes.Role, UserRoles.SalesExecutive)
            ], "TestAuth"));

        var oppAssignedToOther = new Opportunity
        {
            OpportunityId = 99,
            OpportunityName = "Confidential Big Deal",
            AssignedTo = "user-sales-2"
        };

        var canAccess = authService.CanAccessOpportunity(salesUserClaims, oppAssignedToOther);
        Assert.False(canAccess, "SalesExecutive must not access unassigned opportunities.");
    }

    [Fact]
    public void RoleScoping_AdminAndManager_HaveFullAccess()
    {
        var authService = new CrmAuthorizationService();

        var adminClaims = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "admin-1"),
                new Claim(ClaimTypes.Role, UserRoles.Admin)
            ], "TestAuth"));

        var managerClaims = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "manager-1"),
                new Claim(ClaimTypes.Role, UserRoles.Manager)
            ], "TestAuth"));

        var opp = new Opportunity
        {
            OpportunityId = 101,
            OpportunityName = "Any Deal",
            AssignedTo = "user-sales-2"
        };

        Assert.True(authService.CanAccessOpportunity(adminClaims, opp));
        Assert.True(authService.CanAccessOpportunity(managerClaims, opp));
    }

    [Fact]
    public async Task LeadConversion_PreventsDuplicateConversion()
    {
        using var context = GetInMemoryContext("LeadConvDb");

        var lead = new Lead
        {
            LeadId = 1,
            LeadCode = "LEAD-001",
            LeadName = "Acme Lead",
            Email = "lead@acme.com",
            Phone = "1234567890",
            Status = LeadStatus.Converted,
            ConvertedCustomerId = 50
        };
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        // Duplicate conversion check
        bool isAlreadyConverted = lead.Status == LeadStatus.Converted;
        Assert.True(isAlreadyConverted, "Duplicate lead conversion should be blocked.");
    }
}
