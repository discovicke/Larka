using Npgsql;
using Testcontainers.PostgreSql;

namespace backend.Tests;

/// <summary>
/// En Postgres-container som delas av alla testklasser i körningen. Testcontainers städar bort den efteråt.
/// </summary>
internal static class PostgresServer
{
    private static readonly Lazy<PostgreSqlContainer> _container = new(Start);

    /// <summary>Skapar en tom databas och returnerar connection string till den.</summary>
    public static string CreateDatabase(string name)
    {
        Execute($"CREATE DATABASE \"{name}\"");

        var builder = new NpgsqlConnectionStringBuilder(_container.Value.GetConnectionString()) { Database = name };

        return builder.ConnectionString;
    }

    public static void DropDatabase(string name)
    {
        // Poolade anslutningar håller databasen öppen, så de stängs först.
        NpgsqlConnection.ClearAllPools();
        Execute($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    private static PostgreSqlContainer Start()
    {
        var container = new PostgreSqlBuilder("postgres:17").Build();
        container.StartAsync().GetAwaiter().GetResult();

        return container;
    }

    private static void Execute(string sql)
    {
        using var connection = new NpgsqlConnection(_container.Value.GetConnectionString());
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}
