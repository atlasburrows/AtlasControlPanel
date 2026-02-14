using System.Data;
using Atlas.Application.Common.Interfaces;

namespace Atlas.MAUI.Services;

public class HttpDbConnectionFactory : IDbConnectionFactory
{
    public IDbConnection CreateConnection()
        => throw new NotSupportedException("MAUI app does not support direct database connections. Use the API instead.");
}
