(function (global) {
  'use strict';

  var ALLINONE = (global.ALLINONE = global.ALLINONE || {});
  var config = ALLINONE.config;

  var els = {};
  var toastTimer = null;

  function $(id) { return document.getElementById(id); }

  function cacheDom() {
    els.brandLogo = $('brandLogo');
    els.aiNameLabel = $('aiNameLabel');
    els.chat = $('chat');
    els.chips = $('chips');
    els.composer = $('composer');
    els.input = $('input');
    els.sendBtn = $('sendBtn');
    els.settingsBtn = $('settingsBtn');
    els.assistantPresence = document.querySelector('.assistant-presence');
    els.clayOrb = $('clayOrb');
    els.presenceLabel = $('presenceLabel');
    els.sidebarToggleBtn = $('sidebarToggleBtn');
    els.appShell = $('appShell');
    els.newChatBtn = $('newChatBtn');

    els.setupOverlay = $('setupOverlay');
    els.setupLogo = $('setupLogo');
    els.nameSuggestions = $('nameSuggestions');
    els.setupNameInput = $('setupNameInput');
    els.setupError = $('setupError');
    els.setupStartBtn = $('setupStartBtn');

    els.settingsOverlay = $('settingsOverlay');
    els.settingsClose = $('settingsClose');
    els.settingsNameInput = $('settingsNameInput');
    els.settingsNameSave = $('settingsNameSave');
    els.settingsError = $('settingsError');
    els.settingsPersonality = $('settingsPersonality');
    els.settingsUserName = $('settingsUserName');
    els.settingsSafe = $('settingsSafe');
    els.clearChatBtn = $('clearChatBtn');
    els.resetAppBtn = $('resetAppBtn');

    els.toast = $('toast');
  }

  function logoSVG(size) {
    var s = size || 40;
    var uid = 'clay' + Math.random().toString(36).slice(2, 9);
    return '' +
      '<svg width="' + s + '" height="' + s + '" viewBox="0 0 64 64" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="ALLINONE red clay orb">' +
        '<defs><radialGradient id="' + uid + '" cx="31%" cy="23%" r="83%">' +
          '<stop offset="0%" stop-color="#ffb09a"/><stop offset="22%" stop-color="#e15b4b"/><stop offset="58%" stop-color="#c43a32"/><stop offset="100%" stop-color="#70191f"/>' +
        '</radialGradient></defs>' +
        '<path d="M32 3 C45 2 59 12 60 27 C63 42 51 58 36 61 C21 64 6 53 4 38 C1 23 12 7 26 5 C28 4 30 3 32 3Z" fill="url(#' + uid + ')"/>' +
        '<path d="M14 20 C19 11 29 8 36 10" fill="none" stroke="#ffd4b8" stroke-opacity=".62" stroke-width="3" stroke-linecap="round"/>' +
        '<path d="M15 43 C24 51 39 53 49 43" fill="none" stroke="#64191c" stroke-opacity=".24" stroke-width="2.5" stroke-linecap="round"/>' +
      '</svg>';
  }

  function renderBrandLogo() {
    if (els.brandLogo) els.brandLogo.innerHTML = logoSVG(42);
  }

  function renderSetupLogo() {
    if (els.setupLogo) els.setupLogo.innerHTML = logoSVG(72);
  }

  function escapeHtml(str) {
    return String(str == null ? '' : str).replace(/[&<>"']/g, function (ch) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
    });
  }

  function inlineFormat(str) {
    var t = escapeHtml(str);
    t = t.replace(/`([^`]+)`/g, '<code>$1</code>');
    t = t.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    t = t.replace(/(^|[^*])\*([^*]+)\*/g, '$1<em>$2</em>');
    return t;
  }

  function formatText(raw) {
    var lines = String(raw == null ? '' : raw).split('\n');
    var html = '';
    var inCode = false;
    var codeLines = [];
    var listOpen = false;

    function closeList() {
      if (listOpen) { html += '</ul>'; listOpen = false; }
    }

    for (var i = 0; i < lines.length; i++) {
      var line = lines[i];

      if (/^\s*```/.test(line)) {
        if (inCode) {
          html += '<pre class="code"><code>' + escapeHtml(codeLines.join('\n')) + '</code></pre>';
          codeLines = []; inCode = false;
        } else {
          closeList(); inCode = true; codeLines = [];
        }
        continue;
      }

      if (inCode) { codeLines.push(line); continue; }

      var bullet = line.match(/^\s*[-*\u2022]\s+(.*)$/);
      if (bullet) {
        if (!listOpen) { html += '<ul>'; listOpen = true; }
        html += '<li>' + inlineFormat(bullet[1]) + '</li>';
        continue;
      }

      closeList();

      if (line.trim() === '') { html += '<div class="gap"></div>'; continue; }
      html += '<div>' + inlineFormat(line) + '</div>';
    }

    if (inCode) html += '<pre class="code"><code>' + escapeHtml(codeLines.join('\n')) + '</code></pre>';
    closeList();

    return html;
  }

  function scrollToBottom() {
    if (!els.chat) return;
    try { els.chat.scrollTop = els.chat.scrollHeight; } catch (e) {}
  }

  function formatTime(ts) {
    try {
      var d = ts ? new Date(ts) : new Date();
      return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } catch (e) { return ''; }
  }

  function addMessage(role, text, options) {
    options = options || {};
    if (!els.chat) return null;

    var wrap = document.createElement('div');
    wrap.className = 'msg ' + (role === 'user' ? 'user' : role === 'system' ? 'system' : 'ai');

    if (role === 'ai') {
      var avatar = document.createElement('div');
      avatar.className = 'avatar';
      avatar.innerHTML = logoSVG(30);
      wrap.appendChild(avatar);
    }

    var bubble = document.createElement('div');
    bubble.className = 'bubble';
    bubble.innerHTML = formatText(text);

    if (options.ts && role !== 'system') {
      var time = document.createElement('span');
      time.className = 'time';
      time.textContent = formatTime(options.ts);
      bubble.appendChild(time);
    }

    wrap.appendChild(bubble);
    els.chat.appendChild(wrap);
    if (!options.noScroll) scrollToBottom();
    return wrap;
  }

  function clearChat() {
    if (els.chat) els.chat.innerHTML = '';
    hideTyping();
  }

  function showTyping(mode) {
    hideTyping();
    setPresenceWorking(true, mode);
    if (!els.chat) return;
    var wrap = document.createElement('div');
    wrap.className = 'msg ai typing';
    wrap.id = 'typingIndicator';
    wrap.innerHTML =
      '<div class="avatar">' + logoSVG(30) + '</div>' +
      '<div class="bubble"><span class="dot"></span><span class="dot"></span><span class="dot"></span></div>';
    els.chat.appendChild(wrap);
    scrollToBottom();
  }

  function hideTyping() {
    setPresenceWorking(false);
    var el = $('typingIndicator');
    if (el && el.parentNode) el.parentNode.removeChild(el);
  }

  function modeIcon(mode) {
    var icons = {
      game: '<svg class="orb-mode-icon" viewBox="0 0 64 48" aria-hidden="true"><path d="M17 8h30c8 0 12 7 14 18l2 9c1 6-5 10-10 6L42 31H22L11 41c-5 4-11 0-10-6l2-9C5 15 9 8 17 8Z" fill="#c43a32" stroke="#8e1e20" stroke-width="2.5" stroke-linejoin="round"/><path d="M15 18v12M9 24h12" stroke="#ffe7dc" stroke-width="3.5" stroke-linecap="round"/><circle cx="47" cy="20" r="3" fill="#ffe7dc"/><circle cx="53" cy="26" r="3" fill="#ffe7dc"/><circle cx="43" cy="27" r="2" fill="#ffe7dc"/></svg>',
      image: '<svg class="orb-mode-icon" viewBox="0 0 56 48" aria-hidden="true"><rect x="4" y="4" width="48" height="40" rx="5" fill="#fff2e8" stroke="#b52b2d" stroke-width="3"/><circle cx="38" cy="15" r="5" fill="#ef9b66"/><path d="M8 37 21 23l8 8 6-6 12 12Z" fill="#c43a32"/></svg>',
      video: '<svg class="orb-mode-icon" viewBox="0 0 56 48" aria-hidden="true"><rect x="3" y="5" width="50" height="38" rx="6" fill="#c43a32" stroke="#8e1e20" stroke-width="2"/><path d="m23 15 15 9-15 9Z" fill="#fff0e7"/></svg>',
      website: '<svg class="orb-mode-icon" viewBox="0 0 56 48" aria-hidden="true"><rect x="3" y="4" width="50" height="40" rx="5" fill="#fff2e8" stroke="#b52b2d" stroke-width="3"/><path d="M4 14h48" stroke="#b52b2d" stroke-width="3"/><circle cx="10" cy="9" r="1.5" fill="#c43a32"/><circle cx="16" cy="9" r="1.5" fill="#c43a32"/><path d="M12 22h19M12 28h29M12 34h22" stroke="#c43a32" stroke-width="2.5" stroke-linecap="round"/></svg>',
      search: '<svg class="orb-mode-icon" viewBox="0 0 48 48" aria-hidden="true"><circle cx="20" cy="20" r="13" fill="#fff2e8" stroke="#c43a32" stroke-width="5"/><path d="m30 30 12 12" stroke="#c43a32" stroke-width="6" stroke-linecap="round"/></svg>',
      think: '<svg class="orb-mode-icon" viewBox="0 0 48 48" aria-hidden="true"><path d="M24 4a16 16 0 0 0-9 29v6h18v-6A16 16 0 0 0 24 4Z" fill="#ffe5a6" stroke="#b52b2d" stroke-width="3"/><path d="M17 44h14M18 39h12" stroke="#b52b2d" stroke-width="3" stroke-linecap="round"/><path d="M24 12v9m-4-5 4 5 4-5" stroke="#b52b2d" stroke-width="2.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/></svg>'
    };
    return icons[mode] || '';
  }

  function setPresenceWorking(working, mode) {
    var modes = ['is-writing-code', 'is-making-game', 'is-making-image', 'is-making-video', 'is-building-website', 'is-searching', 'is-thinking'];
    var modeClasses = { code: 'is-writing-code', game: 'is-making-game', image: 'is-making-image', video: 'is-making-video', website: 'is-building-website', search: 'is-searching', think: 'is-thinking' };
    if (els.assistantPresence) {
      els.assistantPresence.classList.toggle('is-working', !!working);
      modes.forEach(function (name) { els.assistantPresence.classList.remove(name); });
      if (working && modeClasses[mode]) els.assistantPresence.classList.add(modeClasses[mode]);
    }
    if (els.clayOrb && mode !== 'code') {
      var highlight = '<span class="orb-highlight"></span><span class="writing-caret"></span>';
      els.clayOrb.innerHTML = working && modeIcon(mode) ? modeIcon(mode) : highlight;
    } else if (els.clayOrb && !working) {
      els.clayOrb.innerHTML = '<span class="orb-highlight"></span><span class="writing-caret"></span>';
    }
    if (els.presenceLabel) {
      var labels = { code: 'Writing code…', game: 'Making a game…', image: 'Creating an image…', video: 'Making a video…', website: 'Building a website…', search: 'Searching…', think: 'Thinking it through…' };
      els.presenceLabel.textContent = !working ? 'Ready to shape an idea' : (labels[mode] || 'Taking shape…');
    }
  }

  function setAssistantName(name) {
    if (els.aiNameLabel) els.aiNameLabel.textContent = name;
    try { document.title = name + ' · ' + config.APP_NAME; } catch (e) {}
  }

  function renderChips(chips, onPick) {
    if (!els.chips) return;
    els.chips.innerHTML = '';
    chips.forEach(function (text) {
      var btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'chip';
      btn.textContent = text;
      btn.addEventListener('click', function () { onPick(text); });
      els.chips.appendChild(btn);
    });
  }

  function renderNameSuggestions(names, onPick) {
    if (!els.nameSuggestions) return;
    els.nameSuggestions.innerHTML = '';
    names.forEach(function (name) {
      var btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'suggestion';
      btn.textContent = name;
      btn.addEventListener('click', function () {
        onPick(name);
        highlightSuggestion(name);
      });
      els.nameSuggestions.appendChild(btn);
    });
  }

  function highlightSuggestion(name) {
    if (!els.nameSuggestions) return;
    Array.prototype.forEach.call(els.nameSuggestions.children, function (child) {
      child.classList.toggle('selected', child.textContent === name);
    });
  }

  function openSetup(initialName) {
    if (!els.setupOverlay) return;
    els.setupOverlay.hidden = false;
    if (els.setupNameInput) els.setupNameInput.value = initialName || '';
    hideSetupError();
    setTimeout(function () {
      try { if (els.setupNameInput) els.setupNameInput.focus(); } catch (e) {}
    }, 60);
  }

  function closeSetup() {
    if (els.setupOverlay) els.setupOverlay.hidden = true;
  }

  function getSetupName() {
    return els.setupNameInput ? els.setupNameInput.value : '';
  }

  function showSetupError(msg) {
    if (!els.setupError) return;
    els.setupError.textContent = msg;
    els.setupError.hidden = false;
  }

  function hideSetupError() {
    if (els.setupError) els.setupError.hidden = true;
  }

  function buildPersonalityOptions(personalities, selectedId) {
    if (!els.settingsPersonality) return;
    els.settingsPersonality.innerHTML = '';
    personalities.forEach(function (p) {
      var opt = document.createElement('option');
      opt.value = p.id;
      opt.textContent = p.label + ' — ' + p.hint;
      els.settingsPersonality.appendChild(opt);
    });
    els.settingsPersonality.value = selectedId;
  }

  function syncSettings(state) {
    if (els.settingsNameInput) els.settingsNameInput.value = state.assistantName;
    if (els.settingsUserName) els.settingsUserName.value = state.userName || '';
    if (els.settingsPersonality) els.settingsPersonality.value = state.personality;
    if (els.settingsSafe) els.settingsSafe.checked = !!state.safeMode;
    hideSettingsError();
  }

  function openSettings(state) {
    if (!els.settingsOverlay) return;
    syncSettings(state);
    els.settingsOverlay.hidden = false;
  }

  function closeSettings() {
    if (els.settingsOverlay) els.settingsOverlay.hidden = true;
  }

  function showSettingsError(msg) {
    if (!els.settingsError) return;
    els.settingsError.textContent = msg;
    els.settingsError.hidden = false;
  }

  function hideSettingsError() {
    if (els.settingsError) els.settingsError.hidden = true;
  }

  function getMessageInput() { return els.input ? els.input.value : ''; }
  function clearMessageInput() { if (els.input) els.input.value = ''; }
  function focusMessageInput() { try { if (els.input) els.input.focus(); } catch (e) {} }

  function toast(message, ms) {
    if (!els.toast) return;
    els.toast.textContent = message;
    els.toast.hidden = false;
    if (toastTimer) clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { els.toast.hidden = true; }, ms || 2200);
  }

  ALLINONE.ui = {
    init: function () {
      cacheDom();
      renderBrandLogo();
      renderSetupLogo();
    },

    els: els,
    logoSVG: logoSVG,
    formatText: formatText,
    escapeHtml: escapeHtml,

    addMessage: addMessage,
    clearChat: clearChat,
    showTyping: showTyping,
    hideTyping: hideTyping,
    scrollToBottom: scrollToBottom,

    setAssistantName: setAssistantName,
    renderChips: renderChips,
    renderNameSuggestions: renderNameSuggestions,
    highlightSuggestion: highlightSuggestion,

    openSetup: openSetup,
    closeSetup: closeSetup,
    getSetupName: getSetupName,
    showSetupError: showSetupError,
    hideSetupError: hideSetupError,

    buildPersonalityOptions: buildPersonalityOptions,
    syncSettings: syncSettings,
    openSettings: openSettings,
    closeSettings: closeSettings,
    showSettingsError: showSettingsError,
    hideSettingsError: hideSettingsError,

    getMessageInput: getMessageInput,
    clearMessageInput: clearMessageInput,
    focusMessageInput: focusMessageInput,

    toast: toast,
    setPresenceWorking: setPresenceWorking
  };
})(window);
