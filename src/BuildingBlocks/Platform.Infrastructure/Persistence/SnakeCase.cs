using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Platform.Infrastructure.Persistence;

/// <summary>Applies the PostgreSQL naming convention of section 7.4: snake_case tables, columns, keys and indexes.</summary>
internal static class SnakeCase
{
    public static string Convert(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var prevLower = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var nextLower = i + 1 < name.Length && char.IsLower(name[i + 1]) && i > 0 && char.IsUpper(name[i - 1]);
                if (prevLower || nextLower)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is { } table && !entity.IsOwned())
            {
                entity.SetTableName(Convert(table));
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(Convert(property.GetColumnName()));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(Convert(key.GetName()!));
            }

            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(Convert(fk.GetConstraintName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(Convert(index.GetDatabaseName()!));
            }
        }
    }
}
