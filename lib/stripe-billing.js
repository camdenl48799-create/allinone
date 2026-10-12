'use strict';

const Stripe = require('stripe');
const { verifyToken } = require('@clerk/backend');

let stripeClient;

class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

function getStripe() {
  if (stripeClient) return stripeClient;

  const key = process.env.STRIPE_SECRET_KEY;
  // This first integration is deliberately test-mode-only. It must not be
  // possible to accidentally charge real customers with this code.
  if (!key || !key.startsWith('sk_test_')) {
    throw new HttpError(503, 'Stripe test mode is not configured. Set STRIPE_SECRET_KEY to your sandbox secret key in the server environment.');
  }

  stripeClient = new Stripe(key);
  return stripeClient;
}

function getAppUrl() {
  const value = process.env.APP_URL;
  if (!value) throw new HttpError(503, 'APP_URL is not configured.');
  let url;
  try {
    url = new URL(value);
  } catch {
    throw new HttpError(503, 'APP_URL must be a valid website URL.');
  }
  if (url.protocol !== 'https:' && url.hostname !== 'localhost' && url.hostname !== '127.0.0.1') {
    throw new HttpError(503, 'APP_URL must use HTTPS except for local development.');
  }
  return url.origin;
}

function getPriceForPlan(plan) {
  if (plan === 'pro') return process.env.STRIPE_PRICE_PRO;
  if (plan === 'pro_plus') return process.env.STRIPE_PRICE_PRO_PLUS;
  throw new HttpError(400, 'Choose a valid plan: pro or pro_plus.');
}

async function requireAuthUser(req) {
  const authorization = req.headers && (req.headers.authorization || req.headers.Authorization);
  const token = typeof authorization === 'string' && authorization.startsWith('Bearer ')
    ? authorization.slice(7).trim()
    : '';

  if (!token) throw new HttpError(401, 'Please sign in to your ALLINONE account first.');

  const secretKey = process.env.CLERK_SECRET_KEY;
  const parties = (process.env.CLERK_AUTHORIZED_PARTIES || '')
    .split(',')
    .map(value => value.trim())
    .filter(Boolean);

  if (!secretKey || parties.length === 0) {
    throw new HttpError(503, 'Website sign-in is not configured yet. Set CLERK_SECRET_KEY and CLERK_AUTHORIZED_PARTIES on the server.');
  }

  try {
    const result = await verifyToken(token, {
      secretKey,
      authorizedParties: parties
    });
    const claims = result && result.data;
    if (!claims || !claims.sub || (result.errors && result.errors.length)) {
      throw new HttpError(401, 'Your sign-in session could not be verified. Please sign in again.');
    }
    // Only a verified Clerk subject is accepted as the account identity.
    return { id: claims.sub };
  } catch (error) {
    if (error instanceof HttpError) throw error;
    throw new HttpError(401, 'Your sign-in session could not be verified. Please sign in again.');
  }
}

function validateUserId(userId) {
  if (typeof userId !== 'string' || !/^[a-zA-Z0-9_:-]{1,256}$/.test(userId)) {
    throw new HttpError(401, 'Invalid signed-in account.');
  }
}

async function findCustomer(stripe, userId, createIfMissing = false) {
  validateUserId(userId);
  const result = await stripe.customers.search({
    query: `metadata['clerkUserId']:'${userId}'`,
    limit: 1
  });
  if (result.data.length) return result.data[0];
  if (!createIfMissing) return null;

  return stripe.customers.create(
    { metadata: { clerkUserId: userId } },
    { idempotencyKey: `allinone-customer-${userId}` }
  );
}

async function listSubscriptions(stripe, customerId) {
  const result = await stripe.subscriptions.list({
    customer: customerId,
    status: 'all',
    limit: 100
  });
  return result.data;
}

function planFromSubscription(subscription) {
  const item = subscription.items && subscription.items.data && subscription.items.data[0];
  const priceId = item && item.price && item.price.id;
  if (priceId && priceId === process.env.STRIPE_PRICE_PRO_PLUS) return 'pro_plus';
  if (priceId && priceId === process.env.STRIPE_PRICE_PRO) return 'pro';
  return 'unknown';
}

function sendError(res, error) {
  const status = error instanceof HttpError ? error.status : 500;
  if (status >= 500) {
    console.error('[ALLINONE billing]', error && error.message ? error.message : 'Unexpected billing error');
  }
  res.setHeader('Cache-Control', 'no-store');
  return res.status(status).json({
    error: status === 500 ? 'Billing could not be completed. Please try again later.' : error.message
  });
}

function parseBody(req) {
  if (req.body && typeof req.body === 'object') return req.body;
  if (typeof req.body === 'string') {
    try { return JSON.parse(req.body); } catch { return {}; }
  }
  return {};
}

module.exports = {
  HttpError,
  getStripe,
  getAppUrl,
  getPriceForPlan,
  requireAuthUser,
  findCustomer,
  listSubscriptions,
  planFromSubscription,
  sendError,
  parseBody
};
