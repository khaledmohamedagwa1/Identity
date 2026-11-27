using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Platform.Application.Tenancy;

namespace Platform.Infrastructure.Tenancy;

/// <summary>
/// Defence in depth for tenant isolation (section 7.3, ADR-0007). Sets the PostgreSQL setting
/// <c>app.tenant_id</c> that row-level security policies compare against:
/// <list type="bullet">
/// <item>at the start of every transaction with <c>set_config(..., true)</c> (local to the transaction, as specified);</item>
/// <item>on every connection open with <c>set_config(..., false)</c>, always overwriting the previous value
/// (empty when there is no tenant), so reads outside an explicit transaction are filtered too and a pooled
/// connection can never carry another request's tenant (TC-F03-06).</item>
/// </list>
/// </summary>
public sealed class TenantRlsInterceptor(ITenantContext tenant) : IDbConnectionInterceptor, IDbTransactionInterceptor
{
    /// <summary>The PostgreSQL configuration parameter used by RLS policies.</summary>
    public const string SettingName = "app.tenant_id";

    private string CurrentValue => tenant.TenantId?.Value.ToString() ?? string.Empty;

    /// <inheritdoc />
    public void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Apply(connection, null, isLocal: false);

    /// <inheritdoc />
    public Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) =>
        ApplyAsync(connection, null, isLocal: false, cancellationToken);

    /// <inheritdoc />
    public DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Apply(connection, result, isLocal: true);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(connection, result, isLocal: true, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private void Apply(DbConnection connection, DbTransaction? transaction, bool isLocal)
    {
        using var command = CreateCommand(connection, transaction, isLocal);
        command.ExecuteNonQuery();
    }

    private async Task ApplyAsync(DbConnection connection, DbTransaction? transaction, bool isLocal, CancellationToken cancellationToken)
    {
        var command = CreateCommand(connection, transaction, isLocal);
        await using (command.ConfigureAwait(false))
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private DbCommand CreateCommand(DbConnection connection, DbTransaction? transaction, bool isLocal)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = isLocal
            ? "SELECT set_config('app.tenant_id', @tid, true)"
            : "SELECT set_config('app.tenant_id', @tid, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tid";
        parameter.Value = CurrentValue;
        command.Parameters.Add(parameter);
        return command;
    }
}
