# ALLINONE WebGPU-first design

## Intent

Turn ALLINONE into a private, browser-first assistant that can provide genuine local-model chat with no hosted AI API keys. It must remain useful when a device cannot run a WebGPU model, and it may optionally use user-approved custom connectors.

## Goals

- Run a selectable language model locally in the browser through WebGPU.
- Keep prompts, chat history, settings, and model execution on-device by default.
- Stream model output without freezing the interface.
- Offer connector-based actions only after clear user approval.
- Preserve the current deterministic offline assistant as a fallback.

## Non-goals

- No automatic cloud fallback, telemetry, remote inference, or bundled secret keys.
- No connector may access local data or make network requests without user consent.
- No guarantee that every browser/device can run a local model.

## Constraints

- ALLINONE is currently a vanilla HTML/CSS/JavaScript web app deployed to Vercel.
- WebLLM requires WebGPU; unavailable or unsupported GPU features need a graceful fallback.
- Model downloads are user-initiated and may be large; model cache storage must be visible and removable.

## Approaches considered

1. **Browser-only WebGPU model.** Simple, fully private, but cannot safely host advanced custom connectors alone.
2. **Native local runtime.** More system access, but changes the product away from the existing web deployment.
3. **WebGPU-first browser app with permissioned connectors.** Selected. It preserves the existing app, enables local-model chat, and isolates optional integrations behind explicit capability prompts.

## Architecture

### Local AI engine

- Add WebLLM as the default inference engine.
- Run the engine in a dedicated Web Worker so model loading and token generation do not block the UI.
- Detect WebGPU before model selection.
- Provide a model catalog with small, medium, and capable device-appropriate choices.
- Show first-download size, loading progress, active model, storage use, and a clear-cache control.
- Stream output into the existing chat UI.
- Continue using the current rule-based brain when WebGPU is unavailable, model initialization fails, or the user selects Offline Lite mode.

### Connector platform

- Introduce a connector registry. A connector declares its name, version, capabilities, settings schema, required permissions, and action definitions.
- Keep all connectors disabled until the user enables one.
- For every action, show the connector name, requested capability, data scope, and destination before it runs.
- Start with browser-safe capabilities: user-selected file import/export, clipboard, downloads, and approved HTTPS requests to user-configured endpoints.
- Store connector configuration locally. Never ship credentials; connectors use a user-managed local login/session or an explicitly entered secret only when the connector requires it.
- Treat custom connector manifests and instructions as untrusted data; validate schemas and restrict capabilities.

### User experience

- Add a model picker and status panel: `Ready`, `Downloading`, `Loading`, `Offline Lite`, or `Unavailable`.
- Add a Connectors section in Settings with enable/disable controls, permissions, endpoint configuration, and local data controls.
- Make the default setup path work with no connector and no account.

## Safety and privacy

- Explicit consent before connector network activity or file access.
- Least-privilege capabilities and per-connector revoke/reset controls.
- Clear indication when a response came from Local AI, Offline Lite, or a connector action.
- No implicit background uploads.

## Delivery sequence

1. Add WebGPU detection, model selection, WebLLM worker, and the graceful Offline Lite fallback.
2. Connect streaming output to the existing conversation UI and add model/cache settings.
3. Add the connector registry, permissions UI, and a sample safe custom connector.
4. Add automated checks for capability validation, fallback behavior, and connector permission gates.

## Builder review notes

- The chosen library requires WebGPU and has no CPU fallback; the current deterministic assistant remains a deliberate compatibility path.
- A connector framework must separate untrusted connector metadata from executable capabilities and must never infer consent from configuration alone.
- Exact starter model IDs and cache quotas remain implementation choices, selected based on WebLLM's current compatible model catalog and tested browser limits.
