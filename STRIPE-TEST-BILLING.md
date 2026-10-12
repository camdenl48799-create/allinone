# ALLINONE Stripe test billing

This is a test-mode-only starter for website/workspace subscriptions.

## Plans

- ALLINONE Pro: $5 USD per month — `price_1UPbkwRnqtg1OjP7opRotxtz`
- ALLINONE Pro+: $15 USD per month — `price_1UPblTRnqtg1OjP7QOaHIPbv`

Both Price IDs were retrieved from the ALLINONE test sandbox. The server code rejects a Stripe secret key unless it starts with `sk_test_`, so this starter cannot be used for live charges.

## API endpoints

- `POST /api/stripe/checkout` with JSON `{"plan":"pro"}` or `{"plan":"pro_plus"}`; requires a valid Clerk session token in `Authorization: Bearer <token>`.
- `POST /api/stripe/portal`; requires a valid Clerk session token and opens the Stripe Customer Portal for the signed-in user's billing profile.
- `GET /api/stripe/status`; requires a valid Clerk session token and reports current subscription status from Stripe.
- `POST /api/stripe/webhook`; verifies Stripe's `Stripe-Signature` header against the raw request body and the test webhook signing secret.

The browser must request a Clerk session token and send it as a bearer token. Never accept a user ID, Stripe customer ID, plan price ID, or entitlement claim supplied by the browser as authoritative.

## Deployment configuration

Set these as **server-side environment variables** in the deployment platform, then redeploy:

- `STRIPE_SECRET_KEY`: the ALLINONE sandbox secret key (`sk_test_...`).
- `STRIPE_WEBHOOK_SECRET`: the signing secret for a test-mode webhook endpoint pointing to `/api/stripe/webhook`.
- `STRIPE_PRICE_PRO`: the Pro test Price ID above.
- `STRIPE_PRICE_PRO_PLUS`: the Pro+ test Price ID above.
- `CLERK_SECRET_KEY`: the secret key for the website's Clerk application.
- `CLERK_AUTHORIZED_PARTIES`: comma-separated exact website origins authorized to use the website Clerk session, such as your deployed HTTPS origin and a local development origin if needed.
- `APP_URL`: the exact public website origin used for Checkout return URLs.

Use the website's Clerk application. The existing `CLERK-AUTH.md` describes the native Windows app's localhost OAuth flow; it does not by itself add website sign-in.

## Important next steps

1. Add website Clerk sign-in to the website/workspace and obtain a session token with the Clerk frontend SDK.
2. Configure the environment variables above without committing secret values.
3. In Stripe test mode, create a webhook endpoint for `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`, `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.paid`, and `invoice.payment_failed`.
4. Test checkout and portal flows with Stripe's test payment details. Do not use a real card for testing.
5. Connect the returned subscription status to actual paid features on the server. Hiding a button in the browser is not an access-control boundary.

## Before going live

This branch intentionally blocks live Stripe secret keys. A production rollout needs a reviewed change to that guard, real-mode products/prices and webhook configuration, secure persistent storage for the Clerk-user-to-Stripe-customer mapping and webhook-driven subscription state, account security review, and review of applicable tax obligations. Stripe Tax should not be enabled until its configuration and your registration/collection responsibilities are reviewed. A parent/guardian should help with any real-money account or legal setup if required.
