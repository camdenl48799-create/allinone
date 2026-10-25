# Running and verifying a local model with ALLINONE

## What this does (and does not do)

The browser app can talk to an OpenAI-compatible **runtime running on the same device** through `127.0.0.1`, `localhost`, or `::1`. The app blocks non-loopback inference endpoints and has no automatic hosted fallback. The runtime itself is a separate program you install and start.

**ONE-1 is not included.** The provider interface lists ONE-1 as planned, and no ONE-1 model weights or trained checkpoint are present in this repository. A third-party model running locally must be labelled as that model; it must not be described as an original ALLINONE-trained model. The integration code is not evidence that model weights exist.

## Example setup using Ollama

1. Install Ollama from its official website as a separate application.
2. In a terminal, download a small model explicitly, for example `ollama pull llama3.2:3b`. This step downloads third-party weights; ALLINONE itself does not download them.
3. Run `ollama list` and copy the exact model name shown.
4. Start ALLINONE from a local web server (not necessarily a `file://` URL), then open **Settings → AI engine**.
5. Choose **Local model runtime**, keep the default endpoint `http://127.0.0.1:11434`, enter the model name from `ollama list`, and press **Test connection**. If the browser reports a CORS error, configure the runtime to allow the exact origin used to serve ALLINONE, then restart the runtime. Do not change the endpoint to a hosted provider.
6. Send a non-math chat message. A working setup should show a model-generated reply; a stopped runtime or missing model should show an error rather than a rule-based or hosted fallback.

The provider integration expects these OpenAI-compatible endpoints on loopback:

- `GET /v1/models` — list installed models
- `POST /v1/chat/completions` — generate a non-streaming response

The local runtime may use a different port. Only enter its loopback address; remote hostnames and IP addresses are rejected. Do not expose the runtime to the public internet.

## Verify a real runtime (Node.js 18+)

The unit tests in `tests/run-tests.js` use a mocked runtime. They verify parsing and safety checks, but **do not prove that an actual model works**. For a real model check, run:

```sh
node scripts/smoke-test-local-model.js
```

Optional environment variables:

- `ALLINONE_LOCAL_ENDPOINT` — loopback runtime base address; defaults to `http://127.0.0.1:11434`.
- `ALLINONE_LOCAL_MODEL` — exact installed model ID; if omitted, the script picks the first ID returned by `/v1/models`.

The smoke test sends one small prompt to the configured local runtime and fails if the endpoint is unreachable, no model is installed, the selected model is missing, or no generated text is returned. It does not send requests to non-loopback hosts.

## Hardware notes

Memory needs vary with model architecture, quantization, context length, and runtime settings. Treat model publisher recommendations as estimates, not guarantees. On an 8 GB RAM computer, start with a small quantized model, close other memory-heavy apps, and expect that response speed may be limited. The smoke test reports the selected model and checks whether it can answer, but it is not a performance benchmark.

## Known limits

- Browser access to localhost may be restricted by CORS or private-network policies depending on the browser and how ALLINONE is served. Configure only the local runtime's origin allowlist; do not weaken the loopback-only endpoint check.
- Arithmetic requests handled by the deterministic path are computed locally without sending them to the model.
- Generated code is displayed as text and is not automatically executed.
- If the runtime is unavailable, ALLINONE shows an error. It does not silently fall back to hosted inference.
