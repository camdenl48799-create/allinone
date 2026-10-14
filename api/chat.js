// /api/chat — adapter for ALLINONE's own ONE model runtime.
//
// This endpoint never calls OpenAI, Anthropic, or another hosted AI provider,
// and it never requires an external AI API key. When a ONE inference service is
// deployed, set ONE_MODEL_URL to its OpenAI-compatible /v1/chat/completions
// endpoint. The model service itself must be owned/operated by ALLINONE.
//
// If the ONE runtime is not connected yet, return a clear message instead of
// asking the user for a third-party API key or pretending the model is ready.

const MAX_MESSAGES = 20;
const MAX_MESSAGE_CHARS = 8000;
const MAX_SYSTEM_CHARS = 2000;

module.exports = async (req, res) => {
  if (req.method !== 'POST') {
    res.status(405).json({ error: 'Method not allowed.' });
    return;
  }

  const modelUrl = process.env.ONE_MODEL_URL;
  if (!modelUrl) {
    res.status(503).json({
      error: "The ONE model runtime is not connected yet. ALLINONE does not need an external AI API key; connect/deploy the ONE inference runtime to enable generative replies."
    });
    return;
  }

  let parsedUrl;
  try {
    parsedUrl = new URL(modelUrl);
  } catch {
    res.status(500).json({ error: 'ONE_MODEL_URL must be a valid HTTPS URL.' });
    return;
  }

  if (parsedUrl.protocol !== 'https:' && parsedUrl.hostname !== 'localhost' && parsedUrl.hostname !== '127.0.0.1') {
    res.status(500).json({ error: 'ONE_MODEL_URL must use HTTPS, except for local development.' });
    return;
  }

  let body = req.body;
  if (typeof body === 'string') {
    try { body = JSON.parse(body); } catch { body = {}; }
  }
  body = body || {};

  const { messages, system } = body;
  if (!Array.isArray(messages) || messages.length === 0) {
    res.status(400).json({ error: 'Request must include a non-empty "messages" array.' });
    return;
  }

  const cleanMessages = messages
    .filter(m => m && typeof m.content === 'string' && m.content.trim() && (m.role === 'user' || m.role === 'assistant'))
    .map(m => ({ role: m.role, content: m.content.slice(0, MAX_MESSAGE_CHARS) }))
    .slice(-MAX_MESSAGES);

  if (cleanMessages.length === 0) {
    res.status(400).json({ error: 'No valid messages to send.' });
    return;
  }

  const outgoingMessages = cleanMessages.slice();
  if (typeof system === 'string' && system.trim()) {
    outgoingMessages.unshift({ role: 'system', content: system.slice(0, MAX_SYSTEM_CHARS) });
  }

  try {
    const upstream = await fetch(modelUrl, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        model: 'ONE',
        messages: outgoingMessages,
        stream: false
      })
    });

    const data = await upstream.json().catch(() => null);
    if (!upstream.ok) {
      res.status(502).json({
        error: 'The ONE model runtime returned an error. Check the ONE runtime service logs.'
      });
      return;
    }

    const text = typeof data?.text === 'string'
      ? data.text.trim()
      : typeof data?.choices?.[0]?.message?.content === 'string'
        ? data.choices[0].message.content.trim()
        : '';

    if (!text) {
      res.status(502).json({ error: 'The ONE model runtime returned no reply text.' });
      return;
    }

    res.status(200).json({ text });
  } catch {
    res.status(502).json({
      error: 'Could not reach the ONE model runtime. No external AI API key is needed; check the runtime URL and service status.'
    });
  }
};
