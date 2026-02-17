using Microsoft.Data.Sqlite;

namespace Atlas.Infrastructure.Data;

public static class SqliteInitializer
{
    public static void EnsureCreated(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Create tables if they don't exist
        var command = connection.CreateCommand();
        command.CommandText = @"
            -- Tasks table
            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Description TEXT,
                Status TEXT NOT NULL DEFAULT 'ToDo',
                Priority TEXT NOT NULL DEFAULT 'Medium',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT,
                AssignedTo TEXT,
                TokensUsed INTEGER NOT NULL DEFAULT 0,
                ApiCalls INTEGER NOT NULL DEFAULT 0,
                EstimatedCost REAL NOT NULL DEFAULT 0.0,
                Currency TEXT NOT NULL DEFAULT 'USD',
                ScheduledAt TEXT,
                RecurrenceType TEXT NOT NULL DEFAULT 'None',
                RecurrenceInterval INTEGER NOT NULL DEFAULT 0,
                RecurrenceDays TEXT,
                NextRunAt TEXT,
                LastRunAt TEXT
            );

            -- ActivityLogs table
            CREATE TABLE IF NOT EXISTS ActivityLogs (
                Id TEXT PRIMARY KEY,
                Action TEXT NOT NULL,
                Description TEXT,
                Timestamp TEXT NOT NULL,
                Category TEXT NOT NULL,
                TokensUsed INTEGER NOT NULL DEFAULT 0,
                ApiCalls INTEGER NOT NULL DEFAULT 0,
                EstimatedCost REAL NOT NULL DEFAULT 0.0,
                Currency TEXT NOT NULL DEFAULT 'USD',
                RelatedTaskId TEXT,
                ParentId TEXT,
                Details TEXT
            );

            -- PermissionRequests table
            CREATE TABLE IF NOT EXISTS PermissionRequests (
                Id TEXT PRIMARY KEY,
                RequestType TEXT NOT NULL,
                Description TEXT,
                Status TEXT NOT NULL DEFAULT 'Pending',
                RequestedAt TEXT NOT NULL,
                ResolvedAt TEXT,
                ResolvedBy TEXT,
                Urgency TEXT,
                CredentialId TEXT,
                Category TEXT,
                ExpiresAt TEXT
            );

            -- SecurityAudits table
            CREATE TABLE IF NOT EXISTS SecurityAudits (
                Id TEXT PRIMARY KEY,
                Action TEXT NOT NULL,
                Severity TEXT NOT NULL,
                Details TEXT,
                Timestamp TEXT NOT NULL
            );

            -- SystemStatus table
            CREATE TABLE IF NOT EXISTS SystemStatus (
                Id TEXT PRIMARY KEY,
                GatewayHealth TEXT,
                ActiveSessions INTEGER NOT NULL DEFAULT 0,
                MemoryUsage REAL NOT NULL DEFAULT 0.0,
                Uptime INTEGER NOT NULL DEFAULT 0,
                LastChecked TEXT NOT NULL,
                AnthropicBalance TEXT,
                TokensRemaining TEXT
            );

            -- CostSummary table
            CREATE TABLE IF NOT EXISTS CostSummary (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL UNIQUE,
                DailyCost REAL NOT NULL DEFAULT 0.0,
                MonthlyCost REAL NOT NULL DEFAULT 0.0,
                TaskBreakdown TEXT
            );

            -- SecureCredentials table
            CREATE TABLE IF NOT EXISTS SecureCredentials (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Category TEXT NOT NULL,
                Username TEXT,
                Description TEXT,
                StorageKey TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastAccessedAt TEXT,
                AccessCount INTEGER NOT NULL DEFAULT 0,
                VaultMode TEXT NOT NULL DEFAULT 'locked'
            );

