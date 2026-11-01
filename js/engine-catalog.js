(function (global) {
  'use strict';

  var ALLINONE = (global.ALLINONE = global.ALLINONE || {});

  /**
   * Model catalog for ALLINONE.
   *
   * - Real WebLLM models are marked available: true and can be downloaded by the user.
   * - ALLINONE One-* / Titan series entries are roadmap only (available: false).
   * - Never claim a third-party model is an original ONE model.
   * - No model weights are shipped in this repository.
   */
  var CATALOG = [
    // --- Selectable WebLLM models (user-initiated download) ---
    {
      id: 'Llama-3.2-1B-Instruct-q4f16_1-MLC',
      displayName: 'Llama 3.2 1B Instruct',
      family: 'Llama',
      source: 'WebLLM',
      sizeLabel: '~712 MB',
      vramHintMB: 900,
      context: 4096,
      available: true,
      recommended: true,
      description: 'Small, capable general model. Good starting point for most devices.'
    },
    {
      id: 'Qwen2.5-0.5B-Instruct-q4f16_1-MLC',
      displayName: 'Qwen2.5 0.5B Instruct',
      family: 'Qwen',
      source: 'WebLLM',
      sizeLabel: '~278 MB',
      vramHintMB: 400,
      context: 4096,
      available: true,
      recommended: true,
      description: 'Tiny and fast. Best choice for low-memory devices.'
    },
    {
      id: 'Qwen2.5-1.5B-Instruct-q4f16_1-MLC',
      displayName: 'Qwen2.5 1.5B Instruct',
      family: 'Qwen',
      source: 'WebLLM',
      sizeLabel: '~868 MB',
      vramHintMB: 1100,
      context: 4096,
      available: true,
      recommended: false,
      description: 'Balanced quality and size. Strong multilingual support.'
    },
    {
      id: 'Llama-3.2-3B-Instruct-q4f16_1-MLC',
      displayName: 'Llama 3.2 3B Instruct',
      family: 'Llama',
      source: 'WebLLM',
      sizeLabel: '~1.76 GB',
      vramHintMB: 2200,
      context: 4096,
      available: true,
      recommended: false,
      description: 'Higher quality. Requires more GPU memory.'
    },
    {
      id: 'Phi-3.5-mini-instruct-q4f16_1-MLC',
      displayName: 'Phi-3.5 Mini Instruct',
      family: 'Phi',
      source: 'WebLLM',
      sizeLabel: '~2.1 GB',
      vramHintMB: 2500,
      context: 4096,
      available: true,
      recommended: false,
      description: 'Strong reasoning for its size. Needs a capable GPU.'
    },

    // --- ALLINONE roadmap models (not available yet) ---
    {
      id: 'one-1',
      displayName: 'One-1',
      family: 'ALLINONE',
      source: 'ALLINONE',
      sizeLabel: '—',
      vramHintMB: null,
      context: null,
      available: false,
      recommended: false,
      description: 'First-generation ALLINONE model. Not released. No weights in this repository.',
      reason: 'ONE-1 is planned and has not been released. See docs/ONE-1-RELEASE-CHECKLIST.md.'
    },
    {
      id: 'one-2',
      displayName: 'One-2',
      family: 'ALLINONE',
      source: 'ALLINONE',
      sizeLabel: '—',
      vramHintMB: null,
      context: null,
      available: false,
      recommended: false,
      description: 'Planned second-generation ALLINONE model.',
      reason: 'Roadmap entry. Not installed in this build.'
    },
    {
      id: 'one-3.x',
      displayName: 'One-3.x',
      family: 'ALLINONE',
      source: 'ALLINONE',
      sizeLabel: '—',
      vramHintMB: null,
      context: null,
      available: false,
      recommended: false,
      description: 'Future model family. Availability depends on a future release.',
      reason: 'Future roadmap entry.'
    },
    {
      id: 'one-4',
      displayName: 'One-4',
      family: 'ALLINONE',
      source: 'ALLINONE',
      sizeLabel: '—',
      vramHintMB: null,
      context: null,
      available: false,
      recommended: false,
      description: 'Future high-capability ALLINONE model.',
      reason: 'Roadmap entry. Not installed in this build.'
    }
  ];

  function list(options) {
    options = options || {};
    var onlyAvailable = options.onlyAvailable === true;
    var result = [];
    for (var i = 0; i < CATALOG.length; i++) {
      var m = CATALOG[i];
      if (onlyAvailable && !m.available) continue;
      result.push(Object.assign({}, m));
    }
    return result;
  }

  function get(id) {
    if (!id) return null;
    for (var i = 0; i < CATALOG.length; i++) {
      if (CATALOG[i].id === id) return Object.assign({}, CATALOG[i]);
    }
    return null;
  }

  function isSelectable(id) {
    var m = get(id);
    return !!(m && m.available);
  }

  ALLINONE.engineCatalog = {
    list: list,
    get: get,
    isSelectable: isSelectable,
    // Expose raw length for tests
    _count: CATALOG.length
  };
})(typeof window !== 'undefined' ? window : global);
