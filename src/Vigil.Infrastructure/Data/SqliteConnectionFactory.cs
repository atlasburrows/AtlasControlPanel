using System.Data;
using Vigil.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;

namespace Vigil.Infrastructure.Data;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString) => _connectionString = connectionString;

    public IDbConnection CreateConnection() => new SqliteConnection(_connectionString);
}
