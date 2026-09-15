(function (global) {
  'use strict';

  var ALLINONE = (global.ALLINONE = global.ALLINONE || {});

  ALLINONE.config = {
    APP_NAME: 'ALLINONE',
    APP_TAGLINE: 'Local-first assistant',
    DEFAULT_ASSISTANT_NAME: 'Nova',

    MIN_NAME_LENGTH: 2,
    MAX_NAME_LENGTH: 18,

    STORAGE_KEY: 'allinone.state.v1',
    MAX_HISTORY: 200,

    SUGGESTED_NAMES: [
      'Nova', 'Ace', 'Pixel', 'Orion', 'Luna', 'Byte',
      'Ember', 'Sage', 'Zephyr', 'Comet', 'Rune', 'Echo'
    ],

    PERSONALITIES: [
      { id: 'friendly',     label: 'Friendly',     hint: 'Warm and encouraging' },
      { id: 'helpful',      label: 'Helpful',      hint: 'Thorough and structured' },
      { id: 'professional', label: 'Professional', hint: 'Neutral and formal' },
      { id: 'chill',        label: 'Chill',        hint: 'Relaxed and casual' },
      { id: 'energetic',    label: 'Energetic',    hint: 'Upbeat and enthusiastic' }
    ],

    DEFAULT_PERSONALITY: 'friendly',

    QUICK_CHIPS: [
      'What can you do?',
      'Tell me a joke',
      'What is (12 * 4) + 7?',
      'What time is it?',
      'Roblox Studio tips',
      'I need coding help',
      'Give me a game idea'
    ],

    RESERVED_NAMES: [
      'allinone', 'admin', 'administrator', 'system', 'moderator',
      'null', 'undefined', 'root', 'guest', 'ai', 'assistant'
    ],

    NAME_BLOCKLIST: [
      'fuck', 'fucking', 'fucker', 'shit', 'bullshit', 'bitch', 'bastard',
      'asshole', 'dick', 'dickhead', 'cunt', 'slut', 'whore', 'piss',
      'prick', 'wanker', 'sex', 'sexy', 'porn', 'porno', 'nsfw', 'nude',
      'nudes', 'naked', 'boob', 'boobs', 'tits', 'penis', 'vagina',
      'horny', 'orgasm', 'rape', 'rapist', 'nazi', 'hitler', 'kkk',
      'slur', 'retard', 'faggot', 'nigger', 'nigga', 'cocaine', 'heroin',
      'meth', 'kill', 'killer', 'murder', 'suicide', 'damn', 'crap',
      'hate', 'idiot', 'stupid'
    ],

    MESSAGE_BLOCKLIST: [
      'fuck', 'fucking', 'fucker', 'motherfucker', 'shit', 'bullshit',
      'bitch', 'bastard', 'asshole', 'dickhead', 'cunt', 'slut', 'whore',
      'porn', 'porno', 'nsfw', 'horny',
      'nazi', 'hitler', 'kkk', 'retard', 'faggot', 'nigger', 'nigga',
      'cocaine', 'heroin', 'meth'
    ],

    CRISIS_PHRASES: [
      'kill myself', 'killing myself', 'kill yourself', 'kys', 'kill urself',
      'i want to die', 'i wanna die', 'end my life', 'hurt myself',
      'self harm', 'self-harm', 'suicide', 'suicidal', 'go die'
    ]
  };

  ALLINONE.modules = {
    registered: [],

    register: function (mod) {
      if (!mod || typeof mod.id !== 'string' || !mod.id) {
        if (global.console) console.warn('[ALLINONE] Ignored module without an id.');
        return false;
      }
      if (this.registered.some(function (m) { return m.id === mod.id; })) {
        return false;
      }
      this.registered.push(mod);
      try {
        if (typeof mod.init === 'function') mod.init(ALLINONE);
      } catch (err) {
        if (global.console) console.error('[ALLINONE] Module failed to init:', mod.id, err);
      }
      return true;
    },

    get: function (id) {
      for (var i = 0; i < this.registered.length; i++) {
        if (this.registered[i].id === id) return this.registered[i];
      }
      return null;
    },

    list: function () { return this.registered.slice(); }
  };
})(window);
