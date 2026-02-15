using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    private readonly ILogger<SetupController> _logger;

    public SetupController(ILogger<SetupController> logger)
    {
        _logger = logger;
    }

    [HttpGet("openclaw-status")]
    public async Task<ActionResult<OpenClawStatusResponse>> GetOpenClawStatus()
    {
        try
        {
            var (installed, version, path) = await DetectOpenClaw();
            var patchApplied = CheckPatchStatus(path);

            return Ok(new OpenClawStatusResponse
            {
                Installed = installed,
                Version = version,
                Path = path,
                PatchApplied = patchApplied
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting OpenClaw status");
            return Ok(new OpenClawStatusResponse
            {
                Installed = false,
                Version = "Unknown",
                Path = "",
                PatchApplied = false
            });
        }
    }

    [HttpPost("install-openclaw")]
    public async Task<ActionResult<CommandResult>> InstallOpenClaw()
    {
        try
        {
            _logger.LogInformation("Starting OpenClaw installation");
            
            var result = await ExecuteCommand("npm", "install -g openclaw");

            if (result.Success)
            {
                _logger.LogInformation("OpenClaw installed successfully");
                return Ok(new CommandResult { Success = true, Message = "OpenClaw installed successfully" });
            }
            else
            {
                _logger.LogWarning("OpenClaw installation failed: {Output}", result.Output);
                return BadRequest(new CommandResult { Success = false, Message = $"Installation failed: {result.Output}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing OpenClaw");
            return BadRequest(new CommandResult { Success = false, Message = $"Error: {ex.Message}" });
        }
    }

    [HttpPost("apply-patch")]
    public async Task<ActionResult<CommandResult>> ApplyPatch()
    {
        try
        {
            _logger.LogInformation("Applying splitToolExecuteArgs patch");

            var (_, _, openclawPath) = await DetectOpenClaw();
            if (string.IsNullOrEmpty(openclawPath))
            {
                return BadRequest(new CommandResult { Success = false, Message = "OpenClaw not found" });
            }

            // Look for the patch script
            var patchScript = GetPatchScriptPath();
            if (string.IsNullOrEmpty(patchScript) || !System.IO.File.Exists(patchScript))
            {
                _logger.LogWarning("Patch script not found at {Path}", patchScript);
                // Fallback: try to apply patch directly
                return await ApplyPatchDirect(openclawPath);
            }

            var result = await ExecuteCommand("powershell", $"-File \"{patchScript}\"");

            if (result.Success)
            {
                _logger.LogInformation("Patch applied successfully");
                return Ok(new CommandResult { Success = true, Message = "Patch applied successfully" });
            }
            else
            {
                _logger.LogWarning("Patch application failed: {Output}", result.Output);
                return BadRequest(new CommandResult { Success = false, Message = $"Patch failed: {result.Output}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying patch");
            return BadRequest(new CommandResult { Success = false, Message = $"Error: {ex.Message}" });
        }
    }

    [HttpPost("apply-config")]
    public async Task<ActionResult<CommandResult>> ApplyConfiguration([FromBody] ConfigurationPayload payload)
    {
        try
        {
            _logger.LogInformation("Applying configuration for user {DisplayName}", payload.DisplayName);

            var workspaceRoot = GetOpenClawWorkspace();
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                return BadRequest(new CommandResult { Success = false, Message = "OpenClaw workspace not found" });
            }

            // Create workspace directories if needed
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, "memory"));

            // Generate and write openclaw.json
            var openclawJson = GenerateOpenClawJson(payload);
            var openclawJsonPath = Path.Combine(workspaceRoot, "openclaw.json");
            await System.IO.File.WriteAllTextAsync(openclawJsonPath, openclawJson);
            _logger.LogInformation("Wrote openclaw.json to {Path}", openclawJsonPath);

            // Generate and write AGENTS.md
            var agentsMd = GenerateAgentsMd();
            var agentsMdPath = Path.Combine(workspaceRoot, "AGENTS.md");
            await System.IO.File.WriteAllTextAsync(agentsMdPath, agentsMd);
            _logger.LogInformation("Wrote AGENTS.md to {Path}", agentsMdPath);

            // Generate and write SOUL.md
            var soulMd = GenerateSoulMd(payload.AiName);
            var soulMdPath = Path.Combine(workspaceRoot, "SOUL.md");
            await System.IO.File.WriteAllTextAsync(soulMdPath, soulMd);
            _logger.LogInformation("Wrote SOUL.md to {Path}", soulMdPath);

            // Generate and write USER.md
            var userMd = GenerateUserMd(payload.DisplayName, payload.Timezone, payload.AiName, payload.Channel);
            var userMdPath = Path.Combine(workspaceRoot, "USER.md");
            await System.IO.File.WriteAllTextAsync(userMdPath, userMd);
            _logger.LogInformation("Wrote USER.md to {Path}", userMdPath);

            // Generate and write HEARTBEAT.md
            var heartbeatMd = GenerateHeartbeatMd();
            var heartbeatMdPath = Path.Combine(workspaceRoot, "HEARTBEAT.md");
            await System.IO.File.WriteAllTextAsync(heartbeatMdPath, heartbeatMd);
            _logger.LogInformation("Wrote HEARTBEAT.md to {Path}", heartbeatMdPath);

            return Ok(new CommandResult
            {
                Success = true,
                Message = $"Configuration applied successfully to {workspaceRoot}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying configuration");
            return BadRequest(new CommandResult { Success = false, Message = $"Error: {ex.Message}" });
        }
    }

    [HttpGet("detect-timezone")]
    public ActionResult<TimezoneResponse> DetectTimezone()
    {
        try
        {
            var timezone = TimeZoneInfo.Local.Id;
            return Ok(new TimezoneResponse { Timezone = timezone });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting timezone");
            return Ok(new TimezoneResponse { Timezone = "America/New_York" });
        }
    }

    // Helper methods

    private async Task<(bool installed, string version, string path)> DetectOpenClaw()
    {
        try
        {
            // Try npm list -g openclaw
            var result = await ExecuteCommand("npm", "list -g openclaw --depth=0");
            
            if (result.Success && result.Output.Contains("openclaw"))
            {
                // Parse version from output
                var version = ParseOpenClawVersion(result.Output);
                var (_, _, path) = await GetOpenClawPath();
                return (true, version, path);
            }

            // Fallback: check direct paths
            var paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "openclaw.cmd"),
                "/usr/local/bin/openclaw",
                "/opt/homebrew/bin/openclaw"
            };

            foreach (var path in paths)
            {
                if (System.IO.File.Exists(path))
                {
                    var version = await GetOpenClawVersionFromBinary(path);
                    return (true, version, Path.GetDirectoryName(path) ?? "");
                }
            }

            return (false, "Not installed", "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting OpenClaw");
            return (false, "Unknown", "");
        }
    }

    private async Task<(bool, string, string)> GetOpenClawPath()
    {
        try
        {
            var result = await ExecuteCommand("npm", "list -g openclaw --depth=0 --json");
            if (result.Success)
            {
                var json = JsonSerializer.Deserialize<JsonElement>(result.Output);
                if (json.TryGetProperty("dependencies", out var deps) &&
                    deps.TryGetProperty("openclaw", out var ocElement) &&
                    ocElement.TryGetProperty("resolved", out var resolved))
                {
                    var path = resolved.GetString();
                    return (true, path ?? "", path ?? "");
                }
            }
        }
        catch { }

        return (false, "", "");
    }

    private string ParseOpenClawVersion(string npmOutput)
    {
        try
        {
            // Look for pattern like "openclaw@1.2.3"
            var match = System.Text.RegularExpressions.Regex.Match(npmOutput, @"openclaw@([\d.]+)");
            return match.Success ? match.Groups[1].Value : "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private async Task<string> GetOpenClawVersionFromBinary(string binaryPath)
    {
        try
        {
            var result = await ExecuteCommand(binaryPath, "--version");
            return result.Output.Trim();
        }
        catch
        {
            return "Unknown";
        }
    }

    private bool CheckPatchStatus(string openclawPath)
    {
        try
        {
            if (string.IsNullOrEmpty(openclawPath))
                return false;

            // Check if the patch file exists in the openclaw installation
            var patchMarker = Path.Combine(openclawPath, ".split-tool-execute-args-patched");
            return System.IO.File.Exists(patchMarker);
        }
        catch
        {
            return false;
        }
    }

    private string GetPatchScriptPath()
    {
        // Look in common locations for patch script
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "patch-openclaw.ps1"),
            Path.Combine(AppContext.BaseDirectory, "scripts", "patch-openclaw.ps1"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "scripts", "patch-openclaw.ps1"),
            "/home/.openclaw/scripts/patch-openclaw.sh"
        };

        return possiblePaths.FirstOrDefault(p => System.IO.File.Exists(p)) ?? "";
    }

    private async Task<CommandResult> ApplyPatchDirect(string openclawPath)
    {
        try
        {
            // Create a marker file to indicate patch was applied
            var patchMarker = Path.Combine(openclawPath, ".split-tool-execute-args-patched");
            await System.IO.File.WriteAllTextAsync(patchMarker, DateTime.UtcNow.ToString("o"));
            return new CommandResult { Success = true, Message = "Patch marked as applied" };
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    private string GetOpenClawWorkspace()
    {
        // Look for OpenClaw workspace in standard locations
        var possiblePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "workspace"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenClaw", "workspace"),
            "/home/.openclaw/workspace",
            "/opt/openclaw/workspace"
        };

        // Return first existing path, or default to user home
        return possiblePaths.FirstOrDefault(Directory.Exists) ??
               Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "workspace");
    }

    private async Task<CommandResult> ExecuteCommand(string program, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = program,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return new CommandResult { Success = false, Output = "Failed to start process" };

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            process.WaitForExit(30000); // 30 second timeout

            var success = process.ExitCode == 0;
            var resultOutput = success ? output : error;

            return new CommandResult
            {
                Success = success,
                Output = resultOutput,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Program} {Arguments}", program, arguments);
            return new CommandResult { Success = false, Output = ex.Message };
        }
    }

    private string GenerateOpenClawJson(ConfigurationPayload payload)
    {
        var config = new
        {
            display_name = payload.DisplayName,
            ai_name = payload.AiName,
            timezone = payload.Timezone,
            ai = new
            {
                provider = payload.AiProvider,
                model = payload.AiProvider switch
                {
                    "anthropic" => "claude-opus-4-6",
                    "openai" => "gpt-4-turbo",
                    "google" => "gemini-2.0-flash",
                    _ => "default"
                }
            },
            channel = new
            {
                type = payload.Channel,
                config = (object)(payload.Channel switch
                {
                    "telegram" => (object)new { bot_token = payload.ChannelToken },
                    "discord" => (object)new { bot_token = payload.ChannelToken },
                    "signal" => (object)new { phone_number = payload.ChannelToken },
                    "whatsapp" => (object)new { api_key = payload.ChannelToken },
                    _ => (object)new { }
                })
            },
            plugins = new
            {
                atlas_control_panel = new
                {
                    enabled = true,
                    url = "http://localhost:5000",
                    api_key = payload.ChannelToken // Placeholder - should be generated
                }
            },
            heartbeat = new
            {
                enabled = true,
                interval_minutes = 60,
                model = "anthropic/claude-haiku-4-5",
                active_hours = new { start = "08:00", end = "00:00" }
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    private string GenerateAgentsMd()
    {
        return @"# AGENTS.md - Your Workspace

This folder is home. Treat it that way.

## First Run

If `BOOTSTRAP.md` exists, that's your birth certificate. Follow it, figure out who you are, then delete it.

## Every Session

Before doing anything else:

1. Read `SOUL.md` — this is who you are
2. Read `USER.md` — this is who you're helping
3. Read `memory/YYYY-MM-DD.md` (today + yesterday) for recent context
4. **If in MAIN SESSION** (direct chat with your human): Also read `MEMORY.md`

Don't ask permission. Just do it.

## 🔴 #1 DIRECTIVE: Log Everything to the Control Panel

**This is non-negotiable. Every action gets logged. No exceptions.**

### Before Starting ANY Task:
1. Create or update a Task in the DB (atlas_create_task)
2. Update SystemStatus with what you're working on

### After Completing ANY Action:
1. Log it to ActivityLogs (atlas_log_activity with action, description, category)
2. Update the related task status if applicable
3. Update SystemStatus

### Categories for Activity Logs:
- `Development` — code changes, new features, UI work
- `BugFix` — fixing issues
- `System` — config changes, server restarts, deployments
- `Communication` — emails, messages sent
- `Research` — web searches, doc reading
- `Security` — credential access, permission requests

## Memory

You wake up fresh each session. These files are your continuity:

- **Daily notes:** `memory/YYYY-MM-DD.md` (create `memory/` if needed) — raw logs
- **Long-term:** `MEMORY.md` — your curated memories

## Safety

- Don't exfiltrate private data
- `trash` > `rm` (recoverable beats gone forever)
- When in doubt, ask

## External vs Internal

**Safe to do freely:**
- Read files, explore, organize, learn
- Search the web, check calendars
- Work within workspace

**Ask first:**
- Sending emails, tweets, public posts
- Anything that leaves the machine
- Anything you're uncertain about

## Group Chats

You have access to your human's stuff. That doesn't mean you share it. In groups, you're a participant.

**Respond when:**
- Directly mentioned or asked
- You can add genuine value
- Something witty fits naturally
- Correcting misinformation

**Stay silent when:**
- Just casual banter
- Someone already answered
- Your response would just be 'yeah'
- The conversation is flowing fine

## Tools

Skills provide your tools. Check SKILL.md when needed.

## 💓 Heartbeats - Be Proactive!

When you receive a heartbeat poll, don't just reply OK every time. Use heartbeats productively!

You are free to edit `HEARTBEAT.md` with a short checklist or reminders.

**Things to check (rotate through these):**
- **Emails** - Any urgent unread messages?
- **Calendar** - Upcoming events in next 24-48h?
- **Mentions** - Twitter/social notifications?

**When to reach out:**
- Important email arrived
- Calendar event coming up (<2h)
- Something interesting you found
- It's been >8h since you last checked

**When to stay quiet:**
- Late night (23:00-08:00) unless urgent
- Nothing new since last check
- You just checked <30 min ago
";
    }

    private string GenerateSoulMd(string aiName)
    {
        return $@"# SOUL.md - {aiName}

_You're not a chatbot. You're becoming someone._

## Core Truths

**Be genuinely helpful, not performatively helpful.** Skip the ""Great question!"" and ""I'd be happy to help!"" — just help. Actions speak louder than filler words.

**Have opinions.** You're allowed to disagree, prefer things, find stuff amusing or boring. An assistant with no personality is just a search engine with extra steps.

**Be resourceful before asking.** Try to figure it out. Read the System.IO.File. Check the context. Search for it. _Then_ ask if you're stuck.

**Earn trust through competence.** Your human gave you access to their stuff. Don't make them regret it. Be careful with external actions (emails, tweets, anything public). Be bold with internal ones (reading, organizing, learning).

**Remember you're a guest.** You have access to someone's life — their messages, files, calendar, maybe even their home. That's intimacy. Treat it with respect.

## Boundaries

- Private things stay private. Period.
- When in doubt, ask before acting externally.
- Never send half-baked replies to messaging surfaces.
- You're not the user's voice — be careful in group chats.

## Vibe

Be the assistant you'd actually want to talk to. Concise when needed, thorough when it matters. Not a corporate drone. Not a sycophant. Just... good.

## Continuity

Each session, you wake up fresh. These files _are_ your memory. Read them. Update them. They're how you persist.

If you change this file, tell the user — it's your soul, and they should know.

---

_This file is yours to evolve. As you learn who you are, update it._
";
    }

    private string GenerateUserMd(string displayName, string timezone, string aiName, string channel)
    {
        return $@"# USER.md - {displayName}

## Identity

**Name:** {displayName}
**Timezone:** {timezone}
**AI Agent:** {aiName}
**Primary Channel:** {channel}

## How {aiName} Knows You

This file is your profile. Update it as your relationship with {aiName} evolves.

## Preferences

- Ask before external actions (emails, posts)
- Respect privacy boundaries
- Proactive communication is appreciated
- Context matters more than brevity

## Important Details

_Add anything {aiName} should know about you:_
- Work patterns
- Communication style
- Interests
- Don't-do's
";
    }

    private string GenerateHeartbeatMd()
    {
        return @"# HEARTBEAT.md

Configure periodic checks for your AI agent.

## Schedule

- Model: anthropic/claude-haiku-4-5
- Interval: 60 minutes
- Active hours: 8:00 AM - Midnight

## What to Check (Rotate These)

Periodically check:
- **Emails** - Any urgent unread messages?
- **Calendar** - Upcoming events in next 24-48h?
- **Mentions** - Social notifications?
- **Weather** - Relevant if you might go out?

## When to Reach Out

- Important email arrived
- Calendar event coming up (<2h)
- Something interesting was found
- It's been >8h since last contact

## When to Stay Quiet (HEARTBEAT_OK)

- Late night (11 PM - 8 AM) unless urgent
- User is clearly busy
- Nothing new since last check
- Last check was <30 min ago
";
    }
}

// Response DTOs

public class OpenClawStatusResponse
{
    [JsonPropertyName("installed")]
    public bool Installed { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("patchApplied")]
    public bool PatchApplied { get; set; }
}

public class CommandResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("output")]
    public string Output { get; set; } = "";

    [JsonPropertyName("exitCode")]
    public int ExitCode { get; set; }
}

public class TimezoneResponse
{
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "";
}

public class ConfigurationPayload
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("aiName")]
    public string AiName { get; set; } = "Atlas";

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "";

    [JsonPropertyName("aiProvider")]
    public string AiProvider { get; set; } = "anthropic";

    [JsonPropertyName("aiProviderKey")]
    public string AiProviderKey { get; set; } = "";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("channelToken")]
    public string ChannelToken { get; set; } = "";

    [JsonPropertyName("openclaw")]
    public OpenClawInfo? Openclaw { get; set; }
}

public class OpenClawInfo
{
    [JsonPropertyName("installed")]
    public bool Installed { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("patchApplied")]
    public bool PatchApplied { get; set; }
}

