(function () {
  'use strict';

  if (window.AsposePreviewRuntime) {
    return;
  }

  var metadata = window.__asposePreview || {};
  var documentPath = metadata.documentPath || '/';
  var documentStateKey = metadata.stateStorageKey || documentPath;
  var stateStorageKey = 'aspose.preview.state.' + documentStateKey;
  var maximumStateBytes = 64 * 1024;
  var sourceEvents = new EventSource('/live/events');
  var events = new EventTarget();
  var stateSequence = 0;
  var lastIntentState = null;
  var activeStateRequest = null;
  var pendingStateRequest = null;
  var stateRefreshDelayMs = 50;

  function parse(event) {
    try {
      return JSON.parse(event.data);
    } catch (_) {
      return null;
    }
  }

  function normalizedAddress(value) {
    return typeof value === 'string' && value.length > 0 &&
      value.length <= 512 && value.indexOf('\r') < 0 &&
      value.indexOf('\n') < 0 ? value : null;
  }

  function normalizedAddresses(values) {
    if (!Array.isArray(values) || values.length > 32) { return []; }
    var result = [];
    values.forEach(function (value) {
      var address = normalizedAddress(value);
      if (address && result.indexOf(address) < 0) { result.push(address); }
    });
    return result;
  }

  function selectedAddresses(root) {
    try {
      return normalizedAddresses(JSON.parse(
        root.dataset.selectedAddresses || '[]'));
    } catch (_) { return []; }
  }

  function cloneMessage(event) {
    return new MessageEvent(event.type, {
      data: event.data,
      lastEventId: event.lastEventId,
      origin: event.origin
    });
  }

  function forward(event) {
    events.dispatchEvent(cloneMessage(event));
  }

  function forwardAs(type, event) {
    events.dispatchEvent(new MessageEvent(type, {
      data: event.data,
      lastEventId: event.lastEventId,
      origin: event.origin
    }));
  }

  function serializedState(state) {
    var value = JSON.stringify(state);
    if (new TextEncoder().encode(value).length > maximumStateBytes) {
      throw new Error('Preview state exceeds the 64 KB client limit.');
    }
    return value;
  }

  function readStoredState() {
    try {
      var value = sessionStorage.getItem(stateStorageKey);
      if (!value || new TextEncoder().encode(value).length > maximumStateBytes) {
        return null;
      }
      return JSON.parse(value);
    } catch (_) {
      return null;
    }
  }

  function storeState(serialized) {
    try {
      sessionStorage.setItem(stateStorageKey, serialized);
    } catch (_) {
      // Storage is optional; the current immutable URL remains usable.
    }
  }

  function rememberState(state) {
    var serialized = serializedState(state);
    lastIntentState = JSON.parse(serialized);
    storeState(serialized);
  }

  function isHistoryTraversal() {
    try {
      var entries = window.performance &&
        window.performance.getEntriesByType('navigation');
      return !!entries && entries.length > 0 &&
        entries[0].type === 'back_forward';
    } catch (_) {
      return false;
    }
  }

  function fetchDocument(revision) {
    return window.fetch(documentPath + '?rev=' + encodeURIComponent(revision), {
      cache: 'no-store',
      credentials: 'same-origin'
    }).then(function (response) {
      if (!response.ok) {
        throw new Error(
          'Preview refresh returned HTTP ' + response.status + '.');
      }
      return response.text();
    }).then(function (html) {
      return new DOMParser().parseFromString(html, 'text/html');
    });
  }

  function requestRefresh() {
    return window.fetch('/live/refresh', {
      method: 'POST',
      credentials: 'same-origin'
    }).then(function (response) {
      if (!response.ok) {
        throw new Error(
          'Preview refresh request returned HTTP ' + response.status + '.');
      }
      return response;
    });
  }

  function publicationUrl(publication) {
    if (!publication || typeof publication.url !== 'string' ||
        typeof publication.revision !== 'number') {
      throw new Error('Preview state response is missing its publication.');
    }

    var url = new URL(publication.url, window.location.href);
    if (url.origin !== window.location.origin) {
      throw new Error('Preview state response is not same-origin.');
    }
    return url;
  }

  function performStateRequest(intent) {
    return window.fetch('/live/state', {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        'X-Aspose-Preview-Revision': String(intent.revision)
      },
      body: intent.serialized
    }).then(function (response) {
      if (response.status === 409) {
        return response.json().then(function (envelope) {
          var error = envelope && envelope.error;
          var details = error && error.details;
          var current = details && details.currentRevision;
          if (error && error.code === 'PREVIEW_STATE_STALE' &&
              intent.retries === 0 && Number.isInteger(current) && current >= 0) {
            intent.revision = current;
            intent.retries++;
            return performStateRequest(intent);
          }
          var failure = new Error(error && error.message ||
            'The selected preview target is stale.');
          failure.code = error && error.code;
          throw failure;
        });
      }
      if (!response.ok) {
        return response.text().then(function (message) {
          var envelope;
          try {
            envelope = JSON.parse(message);
          } catch (_) {
            envelope = null;
          }
          var error = envelope && envelope.error;
          var failure = new Error(error && error.message || message ||
            'Preview state request returned HTTP ' + response.status + '.');
          failure.code = error && error.code;
          throw failure;
        });
      }
      return response.json();
    }).then(function (publication) {
      var url = publicationUrl(publication);
      if (intent.sequence !== stateSequence) {
        return { stale: true };
      }

      storeState(intent.serialized);
      if (intent.replaceHistory) {
        window.location.replace(url.href);
      } else {
        window.location.assign(url.href);
      }
      return publication;
    }).catch(function (error) {
      if (intent.sequence !== stateSequence) {
        return { stale: true };
      }
      lastIntentState = readStoredState();
      throw error;
    });
  }

  function startPendingStateRequest() {
    if (activeStateRequest || !pendingStateRequest ||
        pendingStateRequest.timer !== null) {
      return;
    }

    var intent = pendingStateRequest;
    intent.timer = window.setTimeout(function () {
      if (pendingStateRequest !== intent) {
        return;
      }

      pendingStateRequest = null;
      intent.timer = null;
      activeStateRequest = intent;
      performStateRequest(intent).then(intent.resolve, intent.reject)
        .finally(function () {
          if (activeStateRequest === intent) {
            activeStateRequest = null;
          }
          startPendingStateRequest();
        });
    }, intent.delay);
  }

  function scheduleStateRequest(
    state,
    replaceHistory,
    delay,
    revision,
    source) {
    var serialized = serializedState(state);
    var isUserIntent = source === 'user';
    // Reissue an in-flight user choice against the newer document revision;
    // a background refresh must not replace its state or history behavior.
    var preservedUserIntent = !isUserIntent &&
      ((pendingStateRequest && pendingStateRequest.isUserIntent &&
        pendingStateRequest) ||
       (activeStateRequest && activeStateRequest.isUserIntent &&
        activeStateRequest));
    if (preservedUserIntent) {
      serialized = preservedUserIntent.serialized;
      replaceHistory = preservedUserIntent.replaceHistory;
      delay = 0;
      isUserIntent = true;
    }
    lastIntentState = JSON.parse(serialized);
    var resolveIntent;
    var rejectIntent;
    var promise = new Promise(function (resolve, reject) {
      resolveIntent = resolve;
      rejectIntent = reject;
    });
    var intent = {
      serialized: serialized,
      replaceHistory: replaceHistory,
      delay: delay,
      revision: Number.isInteger(revision) && revision >= 0
        ? revision
        : Number(metadata.revision || 0),
      retries: 0,
      isUserIntent: isUserIntent,
      sequence: ++stateSequence,
      timer: null,
      resolve: resolveIntent,
      reject: rejectIntent,
      promise: promise
    };

    if (pendingStateRequest) {
      window.clearTimeout(pendingStateRequest.timer);
      intent.replaceHistory = pendingStateRequest.replaceHistory &&
        intent.replaceHistory;
      intent.delay = Math.min(pendingStateRequest.delay, intent.delay);
      intent.revision = Math.max(
        pendingStateRequest.revision,
        intent.revision);
      pendingStateRequest.resolve({ stale: true });
    }

    pendingStateRequest = intent;
    startPendingStateRequest();
    return promise;
  }

  function requestState(state, replaceHistory) {
    return scheduleStateRequest(
      state,
      replaceHistory === true,
      0,
      Number(metadata.revision || 0),
      'user');
  }

  function refreshState(state, revision) {
    return scheduleStateRequest(
      state,
      true,
      stateRefreshDelayMs,
      revision,
      'refresh').catch(function (error) {
        dispatchStateFailure(error, revision);
        throw error;
      });
  }

  function dispatchStateFailure(error, revision) {
    var code = error && typeof error.code === 'string' &&
      /^[A-Z][A-Z0-9_]{0,63}$/.test(error.code)
      ? error.code
      : 'VIEW_RENDER_FAILED';
    events.dispatchEvent(new MessageEvent('status', {
      data: JSON.stringify({
        revision: revision || 0,
        state: 'error',
        code: code,
        message: error.message || 'Preview state refresh failed.'
      })
    }));
  }

  Object.defineProperties(events, {
    readyState: {
      get: function () { return sourceEvents.readyState; }
    },
    url: {
      get: function () { return sourceEvents.url; }
    },
    withCredentials: {
      get: function () { return sourceEvents.withCredentials; }
    }
  });
  events.close = function () {
    sourceEvents.close();
  };
  sourceEvents.onopen = function (event) {
    events.dispatchEvent(new Event('open'));
    if (typeof events.onopen === 'function') {
      events.onopen(event);
    }
  };
  sourceEvents.onerror = function (event) {
    events.dispatchEvent(new Event('error'));
    if (typeof events.onerror === 'function') {
      events.onerror(event);
    }
  };

  ['hello', 'activity', 'status', 'focus'].forEach(function (type) {
    sourceEvents.addEventListener(type, forward);
  });
  sourceEvents.addEventListener('update', function (event) {
    forwardAs('beforeupdate', event);
    var state = lastIntentState || readStoredState();
    if (!state) {
      forward(event);
      return;
    }

    var update = parse(event);
    refreshState(state, update && update.revision).catch(function () {
      // The scheduler emits one status event for the coalesced batch.
    });
  });

  function createRefreshQueue(options) {
    var running = false;
    var pendingRevision = 0;

    function drain() {
      if (running || pendingRevision <= options.currentRevision()) {
        return;
      }

      var revision = pendingRevision;
      pendingRevision = 0;
      running = true;
      Promise.resolve()
        .then(function () {
          return options.perform(revision);
        })
        .catch(function (error) {
          if (options.failed) {
            options.failed(error, revision);
          }
        })
        .finally(function () {
          running = false;
          if (options.settled) {
            options.settled(revision);
          }
          drain();
        });
    }

    return Object.freeze({
      enqueue: function (revision) {
        if (typeof revision !== 'number') {
          return;
        }
        pendingRevision = Math.max(pendingRevision, revision);
        drain();
      },
      isRefreshing: function () {
        return running;
      }
    });
  }

  function bindFocusHints(root, options) {
    if (!root || !options || typeof options.productId !== 'string' ||
        typeof options.schemaId !== 'string' ||
        typeof options.resolve !== 'function') {
      throw new Error('Preview focus configuration is invalid.');
    }

    var label = options.label || options.productId;
    var revision = Number(metadata.revision || 0);
    var storageKey = 'aspose.preview.focus.' + options.productId + '.' +
      documentStateKey;
    var status = root.querySelector('[data-client-status]');

    function setStatus(message, error) {
      if (!status) { return; }
      status.textContent = message || '';
      status.classList.toggle('error', !!error);
    }

    function target(message) {
      if (!message || !Array.isArray(message.targets)) { return null; }
      return message.targets.find(function (item) {
        return item && item.productId === options.productId &&
          item.kind === 'hint' && item.schemaVersion === 2 &&
          item.schemaId === options.schemaId && item.payload &&
          typeof item.payload === 'object' && !Array.isArray(item.payload);
      }) || null;
    }
    function receive(message) {
      var item = target(message);
      if (!item) { return; }
      if (typeof message.revision !== 'number') {
        setStatus('Invalid ' + label + ' focus revision.', true);
        return;
      }
      if (message.revision < revision) {
        setStatus('The ' + label + ' focus target is stale.', true);
        return;
      }
      if (message.revision > revision) {
        try { sessionStorage.setItem(storageKey, JSON.stringify(message)); } catch (_) { }
        return;
      }

      var state = options.resolve(item.payload);
      if (!state) {
        setStatus('The edited ' + label + ' target is no longer available.', true);
        return;
      }
      try { sessionStorage.removeItem(storageKey); } catch (_) { }
      refreshState(state, message.revision).catch(function () {
        setStatus('The edited ' + label + ' target could not be selected.', true);
      });
    }

    events.addEventListener('update', function (event) {
      var update = parse(event);
      if (update && typeof update.revision === 'number') {
        revision = Math.max(revision, update.revision);
      }
    });
    events.addEventListener('focus', function (event) {
      receive(parse(event));
    });
    window.requestAnimationFrame(function () {
      try {
        var stored = sessionStorage.getItem(storageKey);
        if (stored) {
          sessionStorage.removeItem(storageKey);
          receive(JSON.parse(stored));
        }
      } catch (_) {
        setStatus('The stored ' + label + ' focus target is invalid.', true);
      }
    });
  }

  function bindAddressSelection(root, options) {
    if (!root || !options || typeof options.createState !== 'function') {
      throw new Error('Preview address selection configuration is invalid.');
    }

    var label = options.label || 'preview';
    var status = root.querySelector('[data-client-status]');
    var copy = root.querySelector('[data-copy-address]');
    var addressLabel = root.querySelector('[data-selected-address-label]');
    var summary = root.querySelector('[data-selected-summary]');
    var targets = Array.prototype.slice.call(
      root.querySelectorAll('[data-preview-address]'));
    var focusTargets = targets.filter(function (target, index, all) {
      var address = normalizedAddress(target.dataset.previewAddress);
      return address && all.findIndex(function (candidate) {
        return normalizedAddress(candidate.dataset.previewAddress) === address;
      }) === index;
    });
    targets.forEach(function (target) { target.tabIndex = -1; });

    function setRovingTarget(address) {
      var active = focusTargets.find(function (target) {
        return target.dataset.previewAddress === address;
      }) || focusTargets[0];
      focusTargets.forEach(function (target) {
        target.tabIndex = target === active ? 0 : -1;
      });
    }

    function setStatus(message, error) {
      if (!status) { return; }
      status.textContent = message || '';
      status.classList.toggle('error', !!error);
    }

    function markStale(message) {
      root.dataset.selectedAddresses = '[]';
      if (copy) { copy.disabled = true; }
      if (addressLabel) { addressLabel.textContent = 'Stale location'; }
      if (summary) { summary.textContent = 'The selected object is unavailable.'; }
      setRovingTarget(null);
      rememberState(options.createState([]));
      setStatus(message ||
        'The selected ' + label + ' location is stale or unavailable.', true);
    }

    function applySelection(addresses) {
      root.dataset.selectedAddresses = JSON.stringify(addresses);
      targets.forEach(function (target) {
        var selected = addresses.indexOf(target.dataset.previewAddress) >= 0;
        target.setAttribute('aria-pressed', selected ? 'true' : 'false');
      });
      setRovingTarget(addresses.length ? addresses[addresses.length - 1] : null);
      if (copy) { copy.disabled = addresses.length === 0; }
      if (addressLabel) {
        addressLabel.textContent = addresses.length === 0
          ? 'No object selected'
          : addresses.length === 1 ? addresses[0] : addresses.length + ' objects';
      }
    }

    function select(addresses) {
      addresses = normalizedAddresses(addresses);
      applySelection(addresses);
      root.setAttribute('aria-busy', 'true');
      setStatus(addresses.length ? 'Selecting ' + label + ' object...' :
        'Clearing ' + label + ' selection...', false);
      requestState(options.createState(addresses), true).catch(function (error) {
        root.removeAttribute('aria-busy');
        if (error && error.code === 'PREVIEW_TARGET_STALE') {
          markStale(error.message);
          return;
        }
        setStatus(error.message || 'The ' + label + ' target is stale.', true);
      });
    }

    root.addEventListener('click', function (event) {
      var target = event.target && event.target.closest &&
        event.target.closest('[data-preview-address]');
      if (target && root.contains(target)) {
        var address = normalizedAddress(target.dataset.previewAddress);
        if (!address) { return; }
        var next = selectedAddresses(root);
        if (event.ctrlKey || event.metaKey) {
          var index = next.indexOf(address);
          if (index >= 0) { next.splice(index, 1); }
          else if (next.length < 32) { next.push(address); }
          else {
            setStatus('Preview selection is limited to 32 objects.', true);
            return;
          }
        } else {
          next = [address];
        }
        select(next);
      }
    });
    root.addEventListener('keydown', function (event) {
      if (event.key === 'Escape' && selectedAddresses(root).length) {
        event.preventDefault();
        select([]);
        return;
      }
      var target = event.target && event.target.closest &&
        event.target.closest('[data-preview-address]');
      if (!target || !root.contains(target) || focusTargets.length < 2) {
        return;
      }
      var current = focusTargets.indexOf(target);
      var next = event.key === 'Home' ? 0 :
        event.key === 'End' ? focusTargets.length - 1 :
        event.key === 'ArrowLeft' || event.key === 'ArrowUp'
          ? (current + focusTargets.length - 1) % focusTargets.length :
        event.key === 'ArrowRight' || event.key === 'ArrowDown'
          ? (current + 1) % focusTargets.length : current;
      if (next !== current) {
        event.preventDefault();
        setRovingTarget(focusTargets[next].dataset.previewAddress);
        focusTargets[next].focus();
      }
    });

    var selected = selectedAddresses(root);
    var selectedTargets = focusTargets.filter(function (target) {
      return selected.indexOf(target.dataset.previewAddress) >= 0;
    });
    if (selected.length === 1 && selectedTargets.length === 0) {
      markStale();
    } else {
      applySelection(selected);
    }
    if (selectedTargets.length > 0) {
      rememberState(options.createState(selected));
      window.requestAnimationFrame(function () {
        selectedTargets[selectedTargets.length - 1].focus();
      });
    }

    if (copy) {
      copy.addEventListener('click', function () {
        var values = selectedAddresses(root);
        if (!values.length || !navigator.clipboard || !navigator.clipboard.writeText) {
          setStatus('No current ' + label + ' address can be copied.', true);
          return;
        }
        navigator.clipboard.writeText(values.join('\n')).then(function () {
          setStatus('Copied ' + values.length + ' ' + label + ' address' +
            (values.length === 1 ? '' : 'es') + '.', false);
        }).catch(function () {
          setStatus('The browser denied clipboard access.', true);
        });
      });
    }
    events.addEventListener('status', function (event) {
      var payload = parse(event);
      if (payload && payload.code === 'PREVIEW_TARGET_STALE') {
        markStale(payload.message);
      }
    });
  }

  function bindPagedNavigator(root, options) {
    if (!root) {
      events.addEventListener('update', function () {
        window.location.reload();
      });
      return;
    }
    if (!options || !/^[a-z][a-z0-9-]{0,31}$/.test(options.unit || '') ||
        !/^[a-z][a-z0-9-]{0,31}$/.test(options.productId || '') ||
        typeof options.schemaId !== 'string' ||
        (options.integerIdentityProperty &&
         !/^[a-z][A-Za-z0-9]{0,31}$/.test(options.integerIdentityProperty)) ||
        (options.preserveSelections !== undefined &&
         typeof options.preserveSelections !== 'boolean')) {
      throw new Error('Preview navigator configuration is invalid.');
    }

    var unit = options.unit;
    var unitProperty = unit.replace(/-([a-z])/g, function (_, value) {
      return value.toUpperCase();
    });
    var countProperty = unitProperty + 'Count';
    var numberProperty = unitProperty + 'Number';
    var stepProperty = unitProperty + 'Step';
    var identityProperty = options.integerIdentityProperty || null;
    var navigatorStateBytes = 16 * 1024;
    var navigatorKey = 'aspose.preview.navigator.' + options.productId + '.' +
      documentStateKey;
    var status = root.querySelector('[data-client-status]');
    var zoom = root.querySelector('[data-zoom]');
    var viewport = root.querySelector('.stage');
    var historyTraversal = isHistoryTraversal();
    var stateNavigationGeneration = 0;
    var pendingStateNavigation = 0;
    var navigationStatusOwner = 0;

    function beginStateNavigation() {
      navigationStatusOwner = 0;
      pendingStateNavigation = ++stateNavigationGeneration;
      return pendingStateNavigation;
    }

    function settleStateNavigation(generation) {
      if (pendingStateNavigation !== generation) {
        return false;
      }
      pendingStateNavigation = 0;
      return true;
    }

    function observeStateNavigation(promise, generation) {
      promise.then(function (result) {
        if (result && result.stale && settleStateNavigation(generation)) {
          root.removeAttribute('aria-busy');
          setStatus('', false);
        }
      }).catch(function (error) {
        if (settleStateNavigation(generation)) {
          navigationStatusOwner = generation;
          root.removeAttribute('aria-busy');
          setStatus(error.message, true);
        }
      });
    }

    function readNavigatorState() {
      try {
        var value = sessionStorage.getItem(navigatorKey);
        if (!value || new TextEncoder().encode(value).length > navigatorStateBytes) {
          return {};
        }
        var parsed = JSON.parse(value);
        return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
          ? parsed
          : {};
      } catch (_) {
        return {};
      }
    }

    function storeNavigatorState(values) {
      try {
        var current = readNavigatorState();
        Object.keys(values).forEach(function (key) {
          if (values[key] === null || values[key] === undefined) {
            delete current[key];
          } else {
            current[key] = values[key];
          }
        });
        var serialized = JSON.stringify(current);
        if (new TextEncoder().encode(serialized).length <= navigatorStateBytes) {
          sessionStorage.setItem(navigatorKey, serialized);
        }
      } catch (_) {
        // Navigation remains functional when browser storage is unavailable.
      }
    }

    function setStatus(message, error) {
      if (!status) {
        return;
      }
      status.textContent = message || '';
      status.classList.toggle('error', !!error);
    }

    function boundedInteger(value, minimum, maximum, fallback) {
      var parsed = Number(value);
      return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum
        ? parsed
        : fallback;
    }

    function normalizedPattern(value) {
      return typeof value === 'string' && value.length <= 256 &&
        value.indexOf('\r') < 0 && value.indexOf('\n') < 0 && value.trim()
        ? value
        : null;
    }

    function addIntegerIdentity(state, source) {
      if (!identityProperty) {
        return;
      }
      var value = source && source[identityProperty];
      if (value === undefined || value === null) {
        var number = state[unitProperty];
        var item = root.querySelector(
          '[data-' + unit + '-number="' + number + '"]');
        value = item && item.dataset[identityProperty];
        if ((value === undefined || value === null) &&
            number === Number(root.dataset[unitProperty])) {
          value = root.dataset[identityProperty];
        }
      }
      var identity = boundedInteger(value, 1, Number.MAX_SAFE_INTEGER, 0);
      if (identity) {
        state[identityProperty] = identity;
      } else {
        delete state[identityProperty];
      }
    }

    function currentState(overrides) {
      var count = boundedInteger(root.dataset[countProperty], 1, 10000, 1);
      var state = {
        pattern: normalizedPattern(root.dataset.pattern),
        matchIndex: boundedInteger(root.dataset.matchIndex, 0, 99, 0),
        zoom: boundedInteger(root.dataset.zoom, 50, 200, 100)
      };
      state[unitProperty] = boundedInteger(
        root.dataset[unitProperty], 1, count, 1);
      var currentValue = state[unitProperty];
      Object.keys(overrides || {}).forEach(function (key) {
        state[key] = overrides[key];
      });
      state[unitProperty] = boundedInteger(
        state[unitProperty], 1, count, currentValue);
      state.zoom = boundedInteger(state.zoom, 50, 200, 100);
      state.matchIndex = boundedInteger(state.matchIndex, 0, 99, 0);
      state.pattern = normalizedPattern(state.pattern);
      if (!state.pattern) {
        delete state.pattern;
        state.matchIndex = 0;
      }
      addIntegerIdentity(state, overrides);
      if (options.preserveSelections) {
        var selections = Object.prototype.hasOwnProperty.call(
          overrides || {}, 'selections')
          ? normalizedAddresses(overrides.selections)
          : selectedAddresses(root);
        if (selections.length) { state.selections = selections; }
        else { delete state.selections; }
      }
      return state;
    }

    function envelope(state) {
      return {
        productId: options.productId,
        kind: 'state',
        schemaVersion: 2,
        schemaId: options.schemaId,
        payload: state
      };
    }

    function request(overrides, replaceHistory) {
      var state = currentState(overrides);
      var storedState = Object.assign({}, state);
      if (options.preserveSelections && !state.selections) {
        storedState.selections = [];
      }
      storeNavigatorState(storedState);
      setStatus('Updating preview...', false);
      root.setAttribute('aria-busy', 'true');
      var navigationGeneration = beginStateNavigation();
      observeStateNavigation(
        requestState(envelope(state), replaceHistory),
        navigationGeneration);
    }

    root.tabIndex = root.tabIndex < 0 ? 0 : root.tabIndex;
    root.addEventListener('keydown', function (event) {
      var tag = event.target && event.target.tagName;
      if (event.altKey || event.ctrlKey || event.metaKey ||
          /^(INPUT|SELECT|TEXTAREA|BUTTON|A)$/.test(tag || '')) {
        return;
      }
      var count = Number(root.dataset[countProperty] || 1);
      var current = Number(root.dataset[unitProperty] || 1);
      var next = event.key === 'Home' ? 1 :
        event.key === 'End' ? count :
        event.key === 'PageUp' ? Math.max(1, current - 1) :
        event.key === 'PageDown' ? Math.min(count, current + 1) : current;
      if (next !== current) {
        event.preventDefault();
        var state = { pattern: null, matchIndex: 0, selections: [] };
        state[unitProperty] = next;
        request(state);
      }
    });

    root.querySelectorAll('[data-' + unit + '-number]').forEach(function (button) {
      button.addEventListener('click', function () {
        var next = {};
        next[unitProperty] = Number(button.dataset[numberProperty]);
        next.pattern = null;
        next.matchIndex = 0;
        next.selections = [];
        request(next);
      });
    });

    root.querySelectorAll('[data-' + unit + '-step]').forEach(function (button) {
      button.addEventListener('click', function () {
        var count = Number(root.dataset[countProperty] || 1);
        var current = Number(root.dataset[unitProperty] || 1);
        var nextValue = button.dataset[stepProperty] === 'next'
          ? Math.min(count, current + 1)
          : Math.max(1, current - 1);
        if (nextValue !== current) {
          var next = { pattern: null, matchIndex: 0, selections: [] };
          next[unitProperty] = nextValue;
          request(next);
        }
      });
    });

    var searchForm = root.querySelector('[data-search-form]');
    if (searchForm) {
      searchForm.addEventListener('submit', function (event) {
        event.preventDefault();
        var pattern = String(new FormData(searchForm).get('pattern') || '').trim();
        if (!pattern) {
          setStatus('Enter text to search for.', true);
          return;
        }
        request({ pattern: pattern, matchIndex: 0, selections: [] });
      });
    }

    root.querySelectorAll('[data-match]').forEach(function (button) {
      button.addEventListener('click', function () {
        var count = Number(root.dataset.matchCount || 0);
        if (count === 0 || !root.dataset.pattern) {
          setStatus('No search matches are available.', true);
          return;
        }
        var current = Number(root.dataset.matchIndex || 0);
        request({
          matchIndex: button.dataset.match === 'next'
            ? (current + 1) % count
            : (current + count - 1) % count,
          selections: []
        });
      });
    });

    var jumpForm = root.querySelector('[data-' + unit + '-jump]');
    if (jumpForm) {
      jumpForm.addEventListener('submit', function (event) {
        event.preventDefault();
        var count = Number(root.dataset[countProperty] || 1);
        var value = Number(new FormData(jumpForm).get(unit));
        if (!Number.isInteger(value) || value < 1 || value > count) {
          setStatus('Use a ' + unit + ' number from 1 to ' + count + '.', true);
          return;
        }
        var next = { pattern: null, matchIndex: 0, selections: [] };
        next[unitProperty] = value;
        request(next);
      });
    }

    function applyZoom(value) {
      var percent = boundedInteger(value, 50, 200, 100);
      document.documentElement.style.setProperty(
        '--' + unit + '-zoom', percent / 100);
      root.dataset.zoom = String(percent);
      if (zoom) {
        zoom.value = String(percent);
      }
      storeNavigatorState({ zoom: percent });
    }

    if (zoom) {
      zoom.addEventListener('change', function () {
        applyZoom(zoom.value);
      });
    }

    var stored = readNavigatorState();
    applyZoom(stored.zoom || root.dataset.zoom || 100);
    window.requestAnimationFrame(function () {
      if (historyTraversal) {
        return;
      }
      if (viewport) {
        viewport.scrollLeft = boundedInteger(
          stored.scrollLeft, 0, Number.MAX_SAFE_INTEGER, 0);
        viewport.scrollTop = boundedInteger(
          stored.scrollTop, 0, Number.MAX_SAFE_INTEGER, 0);
      }
    });
    var storedCurrent = boundedInteger(
      stored[unitProperty], 1, Number(root.dataset[countProperty] || 1), 0);
    var storedIdentity = identityProperty
      ? boundedInteger(stored[identityProperty], 1, Number.MAX_SAFE_INTEGER, 0)
      : 0;
    var currentIdentity = identityProperty
      ? boundedInteger(root.dataset[identityProperty], 1, Number.MAX_SAFE_INTEGER, 0)
      : 0;
    var storedSelections = options.preserveSelections
      ? normalizedAddresses(stored.selections)
      : [];
    var currentSelections = options.preserveSelections
      ? selectedAddresses(root)
      : [];
    if (historyTraversal) {
      rememberState(envelope(currentState()));
    } else if (currentSelections.length) {
      var publishedState = currentState({});
      storeNavigatorState(publishedState);
      rememberState(envelope(publishedState));
    } else if (storedCurrent &&
        (storedCurrent !== Number(root.dataset[unitProperty]) ||
         (storedIdentity && storedIdentity !== currentIdentity) ||
         JSON.stringify(storedSelections) !== JSON.stringify(currentSelections) ||
         normalizedPattern(stored.pattern) !== normalizedPattern(root.dataset.pattern) ||
         boundedInteger(stored.matchIndex, 0, 99, 0) !==
           Number(root.dataset.matchIndex || 0))) {
      var restored = {
        pattern: normalizedPattern(stored.pattern),
        matchIndex: boundedInteger(stored.matchIndex, 0, 99, 0),
        zoom: boundedInteger(stored.zoom, 50, 200, 100)
      };
      restored[unitProperty] = storedCurrent;
      if (storedIdentity) {
        restored[identityProperty] = storedIdentity;
      }
      if (storedSelections.length) {
        restored.selections = storedSelections;
      } else if (options.preserveSelections) {
        restored.selections = [];
      }
      request(restored);
    }

    function saveViewport() {
      var state = {
        scrollLeft: viewport ? viewport.scrollLeft : 0,
        scrollTop: viewport ? viewport.scrollTop : 0
      };
      if (pendingStateNavigation === 0) {
        Object.assign(state, currentState({}));
      }
      if (options.preserveSelections && !state.selections) {
        state.selections = [];
      }
      storeNavigatorState(state);
    }

    window.addEventListener('beforeunload', saveViewport);
    events.addEventListener('beforeupdate', saveViewport);
    events.addEventListener('update', function (event) {
      var state = currentState({});
      var update = parse(event);
      storeNavigatorState(state);
      setStatus('Updating preview...', false);
      root.setAttribute('aria-busy', 'true');
      var navigationGeneration = beginStateNavigation();
      observeStateNavigation(
        refreshState(envelope(state), update && update.revision),
        navigationGeneration);
    });
    events.addEventListener('status', function (event) {
      var payload = parse(event);
      if (pendingStateNavigation !== 0 || navigationStatusOwner !== 0) {
        return;
      }
      root.removeAttribute('aria-busy');
      if (payload && payload.code === 'PREVIEW_TARGET_STALE' &&
          options.preserveSelections) {
        root.dataset.selectedAddresses = '[]';
        storeNavigatorState({ selections: [] });
      }
      if (payload && payload.state === 'error') {
        setStatus(payload.message || payload.code || 'Preview update failed.', true);
      }
    });
  }

  window.AsposePreviewRuntime = Object.freeze({
    events: events,
    parse: parse,
    fetchDocument: fetchDocument,
    requestRefresh: requestRefresh,
    requestState: requestState,
    refreshState: refreshState,
    rememberState: rememberState,
    isHistoryTraversal: isHistoryTraversal,
    createRefreshQueue: createRefreshQueue,
    bindFocusHints: bindFocusHints,
    bindAddressSelection: bindAddressSelection,
    bindPagedNavigator: bindPagedNavigator
  });
}());
