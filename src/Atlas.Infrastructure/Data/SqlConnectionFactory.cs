using System.Data;
using Atlas.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;

namespace Atlas.Infrastructure.Data;

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    public SqlConnectionFactory(string connectionString) => _connectionString = connectionString;
    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
