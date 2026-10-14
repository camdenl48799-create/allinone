# ALLINONE Stripe Subscriptions & Usage Limits — Implementation Plan

**Date:** 2026-10-13  
**Repository:** `camdenl48799-create/allinone`  
**Status:** Plan only — no application code changed  
**Billing mode for development:** Stripe test mode only

## Goal

Add secure subscriptions and server-enforced daily AI request allowances to the existing ALLINONE desktop application. Preserve its local-first design where practical. Stripe secret keys and webhook secrets must never be shipped in the desktop app.

## Subscription tiers

These are the approved starting limits; keep them configurable so they can be changed without rewriting billing logic.

| Tier | Price | Initial daily AI request allowance |
|---|---:|---:|
| Free | $0 | 20 |
| Pro | $5/month | 200 |
| Pro+ | $15/month | 1,000 |
| Max | $20/month | 2,000 |
| Max+ | $25/month | 5,000 |

The existing Stripe test products/prices should be verified against configuration before use. Do not delete, rename, or modify existing Stripe products or subscriptions as part of this work.

## Architecture and security rules

- The desktop client must never contain Stripe secret keys, webhook secrets, or other server credentials.
- A small HTTPS billing service owns Checkout Session creation, subscription status, customer-to-user mapping, usage accounting, and webhook handling.
- Verify Stripe webhook signatures against the raw request body. Make event processing idempotent and tolerate duplicate or out-of-order events.
- Never trust a plan name, subscription status, or usage count sent by the desktop client.
- Use persistent server-side storage for account mapping, subscription state, processed webhook event IDs, and usage counters.
- Do not send AI prompts or chat history to the billing service; it needs only account identity, entitlement, and metering metadata.
- If entitlement or usage cannot be verified, fail safely and show a clear retry/status message rather than granting unlimited requests.
- Keep test and live Stripe credentials/products separate. Do not enable live charges in this implementation.

## Implementation tasks

### 1. Inspect and document the existing app integration points
- [ ] Identify the current authentication/account identity flow, AI request path, settings UI, and test framework in the repository.
- [ ] Record how the desktop app can call a remote HTTPS service without weakening existing Electron/WPF security settings.
- [ ] Confirm whether a supported backend/deployment target and persistent database already exist; do not assume one does.
- [ ] Add a short architecture note describing any required infrastructure before implementing billing.

**Acceptance:** the integration points and any infrastructure gaps are documented, and the current app continues to build.

### 2. Add shared plan configuration and entitlement rules
- [ ] Define Free, Pro, Pro+, Max, and Max+ in one server-owned configuration.
- [ ] Configure prices and Stripe Price IDs using environment variables; verify each configured ID maps to the expected recurring monthly price in test mode.
- [ ] Implement tier lookup and daily allowance calculation in a reusable server-side module.
- [ ] Define the daily reset boundary consistently (UTC calendar day) and document it in the UI.
- [ ] Make allowance values configurable, with the table above as initial defaults.

**Acceptance:** unit tests cover every tier, unknown/missing prices, canceled or past-due subscriptions, and the daily reset boundary.

### 3. Implement billing service and persistent data
- [ ] Add an HTTPS service using the repository's existing stack if suitable; otherwise document and get approval for the smallest supported service stack before adding a new one.
- [ ] Persist account mapping, Stripe customer ID, active subscription/price, entitlement status, usage counters, and processed webhook event IDs.
- [ ] Create or retrieve a Stripe customer safely for an authenticated account.
- [ ] Add an authenticated endpoint to create a Stripe Checkout Session for a selected paid tier.
- [ ] Add an authenticated endpoint to return the current entitlement and remaining daily requests.
- [ ] Add a server-side usage reservation/consume operation that prevents concurrent requests from exceeding the allowance.
- [ ] Add a customer portal session endpoint for subscription management, if the account flow supports it.
- [ ] Keep all credentials in deployment environment variables; provide only placeholder names in `.env.example`.

**Acceptance:** endpoint tests verify authentication, validation, persistence, and that the client cannot choose its own entitlement or remaining count.

