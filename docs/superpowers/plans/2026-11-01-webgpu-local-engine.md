# WebGPU Local Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the browser edition of ALLINONE real, on-device WebGPU chat inference while preserving deterministic safety and Offline Lite behavior.

**Architecture:** The existing static browser UI remains the product entry point. A worker-backed WebLLM adapter becomes the only generative path: it reports model-download progress, streams output, and fails explicitly to Offline Lite when WebGPU or the selected model is unavailable. A later connector plan will consume the same guarded action boundary after this engine is verified.

**Tech Stack:** Vanilla HTML/CSS/JavaScript, Web Workers, WebGPU, `@mlc-ai/web-llm`, Node built-in test runner.

**Spec:** `docs/superpowers/specs/2026-11-01-webgpu-allinone-design.md`

## Global Constraints

- No OpenAI, Anthropic, Gemini, or any hosted AI inference API.
- Never use API keys, hardcoded credentials, or bundled model weights.
- Only user-initiated model downloads; inference and history stay on-device.
- Keep deterministic crisis detection and safe-mode checks before model routing.
- Do not claim a roadmap model is available without a real local runtime.
- Preserve browser local storage and do not remove native billing/authentication code.
- WebGPU absence or initialization failure must visibly use Offline Lite; it must not pretend to be model output.
- Keep the existing #E01E2B visual identity and support responsive UI.

## Review Focus

- WebGPU missing, adapter failure, or required GPU feature unsupported: show a clear Offline Lite status and do not issue a network inference request. (Task 2)
- A model is not selected or not cached: require an explicit download/load action before sending model chat. (Task 3)
- A safe-mode crisis or blocked message: preserve the current deterministic response and never pass text to the worker. (Task 4)
- A streamed response is cancelled, empty, or errors mid-stream: finalize a visible error without corrupting saved history. (Task 4)
- Stored state from v1 lacks engine fields: migrate with Offline Lite defaults and preserve user name, assistant name, personality, and history. (Task 1)

---

## Current-state assessment

- The repository contains two separate products: a static browser app rooted at `index.html` and a native WPF application at `src/ALLINONE`.
- The browser app is a deterministic assistant: `js/app.js` routes every accepted message to `js/brain.js`; it has no WebGPU engine or real inference module.
- `docs/LOCAL-MODEL.md` references `js/providers.js` and `tests/run-tests.js`, but neither exists in the repository. Its described localhost runtime is therefore not wired into the current browser entry point.
- The browser app has no package-based build or test script. `package.json` contains only project metadata.
- The native app has deterministic Local Core and roadmap-only models. It includes Clerk OAuth configuration, which is outside this web-engine scope; no browser Stripe implementation exists, only a subscription plan document.
- `api/chat.js` correctly fails closed and must remain disabled. No active hosted inference endpoint or committed inference API key was found.
- No ONE-1 weights or trained checkpoint are present. The new UI must label models by their actual downloadable identity and keep One-1 unavailable.

## File Structure

- Modify: `index.html` — load engine modules and render model controls/status in Settings.
- Modify: `css/styles.css` — status, progress, selector, and responsive settings styles.
- Modify: `js/config.js` — engine defaults, model catalog metadata, and state schema version.
- Modify: `js/storage.js` — versioned migration helper without changing local-data ownership.
- Create: `js/engine-catalog.js` — validated selectable WebLLM model descriptors; no roadmap model is marked available.
- Create: `js/webgpu-engine.js` — UI-thread controller for availability, worker lifecycle, load progress, stream, cancel, and clear-cache capabilities.
- Create: `js/webgpu-worker.js` — isolated WebLLM worker message handler.
- Modify: `js/app.js` — safe routing, history handling, engine state persistence, model controls, and Offline Lite fallback.
- Modify: `js/ui.js` — DOM bindings and incremental streamed-message rendering.
- Modify: `package.json` — exact WebLLM dependency and `node --test` command; no cloud SDKs.
- Create: `tests/browser-state.test.js` — storage migration and model catalog tests.
- Create: `tests/webgpu-engine.test.js` — fake-worker protocol, unavailable WebGPU, progress, error, and cancellation tests.
- Create: `tests/app-routing.test.js` — safety-before-engine and streamed-history behavior tests.
- Modify: `README.md` and `docs/LOCAL-MODEL.md` — WebGPU install/use, cache, hardware limits, and truthful model labels.

### Task 1: Versioned browser engine state and catalog

**Files:**
- Create: `js/engine-catalog.js`
- Modify: `js/config.js`
- Modify: `js/storage.js`
- Modify: `package.json`
- Test: `tests/browser-state.test.js`

**Interfaces:**
- Produces: `ALLINONE.engineCatalog.list() -> ModelDescriptor[]` and `ALLINONE.storage.migrateState(saved) -> AppState`.
- Consumes: existing `ALLINONE.config` and v1 local-storage records.

- [ ] **Step 1: Write failing migration and catalog tests**

Test that a v1 record retains existing fields while receiving `engine.mode === "offline-lite"`, `engine.modelId === null`, and that only actual WebLLM descriptors are selectable.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/browser-state.test.js`  
Expected: FAIL because the catalog and migration helper do not exist.

- [ ] **Step 3: Implement versioned engine defaults and `migrateState(saved)`**

Use a new state version and validate untrusted stored values. Keep One-1/One-2/One-3.x/One-4 visible only as unavailable roadmap entries with no load action.

- [ ] **Step 4: Add the pinned WebLLM dependency and `npm test` script**

Use an exact version in `package.json`; do not add any hosted inference SDK.

- [ ] **Step 5: Run the state tests**

Run: `npm test -- --test-name-pattern="migration|catalog"`  
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add package.json js/config.js js/storage.js js/engine-catalog.js tests/browser-state.test.js
git commit -m "feat: add local model state and catalog"
```

