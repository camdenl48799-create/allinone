(function (global) {
  'use strict';

  var ALLINONE = global.ALLINONE;
  var config = ALLINONE.config;
  var storage = ALLINONE.storage;
  var safety = ALLINONE.safety;
  var brain = ALLINONE.brain;
  var ui = ALLINONE.ui;

  var state = {
    assistantName: config.DEFAULT_ASSISTANT_NAME,
    userName: '',
    personality: config.DEFAULT_PERSONALITY,
    safeMode: true,
    setupComplete: false,
    history: []
  };

  var busy = false;

  function validPersonality(id) {
    for (var i = 0; i < config.PERSONALITIES.length; i++) {
      if (config.PERSONALITIES[i].id === id) return true;
    }
    return false;
  }

  function loadState() {
    var saved = storage.load(config.STORAGE_KEY, null);
    if (!saved || typeof saved !== 'object') return;

    if (typeof saved.assistantName === 'string') {
      var nameCheck = safety.checkName(saved.assistantName);
      if (nameCheck.ok) state.assistantName = nameCheck.value;
    }

    if (typeof saved.userName === 'string') {
      state.userName = saved.userName.replace(/\s+/g, ' ').trim().slice(0, 24);
    }

    if (validPersonality(saved.personality)) {
      state.personality = saved.personality;
    }

    if (saved.safeMode === false) state.safeMode = false;
    if (saved.setupComplete === true) state.setupComplete = true;

    if (Object.prototype.toString.call(saved.history) === '[object Array]') {
      var cleaned = [];
      for (var i = 0; i < saved.history.length; i++) {
        var m = saved.history[i];
        if (!m || typeof m.text !== 'string') continue;
        if (m.role !== 'user' && m.role !== 'ai') continue;
        cleaned.push({
          role: m.role,
          text: String(m.text),
          ts: typeof m.ts === 'number' ? m.ts : Date.now()
        });
      }
      state.history = cleaned.slice(-config.MAX_HISTORY);
    }

    brain.setUserName(state.userName);
  }

  function saveState() {
    storage.save(config.STORAGE_KEY, {
      assistantName: state.assistantName,
      userName: state.userName,
      personality: state.personality,
      safeMode: state.safeMode,
      setupComplete: state.setupComplete,
      history: state.history.slice(-config.MAX_HISTORY)
    });
  }

  function pushHistory(role, text) {
    state.history.push({ role: role, text: text, ts: Date.now() });
    if (state.history.length > config.MAX_HISTORY) {
      state.history = state.history.slice(-config.MAX_HISTORY);
    }
    saveState();
  }

  function applyAction(action) {
    if (!action || !action.type) return;

    if (action.type === 'rename') {
      var check = safety.checkName(action.value);
      if (check.ok) {
        state.assistantName = check.value;
        saveState();
        ui.setAssistantName(state.assistantName);
        ui.syncSettings(state);
      }
    }

    if (action.type === 'setUserName') {
      state.userName = String(action.value).slice(0, 24);
      brain.setUserName(state.userName);
      saveState();
      ui.syncSettings(state);
    }
  }

  function sendMessage(rawText) {
    var text = String(rawText == null ? '' : rawText).trim();
    if (!text || busy) return;

    if (state.safeMode) {
      var check = safety.checkMessage(text);
      if (!check.ok) {
        ui.addMessage('user', text, { ts: Date.now() });
        ui.addMessage('system', '\u26A0\uFE0F ' + check.reason);
        ui.clearMessageInput();
        return;
      }
    }

    ui.addMessage('user', text, { ts: Date.now() });
    pushHistory('user', text);
    ui.clearMessageInput();
    busy = true;

    ui.showTyping();

    var delay = 320 + Math.min(900, text.length * 14);

    setTimeout(function () {
      ui.hideTyping();

      var result;
      try {
        result = brain.respond(text, {
          assistantName: state.assistantName,
          personality: state.personality,
          userName: state.userName
        });
      } catch (err) {
        if (global.console) console.error('[ALLINONE] Brain error:', err);
        result = { text: 'Something went wrong on my end. Try that again?' };
      }

      applyAction(result.action);

      ui.addMessage('ai', result.text, { ts: Date.now() });
      pushHistory('ai', result.text);
      busy = false;
    }, delay);
  }

  function openSetup() {
    ui.openSetup(state.assistantName || config.DEFAULT_ASSISTANT_NAME);
    ui.renderNameSuggestions(config.SUGGESTED_NAMES, function (name) {
      var input = ui.els.setupNameInput;
      if (input) input.value = name;
    });
    ui.highlightSuggestion(state.assistantName);
  }

  function confirmSetup() {
    var check = safety.checkName(ui.getSetupName());
    if (!check.ok) {
      ui.showSetupError(check.reason);
      return;
    }

    state.assistantName = check.value;
    state.setupComplete = true;
    saveState();

    ui.setAssistantName(state.assistantName);
    ui.closeSetup();

    ui.clearChat();
    state.history = [];
    saveState();
    greet();
    ui.focusMessageInput();
  }

  function greet() {
    var text =
      'Hi! I\'m **' + state.assistantName + '**, your ' + config.APP_NAME + ' assistant.\n\n' +
      'I run entirely on your device — no accounts, no internet, no API keys. ' +
      'I\'m a small local rule-based assistant, so my knowledge is limited and I\'ll be honest about that.\n\n' +
      'Ask me anything, or tap one of the suggestions below.';

    ui.addMessage('ai', text, { ts: Date.now() });
    pushHistory('ai', text);
  }

  function restoreHistory() {
    ui.clearChat();

    if (state.history.length) {
      for (var i = 0; i < state.history.length; i++) {
        var msg = state.history[i];
        ui.addMessage(msg.role, msg.text, { noScroll: true, ts: msg.ts });
      }
      ui.scrollToBottom();
    } else {
      greet();
    }
  }

  function saveNameFromSettings() {
    var check = safety.checkName(ui.els.settingsNameInput.value);
    if (!check.ok) {
      ui.showSettingsError(check.reason);
      return;
    }

    var changed = check.value !== state.assistantName;
    state.assistantName = check.value;
    saveState();
    ui.setAssistantName(state.assistantName);
    ui.hideSettingsError();
    ui.toast('Name updated');

    if (changed) {
      ui.addMessage('system', 'You can call me **' + state.assistantName + '** now.');
    }
  }

  function clearChat() {
    state.history = [];
    saveState();
    ui.clearChat();
    ui.closeSettings();
    greet();
    ui.toast('Chat cleared');
  }

  function resetApp() {
    var ok = false;
    try {
      ok = global.confirm('Reset ALLINONE? This clears your assistant name, settings, and chat history.');
    } catch (e) {
      ok = true;
    }
    if (!ok) return;

    storage.clearAll();
    try { global.location.reload(); }
    catch (e) { /* ignore */ }
  }

  function bindEvents() {
    if (!ui.els || !ui.els.composer) {
      if (global.console) console.warn('[ALLINONE] Composer not found — UI events not bound.');
      return;
    }

    ui.els.composer.addEventListener('submit', function (e) {
      e.preventDefault();
      sendMessage(ui.getMessageInput());
    });

    if (ui.els.settingsBtn) {
      ui.els.settingsBtn.addEventListener('click', function () {
        ui.openSettings(state);
      });
    }
    if (ui.els.settingsClose) {
      ui.els.settingsClose.addEventListener('click', function () { ui.closeSettings(); });
    }
    if (ui.els.settingsOverlay) {
      ui.els.settingsOverlay.addEventListener('click', function (e) {
        if (e.target === ui.els.settingsOverlay) ui.closeSettings();
      });
    }

    if (ui.els.setupNameInput) {
      ui.els.setupNameInput.addEventListener('input', function () {
        ui.hideSetupError();
        ui.highlightSuggestion(ui.els.setupNameInput.value.trim());
      });
      ui.els.setupNameInput.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); confirmSetup(); }
      });
    }
    if (ui.els.setupStartBtn) {
      ui.els.setupStartBtn.addEventListener('click', confirmSetup);
    }

    if (ui.els.settingsNameSave) {
      ui.els.settingsNameSave.addEventListener('click', saveNameFromSettings);
    }
    if (ui.els.settingsNameInput) {
      ui.els.settingsNameInput.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); saveNameFromSettings(); }
      });
    }

    if (ui.els.settingsPersonality) {
      ui.els.settingsPersonality.addEventListener('change', function () {
        var value = ui.els.settingsPersonality.value;
        if (!validPersonality(value)) return;
        state.personality = value;
        saveState();
        ui.toast('Personality updated');
      });
    }

    if (ui.els.settingsUserName) {
      ui.els.settingsUserName.addEventListener('change', function () {
        var name = ui.els.settingsUserName.value.replace(/\s+/g, ' ').trim().slice(0, 24);
        state.userName = name;
        brain.setUserName(name);
        saveState();
        ui.els.settingsUserName.value = name;
      });
    }

    if (ui.els.settingsSafe) {
      ui.els.settingsSafe.addEventListener('change', function () {
        state.safeMode = ui.els.settingsSafe.checked;
        saveState();
        ui.toast(state.safeMode ? 'Safe mode on' : 'Safe mode off');
      });
    }

    if (ui.els.clearChatBtn) ui.els.clearChatBtn.addEventListener('click', clearChat);
    if (ui.els.resetAppBtn)  ui.els.resetAppBtn.addEventListener('click', resetApp);

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && ui.els.settingsOverlay && !ui.els.settingsOverlay.hidden) {
        ui.closeSettings();
      }
    });
  }

  function init() {
    try { ui.init(); }
    catch (err) {
      if (global.console) console.error('[ALLINONE] UI init failed:', err);
    }

    try { loadState(); }
    catch (err) {
      if (global.console) console.error('[ALLINONE] Failed to load saved state — using defaults.', err);
    }

    ui.setAssistantName(state.assistantName);
    ui.renderChips(config.QUICK_CHIPS, sendMessage);
    ui.buildPersonalityOptions(config.PERSONALITIES, state.personality);
    ui.syncSettings(state);
    bindEvents();

    if (state.setupComplete) {
      ui.closeSetup();
      restoreHistory();
    } else {
      openSetup();
    }

    ALLINONE.app = {
      state: state,
      send: sendMessage,
      setAssistantName: function (name) {
        var check = safety.checkName(name);
        if (!check.ok) return false;
        state.assistantName = check.value;
        saveState();
        ui.setAssistantName(state.assistantName);
        ui.syncSettings(state);
        return true;
      },
      addSystemMessage: function (text) { ui.addMessage('system', text); },
      save: saveState
    };
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})(window);
