(function (global) {
  'use strict';

  var ALLINONE = (global.ALLINONE = global.ALLINONE || {});
  var config = ALLINONE.config;

  function escapeRegExp(str) {
    return String(str).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  function normalize(str) {
    return String(str == null ? '' : str)
      .toLowerCase()
      .normalize('NFKD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/0/g, 'o')
      .replace(/[1!|]/g, 'i')
      .replace(/3/g, 'e')
      .replace(/4@/g, 'a')
      .replace(/5\$/g, 's')
      .replace(/7/g, 't')
      .replace(/[^a-z\s]/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();
  }

  function containsWord(normalizedText, word) {
    var w = normalize(word);
    if (!w) return false;
    var re = new RegExp('(^|\\s)' + escapeRegExp(w) + '(\\s|$)');
    return re.test(normalizedText);
  }

  function findBlockedWord(text, list) {
    if (!Array.isArray(list) || !list.length) return null;
    var norm = ' ' + normalize(text) + ' ';
    for (var i = 0; i < list.length; i++) {
      var w = normalize(list[i]);
      if (!w) continue;
      if (new RegExp('(^|\\s)' + escapeRegExp(w) + '(\\s|$)').test(norm)) {
        return list[i];
      }
    }
    return null;
  }

  function findBlockedPhrase(text, phrases) {
    if (!Array.isArray(phrases) || !phrases.length) return null;
    var norm = ' ' + normalize(text) + ' ';
    for (var i = 0; i < phrases.length; i++) {
      var p = normalize(phrases[i]);
      if (!p) continue;
      if (norm.indexOf(' ' + p + ' ') !== -1) return phrases[i];
    }
    return null;
  }

  var ALLOWED_NAME_CHARS = /^[A-Za-z0-9 _'\-]+$/;

  ALLINONE.safety = {
    checkName: function (rawName) {
      var name = String(rawName == null ? '' : rawName)
        .replace(/\s+/g, ' ')
        .trim();

      if (!name) {
        return { ok: false, reason: 'The name cannot be empty.' };
      }
      if (name.length < config.MIN_NAME_LENGTH) {
        return {
          ok: false,
          reason: 'Names need at least ' + config.MIN_NAME_LENGTH + ' characters.'
        };
      }
      if (name.length > config.MAX_NAME_LENGTH) {
        return {
          ok: false,
          reason: 'Names can be at most ' + config.MAX_NAME_LENGTH + ' characters.'
        };
      }
      if (!ALLOWED_NAME_CHARS.test(name)) {
        return {
          ok: false,
          reason: 'Use only letters, numbers, spaces, hyphens and apostrophes.'
        };
      }
      if (!/[A-Za-z]/.test(name)) {
        return {
          ok: false,
          reason: 'The name must contain at least one letter.'
        };
      }
      if (config.RESERVED_NAMES.indexOf(name.toLowerCase()) !== -1) {
        return {
          ok: false,
          reason: '"' + name + '" is a reserved system name.'
        };
      }
      if (findBlockedWord(name, config.NAME_BLOCKLIST)) {
        return {
          ok: false,
          reason: 'That name is not 13+ friendly. Please pick something else.'
        };
      }
      return { ok: true, value: name };
    },

    checkMessage: function (rawText) {
      var text = String(rawText == null ? '' : rawText).trim();

      if (!text) {
        return { ok: false, value: '', reason: 'Message is empty.' };
      }
      if (text.length > 1200) {
        return {
          ok: false,
          value: text,
          reason: 'That message is a bit too long. Try shortening it.'
        };
      }

      if (findBlockedPhrase(text, config.CRISIS_PHRASES)) {
        return {
          ok: false,
          value: text,
          crisis: true,
          reason:
            'That sounds like it might be about self-harm. If you are ' +
            'struggling, please talk to a trusted adult, or contact a ' +
            'local crisis line — you deserve support.'
        };
      }

      if (findBlockedWord(text, config.MESSAGE_BLOCKLIST)) {
        return {
          ok: false,
          value: text,
          reason:
            'Let\'s keep things 13+ friendly. Try rephrasing that without ' +
            'the strong language.'
        };
      }

      return { ok: true, value: text };
    },

    isCrisis: function (text) {
      return !!findBlockedPhrase(text, config.CRISIS_PHRASES);
    },

    normalize: normalize,
    containsWord: containsWord
  };
})(window);
