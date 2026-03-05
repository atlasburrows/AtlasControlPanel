using System.Data;
using Vigil.Application.Common.Interfaces;

namespace Vigil.MAUI.Services;

public class HttpDbConnectionFactory : IDbConnectionFactory
{
    public IDbConnection CreateConnection()
        => throw new NotSupportedException("MAUI app does not support direct database connections. Use the API instead.");
}
