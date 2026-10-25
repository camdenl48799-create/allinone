# Upgrade report

## Scope

This patch intentionally targets only previously flagged areas. The main ALLINONE UI, provider code, and unit-test files were not changed. The separately flagged legacy `edge.html` page was changed only to remove its hosted-AI pathway.

## Changes in this patch

- `api/chat.js`: disabled the unused server-side inference proxy. It previously forwarded conversation content to `ONE_MODEL_URL`, which is inconsistent with the project's on-device inference requirement. Legacy deployments now fail closed with HTTP 410 instead of forwarding prompts.
- `edge.html`: removed the OpenAI API URL/key-driven request path and the related settings text. The page is explicitly an offline demo with limited built-in responses. Its other tools and settings were left in place.
- `docs/LOCAL-MODEL.md`: clarified local-only runtime setup and distinguished mocked unit tests from real model verification.
- `scripts/smoke-test-local-model.js`: added a standalone check that queries a loopback runtime, confirms a model is installed, sends a small generation request, and requires a real non-empty response.
- `docs/ONE-1-RELEASE-CHECKLIST.md`: added release evidence requirements so ONE-1 cannot be confused with a third-party model or an integration adapter.

## Remaining blockers

- ONE-1 weights/checkpoint are not in the repository. They cannot be manufactured by changing UI or adapter code; until a real model is developed and validated, ONE-1 remains planned and unavailable.
- A genuine generation test still requires the user to install/start a compatible local runtime and model. This environment does not include those weights or a running runtime, so this patch does not claim a successful real-model test.
- Web authentication and Stripe billing are not integrated into this static browser app. Existing native Windows authentication code and the Stripe plan document were left untouched; a real web integration needs configured public Clerk settings, a secure server-side checkout endpoint, Stripe Price IDs, and changes to the web app entry point. Those changes are not safe to invent without project configuration.

## Verification

The existing `npm test` suite was run before edits and reported 12 passed / 0 failed. It was not changed. After this patch, it must be rerun to confirm the unmodified passing logic remains intact. The new real-runtime smoke test was added but cannot pass unless a loopback model runtime is installed and running.
