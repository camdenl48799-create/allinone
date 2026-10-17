'use strict';

const {
  HttpError,
  getStripe,
  getAppUrl,
  requireAuthUser,
  findCustomer,
  sendError
} = require('../../lib/stripe-billing');

module.exports = async (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  if (req.method !== 'POST') {
    res.setHeader('Allow', 'POST');
    return res.status(405).json({ error: 'Method not allowed.' });
  }

  try {
    const user = await requireAuthUser(req);
    const stripe = getStripe();
    const customer = await findCustomer(stripe, user.id, false);
    if (!customer) throw new HttpError(404, 'No billing profile was found for this account yet.');

    const session = await stripe.billingPortal.sessions.create({
      customer: customer.id,
      return_url: getAppUrl()
    });
    return res.status(200).json({ url: session.url });
  } catch (error) {
    return sendError(res, error);
  }
};
