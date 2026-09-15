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
    var uid = 'lg' + Math.random().toString(36).slice(2, 9);

    return '' +
      '<svg width="' + s + '" height="' + s + '" viewBox="0 0 64 64" ' +
      'xmlns="http://www.w3.org/2000/svg" role="img" aria-label="ALLINONE logo">' +
        '<defs>' +
          '<radialGradient id="' + uid + '" cx="35%" cy="27%" r="82%">' +
            '<stop offset="0%" stop-color="#ff5b66"/>' +
            '<stop offset="55%" stop-color="#e01e2b"/>' +
            '<stop offset="100%" stop-color="#96060f"/>' +
          '</radialGradient>' +
        '</defs>' +
        '<circle cx="32" cy="32" r="30" fill="url(#' + uid + ')"/>' +
        '<circle cx="32" cy="32" r="30" fill="none" stroke="rgba(255,255,255,0.28)" stroke-width="1.5"/>' +
        '<circle cx="32" cy="32" r="16" fill="#ffffff"/>' +
        '<circle cx="26.5" cy="26" r="4.5" fill="#ffe6e8" opacity="0.95"/>' +
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

  function showTyping() {
    if (!els.chat) return;
    hideTyping();
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
    var el = $('typingIndicator');
    if (el && el.parentNode) el.parentNode.removeChild(el);
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

    toast: toast
  };
})(window);
