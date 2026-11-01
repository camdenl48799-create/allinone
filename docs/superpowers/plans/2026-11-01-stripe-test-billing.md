# Stripe Test Billing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add test-only paid-plan access to ALLINONE using Clerk identities and a secure Stripe backend, without metering or transmitting local AI prompts.

**Architecture:** The static browser app authenticates users with the existing Clerk identity system. Vercel Functions own Stripe Checkout, Customer Portal, entitlement reads, and signed webhooks; a server-side PostgreSQL store records the Clerk-user-to-Stripe-customer mapping and processed webhook IDs. Local WebGPU inference remains independent of billing and never sends chat content to billing endpoints.

**Tech Stack:** Vanilla browser JavaScript, Clerk browser authentication/JWTs, Vercel Functions, Node.js 23 Stripe SDK, PostgreSQL, Stripe Billing/Checkout/Customer Portal in Stripe sandbox.

**Spec:** `docs/superpowers/specs/2026-11-01-webgpu-allinone-design.md`

## Global Constraints

- Use a dedicated Stripe sandbox; do not enable live charges.
- Stripe restricted API keys and webhook secrets live only in server environment variables.
- The browser receives no Stripe secret, webhook secret, or backend database credential.
- Use Clerk as the account identity; verify bearer JWTs server-side.
- Use Stripe Checkout Sessions with `mode: "subscription"`; do not pass `payment_method_types`.
- Use each tier as its own Stripe Product with its own recurring monthly Price.
- Billing grants product access only; it does not meter or proxy local WebGPU inference.
- Verify webhook signatures from the raw body, store processed event IDs, and tolerate duplicate/out-of-order delivery.
- Preserve all existing native application and local-first browser behavior.
- Do not enable `automatic_tax` until an active Stripe Tax registration has been confirmed.

## Review Focus

- Missing/invalid Clerk token: return 401 and never create Checkout or Portal sessions. (Task 2)
- Price ID not allowlisted or tier mismatched: reject it before Stripe is called. (Task 3)
- Replayed/invalid webhook: reject invalid signatures and make the same event ID a no-op. (Task 4)
- Checkout return without a verified webhook: do not grant paid entitlement on the browser redirect. (Task 5)
- Local WebGPU prompt/history: verify it never appears in billing requests, logs, or database records. (Task 5)

---

## Current-state assessment

- The repository has a Stripe subscription plan only; no browser payment code, backend database, or active browser authentication integration exists.
- Native WPF code includes a Clerk OAuth service with environment configuration. The selected design extends Clerk identity to the browser rather than inventing a second account system.
- The current `api/chat.js` intentionally fails closed and must not become a billing or inference proxy.
- The prior billing plan's online-AI usage metering conflicts with the approved local-only WebGPU architecture and is explicitly excluded.

## File Structure

- Create: `api/_shared/auth.js` — Clerk JWT verification and request identity extraction.
- Create: `api/_shared/stripe.js` — server-only `StripeClient` construction from environment.
- Create: `api/_shared/catalog.js` — allowlisted Free, Pro, Pro+, Max, and Max+ tier/Price mapping.
- Create: `api/_shared/store.js` — PostgreSQL access for customer mapping, entitlements, and processed events.
- Create: `api/billing/checkout.js` — authenticated subscription Checkout Session creation.
- Create: `api/billing/portal.js` — authenticated Customer Portal session creation.
- Create: `api/billing/entitlement.js` — authenticated entitlement read.
- Create: `api/billing/webhook.js` — raw-body verified Stripe lifecycle handler.
- Create: `js/auth.js` — Clerk session acquisition and billing API wrapper.
- Create: `js/billing.js` — plan UI state and Checkout/Portal flows.
- Modify: `index.html`, `js/app.js`, `js/ui.js`, `css/styles.css` — account and billing UI only.
- Create: `db/migrations/001_billing.sql` — persistent mapping, entitlement, and webhook-event tables.
- Create: `tests/billing/*.test.js` — unit/integration tests using Stripe fixtures and fake storage.
- Create: `.env.example` — variable names only, never credential values.
- Modify: `README.md` and `docs/STRIPE-TEST-BILLING.md` — test setup and webhook procedure.

### Task 1: Server-owned billing schema and catalog

