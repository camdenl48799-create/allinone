# ALLINONE

**ONE AI. EVERY PROJECT.**

A native Windows AI workspace by **NOT USED INC.** with persistent chats, projects, local files, internet tools, optional local model inference, and an upgradeable model roadmap.

## 0.4.0 highlights

- Persistent local chat sessions with recent-chat history
- New Chat, clear-chat, copy-message, and automatic chat titles
- Local model support through **Ollama** or an **OpenAI-compatible local server**
- No cloud API key required for local inference
- `@SearchInOne`, `@CodeInOne`, `@MathInOne`, `@Project`, `@File`, `@Website`, `@YouTube`, and `@Model`
- Local project workspaces with file attach, preview, open, remove, and delete
- Project context can be supplied to the local model as reference material
- Safe Mode is checked before requests are routed to tools or models
- Public website retrieval blocks localhost/private/link-local targets
- Strong YouTube domain validation
- Light and dark themes
- Optional Clerk browser authentication with PKCE and encrypted local session data
- Update downloads are checksum-verified before installation
- Roadmap models are never presented as connected when they are not

## Run the native app

Requirements: Windows and .NET 10 SDK.

```powershell
dotnet run --project src/ALLINONE/ALLINONE.csproj
```

## Build a Windows x64 publish

```powershell
dotnet publish src/ALLINONE/ALLINONE.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

The publish output contains `ALLINONE.exe`.

## Connect a local model

Open **Settings → Local AI runtime**.

For Ollama, use `http://127.0.0.1:11434` and select a local model.
For an OpenAI-compatible local server, enter its local HTTP endpoint and model name.

ALLINONE restricts the Local Model connection to the same PC. Internet-enabled tools such as SearchInOne are separate and only run when explicitly invoked.

## Authentication

Clerk is optional. See [CLERK-AUTH.md](CLERK-AUTH.md) for browser OAuth + PKCE setup.

## Releases

Windows releases are built by GitHub Actions. Release packages include a SHA-256 checksum file, and the updater verifies the package before installing it.

## Repository layout

```text
src/ALLINONE/     Native WPF application
api/              Legacy web endpoint
js/               Legacy web/demo modules
css/              Legacy web styles
.github/workflows CI and release automation
```

## Security

Never commit API keys, OAuth client secrets, access tokens, model weights, or private credentials.
Local chat and project data is stored under the Windows user's local application data directory.