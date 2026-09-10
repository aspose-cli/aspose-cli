// PDF live-preview client. PreviewRuntime owns transport and snapshots; this
// client owns fixed-layout page navigation and PDF-specific editing feedback.
(function () {
  'use strict';

  var LIVE_STYLE = 'link[href="/live/shell.css"]';
  var PAGE = '.pdf-snapshot-page';
  var meta = window.__asposePreview || {};
  var revision = meta.revision || 0;
  var currentPage = 1;
  var pageCount = 0;
  var zoomMode = 'fit-page';
  var zoom = 1;
  var refreshing = false;
  var queuedRevision = 0;
  var connection = 'live';
  var lastRenderMs = null;
  var observer = null;
  var activityTimer = 0;
  var runtime = window.AsposePreviewRuntime;

  runtime.events.onopen = function () {
    connection = 'live';
    updateStatus();
  };

  runtime.events.onerror = function () {
    connection = 'reconnecting';
    updateStatus();
  };

  runtime.events.addEventListener('hello', function (event) {
    var payload = runtime.parse(event);
    if (payload && payload.revision > revision) {
      refresh(payload.revision);
    }
  });

  runtime.events.addEventListener('activity', function (event) {
    var payload = runtime.parse(event);
    if (payload && payload.state === 'rendering') {
      showActivity('AI is applying PDF changes\u2026', false);
    }
  });

  runtime.events.addEventListener('update', function (event) {
    var payload = runtime.parse(event);
    if (!payload || typeof payload.revision !== 'number') {
      window.location.reload();
      return;
    }

    lastRenderMs = typeof payload.renderMs === 'number' ? payload.renderMs : null;
    hideProblem();
    if (payload.revision > revision) {
      refresh(payload.revision);
    } else {
      finishActivity();
      updateStatus();
    }
  });

  runtime.events.addEventListener('status', function (event) {
    var payload = runtime.parse(event);
    finishActivity();
    showProblem(payload && payload.message
      ? payload.message
      : 'The PDF could not be refreshed.');
  });

  function initialize() {
    var stylesheet = document.querySelector(LIVE_STYLE);
    if (!stylesheet) {
      return;
    }

    document.head.appendChild(stylesheet);
    buildShell(snapshotNodes(document));
  }

  function snapshotNodes(source) {
    return Array.prototype.filter.call(source.body.childNodes, function (node) {
      if (node.nodeType !== Node.ELEMENT_NODE) {
        return node.textContent.trim() !== '';
      }

      if (node.matches && node.matches(LIVE_STYLE)) {
        return false;
      }

      return node.tagName !== 'SCRIPT';
    });
  }

  function buildShell(nodes) {
    document.body.textContent = '';
    document.body.className = 'pdf-shell-body'
      + (window.parent !== window ? ' pdf-shell-embedded' : '');
    var shell = document.createElement('div');
    shell.className = 'pdf-shell';
    shell.innerHTML =
      '<header class="pdf-topbar">' +
        '<div class="pdf-brand" aria-hidden="true">PDF</div>' +
        '<div class="pdf-file-group"><div class="pdf-file"></div>' +
          '<div class="pdf-kind">FIXED-LAYOUT DOCUMENT</div></div>' +
        '<div class="pdf-live"><span class="pdf-live-dot"></span>' +
          '<span class="pdf-connection">Live</span><span class="pdf-revision"></span>' +
          '<span class="pdf-eval" hidden>EVALUATION</span></div>' +
      '</header>' +
      '<nav class="pdf-toolbar" aria-label="PDF preview controls">' +
        '<div class="pdf-control-group">' +
          '<button type="button" data-action="previous" aria-label="Previous page">\u2039</button>' +
          '<button type="button" data-action="page" class="pdf-page-label">Page 1 of 1</button>' +
          '<button type="button" data-action="next" aria-label="Next page">\u203a</button>' +
        '</div><span class="pdf-rule"></span>' +
        '<div class="pdf-control-group">' +
          '<button type="button" data-action="zoom-out" class="pdf-zoom-step" aria-label="Zoom out">\u2212</button>' +
          '<button type="button" data-action="fit-page" aria-label="Fit PDF page to viewport">Page</button>' +
          '<button type="button" data-action="fit-width" aria-label="Fit PDF page to width">Width</button>' +
          '<button type="button" data-action="actual-size" aria-label="Show PDF at 100 percent">100%</button>' +
          '<button type="button" data-action="zoom-in" class="pdf-zoom-step" aria-label="Zoom in">+</button>' +
          '<span class="pdf-zoom-label" aria-live="polite">100%</span>' +
        '</div>' +
        '<button type="button" data-action="refresh" class="pdf-refresh">Refresh</button>' +
      '</nav>' +
      '<main class="pdf-viewport"><div class="pdf-document" role="document"></div>' +
        '<div class="pdf-activity" role="status" aria-live="polite" hidden>' +
          '<span class="pdf-activity-mark" aria-hidden="true">\u2726</span>' +
          '<span class="pdf-spinner" aria-hidden="true"></span>' +
          '<span class="pdf-activity-text"></span></div>' +
        '<div class="pdf-problem" role="alert" hidden></div></main>' +
      '<footer class="pdf-statusbar"><span class="pdf-summary"></span>' +
        '<span class="pdf-detail"></span></footer>';

    var host = shell.querySelector('.pdf-document');
    nodes.forEach(function (node) { host.appendChild(node); });
    document.body.appendChild(shell);
    shell.querySelector('.pdf-file').textContent = meta.file || 'Untitled PDF';
    shell.querySelector('.pdf-eval').hidden = !meta.eval;
    pageCount = pages().length;
    currentPage = Math.min(currentPage, Math.max(1, pageCount));
    if (zoomMode === 'custom') { applyZoom(); }
    else { fitViewport(zoomMode); }
    observePages();
    updateStatus();
  }

  function pages() {
    return Array.prototype.slice.call(document.querySelectorAll(
      '.pdf-document ' + PAGE
    ));
  }

  function fitViewport(mode) {
    var viewport = document.querySelector('.pdf-viewport');
    var active = pages()[currentPage - 1];
    var image = active && active.querySelector('img');
    if (!viewport || !image) {
      return;
    }
    var width = Number(image.getAttribute('width')) || image.naturalWidth;
    var height = Number(image.getAttribute('height')) || image.naturalHeight;
    if (!width || !height) { return; }
    zoomMode = mode;
    var availableWidth = Math.max(1, viewport.clientWidth - (window.innerWidth <= 680 ? 28 : 72));
    var availableHeight = Math.max(1, viewport.clientHeight - (window.innerWidth <= 680 ? 48 : 72));
    zoom = mode === 'actual-size'
      ? 1
      : mode === 'fit-width'
        ? availableWidth / width
        : Math.min(availableWidth / width, availableHeight / height);
    zoom = Math.max(0.1, Math.min(2, zoom));
    applyZoom();
  }

  function applyZoom() {
    pages().forEach(function (page) {
      page.style.zoom = String(zoom);
    });
    var label = document.querySelector('.pdf-zoom-label');
    if (label) {
      label.textContent = Math.round(zoom * 100) + '%';
    }
    ['fit-page', 'fit-width', 'actual-size'].forEach(function (mode) {
      var button = document.querySelector('[data-action="' + mode + '"]');
      if (button) { button.setAttribute('aria-pressed', String(zoomMode === mode)); }
    });
  }

  function observePages() {
    if (observer) {
      observer.disconnect();
    }
    if (!('IntersectionObserver' in window)) {
      return;
    }

    observer = new IntersectionObserver(function (entries) {
      var visible = entries.filter(function (entry) { return entry.isIntersecting; })
        .sort(function (left, right) { return right.intersectionRatio - left.intersectionRatio; });
      if (visible.length) {
        currentPage = pages().indexOf(visible[0].target) + 1;
        updatePageLabel();
      }
    }, { root: document.querySelector('.pdf-viewport'), threshold: [0.2, 0.5, 0.8] });
    pages().forEach(function (page) { observer.observe(page); });
  }

  function goToPage(number, emphasize) {
    var all = pages();
    if (!all.length) {
      return;
    }
    currentPage = Math.max(1, Math.min(number, all.length));
    updatePageLabel();
    if (zoomMode === 'fit-page' || zoomMode === 'fit-width') {
      fitViewport(zoomMode);
    }
    all[currentPage - 1].scrollIntoView({ behavior: 'smooth', block: 'start' });
    if (emphasize) {
      all[currentPage - 1].classList.add('pdf-page-updated');
    }
  }

  function updatePageLabel() {
    var label = document.querySelector('.pdf-page-label');
    if (label) {
      label.textContent = 'Page ' + currentPage + ' of ' + Math.max(1, pageCount);
    }
  }

  function updateStatus() {
    var dot = document.querySelector('.pdf-live-dot');
    var state = document.querySelector('.pdf-connection');
    var rev = document.querySelector('.pdf-revision');
    var summary = document.querySelector('.pdf-summary');
    var detail = document.querySelector('.pdf-detail');
    if (!dot || !state || !rev || !summary || !detail) {
      return;
    }
    dot.classList.toggle('pdf-live-dot-reconnecting', connection !== 'live');
    state.textContent = connection === 'live' ? 'Live' : 'Reconnecting';
    rev.textContent = 'Revision ' + revision;
    summary.textContent = pageCount + (pageCount === 1 ? ' page' : ' pages');
    detail.textContent = lastRenderMs === null
      ? 'Watching PDF structure and pages'
      : 'Rendered in ' + lastRenderMs + ' ms';
    updatePageLabel();
  }

  function showActivity(message, complete) {
    var activity = document.querySelector('.pdf-activity');
    if (!activity) {
      return;
    }
    window.clearTimeout(activityTimer);
    activity.classList.toggle('pdf-activity-complete', complete);
    activity.querySelector('.pdf-activity-text').textContent = message;
    activity.hidden = false;
  }

  function finishActivity() {
    var activity = document.querySelector('.pdf-activity');
    if (activity) {
      activity.hidden = true;
    }
  }

  function showProblem(message) {
    var problem = document.querySelector('.pdf-problem');
    if (problem) {
      problem.textContent = message + ' Showing the last good PDF snapshot.';
      problem.hidden = false;
    }
  }

  function hideProblem() {
    var problem = document.querySelector('.pdf-problem');
    if (problem) {
      problem.hidden = true;
    }
  }

  function refresh(nextRevision) {
    if (refreshing) {
      queuedRevision = Math.max(queuedRevision, nextRevision);
      return;
    }
    refreshing = true;
    showActivity('AI is applying PDF changes\u2026', false);
    var rememberedPage = currentPage;
    runtime.fetchDocument(nextRevision).then(function (parsed) {
      revision = nextRevision;
      buildShell(snapshotNodes(parsed).map(function (node) {
        return document.importNode(node, true);
      }));
      currentPage = Math.min(rememberedPage, Math.max(1, pageCount));
      goToPage(currentPage, true);
      showActivity('PDF pages updated', true);
      activityTimer = window.setTimeout(finishActivity, 900);
      hideProblem();
    }).catch(function (error) {
      finishActivity();
      showProblem(error.message);
    }).finally(function () {
      refreshing = false;
      updateStatus();
      if (queuedRevision > revision) {
        var queued = queuedRevision;
        queuedRevision = 0;
        refresh(queued);
      }
    });
  }

  document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-action]');
    if (!button) {
      return;
    }
    switch (button.getAttribute('data-action')) {
      case 'previous': goToPage(currentPage - 1, false); break;
      case 'next': goToPage(currentPage + 1, false); break;
      case 'page': goToPage(currentPage, false); break;
      case 'zoom-out':
        zoomMode = 'custom';
        zoom = Math.max(0.35, Math.round((zoom - 0.1) * 10) / 10);
        applyZoom();
        break;
      case 'fit-page': fitViewport('fit-page'); break;
      case 'fit-width': fitViewport('fit-width'); break;
      case 'actual-size': fitViewport('actual-size'); break;
      case 'zoom-in':
        zoomMode = 'custom';
        zoom = Math.min(2, Math.round((zoom + 0.1) * 10) / 10);
        applyZoom();
        break;
      case 'refresh':
        showActivity('Refreshing PDF preview\u2026', false);
        runtime.requestRefresh()
          .catch(function () { showProblem('The refresh request could not be sent.'); });
        break;
    }
  });
  window.addEventListener('resize', function () {
    if (zoomMode === 'fit-page' || zoomMode === 'fit-width') {
      window.requestAnimationFrame(function () { fitViewport(zoomMode); });
    }
  });

  document.addEventListener('keydown', function (event) {
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
      return;
    }
    if (event.key === 'PageUp') {
      event.preventDefault();
      goToPage(currentPage - 1, false);
    } else if (event.key === 'PageDown') {
      event.preventDefault();
      goToPage(currentPage + 1, false);
    }
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize, { once: true });
  } else {
    initialize();
  }
}());
