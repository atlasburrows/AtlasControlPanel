using System.Data;
using Atlas.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;

namespace Atlas.Infrastructure.Data;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString) => _connectionString = connectionString;

    public IDbConnection CreateConnection() => new SqliteConnection(_connectionString);
}
