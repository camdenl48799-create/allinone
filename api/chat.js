// /api/chat — server-side proxy to the Anthropic API.
//
// The frontend (index.html) never sees or holds an API key. It POSTs the
// conversation to this endpoint, this function attaches the real secret
// key (read from a Vercel environment variable) and calls Anthropic on
// the server, then returns just the reply text to the browser.
//
// Setup on Vercel:
//   Project Settings -> Environment Variables -> add ANTHROPIC_API_KEY
//   (get a key from https://console.anthropic.com). Redeploy after adding it.

const ANTHROPIC_URL = 'https://api.anthropic.com/v1/messages';
const MODEL = 'claude-sonnet-5';
const MAX_TOKENS = 1024;

module.exports = async (req, res) => {
  if (req.method !== 'POST') {
    res.status(405).json({ error: 'Method not allowed.' });
    return;
  }

  const apiKey = process.env.ANTHROPIC_API_KEY;
  if (!apiKey) {
    res.status(500).json({
      error: 'The AI backend is not configured yet. Add ANTHROPIC_API_KEY in the Vercel project settings and redeploy.'
    });
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
    .map(m => ({ role: m.role, content: m.content.slice(0, 8000) }))
    .slice(-20);

  if (cleanMessages.length === 0) {
    res.status(400).json({ error: 'No valid messages to send.' });
    return;
  }

  try {
    const upstream = await fetch(ANTHROPIC_URL, {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'x-api-key': apiKey,
        'anthropic-version': '2023-06-01'
      },
      body: JSON.stringify({
        model: MODEL,
        max_tokens: MAX_TOKENS,
        system: typeof system === 'string' ? system.slice(0, 2000) : undefined,
        messages: cleanMessages
      })
    });

    const data = await upstream.json().catch(() => null);

    if (!upstream.ok) {
      const detail = data && data.error && data.error.message ? data.error.message : `Upstream error (${upstream.status}).`;
      res.status(upstream.status >= 400 && upstream.status < 600 ? upstream.status : 502).json({ error: detail });
      return;
    }

    const text = Array.isArray(data && data.content)
      ? data.content.filter(b => b.type === 'text').map(b => b.text).join('\n').trim()
      : '';

    res.status(200).json({ text: text || "I didn't get a response back — try again." });
  } catch (err) {
    res.status(502).json({ error: 'Could not reach the AI provider. Please try again.' });
  }
};
