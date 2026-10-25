#!/usr/bin/env node
'use strict';

// Real local-runtime smoke test. This script deliberately accepts loopback
// addresses only and has no fallback to hosted inference.
const DEFAULT_ENDPOINT = 'http://127.0.0.1:11434';
const endpointInput = (process.env.ALLINONE_LOCAL_ENDPOINT || DEFAULT_ENDPOINT).trim();
const requestedModel = (process.env.ALLINONE_LOCAL_MODEL || '').trim();
const TIMEOUT_MS = 15000;

function fail(message) {
  console.error('FAIL', message);
  process.exitCode = 1;
}

function isLoopbackUrl(value) {
  try {
    const url = new URL(value);
    const hostname = url.hostname.toLowerCase();
    const loopback = hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '[::1]';
    return loopback && (url.protocol === 'http:' || url.protocol === 'https:') && !url.username && !url.password;
  } catch {
    return false;
  }
}

function normalizeBase(value) {
  const url = new URL(value);
  url.search = '';
  url.hash = '';
  let path = url.pathname.replace(/\/+$/, '');
  if (path.endsWith('/v1')) path = path.slice(0, -3);
  url.pathname = path;
  return url.toString().replace(/\/+$/, '');
}

async function fetchWithTimeout(url, options = {}) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    return await fetch(url, { ...options, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

async function main() {
  if (typeof fetch !== 'function') {
    fail('Node.js 18 or newer is required (global fetch is unavailable).');
    return;
  }
  if (!isLoopbackUrl(endpointInput)) {
    fail('Endpoint rejected. ALLINONE only allows a local loopback runtime such as http://127.0.0.1:11434.');
    return;
  }

  const base = normalizeBase(endpointInput);
  let modelList;
  try {
    const response = await fetchWithTimeout(base + '/v1/models');
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    modelList = await response.json();
  } catch (error) {
    const detail = error.name === 'AbortError' ? 'request timed out' : error.message;
    fail(`Cannot reach local runtime at ${base}: ${detail}. Start the runtime and try again.`);
    return;
  }

  const models = Array.isArray(modelList?.data)
    ? modelList.data.filter(item => item && typeof item.id === 'string' && item.id.trim())
    : [];
  if (models.length === 0) {
    fail('The runtime is reachable but /v1/models returned no model IDs. Install/load a model first.');
    return;
  }

  const ids = models.map(item => item.id);
  const model = requestedModel || ids[0];
  if (!ids.includes(model)) {
    fail(`Model "${model}" was not reported as installed. Available IDs: ${ids.join(', ')}`);
    return;
  }
  console.log('PASS local runtime reachable:', base);
  console.log('PASS model listed by runtime:', model);

  let payload;
  try {
    const response = await fetchWithTimeout(base + '/v1/chat/completions', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        model,
        messages: [
          { role: 'system', content: 'You are a test assistant. Reply briefly.' },
          { role: 'user', content: 'Reply with exactly: ALLINONE local model test passed.' }
        ],
        stream: false,
        temperature: 0
      })
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    payload = await response.json();
  } catch (error) {
    const detail = error.name === 'AbortError' ? 'request timed out' : error.message;
    fail(`Generation request failed for model "${model}": ${detail}`);
    return;
  }

  const reply = payload?.choices?.[0]?.message?.content;
  if (typeof reply !== 'string' || reply.trim().length === 0) {
    fail('The runtime responded, but returned no non-empty choices[0].message.content.');
    return;
  }

  console.log('PASS real generation returned non-empty text.');
  console.log('Reply:', reply.trim().replace(/\s+/g, ' ').slice(0, 240));
  console.log('\nSmoke test passed. This confirms the selected local runtime generated text; it is not a quality benchmark and does not establish that the model is an original ONE model.');
}

main().catch(error => {
  fail(`Unexpected smoke-test error: ${error.message}`);
});
