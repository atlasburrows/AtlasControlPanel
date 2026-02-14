import type { OpenClawPluginApi, DiagnosticEventPayload, OpenClawPluginService } from "openclaw/plugin-sdk";
import { onDiagnosticEvent } from "openclaw/plugin-sdk";

interface AtlasConfig {
  apiUrl?: string;
  apiKey?: string;
  autoLog?: {
    toolCalls?: boolean;
    execCommands?: boolean;
    sessionEvents?: boolean;
    messageSends?: boolean;
  };
  batchIntervalMs?: number;
}

interface ActivityEntry {
  action: string;
  description: string;
  category: number; // enum: 0=Development, 1=BugFix, 2=System, 3=Communication, 4=Research, 5=Security
  details?: string;
}

// ── Module-level config (set by service.start, read by tools) ──
let runtimeApiUrl = "http://localhost:5263";
let runtimeApiKey: string | undefined;

const CATEGORY_MAP: Record<string, number> = {
  Development: 0, BugFix: 1, System: 2, Communication: 3, Research: 4, Security: 5,
};

function apiHeaders(): Record<string, string> {
  const h: Record<string, string> = { "Content-Type": "application/json" };
  if (runtimeApiKey) h["X-Api-Key"] = runtimeApiKey;
  return h;
}

