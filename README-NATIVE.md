# ALLINONE — Native Windows

ALLINONE is a native WPF/.NET 10 Windows workspace built around the idea **ONE AI. EVERY PROJECT.**

## Current native features

- Native WPF application; no Electron runtime required.
- Self-contained Windows x64 publish.
- First-launch setup wizard with Safe Mode.
- Persistent local chat sessions and recent-chat history.
- Chat copy actions, timestamps, automatic chat titles, and clear-chat controls.
- Projects with file attachment, preview, open, remove, and delete controls.
- Local text/code context can be supplied to the local model.
- @SearchInOne, @CodeInOne, @MathInOne, @Project, @File, @Website, @YouTube, and @Model routing.
- Optional local AI inference through Ollama or an OpenAI-compatible local HTTP server.
- Local Model endpoints are restricted to the same machine.
- Public website retrieval blocks localhost/private/link-local network targets.
- Optional Clerk browser authentication using OAuth + PKCE with DPAPI-protected session data.
- Light and dark themes.
- GitHub release updater with SHA-256 package verification.
- Pull-request CI restores, builds, and publishes the Windows x64 app.

## Model truthfulness

The roadmap catalog contains future ALLINONE, CodeInOne, and MathInOne model families, but unavailable entries are explicitly labeled as unavailable. ALLINONE does not simulate a missing model runtime.

Local Core provides deterministic local functionality. Local Model connects to a model server already running on the Windows machine, so no cloud API key is required for inference.

## Local model setup

Open Settings → Local AI runtime.

For Ollama, the usual local endpoint is http://127.0.0.1:11434.
For an OpenAI-compatible local server, enter its localhost endpoint and model name.

## Build

Use the .NET 10 SDK on Windows:

dotnet restore src/ALLINONE/ALLINONE.csproj
dotnet build src/ALLINONE/ALLINONE.csproj -c Release
dotnet publish src/ALLINONE/ALLINONE.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

The application stores chats and project metadata below the current Windows user's local application-data directory.

Never commit API keys, OAuth client secrets, access tokens, or model weights.