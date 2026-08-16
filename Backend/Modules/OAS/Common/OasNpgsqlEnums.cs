using Npgsql;

namespace MyApi.Modules.OAS.Common;

/// <summary>
/// Central registry mapping every native Postgres enum type used by the OAS
/// schema (public/OAS-SQL/001_schema.sql) to its C# enum, via Npgsql's
/// `NpgsqlDataSourceBuilder.MapEnum&lt;T&gt;`. Without this, Npgsql sends
/// enum-typed parameters as plain text, which Postgres does not implicitly
/// cast to a custom enum column — every insert/update on an enum column
/// would fail at runtime despite compiling and despite matching the SQL
/// schema exactly. One registration call per enum, added here the same lot
/// the enum is introduced — never scattered across sub-modules, so nobody
/// forgets one.
///
/// Requirement: the C# enum's member names must match the Postgres enum's
/// labels exactly (case-sensitive) — e.g. `OasAppRole.@operator` reflects
/// as "operator", matching `oas_app_role`'s 'operator' label.
/// </summary>
public static class OasNpgsqlEnums
{
    public static NpgsqlDataSourceBuilder ConfigureMappings(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<ShopFloorAuth.Models.OasAppRole>("oas_app_role");
        builder.MapEnum<ShopFloorAuth.Models.OasWorkspace>("oas_workspace");
        builder.MapEnum<Equipments.Models.OasCriticality>("oas_criticality");
        builder.MapEnum<Products.Models.OasOrderStatus>("oas_order_status");
        builder.MapEnum<Causes.Models.OasCauseDomain>("oas_cause_domain");
        builder.MapEnum<Causes.Models.OasEventType>("oas_event_type");
        builder.MapEnum<Shifts.Models.OasShiftCode>("oas_shift_code");
        builder.MapEnum<Imports.Models.OasImportStatus>("oas_import_status");
        builder.MapEnum<Declarations.Models.OasDeclarationKind>("oas_declaration_kind");
        builder.MapEnum<Quality.Models.OasCheckType>("oas_check_type");
        builder.MapEnum<Quality.Models.OasCheckResult>("oas_check_result");
        builder.MapEnum<Events.Models.OasEventStatus>("oas_event_status");
        builder.MapEnum<Events.Models.OasClosureType>("oas_closure_type");
        builder.MapEnum<Events.Models.OasEscalationLevel>("oas_escalation_level");
        builder.MapEnum<Events.Models.OasNotifyResponse>("oas_notify_response");
        builder.MapEnum<PostStates.Models.OasPostStateValue>("oas_post_state");
        builder.MapEnum<Audit.Models.OasAuditAction>("oas_audit_action");
        return builder;
    }
}
