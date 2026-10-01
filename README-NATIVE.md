# ALLINONE — Native Windows

ALLINONE now has a native WPF/.NET 10 Windows foundation.

- No Electron runtime.
- The application does not launch PowerShell.
- First launch opens a setup wizard.
- Settings live in the user's local application-data folder.
- GitHub Actions publishes a self-contained Windows x64 executable.
- The model layer is provider-neutral so a high-capability AI backend can be added without rebuilding the UI.

The current fallback is deliberately deterministic; it is not claimed to be a full AI model. The next AI layer should provide model inference, long-context memory, retrieval, tool permissions, streaming, and Safe Mode enforcement.
