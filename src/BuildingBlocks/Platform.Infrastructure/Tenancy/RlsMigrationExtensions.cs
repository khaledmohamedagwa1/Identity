using Microsoft.EntityFrameworkCore.Migrations;

namespace Platform.Infrastructure.Tenancy;

/// <summary>
/// Migration helpers that put a tenant table under row-level security (section 7.3). Every table of
/// an <c>ITenantOwned</c> entity must call <see cref="EnableTenantRls"/> in its migration; the
/// architecture/integration suite checks it.
/// </summary>
public static class RlsMigrationExtensions
{
    /// <summary>SQL expression for the current tenant; empty or missing setting yields NULL, so no row matches.</summary>
    public const string CurrentTenantExpression = "nullif(current_setting('app.tenant_id', true), '')::uuid";

    /// <summary>Returns the SQL enabling RLS with the <c>tenant_isolation</c> policy on <paramref name="schema"/>.<paramref name="table"/>.</summary>
    public static string EnableTenantRlsSql(string schema, string table, string tenantColumn = "tenant_id") =>
        $"""
        ALTER TABLE {schema}.{table} ENABLE ROW LEVEL SECURITY;
        ALTER TABLE {schema}.{table} FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON {schema}.{table}
          USING ({tenantColumn} = {CurrentTenantExpression})
          WITH CHECK ({tenantColumn} = {CurrentTenantExpression});
        """;

    /// <summary>Returns the SQL that reverses <see cref="EnableTenantRlsSql"/>.</summary>
    public static string DisableTenantRlsSql(string schema, string table) =>
        $"""
        DROP POLICY IF EXISTS tenant_isolation ON {schema}.{table};
        ALTER TABLE {schema}.{table} NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE {schema}.{table} DISABLE ROW LEVEL SECURITY;
        """;

    /// <summary>Enables RLS and the tenant policy on a table (use in <c>Up</c>).</summary>
    public static MigrationBuilder EnableTenantRls(this MigrationBuilder migrationBuilder, string schema, string table, string tenantColumn = "tenant_id")
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(EnableTenantRlsSql(schema, table, tenantColumn));
        return migrationBuilder;
    }

    /// <summary>Removes the tenant policy and RLS from a table (use in <c>Down</c>).</summary>
    public static MigrationBuilder DisableTenantRls(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(DisableTenantRlsSql(schema, table));
        return migrationBuilder;
    }
}
