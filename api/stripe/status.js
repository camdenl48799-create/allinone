'use strict';

const {
  getStripe,
  requireAuthUser,
  findCustomer,
  listSubscriptions,
  planFromSubscription,
  sendError
} = require('../../lib/stripe-billing');

module.exports = async (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  if (req.method !== 'GET') {
    res.setHeader('Allow', 'GET');
    return res.status(405).json({ error: 'Method not allowed.' });
  }

  try {
    const user = await requireAuthUser(req);
    const stripe = getStripe();
    const customer = await findCustomer(stripe, user.id, false);
    if (!customer) {
      return res.status(200).json({ plan: 'free', status: 'none', entitled: false });
    }

    const subscriptions = await listSubscriptions(stripe, customer.id);
    const subscription = subscriptions.find(sub => ['active', 'trialing'].includes(sub.status));
    if (!subscription) {
      const mostRecent = subscriptions[0];
      return res.status(200).json({
        plan: 'free',
        status: mostRecent ? mostRecent.status : 'none',
        entitled: false
      });
    }

    return res.status(200).json({
      plan: planFromSubscription(subscription),
      status: subscription.status,
      entitled: true,
      cancelAtPeriodEnd: Boolean(subscription.cancel_at_period_end),
      currentPeriodEnd: subscription.current_period_end || null
    });
  } catch (error) {
    return sendError(res, error);
  }
};
