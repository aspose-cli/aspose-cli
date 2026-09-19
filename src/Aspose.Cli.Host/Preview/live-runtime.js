(function () {
  'use strict';

  if (window.AsposePreviewRuntime) {
    return;
  }

  var metadata = window.__asposePreview || {};
  var documentPath = metadata.documentPath || '/';
  var sourceEvents = new EventSource('/live/events');
  var events = new EventTarget();

  function parse(event) {
    try {
      return JSON.parse(event.data);
    } catch (_) {
      return null;
    }
  }

  function forward(event) {
    events.dispatchEvent(new MessageEvent(event.type, {
      data: event.data,
      lastEventId: event.lastEventId,
      origin: event.origin
    }));
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

  ['hello', 'activity', 'update', 'status'].forEach(function (type) {
    sourceEvents.addEventListener(type, forward);
  });
  // Focus targets travel in the host's versioned envelope; product clients
  // receive only their own payloads.
  sourceEvents.addEventListener('focus', function (event) {
    var focus = parse(event);
    if (!focus || !Array.isArray(focus.targets)) {
      return;
    }
    events.dispatchEvent(new MessageEvent('focus', {
      data: JSON.stringify({
        revision: focus.revision,
        targets: focus.targets.map(function (target) {
          return target && target.payload && typeof target.payload === 'object'
            ? target.payload
            : target;
        })
      }),
      lastEventId: event.lastEventId,
      origin: event.origin
    }));
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

  window.AsposePreviewRuntime = Object.freeze({
    events: events,
    parse: parse,
    fetchDocument: fetchDocument,
    requestRefresh: requestRefresh,
    createRefreshQueue: createRefreshQueue
  });
}());
