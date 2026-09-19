/* Shared document viewer of aspose-cli.
 *
 * The kit owns everything products share: the shell (top bar, toolbar,
 * sidebar, stage, review panel and status bar), zoom, keyboard navigation,
 * theming, element spotlights and the built-in part layouts:
 *   pages - a continuous vertical run of pages,
 *   deck  - one slide at a time on a stage, with a slideshow,
 *   tabs  - one part at a time behind a tab strip.
 * A product presenter registers with definePresenter and describes how each
 * of its views is presented; its stylesheet sets the product accent. The page
 * supplies the document: a static review page inlines the view.json and
 * review.json that sit beside it and loads parts relative to itself. */
(function () {
  'use strict';

  var SVG_NS = 'http://www.w3.org/2000/svg';
  // Matches the stage padding in kit.css; fit zooms leave it around a part.
  var GUTTER = 24;
  // The automatic zoom fits the width but never enlarges beyond this.
  var AUTO_ZOOM_LIMIT = 1.25;
  var ZOOM_STEPS = [0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4];
  var SPOTLIGHT_MS = 1800;
  // How long programmatic scrolling may take before scroll tracking resumes
  // where scrollend is not supported.
  var SCROLL_SETTLE_MS = 1000;
  var ICONS = {
    sidebar: 'M4 5h16v14H4z M9.5 5v14',
    previous: 'M14.5 6l-6 6 6 6',
    next: 'M9.5 6l6 6-6 6',
    zoomOut: 'M6 12h12',
    zoomIn: 'M12 6v12 M6 12h12',
    fitWidth: 'M4 12h16 M7.5 8.5L4 12l3.5 3.5 M16.5 8.5L20 12l-3.5 3.5',
    fitPage: 'M9 4H4v5 M15 4h5v5 M9 20H4v-5 M15 20h5v-5',
    review: 'M6 21V4 M6 4h11l-2.5 4 2.5 4H6',
    slideshow: 'M8 5.5v13l10.5-6.5z',
    close: 'M6 6l12 12 M18 6L6 18'
  };
  var LAYOUTS = { pages: pagesLayout, deck: deckLayout, tabs: tabsLayout };
  var DEFAULT_ZOOM = { pages: 'auto', deck: 'fit-page', tabs: 'auto' };

  var presenters = Object.create(null);
  var reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  var narrowScreen = window.matchMedia('(max-width: 760px)');

  /** Registers how one product presents its views. */
  function definePresenter(product, presenter) {
    presenters[product] = presenter;
  }

  /**
   * Starts the viewer on the document the page carries in its
   * aspose-viewer-data block; options.base prefixes every part file.
   */
  function start(options) {
    var data = JSON.parse(document.getElementById('aspose-viewer-data').textContent);
    var review = data.review;
    var presenter = presenters[review.product];
    var spec = presenter && presenter.views[data.view.view];
    if (!spec) {
      throw new Error('No presenter shows the ' + data.view.view + ' view of ' + review.product + '.');
    }
    createViewer(document.body, {
      product: review.product,
      file: baseName(review.input),
      license: review.license ? review.license.mode : null,
      view: data.view,
      review: review,
      base: options.base || ''
    }, presenter, spec);
  }

  function createViewer(host, doc, presenter, spec) {
    var parts = doc.view.parts;
    var total = Math.max(doc.view.totalParts, parts.length);
    var defaultZoom = spec.zoom || DEFAULT_ZOOM[spec.layout];
    var state = { index: -1, zoom: defaultZoom };
    var ctx = {
      parts: parts,
      total: total,
      spec: spec,
      url: function (part) { return doc.base + part.file; },
      fit: function (width, height, viewport, zoom) {
        return fitScale(zoom || state.zoom, width, height, viewport);
      },
      go: go,
      select: select
    };
    var layout = LAYOUTS[spec.layout](ctx);
    var sidebar = spec.sidebar ? createSidebar(ctx) : null;
    var panel = doc.review ? reviewPanel(ctx, doc.review, function () { togglePanel(false); }) : null;

    var controls = {};
    var toolbar = el('div', 'av-toolbar');
    toolbar.setAttribute('role', 'toolbar');
    toolbar.setAttribute('aria-label', 'Viewer');
    if (sidebar) {
      controls.sidebar = iconButton('sidebar', 'Show or hide ' + sidebar.title.toLowerCase(), toggleSidebar);
      controls.sidebar.setAttribute('aria-controls', sidebar.element.id);
      toolbar.append(controls.sidebar, separator());
    }
    controls.previous = iconButton('previous', 'Previous ' + spec.noun.toLowerCase(), function () { go(state.index - 1); });
    controls.position = el('span', 'av-position');
    controls.next = iconButton('next', 'Next ' + spec.noun.toLowerCase(), function () { go(state.index + 1); });
    controls.zoomOut = iconButton('zoomOut', 'Zoom out (-)', function () { zoomStep(-1); });
    controls.zoom = textButton('av-zoom-label', '100%', function () { setZoom(defaultZoom); });
    controls.zoom.title = 'Reset zoom (0)';
    controls.zoomIn = iconButton('zoomIn', 'Zoom in (+)', function () { zoomStep(1); });
    controls.fitWidth = iconButton('fitWidth', 'Fit width', function () { setZoom('fit-width'); });
    controls.fitPage = iconButton('fitPage', 'Fit ' + spec.noun.toLowerCase(), function () { setZoom('fit-page'); });
    controls.fitWidth.classList.add('av-fit');
    controls.fitPage.classList.add('av-fit');
    toolbar.append(
      controls.previous, controls.position, controls.next, separator(),
      controls.zoomOut, controls.zoom, controls.zoomIn, controls.fitWidth, controls.fitPage);
    if (layout.tools) {
      toolbar.appendChild(separator());
      layout.tools.forEach(function (tool) { toolbar.appendChild(tool); });
    }
    if (panel) {
      controls.review = textButton('av-review-toggle', null, togglePanel);
      controls.review.append(icon('review'), el('span', 'av-review-label', 'Review'));
      controls.review.setAttribute('aria-controls', panel.id);
      if (doc.review.findings.length) {
        controls.review.appendChild(el('span', 'av-count', doc.review.findings.length));
      }
      toolbar.append(el('span', 'av-spacer'), controls.review);
    }

    var body = el('div', 'av-body');
    var stage = el('main', 'av-stage');
    stage.appendChild(layout.element);
    if (sidebar) {
      body.appendChild(sidebar.element);
      sidebar.element.addEventListener('click', function (event) {
        // An overlay sidebar gives the stage back once a part is picked.
        if (narrowScreen.matches && event.target.closest('button')) {
          toggleSidebar(false);
        }
      });
    }
    body.appendChild(stage);
    if (panel) {
      body.appendChild(panel);
    }

    var statusbar = el('footer', 'av-statusbar');
    var statusPart = el('span', 'av-status-part');
    var statusCoverage = el('span', 'av-status-coverage', coverageText());
    statusCoverage.setAttribute('data-omitted', String(total > parts.length));
    statusbar.append(statusPart, statusCoverage);

    var app = el('div', 'av-app');
    app.setAttribute('data-product', doc.product);
    app.setAttribute('data-layout', spec.layout);
    app.append(topBar(doc, presenter), toolbar, body, statusbar);
    host.appendChild(app);

    if (sidebar) {
      toggleSidebar(!narrowScreen.matches);
    }
    if (panel) {
      togglePanel(doc.review.findings.length > 0 && !narrowScreen.matches);
    }
    if (parts.length === 0) {
      layout.element.replaceWith(el('p', 'av-empty', 'No ' + plural(spec.noun) + ' were rendered.'));
      [controls.previous, controls.next, controls.zoomOut, controls.zoom, controls.zoomIn,
        controls.fitWidth, controls.fitPage].forEach(function (control) { control.disabled = true; });
      return;
    }

    layout.rescale();
    go(0);
    layout.focus.focus({ preventScroll: true });
    document.addEventListener('keydown', onKeyDown);
    var resizeQueued = false;
    new ResizeObserver(function () {
      if (resizeQueued || typeof state.zoom === 'number') {
        return;
      }
      resizeQueued = true;
      requestAnimationFrame(function () {
        resizeQueued = false;
        layout.rescale();
        refreshZoom();
      });
    }).observe(stage);

    /** Brings a part, and optionally one element box on it, into view. */
    function go(index, box) {
      if (index < 0 || index >= parts.length) {
        return;
      }
      select(index, layout.show(index, box));
    }

    /**
     * Reflects the reading position in the chrome: the part in view and,
     * when the layout knows it, the line being read in part CSS pixels.
     * Layouts call it while scrolling.
     */
    function select(index, line) {
      if (sidebar) {
        sidebar.select(index, line === undefined ? Infinity : line);
      }
      if (index === state.index) {
        return;
      }
      state.index = index;
      var part = parts[index];
      var position = spec.noun + ' ' + (index + 1) + ' of ' + total;
      controls.position.textContent = position;
      controls.previous.disabled = index === 0;
      controls.next.disabled = index === parts.length - 1;
      var details = [position];
      if (part.label !== spec.noun + ' ' + (index + 1)) {
        details.push(part.label);
      }
      if (part.hidden) {
        details.push('Hidden');
      }
      var status = spec.status && spec.status(part);
      if (status) {
        details.push(status);
      }
      statusPart.textContent = details.join(' \u00b7 ');
      refreshZoom();
    }

    function setZoom(zoom) {
      state.zoom = zoom;
      layout.rescale();
      refreshZoom();
    }

    function zoomStep(direction) {
      var scale = layout.scale();
      var steps = direction > 0 ? ZOOM_STEPS : ZOOM_STEPS.slice().reverse();
      for (var i = 0; i < steps.length; i++) {
        if (direction > 0 ? steps[i] > scale * 1.01 : steps[i] < scale * 0.99) {
          setZoom(steps[i]);
          return;
        }
      }
    }

    function refreshZoom() {
      var part = parts[state.index];
      var fixed = !part || part.kind !== 'image';
      controls.zoom.textContent = fixed ? '\u2014' : Math.round(layout.scale() * 100) + '%';
      [controls.zoomOut, controls.zoom, controls.zoomIn, controls.fitWidth, controls.fitPage]
        .forEach(function (control) { control.disabled = fixed; });
      controls.fitWidth.setAttribute('aria-pressed', String(state.zoom === 'fit-width'));
      controls.fitPage.setAttribute('aria-pressed', String(state.zoom === 'fit-page'));
    }

    function toggleSidebar(open) {
      var show = typeof open === 'boolean' ? open : sidebar.element.hidden;
      sidebar.element.hidden = !show;
      controls.sidebar.setAttribute('aria-expanded', String(show));
    }

    function togglePanel(open) {
      var show = typeof open === 'boolean' ? open : panel.hidden;
      panel.hidden = !show;
      controls.review.setAttribute('aria-expanded', String(show));
    }

    function onKeyDown(event) {
      if (event.defaultPrevented || event.ctrlKey || event.altKey || event.metaKey || isTyping(event.target)) {
        return;
      }
      if (layout.keydown && layout.keydown(event)) {
        event.preventDefault();
        return;
      }
      if (controls.zoomIn.disabled) {
        return;
      }
      switch (event.key) {
        case '+':
        case '=':
          zoomStep(1);
          break;
        case '-':
        case '_':
          zoomStep(-1);
          break;
        case '0':
          setZoom(defaultZoom);
          break;
        default:
          return;
      }
      event.preventDefault();
    }

    function coverageText() {
      return total > parts.length
        ? parts.length + ' of ' + total + ' ' + plural(spec.noun) + ' rendered'
        : total + ' ' + (total === 1 ? spec.noun.toLowerCase() : plural(spec.noun));
    }
  }

  // ---- Layouts ------------------------------------------------------------
  //
  // A layout places parts on the stage. It exposes its element, the element
  // that takes keyboard focus, show(index, box) to bring a part into view
  // (returning the reading line on it when the layout scrolls through
  // parts), rescale() to apply the current zoom, scale() for the effective
  // zoom of the part in view and optionally toolbar tools and a keydown hook.

  /** Every page in one scrolling column; scrolling selects the page in view. */
  function pagesLayout(ctx) {
    var scroller = scrollerElement();
    var column = el('div', 'av-pages');
    scroller.appendChild(column);
    var widest = 1;
    var tallest = 1;
    var frames = ctx.parts.map(function (part, index) {
      var frame = partFrame(ctx, part);
      var page = el('div', 'av-page');
      page.append(frame.element, el('span', 'av-page-number', index + 1));
      column.appendChild(page);
      widest = Math.max(widest, part.width || 0);
      tallest = Math.max(tallest, part.height || 0);
      return frame;
    });
    if (ctx.total > frames.length) {
      column.appendChild(el('p', 'av-omitted', omittedText(ctx)));
    }

    var scale = 1;
    // While a programmatic scroll runs, the requested page stays selected;
    // tracking the pages it passes would make the pager count through them.
    var navigating = false;
    var settleTimer = 0;
    var queued = false;
    scroller.addEventListener('scroll', function () {
      if (queued || navigating) {
        return;
      }
      queued = true;
      requestAnimationFrame(function () {
        queued = false;
        var index = current();
        ctx.select(index, (readingLine() - frames[index].element.offsetTop) / scale);
      });
    }, { passive: true });
    scroller.addEventListener('scrollend', settle);

    function settle() {
      navigating = false;
      clearTimeout(settleTimer);
    }

    /** Where people read: the upper third of the stage. */
    function readingLine() {
      return scroller.scrollTop + scroller.clientHeight / 3;
    }

    /** The last page whose top is above the reading line. */
    function current() {
      var line = readingLine();
      var low = 0;
      var high = frames.length - 1;
      while (low < high) {
        var middle = (low + high + 1) >> 1;
        if (frames[middle].element.offsetTop <= line) {
          low = middle;
        } else {
          high = middle - 1;
        }
      }
      return low;
    }

    return {
      element: scroller,
      focus: scroller,
      show: function (index, box) {
        var frame = frames[index];
        var top = box ? frame.element.offsetTop + box.y * scale - scroller.clientHeight / 4
          : index === 0 ? 0
            : frame.element.offsetTop - GUTTER / 2;
        top = Math.max(0, Math.min(top, scroller.scrollHeight - scroller.clientHeight));
        if (Math.abs(top - scroller.scrollTop) >= 1) {
          navigating = true;
          clearTimeout(settleTimer);
          settleTimer = setTimeout(settle, SCROLL_SETTLE_MS);
          scroller.scrollTo({ top: top, behavior: motion() });
        }
        if (box) {
          frame.spotlight(box);
        }
        return (top + scroller.clientHeight / 3 - frame.element.offsetTop) / scale;
      },
      rescale: function () {
        // Keep the same spot of the page in view across the size change.
        var anchor = frames[current()].element;
        var within = (scroller.scrollTop - anchor.offsetTop) / (anchor.offsetHeight || 1);
        scale = ctx.fit(widest, tallest, scroller);
        frames.forEach(function (frame) { frame.size(scale); });
        scroller.scrollTop = anchor.offsetTop + within * anchor.offsetHeight;
      },
      scale: function () { return scale; }
    };
  }

  /** One slide at a time with speaker notes and a full-screen slideshow. */
  function deckLayout(ctx) {
    var root = el('div', 'av-deck');
    var stage = singlePartStage(ctx);
    root.appendChild(stage.element);
    var notes = null;
    if (ctx.spec.notes) {
      var pane = el('section', 'av-notes');
      pane.setAttribute('aria-label', 'Speaker notes');
      notes = el('p', 'av-notes-text');
      pane.append(el('h2', 'av-notes-title', 'Notes'), notes);
      root.appendChild(pane);
    }
    var slideshow = iconButton('slideshow', 'Start slideshow', function () {
      if (stage.element.requestFullscreen) {
        stage.element.requestFullscreen().catch(function () {});
      }
    });
    stage.element.addEventListener('click', function () {
      if (presenting()) {
        ctx.go(stage.index() + 1);
      }
    });
    document.addEventListener('fullscreenchange', function () {
      stage.fit(presentingZoom());
    });

    function presenting() {
      return document.fullscreenElement === stage.element;
    }

    function presentingZoom() {
      return presenting() ? 'fit-page' : undefined;
    }

    return {
      element: root,
      focus: stage.element,
      tools: [slideshow],
      show: function (index, box) {
        stage.show(index, box, presentingZoom());
        if (notes) {
          var text = ctx.spec.notes(ctx.parts[index]);
          notes.textContent = text || 'No notes';
          notes.classList.toggle('av-quiet', !text);
        }
      },
      rescale: function () { stage.fit(presentingZoom()); },
      scale: stage.scale,
      keydown: function (event) {
        var index = stage.index();
        switch (event.key) {
          case 'ArrowLeft':
          case 'ArrowUp':
          case 'PageUp':
            ctx.go(index - 1);
            return true;
          case 'ArrowRight':
          case 'ArrowDown':
          case 'PageDown':
            ctx.go(index + 1);
            return true;
          case ' ':
            if (event.target.closest('button, a')) {
              return false;
            }
            ctx.go(index + 1);
            return true;
          case 'Home':
            ctx.go(0);
            return true;
          case 'End':
            ctx.go(ctx.parts.length - 1);
            return true;
          default:
            return false;
        }
      }
    };
  }

  /** One part at a time behind a tab strip, like the sheets of a workbook. */
  function tabsLayout(ctx) {
    var root = el('div', 'av-tabs');
    var stage = singlePartStage(ctx);
    stage.element.setAttribute('role', 'tabpanel');
    var strip = el('div', 'av-tabstrip');
    strip.setAttribute('role', 'tablist');
    strip.setAttribute('aria-label', capitalize(plural(ctx.spec.noun)));
    var tabs = ctx.parts.map(function (part, index) {
      var tab = el('button', 'av-tab', part.label);
      tab.type = 'button';
      tab.id = 'av-tab-' + index;
      tab.setAttribute('role', 'tab');
      tab.addEventListener('click', function () { ctx.go(index); });
      strip.appendChild(tab);
      return tab;
    });
    if (ctx.total > tabs.length) {
      strip.appendChild(el('span', 'av-tab-more', omittedText(ctx)));
    }
    strip.addEventListener('keydown', function (event) {
      var index = stage.index();
      var next = event.key === 'ArrowLeft' ? index - 1
        : event.key === 'ArrowRight' ? index + 1
          : event.key === 'Home' ? 0
            : event.key === 'End' ? tabs.length - 1
              : -1;
      if (next < 0 || next >= tabs.length) {
        return;
      }
      event.preventDefault();
      ctx.go(next);
      tabs[next].focus();
    });
    root.append(stage.element, strip);
    var selected = -1;

    return {
      element: root,
      focus: stage.element,
      show: function (index, box) {
        stage.show(index, box);
        if (selected >= 0) {
          tabs[selected].setAttribute('aria-selected', 'false');
          tabs[selected].tabIndex = -1;
        }
        selected = index;
        tabs[index].setAttribute('aria-selected', 'true');
        tabs[index].tabIndex = 0;
        stage.element.setAttribute('aria-labelledby', tabs[index].id);
        reveal(strip, tabs[index]);
      },
      rescale: function () { stage.fit(); },
      scale: stage.scale
    };
  }

  /** A scrolling viewport that shows one part at a time, centered. */
  function singlePartStage(ctx) {
    var viewport = scrollerElement();
    var slot = el('div', 'av-slot');
    viewport.appendChild(slot);
    var frames = [];
    var index = -1;
    var scale = 1;

    function fit(zoom) {
      var frame = frames[index];
      if (!frame) {
        return;
      }
      scale = frame.part.kind === 'image'
        ? ctx.fit(frame.part.width, frame.part.height, viewport, zoom)
        : 1;
      frame.size(scale);
    }

    return {
      element: viewport,
      index: function () { return index; },
      scale: function () { return scale; },
      fit: fit,
      show: function (next, box, zoom) {
        index = next;
        var frame = frames[next] || (frames[next] = partFrame(ctx, ctx.parts[next]));
        viewport.setAttribute('data-kind', frame.part.kind);
        slot.replaceChildren(frame.element);
        fit(zoom);
        viewport.scrollTo(box
          ? {
            left: frame.element.offsetLeft + box.x * scale - viewport.clientWidth / 4,
            top: frame.element.offsetTop + box.y * scale - viewport.clientHeight / 4
          }
          : { left: 0, top: 0 });
        if (box) {
          frame.spotlight(box);
        }
      }
    };
  }

  /** One rendered part: an image sized by the zoom, or a product HTML document. */
  function partFrame(ctx, part) {
    var element = el('section', 'av-part');
    element.setAttribute('data-part-id', part.id);
    element.setAttribute('data-kind', part.kind);
    element.setAttribute('aria-label', part.label);
    var media;
    if (part.kind === 'image') {
      media = el('img', 'av-part-media');
      media.alt = part.label;
      media.loading = 'lazy';
      media.decoding = 'async';
      media.draggable = false;
    } else {
      media = el('iframe', 'av-part-media');
      media.title = part.label;
    }
    media.src = ctx.url(part);
    element.appendChild(media);
    return {
      element: element,
      part: part,
      size: function (scale) {
        if (part.kind === 'image') {
          element.style.width = Math.round(part.width * scale) + 'px';
          element.style.height = Math.round(part.height * scale) + 'px';
        }
      },
      /** Briefly marks an element box; boxes are in the part's CSS pixels. */
      spotlight: function (box) {
        var mark = el('div', 'av-spotlight');
        mark.style.left = percent(box.x, part.width);
        mark.style.top = percent(box.y, part.height);
        mark.style.width = percent(box.width, part.width);
        mark.style.height = percent(box.height, part.height);
        element.appendChild(mark);
        setTimeout(function () { mark.remove(); }, SPOTLIGHT_MS);
      }
    };
  }

  function scrollerElement() {
    var scroller = el('div', 'av-scroller');
    scroller.tabIndex = 0;
    return scroller;
  }

  function fitScale(zoom, width, height, viewport) {
    if (typeof zoom === 'number') {
      return zoom;
    }
    var byWidth = (viewport.clientWidth - 2 * GUTTER) / width;
    var byPage = Math.min(byWidth, (viewport.clientHeight - 2 * GUTTER) / height);
    var scale = zoom === 'fit-page' ? byPage
      : zoom === 'fit-width' ? byWidth
        : Math.min(byWidth, AUTO_ZOOM_LIMIT);
    return Math.max(0.1, scale);
  }

  // ---- Sidebar ------------------------------------------------------------

  function createSidebar(ctx) {
    var content = (ctx.spec.sidebar === 'outline' && outline(ctx)) || thumbnails(ctx);
    var element = el('nav', 'av-sidebar');
    element.id = 'av-sidebar';
    element.setAttribute('aria-label', content.title);
    element.append(el('h2', 'av-sidebar-title', content.title), content.list);
    return { element: element, title: content.title, select: content.select };
  }

  function thumbnails(ctx) {
    var list = el('ol', 'av-sidebar-list av-thumbs');
    var buttons = ctx.parts.map(function (part, index) {
      var button = el('button', 'av-thumb');
      button.type = 'button';
      button.title = partName(ctx, index);
      button.setAttribute('data-hidden', String(Boolean(part.hidden)));
      if (part.kind === 'image') {
        var image = el('img', 'av-thumb-image');
        image.alt = '';
        image.loading = 'lazy';
        image.decoding = 'async';
        image.width = part.width;
        image.height = part.height;
        image.src = ctx.url(part);
        button.appendChild(image);
      }
      button.appendChild(el('span', 'av-thumb-number', index + 1));
      if (part.hidden) {
        button.appendChild(el('span', 'av-hidden-badge', 'Hidden'));
      }
      button.addEventListener('click', function () { ctx.go(index); });
      var item = el('li');
      item.appendChild(button);
      list.appendChild(item);
      return button;
    });
    if (ctx.total > buttons.length) {
      list.appendChild(el('li', 'av-omitted', omittedText(ctx)));
    }
    return {
      title: capitalize(plural(ctx.spec.noun)),
      list: list,
      select: marker(list, buttons)
    };
  }

  /** The headings the product placed on its parts, or null when there are none. */
  function outline(ctx) {
    var entries = [];
    ctx.parts.forEach(function (part, index) {
      var elements = part.elements || [];
      elements.forEach(function (element, position) {
        if (element.kind !== 'heading' || !element.label) {
          return;
        }
        var last = entries[entries.length - 1];
        var continued = position === 0 && last && last.index === index - 1 && last.lastOnPart
          && last.element.digest === element.digest;
        if (!continued) {
          entries.push({ index: index, element: element, lastOnPart: position === elements.length - 1 });
        }
      });
    });
    if (entries.length === 0) {
      return null;
    }
    var list = el('ol', 'av-sidebar-list');
    var buttons = entries.map(function (entry) {
      var button = el('button', 'av-outline-item', entry.element.label);
      button.type = 'button';
      button.title = entry.element.label;
      button.setAttribute('data-level', String(Math.min(Math.max(entry.element.level || 1, 1), 6)));
      button.addEventListener('click', function () { ctx.go(entry.index, entry.element.box); });
      var item = el('li');
      item.appendChild(button);
      list.appendChild(item);
      return button;
    });
    var mark = marker(list, buttons);

    function above(entry, index, line) {
      return entry.index < index || (entry.index === index && entry.element.box.y <= line);
    }

    return {
      title: 'Outline',
      list: list,
      select: function (index, line) {
        // The current heading is the last one that starts above the reading line.
        var current = -1;
        while (current + 1 < entries.length && above(entries[current + 1], index, line)) {
          current++;
        }
        mark(current);
      }
    };
  }

  /** Marks one item of a list as current and keeps it in view. */
  function marker(list, items) {
    var current = -1;
    return function (index) {
      if (index === current) {
        return;
      }
      if (current >= 0) {
        items[current].removeAttribute('aria-current');
      }
      current = index;
      if (index >= 0) {
        items[index].setAttribute('aria-current', 'true');
        reveal(list, items[index]);
      }
    };
  }

  // ---- Review panel ---------------------------------------------------------

  function reviewPanel(ctx, review, onClose) {
    var panel = el('aside', 'av-panel');
    panel.id = 'av-review';
    panel.setAttribute('aria-label', 'Review');
    var header = el('div', 'av-panel-header');
    header.append(el('h2', 'av-panel-title', 'Review'), iconButton('close', 'Close review', onClose));
    var body = el('div', 'av-panel-body');
    if (review.visualInspectionRequired) {
      var callout = el('div', 'av-callout');
      callout.append(
        el('strong', null, 'Visual inspection required'),
        el('span', null, 'Automated checks cannot judge appearance. Look at every '
          + ctx.spec.noun.toLowerCase() + ' before accepting the output.'));
      body.appendChild(callout);
    }
    body.append(coverageSection(ctx, review.coverage), findingsSection(ctx, review.findings));
    if (review.warnings && review.warnings.length) {
      body.appendChild(warningsSection(review.warnings));
    }
    var link = el('a', 'av-panel-link', 'Open review.json');
    link.href = 'review.json';
    body.appendChild(link);
    panel.append(header, body);
    return panel;
  }

  function coverageSection(ctx, coverage) {
    var section = el('section');
    section.append(
      el('h3', 'av-section-title', 'Coverage'),
      el('p', 'av-coverage', coverage.renderedItems + ' of ' + coverage.expectedItems + ' '
        + plural(ctx.spec.noun) + ' rendered' + (coverage.complete ? '' : ' \u00b7 incomplete')));
    if (coverage.metrics.length) {
      var metrics = el('dl', 'av-metrics');
      coverage.metrics.forEach(function (metric) {
        metrics.append(
          el('dt', null, metric.name),
          el('dd', null, metric.value + (metric.unit ? ' ' + metric.unit : '')));
      });
      section.appendChild(metrics);
    }
    return section;
  }

  function findingsSection(ctx, findings) {
    var section = el('section');
    section.appendChild(el('h3', 'av-section-title', 'Findings (' + findings.length + ')'));
    if (!findings.length) {
      section.appendChild(el('p', 'av-quiet', 'No automated findings.'));
      return section;
    }
    var partByUrl = Object.create(null);
    ctx.parts.forEach(function (part, index) { partByUrl[ctx.url(part)] = index; });
    var list = el('ul', 'av-findings');
    findings.forEach(function (finding) {
      var item = el('li', 'av-finding');
      item.setAttribute('data-severity', finding.severity);
      var head = el('div', 'av-finding-head');
      head.append(el('span', 'av-severity', finding.severity), el('code', 'av-code', finding.code));
      item.append(head, el('p', null, finding.message));
      if (finding.location) {
        item.appendChild(el('p', 'av-quiet', finding.location));
      }
      if (finding.hint) {
        item.appendChild(el('p', 'av-quiet', 'Fix: ' + finding.hint));
      }
      // Evidence that names exactly one part can take the reader there.
      var targets = (finding.evidence || []).filter(function (path) { return path in partByUrl; });
      if (targets.length === 1) {
        var index = partByUrl[targets[0]];
        item.appendChild(textButton('av-evidence', 'Show ' + partName(ctx, index), function () { ctx.go(index); }));
      }
      list.appendChild(item);
    });
    section.appendChild(list);
    return section;
  }

  function warningsSection(warnings) {
    var section = el('section');
    section.appendChild(el('h3', 'av-section-title', 'Warnings (' + warnings.length + ')'));
    var list = el('ul', 'av-findings');
    warnings.forEach(function (warning) {
      var item = el('li', 'av-finding');
      item.setAttribute('data-severity', 'warning');
      item.append(el('code', 'av-code', warning.code), el('p', null, warning.message));
      list.appendChild(item);
    });
    section.appendChild(list);
    return section;
  }

  // ---- Chrome ---------------------------------------------------------------

  function topBar(doc, presenter) {
    var bar = el('header', 'av-topbar');
    var brand = el('div', 'av-brand', presenter.glyph);
    brand.setAttribute('aria-hidden', 'true');
    var title = el('div', 'av-title');
    var file = el('h1', 'av-file', doc.file);
    file.title = doc.file;
    title.append(el('div', 'av-kind', presenter.kind), file);
    var badges = el('div', 'av-badges');
    if (doc.license === 'evaluation') {
      var evaluation = el('span', 'av-badge av-badge-evaluation', 'Evaluation');
      evaluation.title = 'Rendered in evaluation mode: parts carry an Aspose evaluation watermark.';
      badges.appendChild(evaluation);
    }
    if (doc.review) {
      var snapshot = el('span', 'av-badge av-badge-snapshot', 'Review snapshot');
      snapshot.title = 'Static evidence written by aspose-cli review.';
      badges.appendChild(snapshot);
    }
    bar.append(brand, title, badges);
    return bar;
  }

  // ---- Helpers --------------------------------------------------------------

  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) {
      node.className = className;
    }
    if (text !== undefined && text !== null) {
      node.textContent = String(text);
    }
    return node;
  }

  function icon(name) {
    var svg = document.createElementNS(SVG_NS, 'svg');
    svg.setAttribute('class', 'av-icon');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('aria-hidden', 'true');
    var path = document.createElementNS(SVG_NS, 'path');
    path.setAttribute('d', ICONS[name]);
    svg.appendChild(path);
    return svg;
  }

  function iconButton(name, label, onClick) {
    var button = el('button', 'av-button');
    button.type = 'button';
    button.title = label;
    button.setAttribute('aria-label', label);
    button.appendChild(icon(name));
    button.addEventListener('click', onClick);
    return button;
  }

  function textButton(className, text, onClick) {
    var button = el('button', 'av-button ' + className, text);
    button.type = 'button';
    button.addEventListener('click', onClick);
    return button;
  }

  function separator() {
    var line = el('span', 'av-separator');
    line.setAttribute('aria-hidden', 'true');
    return line;
  }

  /** Scrolls a positioned container just enough to show one of its items. */
  function reveal(container, item) {
    if (item.offsetTop < container.scrollTop) {
      container.scrollTop = item.offsetTop;
    } else if (item.offsetTop + item.offsetHeight > container.scrollTop + container.clientHeight) {
      container.scrollTop = item.offsetTop + item.offsetHeight - container.clientHeight;
    }
    if (item.offsetLeft < container.scrollLeft) {
      container.scrollLeft = item.offsetLeft;
    } else if (item.offsetLeft + item.offsetWidth > container.scrollLeft + container.clientWidth) {
      container.scrollLeft = item.offsetLeft + item.offsetWidth - container.clientWidth;
    }
  }

  function partName(ctx, index) {
    var name = ctx.spec.noun + ' ' + (index + 1);
    var label = ctx.parts[index].label;
    return label && label !== name ? name + ': ' + label : name;
  }

  function omittedText(ctx) {
    var count = ctx.total - ctx.parts.length;
    return count + ' more ' + (count === 1 ? ctx.spec.noun.toLowerCase() : plural(ctx.spec.noun)) + ' not rendered';
  }

  function plural(noun) {
    return noun.toLowerCase() + 's';
  }

  function capitalize(text) {
    return text.charAt(0).toUpperCase() + text.slice(1);
  }

  function percent(value, total) {
    return (value / total * 100) + '%';
  }

  function baseName(path) {
    return String(path).split(/[\\/]/).pop();
  }

  function motion() {
    return reducedMotion.matches ? 'auto' : 'smooth';
  }

  function isTyping(target) {
    return target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName);
  }

  window.AsposeViewer = { definePresenter: definePresenter, start: start };
})();
