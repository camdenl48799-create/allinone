'use strict';

const { describe, it } = require('node:test');
const assert = require('node:assert/strict');

// Minimal browser globals for the IIFE modules
global.window = global;
global.localStorage = {
  _data: {},
  getItem(k) { return this._data[k] ?? null; },
  setItem(k, v) { this._data[k] = String(v); },
  removeItem(k) { delete this._data[k]; },
  get length() { return Object.keys(this._data).length; },
  key(i) { return Object.keys(this._data)[i] ?? null; }
};

// Load modules in dependency order
require('../js/config.js');
require('../js/engine-catalog.js');
require('../js/storage.js');

const { config, engineCatalog, storage } = global.ALLINONE;

describe('engine catalog', () => {
  it('exposes list and get', () => {
    assert.equal(typeof engineCatalog.list, 'function');
    assert.equal(typeof engineCatalog.get, 'function');
    assert.equal(typeof engineCatalog.isSelectable, 'function');
  });

  it('contains selectable WebLLM models', () => {
    const available = engineCatalog.list({ onlyAvailable: true });
    assert.ok(available.length >= 2, 'expected at least two downloadable models');
    for (const m of available) {
      assert.equal(m.available, true);
      assert.ok(m.id && m.displayName);
      assert.notEqual(m.family, 'ALLINONE');
    }
  });

  it('marks One-* models as unavailable roadmap entries', () => {
    const one1 = engineCatalog.get('one-1');
    assert.ok(one1);
    assert.equal(one1.available, false);
    assert.equal(one1.family, 'ALLINONE');
    assert.ok(one1.reason && one1.reason.length > 0);

    const one2 = engineCatalog.get('one-2');
    assert.ok(one2);
    assert.equal(one2.available, false);
  });

  it('isSelectable returns false for roadmap models', () => {
    assert.equal(engineCatalog.isSelectable('one-1'), false);
    assert.equal(engineCatalog.isSelectable('one-4'), false);
  });

  it('isSelectable returns true for a real WebLLM model', () => {
    const available = engineCatalog.list({ onlyAvailable: true });
    assert.ok(available.length > 0);
    assert.equal(engineCatalog.isSelectable(available[0].id), true);
  });
});

describe('storage migration', () => {
  it('creates a clean v2 state from null', () => {
    const state = storage.migrateState(null);
    assert.equal(state.version, 2);
    assert.equal(state.engine.mode, 'offline-lite');
    assert.equal(state.engine.modelId, null);
    assert.equal(state.setupComplete, false);
    assert.ok(Array.isArray(state.history));
  });

  it('preserves v1 user fields and adds engine defaults', () => {
    const v1 = {
      assistantName: 'Pixel',
      userName: 'Camden',
      personality: 'chill',
      safeMode: false,
      setupComplete: true,
      history: [{ role: 'user', text: 'hi', ts: 1 }]
    };
    const state = storage.migrateState(v1);
    assert.equal(state.version, 2);
    assert.equal(state.assistantName, 'Pixel');
    assert.equal(state.userName, 'Camden');
    assert.equal(state.personality, 'chill');
    assert.equal(state.safeMode, false);
    assert.equal(state.setupComplete, true);
    assert.equal(state.history.length, 1);
    assert.equal(state.engine.mode, 'offline-lite');
    assert.equal(state.engine.modelId, null);
  });

  it('keeps a previously selected selectable model', () => {
    const available = engineCatalog.list({ onlyAvailable: true });
    const modelId = available[0].id;
    const saved = {
      assistantName: 'Nova',
      engine: { mode: 'webgpu', modelId }
    };
    const state = storage.migrateState(saved);
    assert.equal(state.engine.mode, 'webgpu');
    assert.equal(state.engine.modelId, modelId);
  });

  it('drops a non-selectable (roadmap) modelId', () => {
    const saved = {
      assistantName: 'Nova',
      engine: { mode: 'webgpu', modelId: 'one-1' }
    };
    const state = storage.migrateState(saved);
    assert.equal(state.engine.modelId, null);
  });
});
