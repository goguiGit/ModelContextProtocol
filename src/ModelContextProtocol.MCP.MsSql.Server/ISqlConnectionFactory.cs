using Microsoft.Data.SqlClient;

namespace ModelContextProtocol.MCP.MsSql.Server;

/// <summary>
/// Defines a factory interface for creating SQL database connections.
/// </summary>
public interface ISqlConnectionFactory
{
    Task<SqlConnection> GetOpenConnectionAsync();
}