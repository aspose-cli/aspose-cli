// Slides live-preview client. PreviewRuntime owns transport and immutable
// snapshots; this client owns slide navigation and presentation feedback.
(function () {
  'use strict';

  var LIVE_STYLE = 'link[href="/live/shell.css"]';
  var meta = window.__asposePreview || {};
  var revision = meta.revision || 0;
  var currentSlide = 1;
  var slideCount = 0;
  var zoomMode = 'fit-page';
  var zoom = 1;
  var activityTimer = 0;
  var runtime = window.AsposePreviewRuntime;

  runtime.events.addEventListener('hello', function (event) {
    var payload = runtime.parse(event);
    if (payload && payload.revision > revision) { refresh(payload.revision); }
  });
  runtime.events.addEventListener('activity', function (event) {
    var payload = runtime.parse(event);
    if (payload && payload.state === 'rendering') {
      showActivity('AI is updating the presentation\u2026', false);
    }
  });
  runtime.events.addEventListener('update', function (event) {
    var payload = runtime.parse(event);
    if (payload && payload.revision > revision) { refresh(payload.revision); }
  });
  runtime.events.addEventListener('status', function (event) {
    var payload = runtime.parse(event);
    showProblem((payload && payload.message ? payload.message : 'The presentation could not be refreshed.')
      + ' Showing the last good slide snapshot.');
    hideActivity();
  });

  function snapshot(source) {
    return source.querySelector('.slides-snapshot');
  }

  function initialize() {
    var stylesheet = document.querySelector(LIVE_STYLE);
    var content = snapshot(document);
    if (!stylesheet || !content) { return; }
    document.head.appendChild(stylesheet);
    buildShell(content);
  }

  function buildShell(content) {
    document.body.textContent = '';
    document.body.className = 'slides-shell-body';
    var shell = document.createElement('div');
    shell.className = 'slides-shell';
    shell.innerHTML =
      '<header class="slides-topbar"><div class="slides-brand">S</div>' +
        '<div class="slides-file-group"><div class="slides-file"></div>' +
        '<div class="slides-kind">PRESENTATION</div></div>' +
        '<div class="slides-live"><span class="slides-live-dot"></span>' +
        '<span>Live</span><span class="slides-revision"></span>' +
        '<span class="slides-eval" hidden>EVALUATION</span></div></header>' +
      '<nav class="slides-toolbar" aria-label="Slide preview controls">' +
        '<button type="button" data-action="previous" aria-label="Previous slide">\u2039</button>' +
        '<button type="button" data-action="slide" class="slides-position">Slide 1 of 1</button>' +
        '<button type="button" data-action="next" aria-label="Next slide">\u203a</button>' +
        '<span class="slides-rule"></span><div class="slides-fit-controls">' +
          '<button type="button" data-action="fit-page" aria-label="Fit slide to page">Page</button>' +
          '<button type="button" data-action="fit-width" aria-label="Fit slide to width">Width</button>' +
          '<button type="button" data-action="actual-size" aria-label="Show slide at 100 percent">100%</button>' +
        '</div>' +
        '<button type="button" data-action="refresh" class="slides-refresh">Refresh</button></nav>' +
      '<main class="slides-workspace"></main>' +
      '<div class="slides-activity" role="status" aria-live="polite" hidden>' +
        '<span class="slides-spark">\u2726</span><span class="slides-spinner"></span>' +
        '<span class="slides-activity-text"></span></div>' +
      '<div class="slides-problem" role="alert" hidden></div>' +
      '<footer class="slides-status"><span class="slides-summary"></span>' +
        '<span class="slides-detail">Watching slides, layouts and speaker notes</span></footer>';
    shell.querySelector('.slides-workspace').appendChild(content);
    document.body.appendChild(shell);
    shell.querySelector('.slides-file').textContent = meta.file || 'Untitled presentation';
    shell.querySelector('.slides-eval').hidden = !meta.eval;
    bindThumbs();
    showSlide(Math.min(currentSlide, Math.max(1, slides().length)), false);
  }

  function slides() {
    return Array.prototype.slice.call(document.querySelectorAll('.slides-snapshot-slide'));
  }

  function thumbs() {
    return Array.prototype.slice.call(document.querySelectorAll('.slides-thumb'));
  }

  function bindThumbs() {
    thumbs().forEach(function (thumb) {
      thumb.addEventListener('click', function () {
        showSlide(Number(thumb.getAttribute('data-slide')), false);
      });
    });
  }

  function showSlide(number, updated) {
    var all = slides();
    if (!all.length) { return; }
    slideCount = all.length;
    currentSlide = Math.max(1, Math.min(number, slideCount));
    all.forEach(function (slide, index) {
      slide.hidden = index + 1 !== currentSlide;
      slide.classList.toggle('slides-slide-updated', updated && index + 1 === currentSlide);
    });
    thumbs().forEach(function (thumb, index) {
      if (index + 1 === currentSlide) { thumb.setAttribute('aria-current', 'true'); }
      else { thumb.removeAttribute('aria-current'); }
    });
    var active = thumbs()[currentSlide - 1];
    if (active) { active.scrollIntoView({ block: 'nearest' }); }
    document.querySelector('.slides-position').textContent =
      'Slide ' + currentSlide + ' of ' + slideCount;
    document.querySelector('.slides-summary').textContent =
      slideCount + (slideCount === 1 ? ' slide' : ' slides');
    document.querySelector('.slides-revision').textContent = 'Revision ' + revision;
    fitViewport(zoomMode);
  }

  function fitViewport(mode) {
    var stage = document.querySelector('.slides-snapshot-stage');
    var active = slides()[currentSlide - 1];
    var image = active && active.querySelector('.slides-canvas img');
    if (!stage || !active || !image) { return; }
    var width = Number(image.getAttribute('width')) || image.naturalWidth;
    var height = Number(image.getAttribute('height')) || image.naturalHeight;
    if (!width || !height) { return; }

    zoomMode = mode;
    var availableWidth = Math.max(1, stage.clientWidth - (window.innerWidth <= 680 ? 20 : 84));
    var availableHeight = Math.max(1, stage.clientHeight - (window.innerWidth <= 680 ? 48 : 88));
    var widthScale = availableWidth / width;
    zoom = mode === 'actual-size'
      ? 1
      : mode === 'fit-width'
        ? widthScale
        : Math.min(widthScale, availableHeight / height);
    zoom = Math.max(0.1, Math.min(2, zoom));
    active.style.width = Math.round(width * zoom) + 'px';
    stage.classList.toggle('slides-stage-overflow', width * zoom > availableWidth + 1);
    updateFitControls();
  }

  function updateFitControls() {
    ['fit-page', 'fit-width', 'actual-size'].forEach(function (mode) {
      var button = document.querySelector('[data-action="' + mode + '"]');
      if (button) { button.setAttribute('aria-pressed', String(zoomMode === mode)); }
    });
  }

  function showActivity(message, complete) {
    var panel = document.querySelector('.slides-activity');
    if (!panel) { return; }
    window.clearTimeout(activityTimer);
    panel.classList.toggle('slides-activity-complete', complete);
    panel.querySelector('.slides-activity-text').textContent = message;
    panel.hidden = false;
  }

  function hideActivity() {
    var panel = document.querySelector('.slides-activity');
    if (panel) { panel.hidden = true; }
  }

  function showProblem(message) {
    var problem = document.querySelector('.slides-problem');
    if (problem) { problem.textContent = message; problem.hidden = false; }
  }

  function hideProblem() {
    var problem = document.querySelector('.slides-problem');
    if (problem) { problem.hidden = true; }
  }

  var refreshQueue = runtime.createRefreshQueue({
    currentRevision: function () { return revision; },
    perform: function (nextRevision) {
      showActivity('AI is updating the presentation\u2026', false);
          var remembered = currentSlide;
      return runtime.fetchDocument(nextRevision).then(function (parsed) {
        var next = snapshot(parsed);
        if (!next) { throw new Error('Preview refresh did not contain a slide snapshot.'); }
        revision = nextRevision;
        currentSlide = remembered;
        buildShell(document.importNode(next, true));
        showSlide(Math.min(remembered, slideCount), true);
        showActivity('Presentation updated', true);
        activityTimer = window.setTimeout(hideActivity, 900);
        hideProblem();
      });
    },
    failed: function (error) {
      hideActivity();
      showProblem(error.message + ' Showing the last good slide snapshot.');
    }
  });

  function refresh(nextRevision) {
    refreshQueue.enqueue(nextRevision);
  }

  document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-action]');
    if (!button) { return; }
    switch (button.getAttribute('data-action')) {
      case 'previous': showSlide(currentSlide - 1, false); break;
      case 'next': showSlide(currentSlide + 1, false); break;
      case 'slide': showSlide(currentSlide, false); break;
      case 'fit-page': fitViewport('fit-page'); break;
      case 'fit-width': fitViewport('fit-width'); break;
      case 'actual-size': fitViewport('actual-size'); break;
      case 'refresh':
        showActivity('Refreshing presentation preview\u2026', false);
        runtime.requestRefresh()
          .catch(function () { showProblem('The refresh request could not be sent.'); });
        break;
    }
  });
  window.addEventListener('resize', function () {
    if (zoomMode !== 'actual-size') {
      window.requestAnimationFrame(function () { fitViewport(zoomMode); });
    }
  });
  document.addEventListener('keydown', function (event) {
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) { return; }
    if (event.key === 'ArrowLeft' || event.key === 'PageUp') {
      event.preventDefault(); showSlide(currentSlide - 1, false);
    } else if (event.key === 'ArrowRight' || event.key === 'PageDown') {
      event.preventDefault(); showSlide(currentSlide + 1, false);
    }
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize, { once: true });
  } else { initialize(); }
}());
