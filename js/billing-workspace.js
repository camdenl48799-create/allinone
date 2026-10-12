(function () {
  'use strict';

  var publishableKey = window.ALLINONE_CLERK_PUBLISHABLE_KEY || '';
  var warning = document.getElementById('allinone-setup-warning');
  var signInRoot = document.getElementById('allinone-sign-in');
  var workspace = document.getElementById('allinone-workspace');
  var userButtonRoot = document.getElementById('allinone-user-button');
  var message = document.getElementById('allinone-billing-message');
  var planStatus = document.getElementById('allinone-plan-status');
  var greeting = document.getElementById('allinone-greeting');
  var signInMounted = false;
  var userButtonMounted = false;
  var busy = false;

  function showWarning(text) {
    warning.hidden = false;
    warning.textContent = text;
  }

  function showMessage(text) {
    message.hidden = false;
    message.textContent = text;
  }

  function setBusy(value) {
    busy = value;
    document.querySelectorAll('[data-plan], #allinone-manage-billing, #allinone-refresh-status, #allinone-sign-out')
      .forEach(function (button) { button.disabled = value; });
  }

  function loadScript(src, attrs) {
    return new Promise(function (resolve, reject) {
      var script = document.createElement('script');
      script.src = src;
      script.async = true;
      script.crossOrigin = 'anonymous';
      Object.keys(attrs || {}).forEach(function (key) { script.setAttribute(key, attrs[key]); });
      script.onload = resolve;
      script.onerror = function () { reject(new Error('Could not load the sign-in service. Check your connection and Clerk setup.')); };
      document.head.appendChild(script);
    });
  }

  function getClerkDomain(key) {
    try {
      var encoded = key.split('_')[2];
      if (!encoded) return '';
      return atob(encoded).replace(/\$$/, '');
    } catch (_) {
      return '';
    }
  }

  async function getSessionToken() {
    var clerk = window.Clerk;
    if (!clerk || !clerk.session) throw new Error('Please sign in again.');
    var token = await clerk.session.getToken();
    if (!token) throw new Error('Your session expired. Please sign in again.');
    return token;
  }

  async function apiRequest(path, method, body) {
    var token = await getSessionToken();
    var response = await fetch(path, {
      method: method,
      headers: {
        'Authorization': 'Bearer ' + token,
        'Content-Type': 'application/json'
      },
      body: body ? JSON.stringify(body) : undefined,
      cache: 'no-store'
    });
    var data = await response.json().catch(function () { return {}; });
    if (!response.ok) throw new Error(data.error || 'The billing request failed. Please try again.');
    return data;
  }

  async function refreshStatus() {
    planStatus.textContent = 'Checking…';
    try {
      var data = await apiRequest('/api/stripe/status', 'GET');
      var names = { free: 'Free', pro: 'Pro', pro_plus: 'Pro+', unknown: 'Unknown plan' };
      planStatus.textContent = (names[data.plan] || 'Free') + ' · ' + (data.status || 'no subscription');
      if (data.cancelAtPeriodEnd) planStatus.textContent += ' · cancels at period end';
    } catch (error) {
      planStatus.textContent = 'Not configured yet';
      showMessage(error.message);
    }
  }

  async function startCheckout(plan) {
    if (busy) return;
    setBusy(true);
    message.hidden = true;
    try {
      var data = await apiRequest('/api/stripe/checkout', 'POST', { plan: plan });
      if (!data.url || typeof data.url !== 'string' || !data.url.startsWith('https://checkout.stripe.com/')) {
        throw new Error('Stripe did not return a valid hosted Checkout link.');
      }
      window.location.assign(data.url);
    } catch (error) {
      showMessage(error.message);
      setBusy(false);
    }
  }

  async function openPortal() {
    if (busy) return;
    setBusy(true);
    message.hidden = true;
    try {
      var data = await apiRequest('/api/stripe/portal', 'POST');
      if (!data.url || typeof data.url !== 'string' || !data.url.startsWith('https://billing.stripe.com/')) {
        throw new Error('Stripe did not return a valid billing portal link.');
      }
      window.location.assign(data.url);
    } catch (error) {
      showMessage(error.message);
      setBusy(false);
    }
  }

  function renderAuth() {
    var clerk = window.Clerk;
    var signedIn = Boolean(clerk && clerk.isSignedIn);
    signInRoot.hidden = signedIn;
    workspace.hidden = !signedIn;
    if (!signedIn) return;

    var user = clerk.user || {};
    var firstName = user.firstName || user.username || (user.primaryEmailAddress && user.primaryEmailAddress.emailAddress) || 'there';
    greeting.textContent = 'You’re signed in as ' + firstName + '.';
    if (!userButtonMounted) {
      try {
        clerk.mountUserButton(userButtonRoot);
        userButtonMounted = true;
      } catch (_) { /* The account page remains usable if the optional menu cannot mount. */ }
    }
    refreshStatus();
  }

  async function init() {
    if (!publishableKey || !publishableKey.startsWith('pk_') || publishableKey.indexOf('replace') !== -1) {
      showWarning('Website sign-in is not configured yet. Add your website Clerk Publishable Key to js/clerk-config.js. It is public; never put a secret key in this file.');
      return;
    }

    var domain = getClerkDomain(publishableKey);
    if (!domain || !/^[a-zA-Z0-9.-]+$/.test(domain)) {
      showWarning('The Clerk Publishable Key does not look valid. Copy the website key from Clerk Dashboard → API keys.');
      return;
    }

    try {
      await loadScript('https://' + domain + '/npm/@clerk/ui@1/dist/ui.browser.js');
      await loadScript('https://' + domain + '/npm/@clerk/clerk-js@6/dist/clerk.browser.js', {
        'data-clerk-publishable-key': publishableKey
      });
      if (!window.Clerk || typeof window.Clerk.load !== 'function') {
        throw new Error('Clerk loaded, but its browser SDK did not initialize.');
      }
      await window.Clerk.load({ ui: { ClerkUI: window.__internal_ClerkUICtor } });

      if (!window.Clerk.isSignedIn) {
        window.Clerk.mountSignIn(signInRoot, {
          signUpForceRedirectUrl: window.location.href.split('?')[0],
          signInForceRedirectUrl: window.location.href.split('?')[0]
        });
        signInMounted = true;
      }

      window.Clerk.addListener(function () {
        renderAuth();
      });
      renderAuth();

      document.querySelectorAll('[data-plan]').forEach(function (button) {
        button.addEventListener('click', function () { startCheckout(button.getAttribute('data-plan')); });
      });
      document.getElementById('allinone-manage-billing').addEventListener('click', openPortal);
      document.getElementById('allinone-refresh-status').addEventListener('click', refreshStatus);
      document.getElementById('allinone-sign-out').addEventListener('click', async function () {
        if (busy) return;
        setBusy(true);
        try {
          await window.Clerk.signOut();
          window.location.reload();
        } catch (error) {
          showMessage(error.message || 'Could not sign out.');
          setBusy(false);
        }
      });

      var params = new URLSearchParams(window.location.search);
      if (params.get('billing') === 'success') {
        showMessage('You returned from Checkout. We are checking the subscription status with Stripe; this page never treats a redirect alone as proof of payment.');
        await refreshStatus();
        window.history.replaceState({}, document.title, window.location.pathname);
      } else if (params.get('billing') === 'cancelled') {
        showMessage('Checkout was cancelled. No subscription was created by this return alone.');
        window.history.replaceState({}, document.title, window.location.pathname);
      }
    } catch (error) {
      showWarning(error.message || 'Could not initialize website sign-in.');
      if (signInMounted && window.Clerk) {
        try { window.Clerk.unmountSignIn(signInRoot); } catch (_) {}
      }
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
