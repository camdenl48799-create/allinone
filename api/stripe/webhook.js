'use strict';

const {
  getStripe,
  sendError
} = require('../../lib/stripe-billing');

module.exports = async (req, res) => {
  if (req.method !== 'POST') {
    res.setHeader('Allow', 'POST');
    return res.status(405).send('Method not allowed.');
  }

  try {
    const secret = process.env.STRIPE_WEBHOOK_SECRET;
    if (!secret || !secret.startsWith('whsec_')) {
      return res.status(503).json({ error: 'Stripe webhook signing secret is not configured.' });
    }

    const stripe = getStripe();
    const signature = req.headers['stripe-signature'];
    if (!signature) return res.status(400).send('Missing Stripe signature.');

    const chunks = [];
    for await (const chunk of req) chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
    const rawBody = Buffer.concat(chunks);
    let event;
    try {
      event = stripe.webhooks.constructEvent(rawBody, signature, secret);
    } catch (error) {
      return res.status(400).send('Invalid Stripe webhook signature.');
    }

    // The status endpoint reads current subscription state directly from Stripe.
    // Keep verified event IDs/types in server logs for diagnostics; never trust
    // browser redirects as proof that a payment succeeded.
    const handledTypes = new Set([
      'checkout.session.completed',
      'checkout.session.async_payment_succeeded',
      'checkout.session.async_payment_failed',
      'customer.subscription.created',
      'customer.subscription.updated',
      'customer.subscription.deleted',
      'invoice.paid',
      'invoice.payment_failed'
    ]);
    if (handledTypes.has(event.type)) {
      console.info('[ALLINONE billing webhook]', JSON.stringify({
        id: event.id,
        type: event.type,
        livemode: event.livemode
      }));
    }

    return res.status(200).json({ received: true });
  } catch (error) {
    return sendError(res, error);
  }
};

module.exports.config = {
  api: { bodyParser: false }
};
