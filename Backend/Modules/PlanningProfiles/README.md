# Planning Profiles backend module — wiring TODO

Add to `Backend/Program.cs` near other AddScoped registrations (e.g. after the Planning service):

```csharp
using MyApi.Modules.PlanningProfiles.Services;

builder.Services.AddScoped<IPlanningProfileService, PlanningProfileService>();
```

Register entities on `ApplicationDbContext` (if your project uses explicit DbSet declarations):

```csharp
public DbSet<MyApi.Modules.PlanningProfiles.Models.PlanningProfile> PlanningProfiles { get; set; } = null!;
public DbSet<MyApi.Modules.PlanningProfiles.Models.UserActivePlanningProfile> UserActivePlanningProfiles { get; set; } = null!;
```

If the context uses convention-based discovery (`modelBuilder.Set<T>()` via `ITenantEntity` scan), no DbSet declaration is required.

Run the migration:

```bash
psql $DATABASE_URL -f Backend/Migrations/20260518_planning_profiles.sql
```
