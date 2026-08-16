using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MyApi.Infrastructure;
using MyApi.Modules.Auth.Models;
using MyApi.Modules.Tenants.Models;
using MyApi.Modules.Users.Models;
using MyApi.Modules.Roles.Models;
using MyApi.Modules.Skills.Models;
using MyApi.Modules.Contacts.Models;
using MyApi.Modules.Articles.Models;
using MyApi.Modules.Calendar.Models;
using MyApi.Modules.Projects.Models;
using MyApi.Modules.Lookups.Models;
using MyApi.Modules.Offers.Models;
using MyApi.Modules.Deals.Models;
using MyApi.Modules.Auth.Data.Configurations;
using MyApi.Modules.Users.Data.Configurations;
using MyApi.Modules.Roles.Data.Configurations;
using MyApi.Modules.Skills.Data.Configurations;
using MyApi.Modules.Contacts.Data.Configurations;
using MyApi.Modules.Offers.Data;
using MyApi.Data.SeedData;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.Sales.Data;
using MyApi.Modules.Installations.Models;
using MyApi.Modules.Installations.Data;
using MyApi.Modules.Dispatches.Models;
using MyApi.Modules.Dispatches.Data;
using MyApi.Modules.ServiceOrders.Models;
using MyApi.Modules.ServiceOrders.Data;
using MyApi.Modules.Planning.Models;
using MyApi.Modules.PlanningProfiles.Models;
using MyApi.Modules.Notifications.Models;
using MyApi.Modules.Notifications.Data;
using MyApi.Modules.Shared.Models;
using MyApi.Modules.Shared.Data.Configurations;
using MyApi.Modules.Preferences.Models;
using MyApi.Modules.DynamicForms.Models;
using MyApi.Modules.AiChat.Models;
using MyApi.Modules.WorkflowEngine.Models;
using MyApi.Modules.Documents.Models;
using MyApi.Modules.Signatures.Models;
using MyApi.Modules.WebsiteBuilder.Models;
using MyApi.Modules.WebsiteBuilder.Data.Configurations;
using MyApi.Modules.EmailAccounts.Models;
using MyApi.Modules.UserAiSettings.Models;
using MyApi.Modules.Payments.Models;
using MyApi.Modules.RetenueSource.Models;
using MyApi.Modules.RetenueSource.Data;
using MyApi.Modules.SupportTickets.Models;
using MyApi.Modules.Settings.Models;
using MyApi.Modules.HR.Models;
using MyApi.Modules.Sync.Models;
using MyApi.Modules.OfflineHydration.Models;

namespace MyApi.Data
{
    public partial class ApplicationDbContext : DbContext
    {
        // ═══ MULTI-TENANCY: Current tenant ID for this request scope ═══
        private int _currentTenantId = 0; // Default tenant

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        /// <summary>Set the tenant ID for this context instance (called from DI registration).</summary>
        public void SetTenantId(int tenantId) { _currentTenantId = tenantId; }
        public int GetTenantId() => _currentTenantId;

        // Tenants Module
        public DbSet<Tenant> Tenants { get; set; }

        // Users Module
        public DbSet<MainAdminUser> MainAdminUsers { get; set; }
        public DbSet<UserPreferences> UserPreferences { get; set; }
        public DbSet<User> Users { get; set; }
        
        // Roles & Skills Module  
        public DbSet<Role> Roles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<UserSkill> UserSkills { get; set; }
        public DbSet<RoleSkill> RoleSkills { get; set; }

        // User Groups Module
        public DbSet<MyApi.Modules.UserGroups.Models.UserGroup> UserGroups { get; set; }
        public DbSet<MyApi.Modules.UserGroups.Models.UserGroupMember> UserGroupMembers { get; set; }
        
        // Contacts Module
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ContactNote> ContactNotes { get; set; }
        public DbSet<ContactActivity> ContactActivities { get; set; }
        public DbSet<ContactTag> ContactTags { get; set; }
        public DbSet<ContactTagAssignment> ContactTagAssignments { get; set; }
        public DbSet<ContactUserGroupAssignment> ContactUserGroups { get; set; }


        // Articles Module (Materials & Services)
        public DbSet<Article> Articles { get; set; }
        public DbSet<ArticleCategory> ArticleCategories { get; set; }
        public DbSet<ArticleNote> ArticleNotes { get; set; }
        // NOTE: ArticleGroups is now replaced by generic Lookups system with LookupType='article-groups'
        // Table has been dropped. Keep this commented for reference but don't use it.
        // public DbSet<ArticleGroup> ArticleGroups { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }

        // Calendar entities
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<EventType> EventTypes { get; set; }
        public DbSet<EventAttendee> EventAttendees { get; set; }
        public DbSet<EventReminder> EventReminders { get; set; }

        // Email Accounts Module (Gmail/Outlook OAuth)
        public DbSet<ConnectedEmailAccount> ConnectedEmailAccounts { get; set; }
        public DbSet<MyApi.Modules.EmailAccounts.Models.CustomEmailAccount> CustomEmailAccounts { get; set; }
        public DbSet<EmailBlocklistItem> EmailBlocklistItems { get; set; }
        public DbSet<SyncedEmail> SyncedEmails { get; set; }
        public DbSet<SyncedEmailAttachment> SyncedEmailAttachments { get; set; }
        public DbSet<SyncedCalendarEvent> SyncedCalendarEvents { get; set; }
        public DbSet<MyApi.Modules.EmailAccounts.Models.OutboundEmailLog> OutboundEmailLogs { get; set; }

        // Tasks Module
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectNote> ProjectNotes { get; set; }
        public DbSet<ProjectActivity> ProjectActivities { get; set; }
        public DbSet<ProjectTask> ProjectTasks { get; set; }
        public DbSet<DailyTask> DailyTasks { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }
        public DbSet<TaskAttachment> TaskAttachments { get; set; }
        public DbSet<TaskTimeEntry> TaskTimeEntries { get; set; }
        public DbSet<TaskChecklist> TaskChecklists { get; set; }
        public DbSet<TaskChecklistItem> TaskChecklistItems { get; set; }
        public DbSet<RecurringTask> RecurringTasks { get; set; }
        public DbSet<RecurringTaskLog> RecurringTaskLogs { get; set; }
        public DbSet<ProjectSettings> ProjectSettings { get; set; }
        public DbSet<ProjectColumn> ProjectColumns { get; set; }

