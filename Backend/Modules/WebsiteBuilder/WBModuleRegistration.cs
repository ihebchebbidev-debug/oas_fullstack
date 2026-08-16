// ============================================================
// Website Builder Module — Dependency Injection Registration
// Add this to your Program.cs or Startup.cs
// ============================================================
//
// STEP 1: Add these using statements at the top of Program.cs:
//
//   using MyApi.Modules.WebsiteBuilder.Models;
//   using MyApi.Modules.WebsiteBuilder.Services;
//   using MyApi.Modules.WebsiteBuilder.Data.Configurations;
//
// STEP 2: Add DbSet properties to ApplicationDbContext.cs:
//
//   // Website Builder Module
//   public DbSet<WBSite> WBSites { get; set; }
//   public DbSet<WBPage> WBPages { get; set; }
//   public DbSet<WBPageVersion> WBPageVersions { get; set; }
//   public DbSet<WBGlobalBlock> WBGlobalBlocks { get; set; }
//   public DbSet<WBGlobalBlockUsage> WBGlobalBlockUsages { get; set; }
//   public DbSet<WBBrandProfile> WBBrandProfiles { get; set; }
//   public DbSet<WBFormSubmission> WBFormSubmissions { get; set; }
//   public DbSet<WBMedia> WBMedia { get; set; }
//   public DbSet<WBTemplate> WBTemplates { get; set; }
//   public DbSet<WBActivityLog> WBActivityLogs { get; set; }
//
// STEP 3: Add entity configurations in ApplyEntityConfigurations():
//
//   // Website Builder Module configurations
//   new WBSiteConfiguration().Configure(modelBuilder);
//   new WBPageConfiguration().Configure(modelBuilder);
//   new WBPageVersionConfiguration().Configure(modelBuilder);
//   new WBGlobalBlockConfiguration().Configure(modelBuilder);
//   new WBGlobalBlockUsageConfiguration().Configure(modelBuilder);
//   new WBBrandProfileConfiguration().Configure(modelBuilder);
//   new WBFormSubmissionConfiguration().Configure(modelBuilder);
//   new WBMediaConfiguration().Configure(modelBuilder);
//   new WBTemplateConfiguration().Configure(modelBuilder);
//   new WBActivityLogConfiguration().Configure(modelBuilder);
//
// STEP 4: Register services in Program.cs (builder.Services section):
//
//   // Website Builder Module
//   builder.Services.AddScoped<IWBActivityLogService, WBActivityLogService>();
//   builder.Services.AddScoped<IWBSiteService, WBSiteService>();
//   builder.Services.AddScoped<IWBPageService, WBPageService>();
//   builder.Services.AddScoped<IWBGlobalBlockService, WBGlobalBlockService>();
//   builder.Services.AddScoped<IWBBrandProfileService, WBBrandProfileService>();
//   builder.Services.AddScoped<IWBFormSubmissionService, WBFormSubmissionService>();
//   builder.Services.AddScoped<IWBMediaService, WBMediaService>();
//   builder.Services.AddScoped<IWBTemplateService, WBTemplateService>();
//
// NOTE: Register IWBActivityLogService FIRST since other services depend on it.
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using MyApi.Modules.WebsiteBuilder.Services;

namespace MyApi.Modules.WebsiteBuilder
{
    /// <summary>
    /// Registration helper for the Website Builder module.
    /// Call builder.Services.AddWebsiteBuilderServices() in Program.cs
    /// </summary>
    public static class WBModuleRegistration
    {
        public static IServiceCollection AddWebsiteBuilderServices(this IServiceCollection services)
        {
            // Register IWBActivityLogService FIRST since other services depend on it
            services.AddScoped<IWBActivityLogService, WBActivityLogService>();
            services.AddScoped<IWBSiteService, WBSiteService>();
            services.AddScoped<IWBPageService, WBPageService>();
            services.AddScoped<IWBGlobalBlockService, WBGlobalBlockService>();
            services.AddScoped<IWBBrandProfileService, WBBrandProfileService>();
            services.AddScoped<IWBFormSubmissionService, WBFormSubmissionService>();
            services.AddScoped<IWBMediaService, WBMediaService>();
            services.AddScoped<IWBTemplateService, WBTemplateService>();
            return services;
        }
    }
}
