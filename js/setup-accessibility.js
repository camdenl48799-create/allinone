/*
 * ALLINONE — setup and dialog accessibility enhancements
 *
 * No dependencies. No CSS. No network requests.
 *
 * Uses existing elements:
 *   .app
 *   #setupOverlay
 *   #setupNameInput
 *   #setupError
 *   #nameSuggestions
 *   #settingsOverlay
 *   #settingsNameInput
 *   #settingsError
 *
 * Existing application code remains responsible for:
 *   - Opening and closing dialogs using the hidden attribute
 *   - Validating and saving assistant names
 *   - Completing setup
 *   - Sending messages
 *   - Authentication and billing
 */

(function () {
  'use strict';

  var INSTALL_KEY = '__allinoneSetupAccessibilityInstalled';

  function init() {
    if (window[INSTALL_KEY]) return;

    var workspace = document.querySelector('.app');

    var setupOverlay = document.getElementById('setupOverlay');
    var settingsOverlay = document.getElementById('settingsOverlay');

    var overlays = [setupOverlay, settingsOverlay].filter(function (element) {
      return Boolean(element);
    });

    if (!overlays.length) return;

    /*
     * Safety check: never make a dialog inert by making its own
     * ancestor inert. Expected structure: overlays outside .app.
     */
    var canIsolateWorkspace = workspace && overlays.every(function (overlay) {
      return !workspace.contains(overlay);
    });

    window[INSTALL_KEY] = true;

    var activeOverlay = null;
    var previousFocus = null;
    var workspaceWasInert = false;
    var redirectingFocus = false;

    var focusableSelector = [
      'a[href]',
      'button:not([disabled])',
      'input:not([disabled]):not([type="hidden"])',
      'select:not([disabled])',
      'textarea:not([disabled])',
      '[tabindex]:not([tabindex="-1"])'
    ].join(',');

    function isVisible(element) {
      if (!element || !element.isConnected) return false;
      if (element.closest('[hidden], [inert]')) return false;

      var styles = window.getComputedStyle(element);

      return styles.visibility !== 'hidden' &&
        styles.display !== 'none' &&
        element.getClientRects().length > 0;
    }

    function getFocusable(overlay) {
      return Array.prototype.filter.call(
        overlay.querySelectorAll(focusableSelector),
        function (element) {
          return element.tabIndex >= 0 &&
            !element.disabled &&
            isVisible(element);
        }
      );
    }

    function getDialog(overlay) {
      return overlay.querySelector('[role="dialog"]') || overlay;
    }

    function focusElement(element) {
      if (!element || typeof element.focus !== 'function') return;

      try {
        element.focus({ preventScroll: true });
      } catch (error) {
        element.focus();
      }
    }

    function focusInside(overlay) {
      if (!overlay || !isVisible(overlay)) return;

      var preferred = overlay === setupOverlay
        ? document.getElementById('setupNameInput')
        : document.getElementById('settingsClose');

      if (
        preferred &&
        overlay.contains(preferred) &&
        !preferred.disabled &&
        isVisible(preferred)
      ) {
        focusElement(preferred);
        return;
      }

      var controls = getFocusable(overlay);

      focusElement(controls[0] || getDialog(overlay));
    }

    function restoreFocus(closedOverlay, target) {
      /*
       * Give the application's own close/completion handler priority.
       * Never overwrite focus that it deliberately moved elsewhere.
       */
      window.requestAnimationFrame(function () {
        if (activeOverlay) return;

        var current = document.activeElement;

        if (
          current &&
          current !== document.body &&
          !closedOverlay.contains(current) &&
          isVisible(current)
        ) {
          return;
        }

        /*
         * Setup completion already owns composer focus.
         * Restore only Settings' previous focus target.
         */
        if (
          closedOverlay === settingsOverlay &&
          target &&
          !target.disabled &&
          isVisible(target)
        ) {
          focusElement(target);
        }
      });
    }

    function closeActiveDialog() {
      if (!activeOverlay) return;

      var closed = activeOverlay;
      var target = previousFocus;

      activeOverlay = null;
      previousFocus = null;

      /*
       * Restore the workspace to the exact inert state it had before
       * this dialog opened; don't remove inert if another feature set it.
       */
      if (canIsolateWorkspace && !workspaceWasInert) {
        workspace.removeAttribute('inert');
      }
      workspaceWasInert = false;

      restoreFocus(closed, target);
    }

    function activateDialog(overlay) {
      if (activeOverlay === overlay) return;

      if (activeOverlay) closeActiveDialog();

      previousFocus = document.activeElement;
      activeOverlay = overlay;

      if (canIsolateWorkspace) {
        workspaceWasInert = workspace.hasAttribute('inert');
        workspace.setAttribute('inert', '');
      }

      /*
       * Respect focus already placed inside by existing app code.
       */
      if (!overlay.contains(document.activeElement)) {
        focusInside(overlay);
      }
    }

    function syncDialogs() {
      var visibleOverlay = null;

      overlays.forEach(function (overlay) {
        if (isVisible(overlay)) visibleOverlay = overlay;
      });

      if (visibleOverlay) {
        activateDialog(visibleOverlay);
      } else {
        closeActiveDialog();
      }
    }

    overlays.forEach(function (overlay) {
      var dialog = getDialog(overlay);

      if (!dialog.hasAttribute('tabindex')) {
        dialog.setAttribute('tabindex', '-1');
      }

      var observer = new MutationObserver(syncDialogs);

      observer.observe(overlay, {
        attributes: true,
        attributeFilter: ['hidden']
      });
    });

    document.addEventListener('keydown', function (event) {
      if (
        event.defaultPrevented ||
        event.key !== 'Tab' ||
        !activeOverlay ||
        !isVisible(activeOverlay)
      ) {
        return;
      }

      var controls = getFocusable(activeOverlay);

      if (!controls.length) {
        event.preventDefault();
        focusElement(getDialog(activeOverlay));
        return;
      }

      var first = controls[0];
      var last = controls[controls.length - 1];
      var current = document.activeElement;

      if (!activeOverlay.contains(current)) {
        event.preventDefault();
        focusElement(event.shiftKey ? last : first);
      } else if (event.shiftKey && current === first) {
        event.preventDefault();
        focusElement(last);
      } else if (!event.shiftKey && current === last) {
        event.preventDefault();
        focusElement(first);
      }
    });

    document.addEventListener('focusin', function (event) {
      if (
        redirectingFocus ||
        !activeOverlay ||
        !isVisible(activeOverlay) ||
        activeOverlay.contains(event.target)
      ) {
        return;
      }

      redirectingFocus = true;

      try {
        focusInside(activeOverlay);
      } finally {
        redirectingFocus = false;
      }
    });

    function connectValidation(inputId, errorId) {
      var input = document.getElementById(inputId);
      var error = document.getElementById(errorId);

      if (!input || !error) return;

      var descriptions = (
        input.getAttribute('aria-describedby') || ''
      ).split(/\s+/).filter(Boolean);

      if (descriptions.indexOf(errorId) === -1) {
        descriptions.push(errorId);
      }

      input.setAttribute(
        'aria-describedby',
        descriptions.join(' ')
      );

      function syncValidation() {
        var hasError = !error.hidden &&
          Boolean(error.textContent.trim());

        input.setAttribute(
          'aria-invalid',
          hasError ? 'true' : 'false'
        );
      }

      var observer = new MutationObserver(syncValidation);

      observer.observe(error, {
        attributes: true,
        attributeFilter: ['hidden'],
        childList: true,
        characterData: true,
        subtree: true
      });

      syncValidation();
    }

    connectValidation('setupNameInput', 'setupError');
    connectValidation('settingsNameInput', 'settingsError');

    var suggestions = document.getElementById('nameSuggestions');

    if (suggestions) {
      function syncSuggestions() {
        Array.prototype.forEach.call(
          suggestions.querySelectorAll('button.suggestion'),
          function (button) {
            button.setAttribute(
              'aria-pressed',
              button.classList.contains('selected')
                ? 'true'
                : 'false'
            );
          }
        );
      }

      var suggestionObserver = new MutationObserver(syncSuggestions);

      suggestionObserver.observe(suggestions, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ['class']
      });

      syncSuggestions();
    }

    /*
     * Prevent Enter used for input-method composition from reaching
     * an existing setup Enter-to-submit handler.
     *
     * Normal Enter presses are untouched.
     */
    var setupName = document.getElementById('setupNameInput');

    if (setupName) {
      setupName.addEventListener('keydown', function (event) {
        var composing = event.isComposing || event.keyCode === 229;

        if (composing && (event.key === 'Enter' || event.keyCode === 229)) {
          event.stopImmediatePropagation();
        }
      }, true);
    }

    syncDialogs();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init, { once: true });
  } else {
    init();
  }
})();
