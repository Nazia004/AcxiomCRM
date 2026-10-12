using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class DbInitializer(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ILogger<DbInitializer> logger) : IDbInitializer
{
    public async Task InitializeAsync()
    {
        try
        {
            // Apply migrations automatically
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
            }

            // 1. Seed Roles
            string[] roles = [UserRoles.Admin, UserRoles.Manager, UserRoles.SalesExecutive];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    logger.LogInformation("Role created: {Role}", role);
                }
            }

            // 2. Seed Default Users
            var usersToSeed = new (string Email, string Name, string Role, string Password)[]
            {
                ("admin@acxiomcrm.com", "System Administrator", UserRoles.Admin, "Admin@12345"),
                ("manager@acxiomcrm.com", "Sales Manager", UserRoles.Manager, "Manager@12345"),
                ("sales@acxiomcrm.com", "Alex Vance", UserRoles.SalesExecutive, "Sales@12345"),
                ("sarah@acxiomcrm.com", "Sarah Connor", UserRoles.SalesExecutive, "Sales@12345")
            };

            var userMap = new Dictionary<string, ApplicationUser>();

            foreach (var item in usersToSeed)
            {
                var user = await userManager.FindByEmailAsync(item.Email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = item.Email,
                        Email = item.Email,
                        FullName = item.Name,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(user, item.Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, item.Role);
                        logger.LogInformation("User created: {Email} ({Role})", item.Email, item.Role);
                    }
                }
                userMap[item.Email] = user;
            }

            // 3. Seed Sample CRM Data if empty
            if (!await context.Customers.AnyAsync())
            {
                var adminUser = userMap.GetValueOrDefault("admin@acxiomcrm.com");
                var salesUser = userMap.GetValueOrDefault("sales@acxiomcrm.com");
                var sarahUser = userMap.GetValueOrDefault("sarah@acxiomcrm.com");

                var customer1 = new Customer
                {
                    CustomerCode = "CUST-1001",
                    CustomerName = "Acme Global Industries",
                    Email = "contact@acmeglobal.com",
                    Phone = "+1 555-019-2831",
                    CompanyName = "Acme Global",
                    Address = "100 Industrial Parkway",
                    City = "Chicago",
                    State = "Illinois",
                    Status = CustomerStatus.Active,
                    CreatedDate = DateTime.UtcNow.AddDays(-30),
                    CreatedBy = "admin@acxiomcrm.com",
                    OwnerId = salesUser?.Id ?? adminUser?.Id,
                    Notes = "Enterprise manufacturing client requiring quarterly renewals."
                };

                var customer2 = new Customer
                {
                    CustomerCode = "CUST-1002",
                    CustomerName = "Nexus Cloud Solutions",
                    Email = "info@nexuscloud.io",
                    Phone = "+1 555-018-9942",
                    CompanyName = "Nexus Cloud",
                    Address = "450 Tech Hub Blvd",
                    City = "Austin",
                    State = "Texas",
                    Status = CustomerStatus.Active,
                    CreatedDate = DateTime.UtcNow.AddDays(-20),
                    CreatedBy = "admin@acxiomcrm.com",
                    OwnerId = sarahUser?.Id ?? adminUser?.Id,
                    Notes = "Fast-growing SaaS provider evaluating CRM integration."
                };

                var customer3 = new Customer
                {
                    CustomerCode = "CUST-1003",
                    CustomerName = "Vertex Health Systems",
                    Email = "procurement@vertexhealth.org",
                    Phone = "+1 555-014-4421",
                    CompanyName = "Vertex Health",
                    Address = "78 Healthcare Ave",
                    City = "Boston",
                    State = "Massachusetts",
                    Status = CustomerStatus.Active,
                    CreatedDate = DateTime.UtcNow.AddDays(-10),
                    CreatedBy = "manager@acxiomcrm.com",
                    OwnerId = salesUser?.Id ?? adminUser?.Id,
                    Notes = "HIPAA compliant customer service expansion."
                };

                context.Customers.AddRange(customer1, customer2, customer3);
                await context.SaveChangesAsync();

                // Seed Leads
                var lead1 = new Lead
                {
                    LeadCode = "LEAD-1001",
                    LeadName = "FinTech Horizon Group",
                    Email = "leads@fintechhorizon.com",
                    Phone = "+1 555-012-7711",
                    CompanyName = "FinTech Horizon",
                    Source = "Website",
                    Status = LeadStatus.New,
                    Priority = "High",
                    ExpectedValue = 45000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-5),
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    Notes = "Inbound demo request from marketing landing page."
                };

                var lead2 = new Lead
                {
                    LeadCode = "LEAD-1002",
                    LeadName = "Quantum Logistics Ltd",
                    Email = "inquiry@quantumlog.com",
                    Phone = "+1 555-017-3388",
                    CompanyName = "Quantum Logistics",
                    Source = "Referral",
                    Status = LeadStatus.Contacted,
                    Priority = "Medium",
                    ExpectedValue = 28000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-12),
                    AssignedTo = sarahUser?.Id ?? adminUser?.Id,
                    Notes = "Referred by Acme Global. Preliminary scoping call held."
                };

                var lead3 = new Lead
                {
                    LeadCode = "LEAD-1003",
                    LeadName = "Apex Retail Group",
                    Email = "growth@apexretail.com",
                    Phone = "+1 555-016-5544",
                    CompanyName = "Apex Retail",
                    Source = "Event",
                    Status = LeadStatus.Qualified,
                    Priority = "High",
                    ExpectedValue = 75000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-18),
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    Notes = "Met at TechExpo 2026. Ready for formal RFP proposal."
                };

                var lead4 = new Lead
                {
                    LeadCode = "LEAD-1004",
                    LeadName = "Starlight Media",
                    Email = "ops@starlightmedia.com",
                    Phone = "+1 555-011-2299",
                    CompanyName = "Starlight Media",
                    Source = "Cold Call",
                    Status = LeadStatus.Lost,
                    Priority = "Low",
                    ExpectedValue = 12000m,
                    CreatedDate = DateTime.UtcNow.AddDays(-25),
                    AssignedTo = sarahUser?.Id ?? adminUser?.Id,
                    Notes = "Decided to retain existing legacy platform for current fiscal year."
                };

                context.Leads.AddRange(lead1, lead2, lead3, lead4);
                await context.SaveChangesAsync();

                // Seed Opportunities
                var opp1 = new Opportunity
                {
                    OpportunityName = "Acme Annual Enterprise Upgrade",
                    CustomerId = customer1.CustomerId,
                    Amount = 95000m,
                    Stage = OpportunityStage.Negotiation,
                    Probability = 80,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow.AddDays(-20),
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    Notes = "Contract revision underway with legal team."
                };

                var opp2 = new Opportunity
                {
                    OpportunityName = "Nexus Cloud Multi-Tenant Addon",
                    CustomerId = customer2.CustomerId,
                    Amount = 42000m,
                    Stage = OpportunityStage.Proposal,
                    Probability = 60,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow.AddDays(-15),
                    AssignedTo = sarahUser?.Id ?? adminUser?.Id,
                    Notes = "Proposal submitted, awaiting CFO evaluation."
                };

                var opp3 = new Opportunity
                {
                    OpportunityName = "Vertex Patient Portal Expansion",
                    CustomerId = customer3.CustomerId,
                    Amount = 120000m,
                    Stage = OpportunityStage.Won,
                    Probability = 100,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(-2),
                    Status = "Won",
                    CreatedDate = DateTime.UtcNow.AddDays(-40),
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    Notes = "Signed agreement delivered. Kickoff scheduled."
                };

                var opp4 = new Opportunity
                {
                    OpportunityName = "Acme Security Compliance Suite",
                    CustomerId = customer1.CustomerId,
                    Amount = 30000m,
                    Stage = OpportunityStage.Qualification,
                    Probability = 30,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(45),
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow.AddDays(-5),
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    Notes = "Initial requirements gathering meeting held."
                };

                context.Opportunities.AddRange(opp1, opp2, opp3, opp4);
                await context.SaveChangesAsync();

                // Seed Follow-ups
                var followUp1 = new FollowUp
                {
                    CustomerId = customer1.CustomerId,
                    FollowUpDate = DateTime.UtcNow.AddDays(2).Date.AddHours(14),
                    FollowUpType = "Call",
                    Subject = "Contract review discussion with VP",
                    Remarks = "Confirm approval of SLA clauses.",
                    Status = FollowUpStatus.Planned,
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-2)
                };

                var followUp2 = new FollowUp
                {
                    LeadId = lead1.LeadId,
                    FollowUpDate = DateTime.UtcNow.AddDays(1).Date.AddHours(11),
                    FollowUpType = "Meeting",
                    Subject = "Product walkthrough demo",
                    Remarks = "Present CRM workflow capabilities.",
                    Status = FollowUpStatus.Planned,
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-1)
                };

                var followUp3 = new FollowUp
                {
                    CustomerId = customer2.CustomerId,
                    FollowUpDate = DateTime.UtcNow.AddDays(-3).Date.AddHours(10),
                    FollowUpType = "Email",
                    Subject = "Send updated commercial quote",
                    Remarks = "Sent revised pricing tier PDF.",
                    Status = FollowUpStatus.Completed,
                    AssignedTo = sarahUser?.Id ?? adminUser?.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-5)
                };

                context.FollowUps.AddRange(followUp1, followUp2, followUp3);

                // Seed Activities
                var activity1 = new Activity
                {
                    ActivityType = ActivityTypes.Call,
                    Subject = "Introductory discovery discussion",
                    Description = "Client described migration requirements from Salesforce.",
                    ActivityDate = DateTime.UtcNow.AddDays(-10),
                    CustomerId = customer1.CustomerId,
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    Status = "Completed",
                    CreatedDate = DateTime.UtcNow.AddDays(-10)
                };

                var activity2 = new Activity
                {
                    ActivityType = ActivityTypes.Meeting,
                    Subject = "Quarterly Business Review",
                    Description = "Reviewed system utilization and planned upcoming scale.",
                    ActivityDate = DateTime.UtcNow.AddDays(-4),
                    CustomerId = customer3.CustomerId,
                    AssignedTo = salesUser?.Id ?? adminUser?.Id,
                    Status = "Completed",
                    CreatedDate = DateTime.UtcNow.AddDays(-4)
                };

                var activity3 = new Activity
                {
                    ActivityType = ActivityTypes.Email,
                    Subject = "Shared AcxiomCRM product sheet",
                    Description = "Sent overview brochure and pricing guide.",
                    ActivityDate = DateTime.UtcNow.AddDays(-8),
                    LeadId = lead2.LeadId,
                    AssignedTo = sarahUser?.Id ?? adminUser?.Id,
                    Status = "Completed",
                    CreatedDate = DateTime.UtcNow.AddDays(-8)
                };

                context.Activities.AddRange(activity1, activity2, activity3);

                // Seed Audit Logs
                var auditLog1 = new AuditLog
                {
                    UserId = adminUser?.Id,
                    UserName = adminUser?.UserName ?? "admin@acxiomcrm.com",
                    Action = "System Seed",
                    EntityName = "Database",
                    RecordId = "Init",
                    CreatedDate = DateTime.UtcNow.AddDays(-30),
                    IpAddress = "127.0.0.1",
                    Result = "Success",
                    Details = "Baseline application roles and test accounts initialized."
                };

                context.AuditLogs.Add(auditLog1);
                await context.SaveChangesAsync();
                logger.LogInformation("Database seeded with sample CRM records successfully.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }
}
