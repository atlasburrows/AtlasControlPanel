# AtlasControlPanel Database Setup

## Overview

This document describes the database setup for the Vigil application. The complete schema is generated and stored in `setup.sql`.

## Quick Start

### Run the setup script:
```powershell
sqlcmd -S localhost -E -i setup.sql
```

### Or from SQL Server Management Studio:
1. Open SSMS
2. Connect to `localhost` (Windows auth)
3. File → Open → Select `setup.sql`
4. Execute (F5)

## What's Included

The `setup.sql` script is **idempotent** — it can be run multiple times safely and will:

✓ Create the `AtlasControlPanel` database (if it doesn't exist)
✓ Create 9 tables with proper schema
✓ Create 14 indexes for performance
✓ Create 33 stored procedures
✓ Insert initial SystemStatus record
✓ Use `IF NOT EXISTS` guards on all objects

## Database Schema

### Tables (9)

| Table | Purpose |
|-------|---------|
| **ActivityLogs** | Log all user activities, token usage, API calls |
| **ChatMessages** | Store conversation history |
| **CostSummary** | Daily and monthly cost aggregations |
| **DailyCosts** | Per-day cost tracking |
| **PermissionRequests** | Credential access requests (approval workflow) |
| **SecureCredentials** | Encrypted credential vault |
| **SecurityAudits** | Security events and audit trail |
| **SystemStatus** | Gateway health, sessions, resource usage |
| **Tasks** | Task management with scheduling/recurrence |

### Stored Procedures (33)

#### ActivityLogs (5 procedures)
- `sp_ActivityLogs_Create` - Insert activity log
- `sp_ActivityLogs_GetAll` - Get recent activities
- `sp_ActivityLogs_GetById` - Get specific activity
- `sp_ActivityLogs_GetByTaskId` - Activities for a task
- `sp_ActivityLogs_GetWithDetails` - Get parent activities with hierarchy

#### ChatMessages (2 procedures)
- `sp_ChatMessages_Add` - Insert message
- `sp_ChatMessages_GetRecent` - Fetch recent messages

#### CostSummary (3 procedures)
- `sp_CostSummary_GetDaily` - Get daily cost for date
- `sp_CostSummary_GetMonthly` - Get monthly aggregation
- `sp_CostSummary_Upsert` - Insert or update cost

#### Credentials (5 procedures)
- `sp_Credentials_Create` - Store new credential
- `sp_Credentials_Delete` - Remove credential
- `sp_Credentials_GetAll` - List all credentials
- `sp_Credentials_GetById` - Get specific credential
- `sp_Credentials_RecordAccess` - Track access for audit

#### DailyCosts (2 procedures)
- `sp_DailyCosts_Get` - Get last N days of costs
- `sp_DailyCosts_Increment` - Add to today's total

#### PermissionRequests (5 procedures)
- `sp_PermissionRequests_Create` - Create access request
- `sp_PermissionRequests_GetAll` - List all requests
- `sp_PermissionRequests_GetPending` - Pending requests only
- `sp_PermissionRequests_Resolve` - Approve/deny request
- `sp_PermissionRequests_Update` - Update request status

#### SecurityAudits (3 procedures)
- `sp_SecurityAudits_Create` - Log security event
- `sp_SecurityAudits_GetAll` - Get recent audits
- `sp_SecurityAudits_GetBySeverity` - Filter by severity level

#### SystemStatus (2 procedures)
- `sp_SystemStatus_Get` - Get current system state
- `sp_SystemStatus_Upsert` - Update system state

#### Tasks (6 procedures)
- `sp_Tasks_Create` - Create new task
- `sp_Tasks_Delete` - Remove task
- `sp_Tasks_GetAll` - List all tasks
- `sp_Tasks_GetById` - Get specific task
- `sp_Tasks_Update` - Update task details
- `sp_Tasks_UpdateStatus` - Change task status

## Data Types

### Common Types
- **Id**: `UNIQUEIDENTIFIER` (GUID) - Primary keys
- **Timestamps**: `DATETIME2` or `DATE` - Timezone-aware
- **Strings**: `NVARCHAR(n)` - Unicode support
- **Costs**: `DECIMAL(18,6)` - Precision for currency
- **Counters**: `INT` - Token/API call counts

### Default Values
- **Id**: `NEWID()` - Auto-generate GUID
- **Timestamps**: `GETUTCDATE()` or `SYSUTCDATETIME()` - UTC time
- **Status**: `'Pending'` or `'ToDo'` - Initial states
- **Counts**: `0` - Zero initialization
- **Currency**: `'USD'` - Default currency

## Indexes

All tables include strategic indexes for common query patterns:
- **Timestamp indexes** (DESC) for activity logs and audits
- **Status indexes** for filtering pending/active records
- **Foreign key indexes** for relationships
- **Category/Type indexes** for grouping operations

## Initial Data

The script inserts one required record:

```sql
SystemStatus with Id = '00000000-0000-0000-0000-000000000001'
```

This is the singleton status record used by monitoring/health check procedures.

## Security Considerations

1. **Windows Authentication**: Script uses `-E` flag (trusted connection)
2. **DPAPI Encryption**: StorageKey values in SecureCredentials are encrypted at rest
3. **Access Tracking**: Credentials record access count and last accessed timestamp
4. **Audit Trail**: SecurityAudits table logs all sensitive operations
5. **Permission Workflow**: Credential access requires approval via PermissionRequests

## Maintenance

### Backup
```sql
BACKUP DATABASE AtlasControlPanel TO DISK = 'C:\backups\AtlasControlPanel.bak'
```

### Restore
```sql
RESTORE DATABASE AtlasControlPanel FROM DISK = 'C:\backups\AtlasControlPanel.bak'
```

### Update Stored Procedure
Edit the procedure definition in `setup.sql` and re-run. The script uses `CREATE OR ALTER` so it safely updates existing procedures.

### Add New Table
1. Add table CREATE statement in `setup.sql`
2. Wrap in `IF NOT EXISTS` guard
3. Run the script

## Troubleshooting

### "Database already exists"
The script handles this with `IF NOT EXISTS`, so it's safe to run multiple times.

### "Table already exists"
All tables use `IF NOT EXISTS` guards — re-running the script won't recreate them.

### "Cannot find stored procedure"
Ensure you're in the `AtlasControlPanel` database context (the script includes `USE AtlasControlPanel;`)

### Performance Issues
Check indexes with:
```sql
USE AtlasControlPanel;
SELECT name, type_desc FROM sys.indexes WHERE object_id = OBJECT_ID('ActivityLogs');
```

## Support

For questions or issues, contact the Vigil development team.

---

**Generated**: 2026-02-12  
**Version**: 1.0  
**Procedures**: 33  
**Tables**: 9  
**Indexes**: 14