### 4. Handle Stripe subscription lifecycle
- [ ] Add a webhook endpoint with raw-body signature verification.
- [ ] Handle checkout completion, subscription creation/update/deletion, invoice payment success, and payment failure events relevant to the chosen Stripe API version.
- [ ] Process each event idempotently and store its event ID.
- [ ] Re-fetch canonical Stripe subscription state when needed rather than trusting event arrival order.
- [ ] Define behavior for cancellation at period end, failed renewal, refund/dispute, and subscription expiration.
- [ ] Provide a local Stripe CLI test procedure for forwarding signed test events.

**Acceptance:** tests reject invalid signatures, safely ignore duplicate events, and update entitlements correctly for renewal, cancellation, and payment failure.

### 5. Connect usage enforcement to AI requests
- [ ] Route eligible online AI requests through a server-controlled allowance check before processing.
- [ ] Atomically reserve/consume one request to avoid race conditions.
- [ ] Release a reservation when a request fails before it is accepted, where appropriate; document the counting rule.
- [ ] Do not meter local-only actions that never invoke an online AI service.
- [ ] Return a structured limit-reached response with the reset time and current plan.
- [ ] Do not promise that subscriptions unlock a stronger model unless model access is separately implemented and verified.

**Acceptance:** tests show each tier can make exactly its configured allowance, concurrent calls cannot bypass limits, and local-only actions do not consume online requests.

### 6. Add subscription and usage UI
- [ ] Add a plan/usage section showing Free, Pro, Pro+, Max, and Max+ with monthly prices and allowances.
- [ ] Show current plan, remaining daily requests, and reset time from the trusted service.
- [ ] Add subscribe/manage-subscription actions using Checkout and the customer portal.
- [ ] Handle browser return/cancel flows and refresh entitlement from the service; never mark a purchase successful based only on a redirect.
- [ ] Provide clear offline, loading, payment-failed, and limit-reached states.
- [ ] Keep the existing visual style and safe-mode behavior.

**Acceptance:** UI tests cover loading, each plan, successful return, canceled Checkout, expired subscription, and exhausted usage.

### 7. Tests, CI, and documentation
- [ ] Add unit and integration tests for plan mapping, authentication, usage metering, checkout, and webhook handling.
- [ ] Use Stripe test mode and mocked/fixed test fixtures; no real charges.
- [ ] Ensure tests do not require a developer's personal Stripe credentials.
- [ ] Update GitHub Actions to run the new test suite and existing checks.
- [ ] Document local setup, required environment variables, database setup/migrations, Stripe test product configuration, webhook forwarding, and test commands.
- [ ] Verify no secret values, customer data, or chat content are committed to the repository or logs.

**Acceptance:** CI passes from a clean checkout, documented test steps work, and a repository secret scan finds no committed credentials.

## Suggested implementation order

1. Repository audit and architecture confirmation.
2. Plan configuration and pure entitlement logic.
3. Persistent billing service and authenticated endpoints.
4. Stripe Checkout and signed webhook lifecycle.
5. AI request metering and concurrency tests.
6. Desktop UI integration.
7. CI, security review, and end-to-end Stripe test-mode walkthrough.

## Definition of done

- All five tiers are represented consistently in the UI and server configuration.
- A user cannot spoof a paid plan or bypass request limits by editing client state.
- Checkout, renewals, cancellations, and payment failures update access based on verified Stripe state.
- Duplicate webhook delivery does not double-apply changes.
- Existing Max and Max+ test products/subscriptions are left intact.
- No live charges are enabled, and no secrets are committed.
- Existing app functionality and automated checks remain healthy.

## Important open dependency

The current repository README describes ALLINONE as a local-first desktop app. Before coding, confirm the supported authentication and deployment/database choices for the required HTTPS billing service. A Stripe secret key cannot safely be embedded in a desktop-only app. If the product must remain entirely offline with no account or server, paid subscription verification and shared server-side usage enforcement cannot be made tamper-resistant; that would require a different, explicitly accepted design.
