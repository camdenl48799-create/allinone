(function (global) {
  'use strict';

  var ALLINONE = (global.ALLINONE = global.ALLINONE || {});
  var config = ALLINONE.config;

  var memoryStore = {};
  var available = true;

  try {
    var probeKey = '__allinone_probe__';
    global.localStorage.setItem(probeKey, '1');
    global.localStorage.removeItem(probeKey);
  } catch (err) {
    available = false;
    if (global.console) {
      console.warn('[ALLINONE] localStorage unavailable — falling back to in-memory storage.');
    }
  }

  var PREFIX = (config && config.STORAGE_KEY
    ? config.STORAGE_KEY.split('.').slice(0, 1).join('.')
    : 'allinone') + '.';

  function rawGet(key) {
    return available ? global.localStorage.getItem(key) : memoryStore[key];
  }

  function rawSet(key, value) {
    if (available) global.localStorage.setItem(key, value);
    else memoryStore[key] = value;
  }

  function rawRemove(key) {
    if (available) global.localStorage.removeItem(key);
    else delete memoryStore[key];
  }

  ALLINONE.storage = {
    available: available,

    load: function (key, fallback) {
      try {
        var raw = rawGet(key);
        if (raw === null || raw === undefined || raw === '') return fallback;
        var parsed = JSON.parse(raw);
        return parsed === null || parsed === undefined ? fallback : parsed;
      } catch (err) {
        if (global.console) {
          console.warn('[ALLINONE] Could not read storage key "' + key + '" — using defaults.', err);
        }
        return fallback;
      }
    },

    save: function (key, value) {
      try {
        var raw = JSON.stringify(value);
        rawSet(key, raw);
        return true;
      } catch (err) {
        if (global.console) {
          console.warn('[ALLINONE] Could not write storage key "' + key + '".', err);
        }
        return false;
      }
    },

    remove: function (key) {
      try { rawRemove(key); }
      catch (err) { /* ignore */ }
    },

    clearAll: function () {
      try {
        if (available) {
          var doomed = [];
          for (var i = 0; i < global.localStorage.length; i++) {
            var k = global.localStorage.key(i);
            if (k && k.indexOf(PREFIX) === 0) doomed.push(k);
          }
          for (var j = 0; j < doomed.length; j++) {
            global.localStorage.removeItem(doomed[j]);
          }
        } else {
          memoryStore = {};
        }
      } catch (err) { /* ignore */ }
    }
  };
})(window);
