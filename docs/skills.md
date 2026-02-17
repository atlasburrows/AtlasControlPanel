# Skill Authoring Guide

Skills are prompt packages that teach AI agents how to use Vigil tools effectively. They combine markdown instructions with optional scripts, and are distributed via ClawHub or bundled with license tiers.

## What Is a Skill?

A skill is a directory containing:

```
my-skill/
├── SKILL.md          # Required: Main instruction file
├── scripts/          # Optional: Helper scripts
│   └── check.ps1
├── templates/        # Optional: Output templates
│   └── report.md
└── README.md         # Optional: Human-readable docs
```

## SKILL.md Structure

The `SKILL.md` file is the core of a skill. It's loaded into the AI agent's context and tells it how to accomplish specific tasks using available tools.

```markdown
# Skill: Daily Operations Report

## Purpose
Generate a daily summary of AI agent operations and costs.

## When to Use
- During heartbeat checks (once per day)
- When the user asks for a status report

## Tools Required
- `atlas_log_activity` — Log the report generation
- `atlas_set_project` — Tag cost to "Control Panel Operations"

## Steps

1. Set project context:
   ```
   atlas_set_project(project: "Control Panel Operations")
   ```

2. Fetch analytics data:
   - Read `/api/analytics/totals?period=day` for today's costs
   - Read `/api/analytics/cost/by-model?days=1` for model breakdown

3. Fetch activity summary:
   - Read `/api/activity?take=20` for recent activities

4. Generate report with template from `templates/report.md`

5. Log the activity:
   ```
   atlas_log_activity(
     action: "Daily Report Generated",
     description: "Generated daily ops report: $X.XX cost, N activities",
     category: "System"
   )
   ```

## Example Output

**Daily Operations Report — 2026-02-16**

| Metric | Value |
|--------|-------|
| Total Cost | $2.45 |
| Requests | 47 |
| Top Model | claude-opus-4-6 (89%) |
| Activities | 23 logged |

## Notes
- Run during morning heartbeat (8-9 AM)
- Skip on weekends unless activity detected
```

## Connecting Skills to Plugin Tools

Skills reference the tools registered by the Vigil plugin. Here's the mapping:

| Skill Action | Plugin Tool | API Endpoint |
|-------------|------------|--------------|
| Create a task | `atlas_create_task` | `POST /api/tasks` |
| Update task status | `atlas_update_task` | `PUT /api/tasks/{id}/status` |
| Log an activity | `atlas_log_activity` | `POST /api/activity` |
| Set project context | `atlas_set_project` | (in-memory, affects cost tagging) |
| Request credential | `atlas_request_credential` | `POST /api/security/credentials/request` |
| Retrieve credential | `atlas_get_credential` | `POST /api/security/credentials/by-name/{name}/decrypt` |

Skills can also reference API endpoints directly (via `web_fetch` or `exec` with curl) for read operations not covered by tools:

```markdown
## Reading Analytics
Fetch cost data directly:
```bash
curl -s http://127.0.0.1:5263/api/analytics/totals?period=week
```
```

## Best Practices

### Do:
- **Be specific** about tool parameters and expected responses
- **Include examples** with realistic data
- **Define when** the skill should be used (triggers, schedules)
- **Handle errors** — tell the agent what to do if a tool call fails
- **Set project context** at the start of multi-step workflows

### Don't:
- Don't hardcode credentials or API keys in skill files
- Don't assume the agent remembers previous skill executions
- Don't create skills that bypass the approval flow for credentials
- Don't embed large data blobs — reference files or APIs instead

## Skill Categories

| Category | Examples | Distribution |
|----------|---------|--------------|
| **Operations** | Daily reports, health checks, cost alerts | Bundled (Free) |
| **Security** | Credential rotation reminders, audit reviews | Bundled (Pro) |
| **Development** | PR review workflows, deployment checklists | ClawHub |
| **Communication** | Email drafting, notification management | ClawHub |
| **Analytics** | Custom cost reports, trend analysis | Bundled (Pro) |

## Publishing to ClawHub

1. Create the skill directory with `SKILL.md`
2. Add a `manifest.json`:
   ```json
   {
     "name": "daily-ops-report",
     "version": "1.0.0",
     "description": "Generate daily operations reports",
     "author": "your-username",
     "tools": ["atlas_log_activity", "atlas_set_project"],
     "tier": "free"
   }
   ```
3. Test locally by placing in your workspace
4. Submit to ClawHub for review

## Full Example: Credential Rotation Reminder

```markdown
# Skill: Credential Rotation Reminder

## Purpose
Check credential ages and remind the owner to rotate old ones.

## When to Use
- Weekly heartbeat check (Mondays)

## Tools Required
- `atlas_log_activity`

## Steps

1. Fetch all credentials:
   ```bash
   curl -s http://127.0.0.1:5263/api/security/credentials
   ```

2. Check each credential's `updatedAt` field
3. If any credential is older than 90 days, notify the owner
4. Log the check:
   ```
   atlas_log_activity(
     action: "Credential Age Check",
     description: "Checked N credentials. M need rotation.",
     category: "Security"
   )
   ```

## Notification Template
> ⚠️ **Credential Rotation Needed**
> The following credentials haven't been updated in 90+ days:
> - {name} (last updated: {date})
>
> Consider rotating these for security.
```