        // Lookups Module
        public DbSet<LookupItem> LookupItems { get; set; }
        public DbSet<Currency> Currencies { get; set; }

        // Offers Module
        public DbSet<Offer> Offers { get; set; }
        public DbSet<OfferItem> OfferItems { get; set; }
        public DbSet<OfferActivity> OfferActivities { get; set; }

        // Deals Module
        public DbSet<Deal> Deals { get; set; }
        public DbSet<DealItem> DealItems { get; set; }
        public DbSet<DealActivity> DealActivities { get; set; }

        // Sales Module
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<SaleActivity> SaleActivities { get; set; }

        // Installations Module
        public DbSet<Installation> Installations { get; set; }
        public DbSet<InstallationNote> InstallationNotes { get; set; }
        public DbSet<MaintenanceHistory> MaintenanceHistories { get; set; }

    // Dispatches Module
    public DbSet<Dispatch> Dispatches { get; set; }
    public DbSet<DispatchTechnician> DispatchTechnicians { get; set; }
    public DbSet<DispatchJob> DispatchJobs { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    public DbSet<Expense> DispatchExpenses { get; set; }
    public DbSet<MaterialUsage> DispatchMaterials { get; set; }
    public DbSet<Attachment> DispatchAttachments { get; set; }
    public DbSet<Note> DispatchNotes { get; set; }
    public DbSet<MyApi.Modules.Dispatches.Models.DispatchAuditLog> DispatchAuditLogs { get; set; }

        public DbSet<ServiceOrder> ServiceOrders { get; set; }
        public DbSet<ServiceOrderJob> ServiceOrderJobs { get; set; }
        public DbSet<ServiceOrderMaterial> ServiceOrderMaterials { get; set; }
        public DbSet<ServiceOrderTimeEntry> ServiceOrderTimeEntries { get; set; }
        public DbSet<ServiceOrderExpense> ServiceOrderExpenses { get; set; }
        public DbSet<ServiceOrderNote> ServiceOrderNotes { get; set; }

        // Invoices Module (Phase B)
        public DbSet<MyApi.Modules.Invoices.Models.Invoice> Invoices { get; set; }
        public DbSet<MyApi.Modules.Invoices.Models.InvoiceLine> InvoiceLines { get; set; }
        public DbSet<MyApi.Modules.Invoices.Models.InvoiceActivity> InvoiceActivities { get; set; }

        // Planning Module
        public DbSet<UserWorkingHours> UserWorkingHours { get; set; }
        public DbSet<UserLeave> UserLeaves { get; set; }
        public DbSet<UserStatusHistory> UserStatusHistory { get; set; }
        public DbSet<DispatchHistory> DispatchHistory { get; set; }
        public DbSet<TechnicianStatusHistory> TechnicianStatusHistory { get; set; }

        // Planning Profiles Module
        public DbSet<PlanningProfile> PlanningProfiles { get; set; }
        public DbSet<UserActivePlanningProfile> UserActivePlanningProfiles { get; set; }
        
        // HR Module
        public DbSet<HrEmployeeSalaryConfig> HrEmployeeSalaryConfigs { get; set; }
        public DbSet<HrAttendance> HrAttendance { get; set; }
        public DbSet<HrAttendanceSettings> HrAttendanceSettings { get; set; }
        public DbSet<HrDepartment> HrDepartments { get; set; }
        public DbSet<HrLeaveBalance> HrLeaveBalances { get; set; }
        public DbSet<HrPayrollRun> HrPayrollRuns { get; set; }
        public DbSet<HrPayrollEntry> HrPayrollEntries { get; set; }
        public DbSet<HrBonusCost> HrBonusCosts { get; set; }
        public DbSet<HrAuditLog> HrAuditLogs { get; set; }
        public DbSet<HrCnssRate> HrCnssRates { get; set; }
        public DbSet<HrPublicHoliday> HrPublicHolidays { get; set; }
        public DbSet<HrEmployeeDocument> HrEmployeeDocuments { get; set; }
        public DbSet<HrSalaryHistory> HrSalaryHistory { get; set; }

        // HR — Performance & Recruitment
        public DbSet<HrReviewCycle> HrReviewCycles { get; set; }
        public DbSet<HrGoal> HrGoals { get; set; }
        public DbSet<HrPerformanceReview> HrPerformanceReviews { get; set; }
        public DbSet<HrJobOpening> HrJobOpenings { get; set; }
        public DbSet<HrApplicant> HrApplicants { get; set; }
        public DbSet<HrInterview> HrInterviews { get; set; }
        public DbSet<HrApplicantNote> HrApplicantNotes { get; set; }

        // Notifications Module
        public DbSet<Notification> Notifications { get; set; }

        // System Logs Module
        public DbSet<SystemLog> SystemLogs { get; set; }

        // Processes Module (admin scheduled jobs)
        public DbSet<MyApi.Modules.Processes.Models.ProcessSchedule> ProcessSchedules { get; set; }
        public DbSet<MyApi.Modules.Processes.Models.ProcessRun> ProcessRuns { get; set; }

        // PDF Settings Module (Global settings)
        public DbSet<PdfSettings> PdfSettings { get; set; }

        // Numbering Module
        public DbSet<MyApi.Modules.Numbering.Models.NumberingSettings> NumberingSettings { get; set; }
        public DbSet<MyApi.Modules.Numbering.Models.NumberSequence> NumberSequences { get; set; }


        // Dynamic Forms Module
        public DbSet<DynamicForm> DynamicForms { get; set; }
        public DbSet<DynamicFormResponse> DynamicFormResponses { get; set; }

        // Dashboards Module
        public DbSet<MyApi.Modules.Dashboards.Models.Dashboard> Dashboards { get; set; }

        // Entity Form Documents (shared - for offers/sales)
        public DbSet<MyApi.Modules.Shared.Models.EntityFormDocument> EntityFormDocuments { get; set; }

        // AI Chat Module
        public DbSet<AiConversation> AiConversations { get; set; }
        public DbSet<AiMessage> AiMessages { get; set; }

        // Workflow Engine Module
        public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
        public DbSet<WorkflowTrigger> WorkflowTriggers { get; set; }
        public DbSet<WorkflowExecution> WorkflowExecutions { get; set; }
        public DbSet<WorkflowExecutionLog> WorkflowExecutionLogs { get; set; }
        public DbSet<WorkflowApproval> WorkflowApprovals { get; set; }
        public DbSet<WorkflowProcessedEntity> WorkflowProcessedEntities { get; set; }

        // Documents Module
        public DbSet<Document> Documents { get; set; }

        // Signatures Module
        public DbSet<UserSignature> UserSignatures { get; set; }

        // Website Builder Module
        public DbSet<WBSite> WBSites { get; set; }
        public DbSet<WBPage> WBPages { get; set; }
        public DbSet<WBPageVersion> WBPageVersions { get; set; }
        public DbSet<WBGlobalBlock> WBGlobalBlocks { get; set; }
        public DbSet<WBGlobalBlockUsage> WBGlobalBlockUsages { get; set; }
        public DbSet<WBBrandProfile> WBBrandProfiles { get; set; }
        public DbSet<WBFormSubmission> WBFormSubmissions { get; set; }
        public DbSet<WBMedia> WBMedia { get; set; }
        public DbSet<WBTemplate> WBTemplates { get; set; }
        public DbSet<WBActivityLog> WBActivityLogs { get; set; }

        // User AI Settings Module
        public DbSet<UserAiKey> UserAiKeys { get; set; }
        public DbSet<UserAiPreference> UserAiPreferences { get; set; }

        // Payments Module
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentPlan> PaymentPlans { get; set; }
        public DbSet<PaymentPlanInstallment> PaymentPlanInstallments { get; set; }
        public DbSet<PaymentItemAllocation> PaymentItemAllocations { get; set; }
        public DbSet<PaymentProofDocument> PaymentProofDocuments { get; set; }

        // Retenue à la Source Module
        public DbSet<RSRecord> RSRecords { get; set; }
        public DbSet<TEJExportLog> TEJExportLogs { get; set; }

        // Support Tickets Module
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<SupportTicketAttachment> SupportTicketAttachments { get; set; }
        public DbSet<SupportTicketComment> SupportTicketComments { get; set; }
        public DbSet<SupportTicketLink> SupportTicketLinks { get; set; }

        // App Settings Module (Global key-value settings)
        public DbSet<AppSettings> AppSettings { get; set; }
        public DbSet<SyncOperationReceipt> SyncOperationReceipts { get; set; }
        public DbSet<SyncChange> SyncChanges { get; set; }

        public DbSet<OfflineHydrationPreference> OfflineHydrationPreferences { get; set; }

        // Reporting Module — pinned dashboard widgets per user/scope
        public DbSet<MyApi.Modules.Reporting.Models.ReportingFavorite> ReportingFavorites { get; set; }

        // Dashboards Module — per-user main dashboard layout (card order + hidden)
        public DbSet<MyApi.Modules.Dashboards.Models.DashboardLayout> DashboardLayouts { get; set; }

        // External Endpoints Module
        public DbSet<MyApi.Modules.ExternalEndpoints.Models.ExternalEndpoint> ExternalEndpoints { get; set; }
        public DbSet<MyApi.Modules.ExternalEndpoints.Models.ExternalEndpointLog> ExternalEndpointLogs { get; set; }
        public DbSet<MyApi.Modules.ExternalEndpoints.Models.WebhookForwardJob> WebhookForwardJobs { get; set; }

        // Purchases Module
        public DbSet<MyApi.Modules.Purchases.Models.PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<MyApi.Modules.Purchases.Models.PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<MyApi.Modules.Purchases.Models.GoodsReceipt> GoodsReceipts { get; set; }
        public DbSet<MyApi.Modules.Purchases.Models.GoodsReceiptItem> GoodsReceiptItems { get; set; }
        public DbSet<MyApi.Modules.Purchases.Models.SupplierInvoice> SupplierInvoices { get; set; }
        public DbSet<MyApi.Modules.Purchases.Models.SupplierInvoiceItem> SupplierInvoiceItems { get; set; }
        public DbSet<MyApi.Modules.Purchases.Models.ArticleSupplier> ArticleSuppliers { get; set; }
        public DbSet<MyApi.Modules.Purchases.Models.ArticleSupplierPriceHistory> ArticleSupplierPriceHistory { get; set; }
        public DbSet<MyApi.Modules.Purchases.Models.PurchaseActivity> PurchaseActivities { get; set; }

        // Plugin Registry — per-tenant module enable/disable overrides
        public DbSet<MyApi.Modules.Plugins.Models.ActivatedModule> ActivatedModules { get; set; }

        // Module Scope Settings — per-module shared/per_company configuration
        public DbSet<MyApi.Modules.Settings.Models.ModuleScopeSetting> ModuleScopeSettings { get; set; }

        // Planning — predefined time & expenses on offer/sale/service-order-job lines
        public DbSet<MyApi.Modules.Planning.Models.PlannedLineEntry> PlannedLineEntries { get; set; }

        // ═══ MODULE SCOPE: shared vs per_company ═══
        // Lazy-loaded once per DbContext from the ModuleScopeSettings table.
        // Used by both the global query filter and StampTenantIdOnNewEntities.
        private Dictionary<string, bool>? _moduleSharedCache;
        private void EnsureModuleScopeLoaded()
        {
            if (_moduleSharedCache != null) return;
            try
            {
                _moduleSharedCache = Set<MyApi.Modules.Settings.Models.ModuleScopeSetting>()
                    .AsNoTracking()
                    .ToList()
                    .ToDictionary(
                        r => r.ModuleKey,
                        r => string.Equals(r.Scope, "shared", StringComparison.OrdinalIgnoreCase),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // Table missing (pre-migration) → degrade to per_company.
                _moduleSharedCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }
        }
        public bool IsModuleShared(string moduleKey)
        {
            if (string.IsNullOrWhiteSpace(moduleKey)) return false;
            EnsureModuleScopeLoaded();
            return _moduleSharedCache!.TryGetValue(moduleKey, out var v) && v;
        }
        /// <summary>Force the next IsModuleShared call to re-read from DB.</summary>
        public void InvalidateModuleScopeCache() { _moduleSharedCache = null; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply all entity configurations
            ApplyEntityConfigurations(modelBuilder);
            
            // Apply seed data
            ApplySeedData(modelBuilder);

            // ═══ PROCESSES (global, non-tenant automation) ═══
            // Unique key guarantees the scheduler seed + concurrent app instances can
            // never create duplicate schedule rows for the same process key.
            modelBuilder.Entity<MyApi.Modules.Processes.Models.ProcessSchedule>()
                .HasIndex(s => s.Key)
                .IsUnique();
            // Run history is always queried by key, newest first.
            modelBuilder.Entity<MyApi.Modules.Processes.Models.ProcessRun>()
                .HasIndex(r => new { r.ProcessKey, r.StartedAt });


            // ═══ MULTI-TENANCY: Global Query Filters ═══
            // Automatically append WHERE "TenantId" = @current to every query
            // on entities implementing ITenantEntity.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                {
                    // Pick the scope-aware filter when the entity opts in via [ModuleScope].
                    var scopeAttr = entityType.ClrType
                        .GetCustomAttributes(typeof(ModuleScopeAttribute), inherit: true)
                        .Cast<ModuleScopeAttribute>()
                        .FirstOrDefault();

                    var methodName = scopeAttr != null
                        ? nameof(ApplyScopedTenantQueryFilter)
                        : nameof(ApplyTenantQueryFilter);

                    var method = typeof(ApplicationDbContext)
                        .GetMethod(methodName,
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .MakeGenericMethod(entityType.ClrType);

                    if (scopeAttr != null)
                        method.Invoke(null, new object[] { modelBuilder, this, scopeAttr.ModuleKey });
                    else
                        method.Invoke(null, new object[] { modelBuilder, this });
                }
            }
        }

        /// <summary>
        /// Per-company filter (entities without [ModuleScope]). Original behaviour.
        /// </summary>
        private static void ApplyTenantQueryFilter<T>(ModelBuilder modelBuilder, ApplicationDbContext ctx)
            where T : class, ITenantEntity
        {
            modelBuilder.Entity<T>().HasQueryFilter(e =>
                ctx._currentTenantId == -1 || e.TenantId == ctx._currentTenantId);
        }

        /// <summary>
        /// Scope-aware filter (entities with [ModuleScope("key")]):
        ///   shared      → WHERE TenantId = 0        (single shared dataset)
        ///   per_company → WHERE TenantId = current  (default)
        ///   view-all (-1) bypass still honoured.
        /// EF translates ctx.IsModuleShared(key) to a runtime parameter, so the
        /// generated SQL is a normal WHERE TenantId = @p clause.
        /// </summary>
        private static void ApplyScopedTenantQueryFilter<T>(
            ModelBuilder modelBuilder, ApplicationDbContext ctx, string moduleKey)
            where T : class, ITenantEntity
        {
            modelBuilder.Entity<T>().HasQueryFilter(e =>
                ctx._currentTenantId == -1
                || (ctx.IsModuleShared(moduleKey)
                        ? e.TenantId == 0
                        : e.TenantId == ctx._currentTenantId));
        }


        // ═══ MULTI-TENANCY: Auto-set TenantId on insert ═══
        public override int SaveChanges()
        {
            StampTenantIdOnNewEntities();
            NormalizeDateTimeKinds();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampTenantIdOnNewEntities();
            NormalizeDateTimeKinds();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            StampTenantIdOnNewEntities();
            NormalizeDateTimeKinds();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            StampTenantIdOnNewEntities();
            NormalizeDateTimeKinds();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // PostgreSQL `timestamp with time zone` columns require DateTimeKind=Utc.
        // DateTimes deserialized from JSON come through as Kind=Unspecified, which
        // makes Npgsql throw a DbUpdateException ("Cannot write DateTime with
        // Kind=Unspecified ...") on SaveChanges. Normalize them centrally so every
        // module benefits without touching each service.
        private void NormalizeDateTimeKinds()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified) continue;
                foreach (var prop in entry.Properties)
                {
                    if (prop.CurrentValue is DateTime dt && dt.Kind == DateTimeKind.Unspecified)
                    {
                        prop.CurrentValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    }
                    else if (prop.CurrentValue is DateTime dt2 && dt2.Kind == DateTimeKind.Local)
                    {
                        prop.CurrentValue = dt2.ToUniversalTime();
                    }
                }
            }
        }