            -- ChatMessages table
            CREATE TABLE IF NOT EXISTS ChatMessages (
                Id TEXT PRIMARY KEY,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            -- CredentialAccessLog table
            CREATE TABLE IF NOT EXISTS CredentialAccessLog (
                Id TEXT PRIMARY KEY,
                CredentialId TEXT NOT NULL,
                CredentialName TEXT NOT NULL,
                Requester TEXT,
                AccessedAt TEXT NOT NULL,
                VaultMode TEXT NOT NULL DEFAULT 'locked',
                AutoApproved INTEGER NOT NULL DEFAULT 0,
                Details TEXT
            );

            -- DailyCosts table
            CREATE TABLE IF NOT EXISTS DailyCosts (
                Date TEXT PRIMARY KEY,
                Cost REAL NOT NULL DEFAULT 0.0
            );

            -- PairingCodes table
            CREATE TABLE IF NOT EXISTS PairingCodes (
                Id TEXT PRIMARY KEY,
                Code TEXT NOT NULL,
                Token TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                IsUsed INTEGER NOT NULL DEFAULT 0
            );

            -- PairedDevices table
            CREATE TABLE IF NOT EXISTS PairedDevices (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                DeviceType TEXT NOT NULL DEFAULT 'mobile',
                ApiKey TEXT NOT NULL UNIQUE,
                Platform TEXT,
                PairedAt TEXT NOT NULL,
                LastSeenAt TEXT,
                LastIpAddress TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- TokenUsage table for granular cost tracking
            CREATE TABLE IF NOT EXISTS TokenUsage (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                Provider TEXT NOT NULL,
                Model TEXT NOT NULL,
                InputTokens INTEGER NOT NULL DEFAULT 0,
                OutputTokens INTEGER NOT NULL DEFAULT 0,
                TotalTokens INTEGER NOT NULL DEFAULT 0,
                CostUsd REAL NOT NULL DEFAULT 0.0,
                DurationMs INTEGER,
                SessionKey TEXT,
                TaskCategory TEXT,
                ContextPercent INTEGER
            );

            -- Indexes for common queries
            CREATE INDEX IF NOT EXISTS idx_tasks_status ON Tasks(Status);
            CREATE INDEX IF NOT EXISTS idx_tasks_priority ON Tasks(Priority);
            CREATE INDEX IF NOT EXISTS idx_activitylogs_timestamp ON ActivityLogs(Timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_activitylogs_task ON ActivityLogs(RelatedTaskId);
            CREATE INDEX IF NOT EXISTS idx_permissionrequests_status ON PermissionRequests(Status);
            CREATE INDEX IF NOT EXISTS idx_credentials_name ON SecureCredentials(Name);
            CREATE INDEX IF NOT EXISTS idx_chatmessages_created ON ChatMessages(CreatedAt DESC);
            CREATE INDEX IF NOT EXISTS idx_pairingcodes_token ON PairingCodes(Token);
            CREATE INDEX IF NOT EXISTS idx_paireddevices_apikey ON PairedDevices(ApiKey);
            CREATE INDEX IF NOT EXISTS idx_paireddevices_active ON PairedDevices(IsActive);
            CREATE INDEX IF NOT EXISTS idx_credentialaccess_credentialid ON CredentialAccessLog(CredentialId);
            CREATE INDEX IF NOT EXISTS idx_credentialaccess_accessed ON CredentialAccessLog(AccessedAt DESC);
            CREATE INDEX IF NOT EXISTS idx_tokenusage_timestamp ON TokenUsage(Timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_tokenusage_model ON TokenUsage(Model);
            CREATE INDEX IF NOT EXISTS idx_tokenusage_session ON TokenUsage(SessionKey);

            -- HealthChecks table
            CREATE TABLE IF NOT EXISTS HealthChecks (
                Id TEXT PRIMARY KEY,
                ServiceName TEXT NOT NULL,
                Status TEXT NOT NULL,
                CheckedAt TEXT NOT NULL,
                ResponseTimeMs REAL,
                Details TEXT,
                AutoRestarted INTEGER NOT NULL DEFAULT 0
            );

            -- HealthEvents table
            CREATE TABLE IF NOT EXISTS HealthEvents (
                Id TEXT PRIMARY KEY,
                ServiceName TEXT NOT NULL,
                EventType TEXT NOT NULL,
                OccurredAt TEXT NOT NULL,
                Details TEXT,
                NotificationSent INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_healthchecks_service_time ON HealthChecks(ServiceName, CheckedAt DESC);
            CREATE INDEX IF NOT EXISTS idx_healthevents_service_time ON HealthEvents(ServiceName, OccurredAt DESC);

            -- CredentialGroups table
            CREATE TABLE IF NOT EXISTS CredentialGroups (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Category TEXT,
                Description TEXT,
                Icon TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            -- CredentialGroupMembers junction table
            CREATE TABLE IF NOT EXISTS CredentialGroupMembers (
                GroupId TEXT NOT NULL,
                CredentialId TEXT NOT NULL,
                PRIMARY KEY (GroupId, CredentialId),
                FOREIGN KEY (GroupId) REFERENCES CredentialGroups(Id) ON DELETE CASCADE,
                FOREIGN KEY (CredentialId) REFERENCES SecureCredentials(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_credentialgroups_name ON CredentialGroups(Name);
            CREATE INDEX IF NOT EXISTS idx_credentialgroupmembers_credential ON CredentialGroupMembers(CredentialId);
        ";

        command.ExecuteNonQuery();
        connection.Close();
    }
}
