# Azure DevOps AI Agents

An AI-powered agent app for Azure DevOps. A conversational **Assistant** handles planning and orchestration, delegating coding tasks to a background **Developer** sub-agent that clones repos, implements work items, and opens pull requests — all through the GitHub Copilot CLI and Azure DevOps MCP server.

---

## Features

- **Conversational assistant** — chat with an AI that understands your Azure DevOps organization, projects, and repos
- **Developer sub-agent** — autonomously implements a work item: gets details, writes code, commits, and creates a PR
- **Agent status bar** — subtle real-time indicator showing when the Developer agent is working
- **Streaming responses** — token-by-token assistant replies via SignalR
- **Repo management** — list and clone ADO repos locally per connection
- **OAuth / Entra ID auth** — secure per-connection Azure DevOps authentication
- **Encrypted token storage** — AES-GCM encrypted PATs/tokens at rest in SQLite
- **Dark-themed UI** — Angular 21 zoneless frontend with signal-based state and Markdown rendering

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 21 (zoneless, standalone components, signals) |
| Backend | .NET 10 Web API (C# 13) |
| AI | GitHub Copilot SDK 0.1.29 (`CopilotClient`) |
| ADO integration | Azure DevOps MCP (`@azure-devops/mcp` via npx) |
| Real-time | ASP.NET Core SignalR |
| Database | SQLite via EF Core 10 (code-first) |
| Auth | Microsoft.Identity.Web 4.4.0 (OAuth / Entra ID) |

---

## Project Structure

```
azure-devops-agents/
├── dev.sh                          # Launch both servers concurrently
├── src/
│   ├── backend/
│   │   └── AzureDevOpsAgents.Api/
│   │       ├── Controllers/        # Connections, Auth, Repos, Chat
│   │       ├── Data/
│   │       │   ├── Entities/       # EF Core entities
│   │       │   ├── Migrations/     # InitialCreate migration
│   │       │   └── AppDbContext.cs
│   │       ├── Hubs/               # SignalR AgentHub
│   │       ├── Models/             # DTOs
│   │       ├── Services/
│   │       │   ├── AssistantAgentService.cs   # Primary Copilot chat agent
│   │       │   ├── DeveloperAgentService.cs   # Autonomous coding sub-agent
│   │       │   ├── AnalystAgentService.cs     # Stub (future)
│   │       │   ├── McpServerManager.cs        # Writes per-session MCP config
│   │       │   ├── RepoCloneService.cs        # Background git clone
│   │       │   └── TokenEncryptionService.cs  # AES-GCM token encryption
│   │       └── Program.cs
│   └── frontend/
│       ├── proxy.conf.json         # /api + /hubs → localhost:5000
│       └── src/app/
│           ├── store/              # Signal-based AppStore
│           ├── services/           # ApiService, SignalRService
│           ├── models/             # TypeScript interfaces
│           ├── shell/              # Root layout component
│           ├── sidebar/            # Connections, sessions, repos
│           ├── chat/               # Message thread + streaming input
│           ├── agent-status/       # Real-time agent status bar
│           ├── add-connection-dialog/
│           └── auth-callback/      # OAuth redirect handler
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) and npm
- [Angular CLI 21](https://angular.dev/): `npm install -g @angular/cli`
- [dotnet-ef tool](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`
- A GitHub account with Copilot access (for the Copilot CLI)
- An Azure DevOps organization

---

## Getting Started

### 1. Configure the backend

Edit `src/backend/AzureDevOpsAgents.Api/appsettings.json` (or use user secrets):

```json
{
  "Database": { "Path": "ado-agents.db" },
  "Encryption": { "Key": "<any-random-string>" },
  "GitHub": { "Token": "<your-github-pat-with-copilot-scope>" },
  "AzureDevOps": {
    "ClientId": "<entra-app-client-id>",
    "ClientSecret": "<entra-app-client-secret>",
    "CallbackUrl": "http://localhost:5000/api/auth/ado/callback"
  },
  "RepoStorage": { "Root": "/data/repos" },
  "McpServer": { "ConfigRoot": "/tmp/ado-agents/mcp-configs" }
}
```

> **Tip:** Use `dotnet user-secrets` in development to keep credentials out of source control.

### 2. Run the app

```bash
./dev.sh
```

This starts:
- **Backend** at `http://localhost:5000`
- **Frontend** at `http://localhost:4200`

Or run them separately:

```bash
# Backend
dotnet run --project src/backend/AzureDevOpsAgents.Api --urls http://localhost:5000

# Frontend
cd src/frontend && ng serve
```

### 3. Add a connection

1. Open `http://localhost:4200`
2. Click **＋** in the sidebar to add an Azure DevOps connection (org URL + project name)
3. Click **Authenticate** on the connection to complete OAuth with Entra ID
4. Clone desired repos using the **Clone** button next to each repo

### 4. Start chatting

Click **New Chat** on an authenticated connection and type a message. The assistant can:

- Summarize work items, sprints, and pipelines
- Answer questions about repos and branches
- Delegate implementation tasks to the Developer agent:
  - Triggered automatically when you ask it to implement a work item
  - The Developer agent: clones the repo, writes code, commits, and creates a PR
  - A status bar shows progress; a link to the PR appears on completion

---

## How the Agent Delegation Works

```
User message
    │
    ▼
AssistantAgentService (CopilotClient + ADO MCP)
    │   streams reply chunks → SignalR → frontend
    │
    ├── detects "DELEGATE_DEVELOP workItemId=X workItemTitle=Y repoName=Z"
    │
    ▼
DeveloperAgentService (new CopilotClient, Cwd=repoPath + ADO MCP)
    │   autopilot prompt: fetch WI → implement → commit → open PR
    │   streams log lines → SignalR AgentLog events
    │   emits AgentStatus events (Queued → Running → Succeeded/Failed)
    ▼
PR URL returned → shown in frontend status bar
```

The MCP server (`@azure-devops/mcp`) is invoked per-session via a temporary JSON config file passed to the Copilot CLI with `--mcp-config`. The Assistant gets `core, work, work-items, repositories` domains; the Developer gets `core, work-items, repositories`.

---

## Database

SQLite database auto-migrates on startup. Tables:

| Table | Description |
|---|---|
| `AzureDevOpsConnections` | Org/project connections with encrypted tokens |
| `ProjectRepos` | Repo metadata and local clone status |
| `ChatSessions` | Conversation sessions per connection |
| `ChatMessages` | User and assistant messages |
| `AgentJobs` | Developer sub-agent job records with logs |

To re-run migrations manually:

```bash
cd src/backend/AzureDevOpsAgents.Api
dotnet ef database update
```

---

## Development Notes

- The frontend uses Angular 21 **zoneless** change detection (`provideZonelessChangeDetection()`). All state uses `signal()` and `computed()`.
- SignalR events drive streaming: `MessageChunk` → assembles reply in `AppStore.appendChunk()`; `AgentStatus` → updates the status bar.
- The `AssistantAgentService` is **scoped** — one `CopilotClient` instance per HTTP request scope, with the MCP config written once at client creation and cleaned up in `DisposeAsync()`.
- The `DeveloperAgentService` creates a **new** `CopilotClient` per job with the repo's local path as `Cwd`.