function createAtlasService(): OpenClawPluginService {
  let unsubscribe: (() => void) | null = null;
  let flushTimer: ReturnType<typeof setInterval> | null = null;
  let statusTimer: ReturnType<typeof setInterval> | null = null;
  let batch: ActivityEntry[] = [];

  // Status tracking
  let lastActivity = "";
  let isOnline = false;

  async function flushBatch() {
    if (batch.length === 0) return;
    const entries = [...batch];
    batch = [];

    for (const entry of entries) {
      try {
        await fetch(`${runtimeApiUrl}/api/activity`, {
          method: "POST",
          headers: apiHeaders(),
          body: JSON.stringify({
            action: entry.action,
            description: entry.description,
            category: entry.category,
            details: entry.details ?? null,
          }),
        });
      } catch {
        // Silently drop — don't crash the gateway over logging
      }
    }
  }

  function enqueue(entry: ActivityEntry) {
    batch.push(entry);
  }

  async function pushStatus(state?: string, detail?: string) {
    const s = state ?? (isOnline ? "Online" : "Offline");
    const d = detail ?? lastActivity;
    const health = d ? `${s}|${d}` : s;
    try {
      await fetch(`${runtimeApiUrl}/api/monitoring/status`, {
        method: "PUT",
        headers: apiHeaders(),
        body: JSON.stringify({ gatewayHealth: health }),
      });
    } catch {
      // Silently drop
    }
  }

  return {
    id: "atlas-control-panel",
    async start(ctx) {
      const cfg = (ctx.config.plugins?.entries?.["atlas-control-panel"]?.config ?? {}) as AtlasConfig;
      runtimeApiUrl = cfg.apiUrl ?? "http://localhost:5263";
      runtimeApiKey = cfg.apiKey;
      const autoLog = {
        toolCalls: cfg.autoLog?.toolCalls !== false,
        execCommands: cfg.autoLog?.execCommands !== false,
        sessionEvents: cfg.autoLog?.sessionEvents !== false,
        messageSends: cfg.autoLog?.messageSends !== false,
      };
      const batchInterval = cfg.batchIntervalMs ?? 5000;

      // Mark online on start
      isOnline = true;
      lastActivity = "Waiting for directive";
      pushStatus();

      // Periodic flush
      flushTimer = setInterval(() => flushBatch(), batchInterval);

      // Periodic status heartbeat (every 30s)
      statusTimer = setInterval(() => pushStatus(), 30000);

      unsubscribe = onDiagnosticEvent((evt: DiagnosticEventPayload) => {
        switch (evt.type) {
          case "model.usage": {
            if (!autoLog.toolCalls) break;
            const model = evt.model ?? "unknown";
            const provider = evt.provider ?? "unknown";
            const tokens = evt.usage?.total ?? 0;
            const cost = evt.costUsd ? `$${evt.costUsd.toFixed(4)}` : "n/a";
            const duration = evt.durationMs ? `${evt.durationMs}ms` : "n/a";
            const contextUsed = evt.context?.used ?? 0;
            const contextLimit = evt.context?.limit ?? 0;
            const contextPct = contextLimit > 0 ? Math.round((contextUsed / contextLimit) * 100) : 0;

            lastActivity = `Processing (${model}, ${contextPct}% context)`;
            pushStatus("Working", lastActivity);

            // Push cost to DailyCosts if available (fire-and-forget)
            if (evt.costUsd && evt.costUsd > 0) {
              fetch(`${runtimeApiUrl}/api/monitoring/cost`, {
                method: "POST",
                headers: apiHeaders(),
                body: JSON.stringify({ costUsd: evt.costUsd }),
              }).catch(() => {});
            }

            enqueue({
              action: "Model Call",
              description: `${provider}/${model} — ${tokens} tokens, ${cost}, ${duration}, context: ${contextPct}%`,
              category: CATEGORY_MAP.System,
              details: JSON.stringify({
                provider, model, tokens,
                costUsd: evt.costUsd,
                durationMs: evt.durationMs,
                contextUsed, contextLimit, contextPct,
                sessionKey: evt.sessionKey,
              }),
            });
            break;
          }

          case "message.processed": {
            if (!autoLog.messageSends) break;
            const channel = evt.channel ?? "unknown";
            const outcome = evt.outcome ?? "unknown";
            const duration = evt.durationMs ? `${evt.durationMs}ms` : "n/a";

            lastActivity = "Waiting for directive";
            pushStatus("Online", lastActivity);

            enqueue({
              action: "Message Processed",
              description: `${channel} — ${outcome} in ${duration}`,
              category: CATEGORY_MAP.Communication,
              details: JSON.stringify({
                channel, outcome,
                durationMs: evt.durationMs,
                sessionKey: evt.sessionKey,
                chatId: evt.chatId,
              }),
            });
            break;
          }

          case "session.state": {
            if (!autoLog.sessionEvents) break;
            const state = evt.state;
            const reason = evt.reason ?? "";

            if (state === "running" || state === "thinking") {
              lastActivity = reason || "Processing";
              pushStatus("Working", lastActivity);
            } else if (state === "waiting" || state === "rate-limited") {
              lastActivity = reason || "Waiting for token reset";
              pushStatus("Waiting", lastActivity);
            } else if (state === "idle") {
              lastActivity = "Waiting for directive";
              pushStatus("Online", lastActivity);
            }

            enqueue({
              action: "Session State",
              description: `State → ${state}${reason ? ` (${reason})` : ""}`,
              category: CATEGORY_MAP.System,
              details: JSON.stringify({ state, reason }),
            });
            break;
          }

          case "session.stuck": {
            if (!autoLog.sessionEvents) break;
            enqueue({
              action: "Session Stuck",
              description: `Session stuck in ${evt.state} for ${evt.ageMs}ms — queue depth: ${evt.queueDepth}`,
              category: CATEGORY_MAP.BugFix,
              details: JSON.stringify({
                state: evt.state, ageMs: evt.ageMs,
                queueDepth: evt.queueDepth, sessionKey: evt.sessionKey,
              }),
            });
            break;
          }

          case "webhook.error": {
            enqueue({
              action: "Webhook Error",
              description: `${evt.channel}/${evt.updateType}: ${evt.error}`,
              category: CATEGORY_MAP.BugFix,
              details: JSON.stringify({ channel: evt.channel, updateType: evt.updateType, error: evt.error }),
            });
            break;
          }

          case "run.attempt": {
            if (evt.attempt > 1) {
              enqueue({
                action: "Run Retry",
                description: `Agent run retry attempt #${evt.attempt}`,
                category: CATEGORY_MAP.System,
                details: JSON.stringify({ attempt: evt.attempt }),
              });
            }
            break;
          }
        }
      });

      ctx.logger.info("atlas-control-panel: active → " + runtimeApiUrl);
    },

    async stop() {
      unsubscribe?.();
      unsubscribe = null;
      if (flushTimer) { clearInterval(flushTimer); flushTimer = null; }
      if (statusTimer) { clearInterval(statusTimer); statusTimer = null; }
      isOnline = false;
      lastActivity = "Gateway stopped";
      await pushStatus("Offline", lastActivity);
      batch = [];
    },
  };
}