**Files:**
- Create: `api/_shared/catalog.js`
- Create: `db/migrations/001_billing.sql`
- Create: `tests/billing/catalog.test.js`
- Create: `.env.example`

**Interfaces:**
- Produces: `getTierByPriceId(priceId)`, `getCheckoutPrice(tierId)`, and PostgreSQL tables `billing_customers`, `billing_entitlements`, `processed_stripe_events`.
- Consumes: server environment variables `STRIPE_PRICE_PRO`, `STRIPE_PRICE_PRO_PLUS`, `STRIPE_PRICE_MAX`, and `STRIPE_PRICE_MAX_PLUS`.

- [ ] **Step 1: Write failing catalog and schema contract tests**

Test that every paid tier maps only to its configured test Price ID; unknown IDs and missing variables fail closed.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/billing/catalog.test.js`  
Expected: FAIL because no catalog exists.

- [ ] **Step 3: Implement immutable test-tier configuration and database migration**

Define Free, Pro, Pro+, Max, and Max+ as separate products/prices. Store Clerk user ID, Stripe customer ID, canonical subscription status, tier, period end, and processed event IDs; do not store chat text.

- [ ] **Step 4: Run catalog tests**

Run: `node --test tests/billing/catalog.test.js`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add api/_shared/catalog.js db/migrations/001_billing.sql tests/billing/catalog.test.js .env.example
git commit -m "feat: add Stripe test billing catalog"
```

### Task 2: Clerk identity and trusted server clients

**Files:**
- Create: `api/_shared/auth.js`
- Create: `api/_shared/stripe.js`
- Create: `api/_shared/store.js`
- Create: `tests/billing/auth.test.js`

**Interfaces:**
- Produces: `requireUser(request) -> { clerkUserId }`, `createStripeClient()`, and `BillingStore`.
- Consumes: Clerk JWKS configuration, `STRIPE_SECRET_KEY`, and database connection configuration.

- [ ] **Step 1: Write failing request-authentication tests**

Cover absent token, malformed token, unverifiable issuer/audience, and a verified Clerk user ID.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/billing/auth.test.js`  
Expected: FAIL because trusted server helpers do not exist.

- [ ] **Step 3: Implement server-only identity, Stripe, and persistence helpers**

Verify Clerk JWTs before any billing action. Instantiate one configured Stripe client in server code; never use global SDK credentials. Make entitlement/event persistence parameterized and transactional.

- [ ] **Step 4: Run authentication tests**

Run: `node --test tests/billing/auth.test.js`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add api/_shared/auth.js api/_shared/stripe.js api/_shared/store.js tests/billing/auth.test.js
git commit -m "feat: add trusted billing service foundation"
```

### Task 3: Checkout and customer portal endpoints

**Files:**
- Create: `api/billing/checkout.js`
- Create: `api/billing/portal.js`
- Create: `tests/billing/checkout.test.js`

**Interfaces:**
- Consumes: `requireUser`, `BillingStore`, `createStripeClient`, and `getCheckoutPrice`.
- Produces: JSON `{ url: string }` for a Stripe-hosted checkout or portal session.

- [ ] **Step 1: Write failing endpoint tests**

Assert unauthenticated and invalid-tier requests fail, allowed tiers create subscription Checkout, and the Portal reuses only the authenticated user's mapped customer.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/billing/checkout.test.js`  
Expected: FAIL because the endpoints do not exist.

- [ ] **Step 3: Implement authenticated Checkout and Portal sessions**

Create/reuse one Stripe Customer per Clerk user. Set Checkout `mode: "subscription"`, allowlisted monthly Price, success/cancel URLs, and a unique integration identifier. Never send entitlement status from the browser into Stripe.

- [ ] **Step 4: Run endpoint tests**

Run: `node --test tests/billing/checkout.test.js`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add api/billing/checkout.js api/billing/portal.js tests/billing/checkout.test.js
git commit -m "feat: add Stripe test checkout and portal"
```

### Task 4: Signed Stripe lifecycle webhooks

**Files:**
- Create: `api/billing/webhook.js`
- Create: `tests/billing/webhook.test.js`
- Modify: `api/_shared/store.js`

**Interfaces:**
- Consumes: raw request body, `STRIPE_WEBHOOK_SECRET`, Stripe event verification, and `BillingStore`.
- Produces: canonical entitlement updates from Checkout completion, subscription changes, invoice payment success/failure, and applicable refund/dispute/fraud events.

- [ ] **Step 1: Write failing webhook tests**

Cover invalid signatures, duplicate event IDs, out-of-order subscription updates, cancellation-at-period-end, payment failure, and refunded/disputed charges.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/billing/webhook.test.js`  
Expected: FAIL because the webhook handler does not exist.

