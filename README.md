# AcxiomCRM — Customer Relationship Management Platform

[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-ASP.NET%20Core%20MVC-blue.svg)](https://learn.microsoft.com/aspnet/core)
[![Database](https://img.shields.io/badge/Database-SQLite%20%2F%20EF%20Core-lightgrey.svg)](https://learn.microsoft.com/ef/core/)
[![UI](https://img.shields.io/badge/UI-Bootstrap%205%20%7C%20Chart.js-success.svg)](https://getbootstrap.com/)

AcxiomCRM is an enterprise-grade, database-backed Customer Relationship Management (CRM) web application designed to manage the end-to-end customer-sales lifecycle. Built with **ASP.NET Core MVC**, **ASP.NET Core Identity**, **Entity Framework Core**, **SQLite**, and **Chart.js**, it incorporates strict role-based authorization, comprehensive audit logging, client-side and server-side validation, and a secure REST API.

---

## 1. Project Overview & Features

### Core Modules
1. **Authentication & Security**
   - ASP.NET Core Identity with PBKDF2 password hashing.
   - Strict password policy (minimum 8 characters, requiring uppercase letters and digits).
   - Account lockout after 5 consecutive failed login attempts with a 15-minute lockout window.
   - Anti-forgery token verification (`[ValidateAntiForgeryToken]`) across state-altering MVC Razor form POST requests.
   - Automatic account deactivation enforcement (deactivated accounts cannot authenticate).

2. **Role-Based Access Control (RBAC)**
   - **Admin**: Full CRM access, user provisioning, role assignments, account unlock, password resets, system audit logs, and reports.
   - **Manager**: Team-level customer, lead, deal, follow-up, and activity tracking, sales pipeline visibility, team performance reports, and audit trail inspection.
   - **Sales Executive**: Scoped access restricted to assigned/owned customers, leads, opportunities, follow-ups, and personal activities. Server-side query scoping prevents unauthorized access via URL/API tampering.

3. **Customer Master Data**
   - Complete CRUD operations with search across name, email, phone, and company.
   - Automatic customer code generation (`CUST-1001`).
   - Business validations: Mandatory name, validated email format, regex-validated phone numbers, and strict uniqueness checks on email and phone.
   - Soft deactivation preserving all historical deals and audit trails.

4. **Lead Management & Conversion**
   - Lifecycle tracking across statuses: `New`, `Contacted`, `Qualified`, `Unqualified`, `Converted`, `Lost`.
   - Priority tagging (`Low`, `Medium`, `High`) and expected value tracking.
   - **Lead Conversion Workflow**: Seamlessly promotes a qualified lead into an active Customer record and an optional Opportunity in the pipeline while preventing accidental duplicate conversions.

5. **Opportunity & Sales Pipeline**
   - Stage progression: `Qualification` (20%), `Proposal` (50%), `Negotiation` (80%), `Won` (100%), `Lost` (0%).
   - Dynamic weighted pipeline derivation: $\text{Weighted Pipeline} = \text{Amount} \times \frac{\text{Probability}}{100}$.
   - Business validation rules: Amount must be $> 0$ for active deals; Probability bounded between $0$ and $100$; Expected Close Date cannot be in the past for active deals.

6. **Follow-Up Scheduling & Tracking**
   - Schedule interactions (Calls, Meetings, Emails, Demos) linked directly to Customers or Leads.
   - Upcoming and Overdue detection badges.
   - Business validation: Planned follow-up dates cannot be earlier than the current day.
   - One-click completion logging remarks and automatic replication into historical Activities.

7. **Activity Management**
   - Unified logging for Calls, Meetings, Emails, and Tasks with timestamps and completion statuses.

8. **Interactive Analytics Dashboard**
   - Real-time KPI cards: Total Customers, Total Leads, Open Leads, Total Opportunities, Open Opportunities, Won Deals, Lost Deals, Total Pipeline Value, and Upcoming Tasks.
   - **Chart.js** Visualizations:
     1. Lead Status Doughnut Chart (New, Contacted, Qualified, Lost, Converted).
     2. Opportunity Pipeline Stage Bar Chart (Qualification, Proposal, Negotiation, Won, Lost).
     3. Monthly Sales & Pipeline Revenue Trend Line Chart.
   - Date range filters: All Time, Today, This Week, This Month.

9. **Audit Trail**
   - Append-only audit logging: Application-level audit trail capturing authentication events, failed logins, account lockouts, user role changes, CRM record creations, updates, deactivations, and lead conversions.
   - Protected against modification: No update or deletion endpoints are exposed for audit records in the web UI or REST API.
   - Captures User ID, Action, Module, Timestamp (UTC), IP Address, Old Value, New Value, and Result.

10. **Reports & Exports**
    - Customer Master Report with CSV export.
    - Lead Generation & Source Report with CSV export.
    - Sales Pipeline & Weighted Forecasting Report with CSV export.
    - Follow-Up Activity & Overdue Report.
    - Conversion & Outcome Analytics Report.
    - Team Performance & Rep Activity Volume Report.

11. **REST API**
    - RESTful Web API endpoints utilizing DTOs (Data Transfer Objects) to protect internal database schemas.
    - Proper HTTP status codes (`200 OK`, `201 Created`, `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict`, `423 Locked`).

---

## 2. Technology Stack

- **Framework**: .NET 10.0 (ASP.NET Core MVC)
- **Language**: C# 13
- **ORM**: Entity Framework Core 10.0
- **Database**: SQLite (`app.db`)
- **Security**: ASP.NET Core Identity
- **Frontend**: Razor Views, Bootstrap 5.3, Bootstrap Icons, HTML5, CSS3, JavaScript
- **Analytics / Charts**: Chart.js
- **Unit Testing**: xUnit, Microsoft.EntityFrameworkCore.InMemory
- **Source Control**: Git

---

## 3. Getting Started & Setup

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or higher
- Git

### Installation & Run

1. **Clone the repository:**
   ```bash
   git clone https://github.com/Nazia004/AcxiomCRM.git
   cd AcxiomCRM
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Database Migration & Seeding:**
   - **Automatic (Default)**: On startup, `Program.cs` invokes `DbInitializer.InitializeAsync()`, which automatically applies pending EF Core migrations (`await context.Database.MigrateAsync()`) and seeds roles and test data into `app.db`.
   - **Manual CLI (Optional)**: If you prefer to apply migrations via terminal before starting:
     ```bash
     dotnet ef database update
     ```

4. **Run the Application:**
   ```bash
   dotnet run --launch-profile http
   ```

5. **Open in Browser:**
   Navigate to [http://localhost:5121](http://localhost:5121).

---

## 4. Evaluator Test Accounts & Credentials

The database is seeded idempotently with test accounts for each role:

| Role | Email | Password | Access Scope |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@acxiomcrm.com` | `Admin@12345` | Full system administration, user management, audit logs, all records and reports |
| **Manager** | `manager@acxiomcrm.com` | `Manager@12345` | Team pipeline overview, all customer/lead records, manager reports, audit logs |
| **Sales Executive** | `sales@acxiomcrm.com` | `Sales@12345` | Assigned customers, leads, deals, follow-ups, personal activities and reports |
| **Sales Executive** | `sarah@acxiomcrm.com` | `Sales@12345` | Secondary sales executive for testing isolation of assigned records |

> **Tip:** The login screen provides quick one-click credential buttons for each test account for rapid evaluation.

---

## 5. REST API Documentation

All API endpoints reside under `/api/`. Protected endpoints require authentication cookies or bearer tokens.

### Authentication Endpoints
- `POST /api/auth/login`
  - **Payload:** `{"email": "admin@acxiomcrm.com", "password": "Admin@12345"}`
  - **Response (200 OK):**
    ```json
    {
      "success": true,
      "message": "Authentication successful.",
      "userId": "...",
      "fullName": "System Administrator",
      "email": "admin@acxiomcrm.com",
      "role": "Admin"
    }
    ```
- `POST /api/auth/logout`

### Customers API
- `GET /api/customers?search=Acme&status=Active&page=1&pageSize=10`
- `GET /api/customers/{id}`
- `POST /api/customers`
  - **Payload:**
    ```json
    {
      "customerName": "Global Logistics Corp",
      "email": "info@globallogistics.com",
      "phone": "+1 555-019-8877",
      "companyName": "Global Logistics",
      "status": "Active"
    }
    ```
  - **Response (201 Created)**
- `PUT /api/customers/{id}`
- `DELETE /api/customers/{id}` (Deactivates customer)

### Leads API
- `GET /api/leads?status=New&page=1`
- `GET /api/leads/{id}`
- `POST /api/leads`
  - **Payload:**
    ```json
    {
      "leadName": "Pacific Retailers",
      "email": "contact@pacificretail.com",
      "phone": "+1 555-014-9922",
      "companyName": "Pacific Retailers",
      "source": "Website",
      "status": "New",
      "priority": "High",
      "expectedValue": 50000
    }
    ```
- `PUT /api/leads/{id}`
- `DELETE /api/leads/{id}`

### Opportunities API
- `GET /api/opportunities?stage=Proposal`
- `GET /api/opportunities/{id}`
- `POST /api/opportunities`
  - **Payload:**
    ```json
    {
      "opportunityName": "Cloud Infrastructure Migration",
      "customerId": 1,
      "amount": 75000,
      "stage": "Proposal",
      "probability": 50,
      "expectedCloseDate": "2026-11-30T00:00:00Z"
    }
    ```
- `PUT /api/opportunities/{id}`
- `DELETE /api/opportunities/{id}`

### Follow-Ups API
- `GET /api/followups?status=Planned`
- `POST /api/followups`
  - **Payload:**
    ```json
    {
      "customerId": 1,
      "followUpDate": "2026-10-20T14:00:00Z",
      "followUpType": "Meeting",
      "subject": "Executive Review Discussion",
      "status": "Planned"
    }
    ```

### Reports API
- `GET /api/reports/pipeline`
  - Returns aggregated pipeline valuation and stage-by-stage totals.

---

## 6. Business Validation Rules

| Rule | Requirement | Validation Behavior |
| :--- | :--- | :--- |
| **Email Format & Uniqueness** | Valid email syntax; duplicate email blocked across customers. | Rejects with `"A customer with this email address already exists."` |
| **Phone Format & Uniqueness** | 7–20 digits standard phone; duplicate phone blocked across customers. | Rejects with `"A customer with this phone number already exists."` |
| **Opportunity Amount** | Positive value $> 0$. | Rejects with `"Opportunity Amount must be greater than 0."` |
| **Probability** | Range $0$ to $100$ inclusive. | Rejects with `"Probability must be between 0 and 100."` |
| **Expected Close Date** | Cannot be in the past for open/active opportunities. | Rejects with `"Expected Close Date cannot be in the past."` |
| **Follow-Up Date** | Cannot be earlier than current date for planned follow-ups. | Rejects with `"Follow-up date cannot be earlier than today."` |
| **Lead Conversion** | Cannot convert already converted leads. | Rejects duplicate conversion attempts and preserves link to created customer. |
| **Ownership Scoping** | Sales Executives cannot modify records assigned to peers. | Server query filters out peer records and returns `403 Forbidden` on direct ID access. |

---

## 7. Automated Testing

The solution includes an automated test suite under `AcxiomCRM.Tests/` using **xUnit** and **EF Core InMemory Database**.

To run all automated tests:
```bash
dotnet test AcxiomCRM.Tests/AcxiomCRM.Tests.csproj
# If running while the local web server is actively executing:
dotnet test AcxiomCRM.Tests/AcxiomCRM.Tests.csproj --no-build
```

### Verified Test Cases:
- `Opportunity_WeightedPipeline_CalculatesCorrectly`: Verifies $\text{Amount} \times \frac{\text{Probability}}{100}$.
- `Customer_DuplicateEmail_Detection_Works`: Verifies server-side rejection of duplicate emails.
- `Customer_DuplicatePhone_Detection_Works`: Verifies server-side rejection of duplicate phone numbers.
- `FollowUp_PlannedPastDate_RejectionRule_Works`: Verifies rejection of past dates for planned follow-ups.
- `Opportunity_BusinessValidations_Work`: Verifies amount $> 0$, probability $0-100$, and future close date enforcement.
- `RoleScoping_SalesExecutive_CannotAccess_OtherUserRecords`: Verifies ownership isolation for Sales Executives.
- `RoleScoping_AdminAndManager_HaveFullAccess`: Verifies elevated permissions for Admin and Manager roles.
- `LeadConversion_PreventsDuplicateConversion`: Verifies duplicate conversion prevention.

---

## 8. Switching from SQLite to Microsoft SQL Server

To switch from SQLite to SQL Server:

1. Install the SQL Server EF Core provider:
   ```bash
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer
   ```
2. Update `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=AcxiomCRM_DB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
   }
   ```
3. In `Program.cs`, update line 13:
   ```csharp
   builder.Services.AddDbContext<ApplicationDbContext>(options =>
       options.UseSqlServer(connectionString));
   ```
4. Re-run migrations:
   ```bash
   dotnet ef database update
   ```

---

## 9. Security & Production Checklist

- [x] Passwords securely hashed with Identity PBKDF2 (no plaintext storage).
- [x] Account lockout configured and active (5 failed attempts, 15 min lockout).
- [x] Sensitive fields (passwords, tokens, connection strings) omitted from DTOs, logs, and Git.
- [x] State-altering MVC actions guarded with Anti-Forgery Tokens (`ValidateAntiForgeryToken`).
- [x] Parameterized Entity Framework Core queries preventing SQL Injection.
- [x] Secure cookie policy configured with `HttpOnly`, `SlidingExpiration`, and expiration limits.
- [x] Append-only audit trail capturing security events and entity modifications (no edit or delete actions exposed).

---

## 10. Repository & Authorship

- **Repository**: https://github.com/Nazia004/AcxiomCRM
- **Author**: Nazia Quadri