// ── Plugin definition ──
const plugin = {
  id: "atlas-control-panel",
  name: "Atlas Control Panel",
  description: "Automatic activity logging and monitoring for the Atlas Control Panel",
  register(api: OpenClawPluginApi) {
    api.registerService(createAtlasService());

    // ── Agent Tools ──
    // All tools read runtimeApiUrl/runtimeApiKey set by service.start()

    api.registerTool({
      name: "atlas_create_task",
      description: "Create a task in the Atlas Control Panel task board",
      parameters: {
        type: "object",
        properties: {
          title: { type: "string", description: "Task title" },
          description: { type: "string", description: "Task description" },
          priority: { type: "string", enum: ["Low", "Medium", "High", "Critical"], description: "Task priority" },
          status: { type: "string", enum: ["Backlog", "InProgress", "Review", "Done"], description: "Initial status" },
        },
        required: ["title"],
      },
      async execute(_id, params) {
        try {
          const res = await fetch(`${runtimeApiUrl}/api/tasks`, {
            method: "POST",
            headers: apiHeaders(),
            body: JSON.stringify({
              title: params.title,
              description: params.description ?? "",
              priority: params.priority ?? "Medium",
              status: params.status ?? "Backlog",
            }),
          });
          const data = await res.json();
          return { content: [{ type: "text", text: `Task created: ${data.id} — ${params.title}` }] };
        } catch (e) {
          return { content: [{ type: "text", text: `Failed to create task: ${e}` }] };
        }
      },
    });

    api.registerTool({
      name: "atlas_update_task",
      description: "Update a task status in the Atlas Control Panel",
      parameters: {
        type: "object",
        properties: {
          id: { type: "string", description: "Task GUID" },
          status: { type: "string", enum: ["Backlog", "InProgress", "Review", "Done"], description: "New status" },
        },
        required: ["id", "status"],
      },
      async execute(_id, params) {
        try {
          await fetch(`${runtimeApiUrl}/api/tasks/${params.id}/status`, {
            method: "PUT",
            headers: apiHeaders(),
            body: JSON.stringify(params.status),
          });
          return { content: [{ type: "text", text: `Task ${params.id} → ${params.status}` }] };
        } catch (e) {
          return { content: [{ type: "text", text: `Failed to update task: ${e}` }] };
        }
      },
    });

    api.registerTool({
      name: "atlas_log_activity",
      description: "Log a custom activity entry to the Atlas Control Panel",
      parameters: {
        type: "object",
        properties: {
          action: { type: "string", description: "Action name" },
          description: { type: "string", description: "Activity description" },
          category: { type: "string", enum: ["Development", "BugFix", "System", "Communication", "Research", "Security"], description: "Activity category" },
        },
        required: ["action", "description"],
      },
      async execute(_id, params) {
        try {
          await fetch(`${runtimeApiUrl}/api/activity`, {
            method: "POST",
            headers: apiHeaders(),
            body: JSON.stringify({
              action: params.action,
              description: params.description,
              category: CATEGORY_MAP[params.category ?? "System"] ?? 2,
            }),
          });
          return { content: [{ type: "text", text: `Logged: ${params.action}` }] };
        } catch (e) {
          return { content: [{ type: "text", text: `Failed to log: ${e}` }] };
        }
      },
    });

    api.registerTool({
      name: "atlas_request_credential",
      description: "Request access to a credential from the Atlas Control Panel. Requires owner approval.",
      parameters: {
        type: "object",
        properties: {
          credentialName: { type: "string", description: "Name of the credential to access" },
          reason: { type: "string", description: "Why this credential is needed" },
          durationMinutes: { type: "number", description: "How long access is needed (minutes)" },
        },
        required: ["credentialName", "reason"],
      },
      async execute(_id, params) {
        try {
          const res = await fetch(`${runtimeApiUrl}/api/security/credentials/request`, {
            method: "POST",
            headers: apiHeaders(),
            body: JSON.stringify({
              credentialName: params.credentialName,
              reason: params.reason,
              durationMinutes: params.durationMinutes ?? 30,
            }),
          });
          if (res.ok) {
            const data = await res.json();
            return { content: [{ type: "text", text: `Credential request submitted (${data.id}). Awaiting owner approval.` }] };
          }
          return { content: [{ type: "text", text: `Credential request failed: ${res.statusText}` }] };
        } catch (e) {
          return { content: [{ type: "text", text: `Failed to request credential: ${e}` }] };
        }
      },
    });
  },
};

export default plugin;