### Task 2: Worker-backed WebGPU engine

**Files:**
- Create: `js/webgpu-engine.js`
- Create: `js/webgpu-worker.js`
- Test: `tests/webgpu-engine.test.js`

**Interfaces:**
- Consumes: `ALLINONE.engineCatalog.get(modelId)`.
- Produces: `ALLINONE.webgpuEngine.create(options)`, with `probe()`, `load(modelId, onProgress)`, `stream(messages, onDelta)`, `cancel()`, and `dispose()`.

- [ ] **Step 1: Write failing engine-protocol tests**

Cover no `navigator.gpu`, worker initialization failure, ordered progress events, cancellation, and worker error propagation using a fake Worker.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/webgpu-engine.test.js`  
Expected: FAIL because the engine controller does not exist.

- [ ] **Step 3: Implement the controller and isolated worker protocol**

The main thread owns UI-safe state; the worker owns WebLLM. Reject `stream()` unless a selected model is fully loaded. Map unavailable GPU conditions to a typed `unavailable` status rather than silently switching models.

- [ ] **Step 4: Run engine tests**

Run: `node --test tests/webgpu-engine.test.js`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add js/webgpu-engine.js js/webgpu-worker.js tests/webgpu-engine.test.js
git commit -m "feat: add WebGPU worker engine"
```

### Task 3: Model controls, load progress, and cache management

**Files:**
- Modify: `index.html`
- Modify: `css/styles.css`
- Modify: `js/ui.js`
- Modify: `js/app.js`
- Test: `tests/app-routing.test.js`

**Interfaces:**
- Consumes: `ALLINONE.webgpuEngine.create()` and catalog descriptors.
- Produces: `ALLINONE.app.selectModel(modelId)`, `loadSelectedModel()`, and `clearModelCache()`.

- [ ] **Step 1: Write failing UI-state tests**

Verify that model download/load is opt-in, a loading status disables duplicate send/load actions, and cache clearing requires an explicit confirmation.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/app-routing.test.js`  
Expected: FAIL because model controls and handlers do not exist.

- [ ] **Step 3: Add responsive model controls to Settings**

Show selected model, actual model source, expected download size, engine status, progress, unload/clear-cache controls, and a clear Offline Lite explanation. Do not say a model is ALLINONE-trained unless its descriptor proves that.

- [ ] **Step 4: Connect controls to persisted engine state**

Persist only model choice and user preferences; do not persist model weights or secrets. On reload, require explicit load if the cached model is not ready.

- [ ] **Step 5: Run UI-state tests**

Run: `node --test tests/app-routing.test.js`  
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add index.html css/styles.css js/ui.js js/app.js tests/app-routing.test.js
git commit -m "feat: add local model controls"
```

### Task 4: Safe streamed conversation routing and Offline Lite fallback

**Files:**
- Modify: `js/app.js`
- Modify: `js/ui.js`
- Test: `tests/app-routing.test.js`
- Modify: `README.md`
- Modify: `docs/LOCAL-MODEL.md`

**Interfaces:**
- Consumes: existing `ALLINONE.safety.checkMessage(text)`, `ALLINONE.brain.respond(text, context)`, and `webgpuEngine.stream(messages, onDelta)`.
- Produces: `ALLINONE.app.send(text)` that records only completed assistant messages and labels their source.

- [ ] **Step 1: Extend failing routing tests**

Assert safe mode returns before engine invocation, loaded WebGPU streams a model-labeled response, unavailable/failed WebGPU chooses explicitly labeled Offline Lite, and a cancelled/empty stream never creates a false completed message.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/app-routing.test.js`  
Expected: FAIL on the new routing assertions.

- [ ] **Step 3: Route accepted messages through the selected local engine**

Keep the existing deterministic safety check before routing. Build the model context from bounded local history, update one assistant bubble as deltas arrive, and save history only after a non-empty final result. On typed engine unavailability, invoke `brain.respond` and add an Offline Lite status marker.

- [ ] **Step 4: Update truthful local-model documentation**

Replace the stale missing-provider instructions with WebGPU requirements, user-initiated model download behavior, supported-device limitations, cache controls, and the exact Offline Lite fallback semantics.

- [ ] **Step 5: Run full verification**

Run: `npm test`  
Expected: PASS.  
Run: `npm run lint` only if a lint script is deliberately added; otherwise do not claim lint coverage.

- [ ] **Step 6: Commit**

```bash
git add js/app.js js/ui.js README.md docs/LOCAL-MODEL.md tests/app-routing.test.js
git commit -m "feat: route chat through local WebGPU inference"
```

## Self-review

- Spec coverage: Tasks 1–4 implement local WebGPU inference, user-selected downloads, streaming, truthful model labels, deterministic fallback, local storage, and safe routing. Connector registry work is deliberately deferred to its own plan after real local inference is verified.
- Gaps: Native WPF, Clerk, and billing are preserved but not modified; the current repository has no implemented browser billing to preserve.
- Type consistency: Task 1 defines catalog/state; Task 2 defines engine methods; Tasks 3–4 consume those exact methods.
- Review focus: Each stated failure mode has an owning test task.
- Proportion: This plan specifies interfaces and behavior without embedding implementation bodies.