        private void StampTenantIdOnNewEntities()
        {
            // ── Resolve the [ModuleScope] key for an entity instance, if any. ──
            static string? GetModuleKey(object entity)
            {
                var attr = entity.GetType()
                    .GetCustomAttributes(typeof(ModuleScopeAttribute), inherit: true)
                    .Cast<ModuleScopeAttribute>()
                    .FirstOrDefault();
                return attr?.ModuleKey;
            }

            // Block writes in view-all mode (TenantId = -1 sentinel) ONLY if no target tenant override
            // When X-Target-Tenant is provided, the middleware sets _currentTenantId to the target tenant
            // so writes are properly scoped. -1 means "all tenants" with no target specified.
            if (_currentTenantId == -1)
            {
                // Allow audit/log entities to be persisted even in view-all mode — they are
                // system-scoped (TenantId = 0) and must never break the request just because
                // no target tenant was provided. Without this, login + other admin actions
                // emit a noisy "primary persist failed" warning on every call.
                // Shared modules are also fine in view-all mode (they always write TenantId = 0).
                var hasBlockingChanges = ChangeTracker.Entries<ITenantEntity>()
                    .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                    .Any(e =>
                        e.Entity is not MyApi.Modules.Shared.Models.SystemLog &&
                        !(GetModuleKey(e.Entity) is string k && IsModuleShared(k)));
                if (hasBlockingChanges)
                {
                    throw new InvalidOperationException(
                        "Cannot create, update or delete tenant-scoped entities in view-all mode without specifying a target tenant. Include the X-Target-Tenant header with the target company ID.");
                }
            }

            foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    // Shared modules always live at TenantId = 0, regardless of current company.
                    var moduleKey = GetModuleKey(entry.Entity);
                    if (moduleKey != null && IsModuleShared(moduleKey))
                    {
                        entry.Entity.TenantId = 0;
                        continue;
                    }

                    // Caller stamped TenantId from a parent entity (e.g. project task ← project).
                    if (entry.Entity.TenantId != 0)
                    {
                        continue;
                    }

                    // In view-all mode, default audit logs to system tenant (0) instead of -1.
                    if (_currentTenantId == -1 && entry.Entity is MyApi.Modules.Shared.Models.SystemLog)
                    {
                        if (entry.Entity.TenantId == 0 || entry.Entity.TenantId == -1)
                            entry.Entity.TenantId = 0;
                    }
                    // Public/anonymous ingest path (e.g. /api/external-receive) runs without an
                    // X-Tenant header → _currentTenantId == 0. The caller (ExternalEndpointService)
                    // explicitly stamps the owning endpoint's TenantId on the log/job entity before
                    // SaveChanges. Honor that explicit non-zero value instead of overwriting it
                    // with 0 — otherwise per-tenant log lists, stats, and retention sweeps break.
                    else if (_currentTenantId == 0 && entry.Entity.TenantId > 0 &&
                             (entry.Entity is MyApi.Modules.ExternalEndpoints.Models.ExternalEndpointLog
                              || entry.Entity is MyApi.Modules.ExternalEndpoints.Models.WebhookForwardJob))
                    {
                        // Keep the explicit TenantId set by the service.
                    }
                    else
                    {
                        entry.Entity.TenantId = _currentTenantId;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    // Protect shared-module rows: never let TenantId drift off 0.
                    var moduleKey = GetModuleKey(entry.Entity);
                    if (moduleKey != null && IsModuleShared(moduleKey) && entry.Entity.TenantId != 0)
                    {
                        entry.Entity.TenantId = 0;
                    }
                }
            }
        }

        private void ApplyEntityConfigurations(ModelBuilder modelBuilder)
        {
            // Auth & Users domain configurations
            new MyApi.Modules.Auth.Data.Configurations.MainAdminUserConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Users.Data.Configurations.UserPreferencesConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Users.Data.Configurations.UserConfiguration().Configure(modelBuilder);
            
            // Roles domain configurations
            new MyApi.Modules.Roles.Data.Configurations.RoleConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Roles.Data.Configurations.UserRoleConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Roles.Data.Configurations.RolePermissionConfiguration().Configure(modelBuilder);
            
            // Skills domain configurations
            new MyApi.Modules.Skills.Data.Configurations.SkillConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Skills.Data.Configurations.UserSkillConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Roles.Data.Configurations.RoleSkillConfiguration().Configure(modelBuilder);

            // User Groups domain configurations
            new MyApi.Modules.UserGroups.Data.Configurations.UserGroupConfiguration().Configure(modelBuilder);
            new MyApi.Modules.UserGroups.Data.Configurations.UserGroupMemberConfiguration().Configure(modelBuilder);
            
            // Contacts domain configurations
            new MyApi.Modules.Contacts.Data.Configurations.ContactConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Contacts.Data.Configurations.ContactNoteConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Contacts.Data.Configurations.ContactTagConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Contacts.Data.Configurations.ContactUserGroupConfiguration().Configure(modelBuilder);


            // Articles domain configurations
            new MyApi.Modules.Articles.Data.Configurations.ArticleNoteConfiguration().Configure(modelBuilder);
            
            // Offers domain configurations
            new MyApi.Modules.Offers.Data.OfferConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Offers.Data.OfferItemConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Offers.Data.OfferActivityConfiguration().Configure(modelBuilder);

            // Deals domain configurations
            new MyApi.Modules.Deals.Data.DealConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Deals.Data.DealItemConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Deals.Data.DealActivityConfiguration().Configure(modelBuilder);
            
            // Sales domain configurations
            new MyApi.Modules.Sales.Data.SaleConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Sales.Data.SaleItemConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Sales.Data.SaleActivityConfiguration().Configure(modelBuilder);

            modelBuilder.ApplyConfiguration(new InstallationConfiguration());
            modelBuilder.ApplyConfiguration(new MaintenanceHistoryConfiguration());
            // Dispatches domain configurations
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Dispatches.Data.DispatchConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Dispatches.Data.DispatchTechnicianConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Dispatches.Data.TimeEntryConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Dispatches.Data.ExpenseConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Dispatches.Data.MaterialUsageConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Dispatches.Data.AttachmentConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Dispatches.Data.NoteConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Dispatches.Data.DispatchAuditLogConfiguration());
            
            modelBuilder.ApplyConfiguration(new ServiceOrderConfiguration());
            modelBuilder.ApplyConfiguration(new ServiceOrderJobConfiguration());
            modelBuilder.ApplyConfiguration(new ServiceOrderMaterialConfiguration());
            modelBuilder.ApplyConfiguration(new ServiceOrderTimeEntryConfiguration());
            modelBuilder.ApplyConfiguration(new ServiceOrderExpenseConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.Planning.Data.PlannedLineEntryConfiguration());

            // Invoices domain configurations (Phase B)
            new MyApi.Modules.Invoices.Data.InvoiceConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Invoices.Data.InvoiceLineConfiguration().Configure(modelBuilder);
            new MyApi.Modules.Invoices.Data.InvoiceActivityConfiguration().Configure(modelBuilder);
            
            // Notifications configuration
            new NotificationConfiguration().Configure(modelBuilder);
            
            // System Logs configuration
            new SystemLogConfiguration().Configure(modelBuilder);
            
            // Entity Form Documents configuration (Status as lowercase string)
            modelBuilder.ApplyConfiguration(new EntityFormDocumentConfiguration());
            
            // Configure remaining entities
            ConfigureArticleEntities(modelBuilder);
            ConfigureCalendarEntities(modelBuilder);
            ConfigureLookupEntities(modelBuilder);
            ConfigureTasksEntities(modelBuilder);
            ConfigurePlanningEntities(modelBuilder);
            ConfigurePlanningProfileEntities(modelBuilder);
            ConfigureHrEntities(modelBuilder);

            // Website Builder Module configurations
            new WBSiteConfiguration().Configure(modelBuilder);
            new WBPageConfiguration().Configure(modelBuilder);
            new WBPageVersionConfiguration().Configure(modelBuilder);
            new WBGlobalBlockConfiguration().Configure(modelBuilder);
            new WBGlobalBlockUsageConfiguration().Configure(modelBuilder);
            new WBBrandProfileConfiguration().Configure(modelBuilder);
            new WBFormSubmissionConfiguration().Configure(modelBuilder);
            new WBMediaConfiguration().Configure(modelBuilder);
            new WBTemplateConfiguration().Configure(modelBuilder);
            new WBActivityLogConfiguration().Configure(modelBuilder);

            // Retenue à la Source configurations
            new RSRecordConfiguration().Configure(modelBuilder);
            new TEJExportLogConfiguration().Configure(modelBuilder);

            modelBuilder.Entity<SyncOperationReceipt>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TenantId, e.DeviceId, e.OpId }).IsUnique();
            });

            modelBuilder.Entity<SyncChange>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TenantId, e.ChangedAt });
                entity.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
            });

            modelBuilder.Entity<OfflineHydrationPreference>(entity =>
            {
                entity.ToTable("OfflineHydrationPreferences");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ModulesJson).HasColumnType("jsonb").IsRequired();
                entity.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();
            });

            modelBuilder.Entity<MyApi.Modules.Reporting.Models.ReportingFavorite>(entity =>
            {
                entity.ToTable("ReportingFavorites");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Scope).HasMaxLength(64).IsRequired();
                entity.Property(e => e.WidgetId).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
                entity.Property(e => e.Source).HasMaxLength(40).IsRequired();
                entity.HasIndex(e => new { e.TenantId, e.UserId, e.Scope, e.WidgetId }).IsUnique();
                entity.HasIndex(e => new { e.TenantId, e.UserId, e.Scope, e.Position });
            });

            modelBuilder.Entity<MyApi.Modules.Dashboards.Models.DashboardLayout>(entity =>
            {
                entity.ToTable("DashboardLayouts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Scope).HasMaxLength(64).IsRequired();
                entity.Property(e => e.OrderJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.HiddenJson).HasColumnType("jsonb").IsRequired();
                entity.HasIndex(e => new { e.TenantId, e.UserId, e.Scope }).IsUnique();
            });

            // External Endpoints Module configurations
            modelBuilder.ApplyConfiguration(new MyApi.Modules.ExternalEndpoints.Database.ExternalEndpointConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.ExternalEndpoints.Database.ExternalEndpointLogConfiguration());
            modelBuilder.ApplyConfiguration(new MyApi.Modules.ExternalEndpoints.Database.WebhookForwardJobConfiguration());

            // Plugin Registry — SQL script creates snake_case table/columns.
            // Keep EF mapping explicit so production does not query "ActivatedModules".
            modelBuilder.Entity<MyApi.Modules.Plugins.Models.ActivatedModule>(entity =>
            {
                entity.ToTable("activated_modules");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.TenantId).HasColumnName("TenantId").HasDefaultValue(0);
                entity.Property(e => e.PluginCode).HasColumnName("plugin_code").HasMaxLength(40).IsRequired();
                entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.HasIndex(e => new { e.TenantId, e.PluginCode }).IsUnique();
                entity.HasIndex(e => e.TenantId);
            });

            // Workflow engine: dedupe + hot-path indexes.
            // The unique index is load-bearing — WorkflowPollingService relies on the
            // insert conflict to detect an entity already claimed by a concurrent poll.
            modelBuilder.Entity<MyApi.Modules.WorkflowEngine.Models.WorkflowProcessedEntity>(entity =>
            {
                entity.HasIndex(e => new { e.TenantId, e.TriggerId, e.EntityType, e.EntityId, e.ProcessedStatus })
                      .IsUnique()
                      .HasDatabaseName("IX_WorkflowProcessedEntities_Dedupe");
            });

            modelBuilder.Entity<MyApi.Modules.WorkflowEngine.Models.WorkflowExecution>(entity =>
            {
                entity.HasIndex(e => new { e.TenantId, e.WorkflowId, e.StartedAt });
                entity.HasIndex(e => new { e.TenantId, e.Status });
                entity.HasIndex(e => new { e.TenantId, e.TriggerEntityType, e.TriggerEntityId });
            });

            modelBuilder.Entity<MyApi.Modules.WorkflowEngine.Models.WorkflowTrigger>(entity =>
            {
                entity.HasIndex(e => new { e.TenantId, e.EntityType, e.IsActive });
            });
        }


        private void ConfigureArticleEntities(ModelBuilder modelBuilder)
        {
            // Article entity configuration - matches actual Article model
            modelBuilder.Entity<Article>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ArticleNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasColumnType("text");
                entity.Property(e => e.Unit).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PurchasePrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SalesPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.StockQuantity).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinStockLevel).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Supplier).HasMaxLength(200);
            });

            // ArticleCategory entity configuration - matches actual model
            modelBuilder.Entity<ArticleCategory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // Location entity configuration - matches actual model
            modelBuilder.Entity<Location>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // InventoryTransaction entity configuration
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne<Article>()
                    .WithMany()
                    .HasForeignKey(e => e.ArticleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // StockTransaction entity configuration
            modelBuilder.Entity<StockTransaction>(entity =>
            {
                entity.ToTable("stock_transactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Quantity).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PreviousStock).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NewStock).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Reason).HasMaxLength(255);
                entity.Property(e => e.ReferenceType).HasMaxLength(50);
                entity.Property(e => e.ReferenceId).HasMaxLength(50);
                entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
                entity.Property(e => e.PerformedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PerformedByName).HasMaxLength(200);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.HasOne(e => e.Article)
                    .WithMany()
                    .HasForeignKey(e => e.ArticleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureCalendarEntities(ModelBuilder modelBuilder)
        {
            // CalendarEvent: FK "Type" -> event_types.Id (DB), contact_id -> Contacts, attendees/reminders
            modelBuilder.Entity<CalendarEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);

                entity.HasOne(e => e.EventTypeNavigation)
                    .WithMany(et => et.CalendarEvents)
                    .HasForeignKey(e => e.Type)
                    .HasPrincipalKey(et => et.Id)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Contact)
                    .WithMany()
                    .HasForeignKey(e => e.ContactId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(e => e.EventAttendees)
                    .WithOne(a => a.CalendarEvent)
                    .HasForeignKey(a => a.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.EventReminders)
                    .WithOne(r => r.CalendarEvent)
                    .HasForeignKey(r => r.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EventType>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<EventAttendee>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<EventReminder>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }

        private void ConfigureLookupEntities(ModelBuilder modelBuilder)
        {
            // LookupItem configuration
            modelBuilder.Entity<LookupItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LookupType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CreatedUser).IsRequired().HasMaxLength(100);
            });

            // Currency configuration
            modelBuilder.Entity<Currency>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(3);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(10);
            });
        }

        private void ConfigureTasksEntities(ModelBuilder modelBuilder)
        {
            // Project entity configuration - matches actual Project model
            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasColumnType("text");
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Priority).HasMaxLength(20);
                entity.Property(e => e.TeamMembers).HasMaxLength(1000);
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ModifiedBy).HasMaxLength(100);
                
                entity.HasOne(e => e.Contact)
                    .WithMany()
                    .HasForeignKey(e => e.ContactId)
                    .OnDelete(DeleteBehavior.SetNull);
            });


            // Project Task entity configuration - simplified to match actual database
            modelBuilder.Entity<ProjectTask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.CreatedBy).HasMaxLength(255);
                entity.Property(e => e.ModifiedBy).HasMaxLength(255);
            });

            // Daily Task entity configuration - simplified to match actual database
            modelBuilder.Entity<DailyTask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Priority).HasMaxLength(10);
                entity.Property(e => e.CreatedBy).HasMaxLength(255);
            });

            // Task Comment entity configuration - simplified to match actual database
            modelBuilder.Entity<TaskComment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Comment).IsRequired().HasMaxLength(2000);
                entity.Property(e => e.CreatedBy).HasMaxLength(255);
            });

            // Task Attachment entity configuration
            modelBuilder.Entity<TaskAttachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UploadedBy).IsRequired().HasMaxLength(100);
                
                entity.HasOne(e => e.ProjectTask)
                    .WithMany()
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Task Time Entry entity configuration
            modelBuilder.Entity<TaskTimeEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserName).HasMaxLength(100);
                entity.Property(e => e.Description).HasColumnType("text");
                entity.Property(e => e.Duration).HasColumnType("decimal(18,2)");
                entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.WorkType).IsRequired().HasMaxLength(50).HasDefaultValue("work");
                entity.Property(e => e.ApprovalStatus).IsRequired().HasMaxLength(20).HasDefaultValue("pending");
                entity.Property(e => e.ApprovalNotes).HasColumnType("text");
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ModifiedBy).HasMaxLength(100);

                entity.HasOne(e => e.ProjectTask)
                    .WithMany()
                    .HasForeignKey(e => e.ProjectTaskId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.DailyTask)
                    .WithMany()
                    .HasForeignKey(e => e.DailyTaskId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.ApprovedBy)
                    .WithMany()
                    .HasForeignKey(e => e.ApprovedById)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => e.ProjectTaskId);
                entity.HasIndex(e => e.DailyTaskId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.ApprovalStatus);
            });

            // Task Checklist entity configuration
            modelBuilder.Entity<TaskChecklist>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasColumnType("text");
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ModifiedBy).HasMaxLength(100);

                entity.HasOne(e => e.ProjectTask)
                    .WithMany()
                    .HasForeignKey(e => e.ProjectTaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.DailyTask)
                    .WithMany()
                    .HasForeignKey(e => e.DailyTaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProjectTaskId);
                entity.HasIndex(e => e.DailyTaskId);
            });

            // Task Checklist Item entity configuration
            modelBuilder.Entity<TaskChecklistItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
                entity.Property(e => e.CompletedByName).HasMaxLength(100);
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ModifiedBy).HasMaxLength(100);

                entity.HasOne(e => e.Checklist)
                    .WithMany(c => c.Items)
                    .HasForeignKey(e => e.ChecklistId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.CompletedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CompletedById)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.ChecklistId);
            });

            // Recurring Task entity configuration
            modelBuilder.Entity<RecurringTask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RecurrenceType).IsRequired().HasMaxLength(50).HasDefaultValue("daily");
                entity.Property(e => e.DaysOfWeek).HasMaxLength(50);
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ModifiedBy).HasMaxLength(100);

                entity.HasOne(e => e.ProjectTask)
                    .WithMany()
                    .HasForeignKey(e => e.ProjectTaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.DailyTask)
                    .WithMany()
                    .HasForeignKey(e => e.DailyTaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProjectTaskId);
                entity.HasIndex(e => e.DailyTaskId);
                entity.HasIndex(e => e.NextOccurrence);
                entity.HasIndex(e => e.IsActive);
            });

            // Recurring Task Log entity configuration
            modelBuilder.Entity<RecurringTaskLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("created");
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(e => e.RecurringTask)
                    .WithMany()
                    .HasForeignKey(e => e.RecurringTaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.RecurringTaskId);
                entity.HasIndex(e => e.GeneratedDate);
            });

            modelBuilder.Entity<ProjectSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TenantId).IsUnique();
                entity.Property(e => e.SettingsJson).HasColumnType("text");
            });

            // AI Conversation entity configuration
            modelBuilder.Entity<AiConversation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Title).HasMaxLength(500).HasDefaultValue("New Conversation");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.IsPinned).HasDefaultValue(false);
                entity.Property(e => e.IsArchived).HasDefaultValue(false);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.UserId, e.IsDeleted });
            });

            // AI Message entity configuration
            modelBuilder.Entity<AiMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Feedback).HasMaxLength(20);

                entity.HasOne(e => e.Conversation)
                    .WithMany(c => c.Messages)
                    .HasForeignKey(e => e.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ConversationId);
                entity.HasIndex(e => e.CreatedAt);
            });
        }

        /// <summary>
        /// Planning module entities: UserLeave, UserWorkingHours, UserStatusHistory
        /// IMPORTANT: These tables intentionally have NO foreign key to "Users" because
        /// user_id can reference either MainAdminUsers (id=1) or Users (id>=2).
        /// We must explicitly tell EF Core to ignore the convention-based FK.
        /// </summary>
        private void ConfigurePlanningProfileEntities(ModelBuilder modelBuilder)
        {
            // planning_profiles table: snake_case column names including tenant_id
            modelBuilder.Entity<PlanningProfile>(entity =>
            {
                entity.ToTable("planning_profiles");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId).HasColumnName("tenant_id");
                // All other properties carry explicit [Column] annotations
                entity.HasIndex(e => new { e.TenantId, e.OwnerUserId })
                    .HasFilter("deleted_at IS NULL");
                entity.HasIndex(e => new { e.TenantId, e.IsShared })
                    .HasFilter("deleted_at IS NULL");
            });

            // user_active_planning_profile: composite PK (user_id, tenant_id)
            modelBuilder.Entity<UserActivePlanningProfile>(entity =>
            {
                entity.ToTable("user_active_planning_profile");
                entity.HasKey(e => new { e.UserId, e.TenantId });
                entity.Property(e => e.TenantId).HasColumnName("tenant_id");
                // UserId, ProfileId, UpdatedAt all carry [Column] annotations
                entity.HasIndex(e => e.ProfileId);
            });
        }

        private void ConfigurePlanningEntities(ModelBuilder modelBuilder)
        {
            // UserLeave - no FK on UserId (supports both MainAdminUsers and Users)
            modelBuilder.Entity<UserLeave>(entity =>
            {
                entity.ToTable("user_leaves");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.LeaveType).HasColumnName("leave_type").HasMaxLength(100).IsRequired();
                entity.Property(e => e.StartDate).HasColumnName("start_date").IsRequired();
                entity.Property(e => e.EndDate).HasColumnName("end_date").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("approved");
                entity.Property(e => e.Reason).HasColumnName("reason");
                entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
                entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                // Explicitly ignore convention-based FK to Users table
                entity.Ignore("User");
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.StartDate, e.EndDate });
                entity.HasIndex(e => e.Status);
            });

            // UserWorkingHours - no FK on UserId (supports both MainAdminUsers and Users)
            modelBuilder.Entity<UserWorkingHours>(entity =>
            {
                entity.ToTable("user_working_hours");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.DayOfWeek).HasColumnName("day_of_week").IsRequired();
                entity.Property(e => e.StartTime).HasColumnName("start_time").IsRequired();
                entity.Property(e => e.EndTime).HasColumnName("end_time").IsRequired();
                entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
                entity.Property(e => e.EffectiveUntil).HasColumnName("effective_until");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.DayOfWeek);
                entity.HasIndex(e => new { e.UserId, e.DayOfWeek }).IsUnique();
            });

            // UserStatusHistory - no FK on UserId (supports both MainAdminUsers and Users)
            modelBuilder.Entity<UserStatusHistory>(entity =>
            {
                entity.ToTable("user_status_history");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.NewStatus).HasColumnName("new_status").HasMaxLength(50).IsRequired();
                entity.Property(e => e.PreviousStatus).HasColumnName("previous_status").HasMaxLength(50);
                entity.Property(e => e.Reason).HasColumnName("reason");
                entity.Property(e => e.ChangedAt).HasColumnName("changed_at");
                entity.Property(e => e.ChangedBy).HasColumnName("changed_by");

                entity.HasIndex(e => e.UserId);
            });

            // TechnicianStatusHistory - snake_case table, no FK on TechnicianId
            modelBuilder.Entity<TechnicianStatusHistory>(entity =>
            {
                entity.ToTable("technician_status_history");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.TechnicianId).HasColumnName("technician_id").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
                entity.Property(e => e.ChangedFrom).HasColumnName("changed_from").HasMaxLength(50);
                entity.Property(e => e.ChangedAt).HasColumnName("changed_at");
                entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
                entity.Property(e => e.Reason).HasColumnName("reason");
                entity.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");

                entity.HasIndex(e => e.TechnicianId);
            });
        }

        private void ConfigureHrEntities(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HrEmployeeSalaryConfig>(entity =>
            {
                entity.ToTable("hr_employee_salary_configs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId).IsUnique();
            });

            modelBuilder.Entity<HrDepartment>(entity =>
            {
                entity.ToTable("hr_departments");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name);
            });

            modelBuilder.Entity<HrLeaveBalance>(entity =>
            {
                entity.ToTable("hr_leave_balances");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.Year, e.LeaveType }).IsUnique();
            });

            // One attendance row per (tenant, employee, day). The unique index is what
            // makes HrService.UpsertAttendanceAsync's concurrent-insert recovery work;
            // without it duplicate rows double-count hours in payroll.
            modelBuilder.Entity<HrAttendance>(entity =>
            {
                entity.ToTable("hr_attendance");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.TenantId, e.UserId, e.Date }).IsUnique();
            });

            modelBuilder.Entity<HrPayrollRun>(entity =>
            {
                entity.ToTable("hr_payroll_runs");
                entity.HasKey(e => e.Id);
                // Unique per tenant/period — backs the duplicate-run guard in HrService.
                entity.HasIndex(e => new { e.TenantId, e.Year, e.Month }).IsUnique();
            });


            modelBuilder.Entity<HrPayrollEntry>(entity =>
            {
                entity.ToTable("hr_payroll_entries");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.PayrollRunId, e.UserId });
            });

            modelBuilder.Entity<HrBonusCost>(entity =>
            {
                entity.ToTable("hr_bonus_costs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.Year, e.Month });
            });

            modelBuilder.Entity<HrAuditLog>(entity =>
            {
                entity.ToTable("hr_audit_logs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.EventType });
            });

            modelBuilder.Entity<HrCnssRate>(entity =>
            {
                entity.ToTable("hr_cnss_rates");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<HrPublicHoliday>(entity =>
            {
                entity.ToTable("hr_public_holidays");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Date);
            });

            modelBuilder.Entity<HrEmployeeDocument>(entity =>
            {
                entity.ToTable("hr_employee_documents");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
            });

            modelBuilder.Entity<HrSalaryHistory>(entity =>
            {
                entity.ToTable("hr_salary_history");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
            });
        }

        private void ApplySeedData(ModelBuilder modelBuilder)
        {
            // Apply seed data from separate files
            new LookupSeedData().Seed(modelBuilder);
            new CurrencySeedData().Seed(modelBuilder);
            new NumberingSeedData().Seed(modelBuilder);
        }
    }
}
