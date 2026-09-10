// Words live-preview client. The shared preview server owns rendering,
// versions, and SSE; this client owns only word-processing presentation.
(function () {
  'use strict';

  var LIVE_STYLESHEET = 'link[href="/live/shell.css"]';
  var PAGE_SELECTOR = '.awpage';
  var currentRevision = metadata().revision || 0;
  var streamRevision = currentRevision;
  var currentPage = 1;
  var pageCount = 0;
  var zoom = 1;
  var zoomMode = 'fit-page';
  var zoomInitialized = false;
  var refreshing = false;
  var queuedRevision = 0;
  var pendingFocus = null;
  var connectionState = 'live';
  var lastRenderMs = null;
  var activityStartedAt = 0;
  var activityTimer = 0;
  var pageObserver = null;
  var runtime = window.AsposePreviewRuntime;

  runtime.events.onopen = function () {
    connectionState = 'live';
    updateStatus();
  };

  runtime.events.addEventListener('hello', function (event) {
    var payload = runtime.parse(event);
    if (!payload || typeof payload.revision !== 'number') {
      return;
    }

    streamRevision = Math.max(streamRevision, payload.revision);
    if (payload.revision > currentRevision) {
      refresh(payload.revision);
    }
  });

  runtime.events.addEventListener('activity', function (event) {
    var payload = runtime.parse(event);
    if (!payload || payload.state !== 'rendering') {
      return;
    }

    showActivity('Applying document changes\u2026');
  });

  runtime.events.addEventListener('update', function (event) {
    var payload = runtime.parse(event);
    if (!payload || typeof payload.revision !== 'number') {
      window.location.reload();
      return;
    }

    lastRenderMs = typeof payload.renderMs === 'number' ? payload.renderMs : null;
    streamRevision = Math.max(streamRevision, payload.revision);
    hideProblem();
    if (payload.revision > currentRevision) {
      refresh(payload.revision);
    } else {
      finishActivity();
      updateStatus();
    }
  });

  runtime.events.addEventListener('focus', function (event) {
    var payload = runtime.parse(event);
    if (!payload || !Array.isArray(payload.targets)) {
      return;
    }

    pendingFocus = payload;
    if (!refreshing && payload.revision <= currentRevision) {
      applyFocus(payload);
      pendingFocus = null;
    }
  });

  runtime.events.addEventListener('status', function (event) {
    var payload = runtime.parse(event);
    if (!payload) {
      return;
    }

    finishActivity();
    showProblem(payload.message || 'The document could not be refreshed.');
  });

  runtime.events.onerror = function () {
    connectionState = 'reconnecting';
    updateStatus();
  };

  function metadata() {
    return window.__asposePreview || {};
  }

  function initialize() {
    var stylesheet = document.querySelector(LIVE_STYLESHEET);
    if (!stylesheet) {
      return;
    }

    document.head.appendChild(stylesheet);
    markEngineHead(document);
    buildShell(documentNodes(document));
  }

  function documentNodes(source) {
    return Array.prototype.filter.call(source.body.childNodes, function (node) {
      if (node.nodeType !== Node.ELEMENT_NODE) {
        return node.textContent.trim() !== '';
      }

      if (node.matches && node.matches(LIVE_STYLESHEET)) {
        return false;
      }

      if (node.tagName !== 'SCRIPT') {
        return true;
      }

      return !(
        node.getAttribute('src') === '/live/client.js'
        || node.textContent.indexOf('window.__asposePreview=') >= 0
      );
    });
  }

  function buildShell(nodes) {
    document.body.textContent = '';
    document.body.className = 'words-shell-body'
      + (window.parent !== window ? ' words-shell-embedded' : '');

    var shell = element('div', 'words-shell');
    shell.innerHTML =
      '<header class="words-topbar">' +
        '<div class="words-brand" aria-hidden="true">W</div>' +
        '<div class="words-file-group">' +
          '<div class="words-file"></div>' +
          '<div class="words-kind">WORD DOCUMENT</div>' +
        '</div>' +
        '<div class="words-top-status">' +
          '<span class="words-live-dot"></span>' +
          '<span class="words-connection">Live</span>' +
          '<span class="words-revision"></span>' +
          '<span class="words-eval" hidden>EVALUATION</span>' +
        '</div>' +
      '</header>' +
      '<nav class="words-toolbar" aria-label="Document preview controls">' +
        '<div class="words-control-group">' +
          '<button type="button" data-action="previous" aria-label="Previous page">\u2039</button>' +
          '<button type="button" data-action="page" class="words-page-label" aria-label="Current page">Page 1 of 1</button>' +
          '<button type="button" data-action="next" aria-label="Next page">\u203a</button>' +
        '</div>' +
        '<div class="words-toolbar-rule"></div>' +
        '<div class="words-control-group">' +
          '<button type="button" data-action="zoom-out" aria-label="Zoom out">\u2212</button>' +
          '<button type="button" data-action="zoom-reset" class="words-zoom-label" aria-label="Reset zoom">100%</button>' +
          '<button type="button" data-action="zoom-in" aria-label="Zoom in">+</button>' +
          '<button type="button" data-action="fit-width" aria-label="Fit page width">Fit width</button>' +
          '<button type="button" data-action="fit-page" aria-label="Fit whole page">Fit page</button>' +
        '</div>' +
        '<button type="button" data-action="refresh" class="words-refresh" aria-label="Refresh document">Refresh</button>' +
      '</nav>' +
      '<main class="words-viewport">' +
        '<div class="words-document" role="document" aria-label="Document pages"></div>' +
        '<div class="words-activity" role="status" aria-live="polite" hidden>' +
          '<span class="words-activity-spark" aria-hidden="true">\u2726</span>' +
          '<span class="words-activity-spinner" aria-hidden="true"></span>' +
          '<span class="words-activity-text">Applying document changes\u2026</span>' +
        '</div>' +
        '<div class="words-problem" role="alert" hidden></div>' +
      '</main>' +
      '<footer class="words-statusbar">' +
        '<span class="words-status-summary"></span>' +
        '<span class="words-status-detail"></span>' +
      '</footer>';

    var documentHost = shell.querySelector('.words-document');
    nodes.forEach(function (node) {
      documentHost.appendChild(node);
    });
    document.body.appendChild(shell);

    shell.querySelector('.words-file').textContent = metadata().file || 'Untitled document';
    shell.querySelector('.words-eval').hidden = !metadata().eval;
    countPages();
    fitViewport(zoomMode);
    applyZoom();
    observePages();
    updateStatus();
  }

  function element(tag, className) {
    var node = document.createElement(tag);
    node.className = className;
    return node;
  }

  function countPages() {
    pageCount = pages().length;
    currentPage = Math.max(1, Math.min(currentPage, Math.max(1, pageCount)));
    updatePageLabel();
  }

  function pages() {
    return Array.prototype.slice.call(document.querySelectorAll(
      '.words-document ' + PAGE_SELECTOR
    ));
  }

  function updatePageLabel() {
    var label = document.querySelector('.words-page-label');
    if (label) {
      label.textContent = 'Page ' + currentPage + ' of ' + Math.max(1, pageCount);
    }
  }

  function observePages() {
    if (pageObserver) {
      pageObserver.disconnect();
    }

    if (!('IntersectionObserver' in window)) {
      return;
    }

    pageObserver = new IntersectionObserver(function (entries) {
      var visible = entries
        .filter(function (entry) { return entry.isIntersecting; })
        .sort(function (left, right) { return right.intersectionRatio - left.intersectionRatio; });
      if (!visible.length) {
        return;
      }

      var index = pages().indexOf(visible[0].target);
      if (index >= 0) {
        currentPage = index + 1;
        updatePageLabel();
      }
    }, {
      root: document.querySelector('.words-viewport'),
      threshold: [0.15, 0.4, 0.7]
    });

    pages().forEach(function (page) {
      pageObserver.observe(page);
    });
  }

  function goToPage(number, emphasize) {
    var allPages = pages();
    if (!allPages.length) {
      return;
    }

    currentPage = Math.max(1, Math.min(number, allPages.length));
    updatePageLabel();
    var target = allPages[currentPage - 1];
    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    if (emphasize) {
      target.classList.remove('words-page-updated');
      void target.offsetWidth;
      target.classList.add('words-page-updated');
      window.setTimeout(function () {
        target.classList.remove('words-page-updated');
      }, 1800);
    }
  }

  function applyZoom() {
    pages().forEach(function (page) {
      page.style.zoom = String(zoom);
    });

    var label = document.querySelector('.words-zoom-label');
    if (label) {
      label.textContent = Math.round(zoom * 100) + '%';
    }
  }

  function fitViewport(mode) {
    if (mode === 'manual' && zoomInitialized) {
      return;
    }
    if (zoomInitialized) {
      if (mode !== zoomMode) {
        zoomMode = mode;
      }
    }

    zoomInitialized = true;
    var viewport = document.querySelector('.words-viewport');
    var firstPage = pages()[0];
    if (!viewport || !firstPage) {
      return;
    }

    var style = getComputedStyle(viewport);
    var availableWidth = viewport.clientWidth
      - parseFloat(style.paddingLeft)
      - parseFloat(style.paddingRight);
    var availableHeight = viewport.clientHeight
      - parseFloat(style.paddingTop)
      - parseFloat(style.paddingBottom);
    var widthScale = availableWidth / firstPage.offsetWidth;
    var heightScale = availableHeight / firstPage.offsetHeight;
    var requested = mode === 'fit-width'
      ? widthScale
      : Math.min(widthScale, heightScale);
    zoom = Math.max(0.5, Math.min(2, Math.floor(requested * 20) / 20));
    zoomMode = mode;
  }

  function updateStatus() {
    var dot = document.querySelector('.words-live-dot');
    var connection = document.querySelector('.words-connection');
    var revision = document.querySelector('.words-revision');
    var summary = document.querySelector('.words-status-summary');
    var detail = document.querySelector('.words-status-detail');
    if (!dot || !connection || !revision || !summary || !detail) {
      return;
    }

    dot.classList.toggle('words-live-dot-reconnecting', connectionState !== 'live');
    connection.textContent = connectionState === 'live' ? 'Live' : 'Reconnecting';
    revision.textContent = 'Revision ' + currentRevision;
    summary.textContent = pageCount + (pageCount === 1 ? ' page' : ' pages');
    detail.textContent = lastRenderMs === null
      ? 'Watching for document changes'
      : 'Updated in ' + lastRenderMs + ' ms';
  }

  function showActivity(message) {
    var activity = document.querySelector('.words-activity');
    if (!activity) {
      return;
    }

    activityStartedAt = Date.now();
    window.clearTimeout(activityTimer);
    activity.classList.remove('words-activity-complete');
    activity.querySelector('.words-activity-text').textContent = message;
    activity.hidden = false;
    document.body.classList.add('words-is-editing');
  }

  function showCompletedActivity() {
    var activity = document.querySelector('.words-activity');
    if (!activity) {
      return;
    }

    activityStartedAt = Date.now();
    window.clearTimeout(activityTimer);
    activity.classList.add('words-activity-complete');
    activity.querySelector('.words-activity-text').textContent = 'Document updated';
    activity.hidden = false;
    document.body.classList.add('words-is-editing');
    activityTimer = window.setTimeout(function () {
      activity.hidden = true;
      activity.classList.remove('words-activity-complete');
      document.body.classList.remove('words-is-editing');
    }, 900);
  }

  function finishActivity() {
    var activity = document.querySelector('.words-activity');
    if (!activity || activity.hidden) {
      return;
    }

    var remaining = Math.max(0, 500 - (Date.now() - activityStartedAt));
    window.clearTimeout(activityTimer);
    activityTimer = window.setTimeout(function () {
      activity.hidden = true;
      document.body.classList.remove('words-is-editing');
    }, remaining);
  }

  function showProblem(message) {
    var problem = document.querySelector('.words-problem');
    if (!problem) {
      return;
    }

    problem.textContent = message + ' Showing the last good version.';
    problem.hidden = false;
  }

  function hideProblem() {
    var problem = document.querySelector('.words-problem');
    if (problem) {
      problem.hidden = true;
    }
  }

  function refresh(requestedRevision) {
    if (refreshing) {
      queuedRevision = Math.max(queuedRevision, requestedRevision);
      return;
    }

    refreshing = true;
    showActivity('Applying document changes\u2026');
    var rememberedPage = currentPage;
    runtime.fetchDocument(requestedRevision)
      .then(function (parsed) {
        replaceEngineHead(parsed);
        currentRevision = requestedRevision;
        buildShell(documentNodes(parsed).map(function (node) {
          return document.importNode(node, true);
        }));
        currentPage = Math.min(rememberedPage, Math.max(1, pageCount));
        updatePageLabel();
        goToPage(currentPage, false);
        animateUpdatedPages();
        showCompletedActivity();
        if (pendingFocus && pendingFocus.revision <= currentRevision) {
          applyFocus(pendingFocus);
          pendingFocus = null;
        }
      })
      .catch(function (error) {
        finishActivity();
        showProblem(error.message);
      })
      .finally(function () {
        refreshing = false;
        updateStatus();
        if (queuedRevision > currentRevision) {
          var next = queuedRevision;
          queuedRevision = 0;
          refresh(next);
        }
      });
  }

  function markEngineHead(source) {
    Array.prototype.forEach.call(
      source.head.querySelectorAll('style, link[rel="stylesheet"]'),
      function (node) {
        if (!node.matches(LIVE_STYLESHEET)) {
          node.setAttribute('data-aspose-cli-words-engine-style', '');
        }
      }
    );
  }

  function replaceEngineHead(source) {
    document.head.querySelectorAll('[data-aspose-cli-words-engine-style]').forEach(function (node) {
      node.remove();
    });
    source.head.querySelectorAll('style, link[rel="stylesheet"]').forEach(function (node) {
      if (!node.matches(LIVE_STYLESHEET)) {
        var copy = document.importNode(node, true);
        copy.setAttribute('data-aspose-cli-words-engine-style', '');
        document.head.appendChild(copy);
      }
    });
  }

  function animateUpdatedPages() {
    pages().forEach(function (page, index) {
      page.style.setProperty('--words-page-delay', Math.min(index * 35, 280) + 'ms');
      page.classList.add('words-page-arrived');
      window.setTimeout(function () {
        page.classList.remove('words-page-arrived');
      }, 900);
    });
  }

  function applyFocus(payload) {
    var pageNumbers = payload.targets
      .map(function (target) { return target.page; })
      .filter(function (page) {
        return Number.isInteger(page) && page > 0 && page <= pageCount;
      });
    if (pageNumbers.length) {
      goToPage(pageNumbers[0], true);
    }
  }

  document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-action]');
    if (!button) {
      return;
    }

    switch (button.getAttribute('data-action')) {
      case 'previous':
        goToPage(currentPage - 1, false);
        break;
      case 'next':
        goToPage(currentPage + 1, false);
        break;
      case 'page':
        goToPage(currentPage, false);
        break;
      case 'zoom-out':
        zoomMode = 'manual';
        zoom = Math.max(0.5, Math.round((zoom - 0.1) * 10) / 10);
        applyZoom();
        break;
      case 'zoom-reset':
        zoomMode = 'manual';
        zoom = 1;
        applyZoom();
        break;
      case 'zoom-in':
        zoomMode = 'manual';
        zoom = Math.min(2, Math.round((zoom + 0.1) * 10) / 10);
        applyZoom();
        break;
      case 'fit-width':
        fitViewport('fit-width');
        applyZoom();
        break;
      case 'fit-page':
        fitViewport('fit-page');
        applyZoom();
        break;
      case 'refresh':
        showActivity('Refreshing document preview\u2026');
        runtime.requestRefresh()
          .catch(function () {
            finishActivity();
            showProblem('The refresh request could not be sent.');
          });
        break;
    }
  });

  document.addEventListener('keydown', function (event) {
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
      return;
    }

    var delta = event.key === 'PageUp'
      ? -1
      : event.key === 'PageDown'
        ? 1
        : 0;
    if (delta !== 0) {
      event.preventDefault();
      goToPage(currentPage + delta, false);
    }
  });

  window.addEventListener('resize', function () {
    if (zoomMode === 'manual') {
      return;
    }
    fitViewport(zoomMode);
    applyZoom();
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize, { once: true });
  } else {
    initialize();
  }
}());
