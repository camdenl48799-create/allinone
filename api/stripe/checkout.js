'use strict';

const {
  HttpError,
  getStripe,
  getAppUrl,
  getPriceForPlan,
  requireAuthUser,
  findCustomer,
  listSubscriptions,
  sendError,
  parseBody
} = require('../../lib/stripe-billing');

module.exports = async (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  if (req.method !== 'POST') {
    res.setHeader('Allow', 'POST');
    return res.status(405).json({ error: 'Method not allowed.' });
  }

  try {
    const user = await requireAuthUser(req);
    const body = parseBody(req);
    const plan = body.plan;
    const price = getPriceForPlan(plan);
    if (!price || !price.startsWith('price_')) {
      throw new HttpError(503, 'The selected monthly price is not configured on the server.');
    }

    const stripe = getStripe();
    const customer = await findCustomer(stripe, user.id, true);
    const subscriptions = await listSubscriptions(stripe, customer.id);
    const alreadySubscribed = subscriptions.some(sub => ['active', 'trialing', 'past_due', 'unpaid'].includes(sub.status));
    if (alreadySubscribed) {
      throw new HttpError(409, 'You already have a subscription. Use Manage subscription to change or cancel it.');
    }

    const appUrl = getAppUrl();
    const session = await stripe.checkout.sessions.create({
      mode: 'subscription',
      customer: customer.id,
      client_reference_id: user.id,
      line_items: [{ price, quantity: 1 }],
      metadata: { clerkUserId: user.id, plan },
      subscription_data: {
        metadata: { clerkUserId: user.id, plan }
      },
      success_url: `${appUrl}/?billing=success&session_id={CHECKOUT_SESSION_ID}`,
      cancel_url: `${appUrl}/?billing=cancelled`
    });

    if (!session.url) throw new Error('Stripe did not return a Checkout URL.');
    return res.status(200).json({ url: session.url });
  } catch (error) {
    return sendError(res, error);
  }
};
