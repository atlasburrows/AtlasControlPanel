using Atlas.Application.Common.Interfaces;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.Sqlite;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using IDbConnectionFactory = Atlas.Application.Common.Interfaces.IDbConnectionFactory;

namespace Atlas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Detect if SQLite or SQL Server based on connection string
        bool isSqlite = connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
                        && !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);

        if (isSqlite)
        {
            // Initialize SQLite database
            SqliteInitializer.EnsureCreated(connectionString);
            
            // Register SQLite factory
            services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(connectionString));
            
            // Register SQLite repositories
            services.AddScoped<ITaskRepository, SqliteTaskRepository>();
            services.AddScoped<IActivityRepository, SqliteActivityRepository>();
            services.AddScoped<ISecurityRepository, SqliteSecurityRepository>();
            services.AddScoped<IMonitoringRepository, SqliteMonitoringRepository>();
            services.AddScoped<ICredentialRepository, SqliteCredentialRepository>();
            services.AddScoped<IChatMessageRepository, SqliteChatMessageRepository>();
            services.AddScoped<IPairingRepository, SqlitePairingRepository>();
            services.AddScoped<ICredentialAccessLogRepository>(sp => new SqliteCredentialAccessLogRepository(connectionString));
            services.AddScoped<ITokenUsageRepository, SqliteTokenUsageRepository>();
        }
        else
        {
            // Register SQL Server factory (existing implementation)
            services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));
            
            // Register SQL Server repositories
            services.AddScoped<ITaskRepository, TaskRepository>();
            services.AddScoped<IActivityRepository, ActivityRepository>();
            services.AddScoped<ISecurityRepository, SecurityRepository>();
            services.AddScoped<IMonitoringRepository, MonitoringRepository>();
            services.AddScoped<ICredentialRepository, CredentialRepository>();
            services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
            services.AddScoped<IPairingRepository, PairingRepository>();
            services.AddScoped<ICredentialAccessLogRepository, CredentialAccessLogRepository>();
            services.AddScoped<ITokenUsageRepository, TokenUsageRepository>();
        }
        
        // Register analytics services
        services.AddScoped<ICostEfficiencyAnalyzer, CostEfficiencyAnalyzer>();
        
        // Data Protection for credential encryption (keys stored per-user)
        services.AddDataProtection()
            .SetApplicationName("AtlasControlPanel");
        services.AddSingleton<ICredentialEncryption, CredentialEncryption>();
        
        services.AddSingleton<ISecretStore, EncryptedFileSecretStore>();

        return services;
    }
}
