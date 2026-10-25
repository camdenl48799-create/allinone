// Deliberately disabled: ALLINONE chat inference must run on the user's device.
// This route previously proxied prompts to ONE_MODEL_URL, which could send
// conversations off-device. The browser app should use js/providers.js and a
// loopback-only local runtime instead. Keep this explicit response so old
// deployments fail closed instead of silently forwarding user conversations.

'use strict';

module.exports = async function disabledChatRoute(req, res) {
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader('X-Content-Type-Options', 'nosniff');

  if (req.method !== 'POST') {
    res.setHeader('Allow', 'POST');
    return res.status(405).json({ error: 'Method not allowed.' });
  }

  return res.status(410).json({
    error: 'Server-side chat inference is disabled. ALLINONE only supports a local model runtime on this device. Open Settings → AI engine to configure a loopback runtime.'
  });
};
