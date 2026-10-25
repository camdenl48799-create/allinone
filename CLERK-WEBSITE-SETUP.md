# Website sign-in for ALLINONE billing

The existing `index.html` is a local-first/offline assistant. The new `billing.html` is a separate account-and-billing workspace so adding sign-in does not silently change the offline app.

## 1. Create or choose a website Clerk application

In Clerk Dashboard, create/select the application for the ALLINONE website. This should be the website app, not just the native Windows app's localhost OAuth configuration in `CLERK-AUTH.md`.

For a first test, enable the sign-in methods you want (email verification is a simple starting point). Add the exact local and deployed website origins to Clerk's allowed origins / domain settings as required by the dashboard.

## 2. Add the public website key

Open Clerk Dashboard → API keys and copy the **Publishable Key**. It starts with `pk_test_` for a development instance.

Edit `js/clerk-config.js` and set:

```js
window.ALLINONE_CLERK_PUBLISHABLE_KEY = 'pk_test_your_website_publishable_key';
```

A Clerk publishable key is intended for browser use. Never put a Clerk Secret Key in this file or in GitHub.

## 3. Configure server environment variables

In Vercel Project Settings → Environment Variables, configure the website Clerk secret and the authorized website origin. Keep secret values out of GitHub and out of browser code.

- `CLERK_SECRET_KEY`: secret key for the same website Clerk application.
- `CLERK_AUTHORIZED_PARTIES`: exact origin(s), comma-separated, such as `https://your-site.example`. Include local development origin only when you actually use it.
- Keep the Stripe variables from `STRIPE-TEST-BILLING.md` set to the test sandbox values.
- `APP_URL`: the exact deployed website origin.

Redeploy after changing server-side environment variables.

## 4. Test sign-in

Open `/billing.html` on the deployed website. Sign up or sign in with the Clerk test app. Once signed in, the page requests a short-lived Clerk session token and sends it as a Bearer token to the billing endpoints. The backend verifies the token and checks its authorized origin before using the verified Clerk user ID.

## 5. Test subscription flow

- Select Try Pro checkout or Try Pro+ checkout to create a Stripe **test-mode** Checkout Session.
- Use Stripe's published test payment details only; never enter real card details for a test.
- After returning from Checkout, the workspace asks the server to read the subscription status from Stripe.
- Manage billing opens Stripe Customer Portal, once a customer profile exists.

## Current limitations

- This page is a test workspace scaffold, not a production-ready subscription gate.
- Do not grant paid features based only on browser state or the success redirect.
- The current status endpoint reads current Stripe subscriptions directly. Before production, implement durable subscription state and idempotent webhook processing, enforce entitlements server-side, and add abuse/rate-limit protection.
- Keep the backend's test-key guard in place until a separate, reviewed production rollout.
- Review applicable tax registration and collection obligations before live sales.