- [ ] **Step 3: Implement raw-body verification and idempotent lifecycle handling**

Verify before parsing business data. Persist the Stripe event ID transactionally, re-fetch canonical subscription state when event order is ambiguous, and update entitlement only from verified Stripe state.

- [ ] **Step 4: Run webhook tests**

Run: `node --test tests/billing/webhook.test.js`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add api/billing/webhook.js api/_shared/store.js tests/billing/webhook.test.js
git commit -m "feat: add verified Stripe subscription webhooks"
```

### Task 5: Browser account and billing workspace

**Files:**
- Create: `js/auth.js`
- Create: `js/billing.js`
- Modify: `index.html`
- Modify: `js/app.js`
- Modify: `js/ui.js`
- Modify: `css/styles.css`
- Create: `tests/billing/browser.test.js`

**Interfaces:**
- Consumes: Clerk browser session, `/api/billing/entitlement`, `/api/billing/checkout`, and `/api/billing/portal`.
- Produces: `ALLINONE.billing.refresh()`, `startCheckout(tierId)`, and `openCustomerPortal()`.

- [ ] **Step 1: Write failing browser billing tests**

Cover signed-out state, loading state, no-entitlement state, paid entitlement state, failed Checkout return, and verification that chat text/history are absent from billing request bodies.

- [ ] **Step 2: Run the tests to verify failure**

Run: `node --test tests/billing/browser.test.js`  
Expected: FAIL because browser billing modules do not exist.

- [ ] **Step 3: Implement Clerk-aware billing UI**

Add sign-in, test-plan, manage-subscription, and entitlement displays matching the ALLINONE design. On Checkout return, refresh server entitlement; never claim success based only on the redirect. Keep model loading and local chat independent.

- [ ] **Step 4: Run browser billing tests**

Run: `node --test tests/billing/browser.test.js`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add js/auth.js js/billing.js index.html js/app.js js/ui.js css/styles.css tests/billing/browser.test.js
git commit -m "feat: add Clerk-backed billing workspace"
```

### Task 6: Sandbox verification, documentation, and CI

**Files:**
- Modify: `README.md`
- Create: `docs/STRIPE-TEST-BILLING.md`
- Modify: `.github/workflows/release.yml`
- Test: `tests/billing/*.test.js`

**Interfaces:**
- Consumes: Stripe sandbox products, Clerk development credentials, database migration, and webhook forwarding.
- Produces: reproducible test-only setup instructions and CI checks.

- [ ] **Step 1: Document sandbox provisioning and webhook forwarding**

Specify restricted server key permissions, Clerk development configuration, separate database, Price ID mapping, raw-webhook setup, and no-live-mode rule. Include the exact events the service acts on.

- [ ] **Step 2: Add CI-safe tests**

Run mocked Stripe/Clerk/database tests without personal credentials. Do not run real Checkout or expose secrets in Actions logs.

- [ ] **Step 3: Run full verification**

Run: `npm test`  
Expected: PASS.  
Run: `dotnet build src/ALLINONE/ALLINONE.csproj -c Release`  
Expected: PASS; no native billing/auth source changes.

- [ ] **Step 4: Commit**

```bash
git add README.md docs/STRIPE-TEST-BILLING.md .github/workflows/release.yml tests
git commit -m "docs: document Stripe sandbox billing"
```

## Self-review

- Spec coverage: Secure test billing, Clerk identity, checkout, portal, webhooks, local-only AI, and client-secret separation are covered.
- Gaps: Live activation, Stripe Tax registration, real product IDs, and production database credentials require explicit operator configuration and are not invented.
- Type consistency: Tasks 1–2 define catalog, identity, storage, and Stripe clients; Tasks 3–5 consume them.
- Review focus: Each listed failure condition has a named owning test task.
- Proportion: Billing is isolated from WebGPU work and can be implemented or reviewed independently.
