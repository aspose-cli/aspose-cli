// Live-reload client of the aspose-cli preview server. Served at /live/client.js
// and injected into every rendered page next to an inline metadata pin
// (window.__asposePreview: revision, view, file and eval flag). Browser
// primitives only, no dependencies.
//
// View-state restore notes — how the Aspose.Cells single-file HTML export
// switches sheets (probed on 26.6 by dumping a two-sheet workbook served by
// the managed preview lifecycle):
//   - Every worksheet renders as <div id='table_N' sheetName='...'> inside
//     <div id='section'>, in workbook order. Nothing is hidden up front: all
//     containers start visible, and the export emits no tab UI of its own
//     (the shell's tab strip below is this client's, driven through
//     activateSheet).
//   - A <head> script declares the globals `activeSheetIndex` (initially 0)
//     and `fnSetActiveSheet(iSh)`, which flips the containers' inline
//     `style.display` between "block" and "none" — that inline style is the
//     single source of truth for which sheet is showing.
//   - refresh() swaps only the <body> (head styles are replaced, head scripts
//     never re-run), so `fnSetActiveSheet` and `activeSheetIndex` survive a
//     swap while the fresh body arrives with every container visible again.
//     Restoring therefore re-applies the inline display state directly rather
//     than calling fnSetActiveSheet (whose early return on an unchanged index
//     would skip the hiding), and then re-points window.activeSheetIndex at
//     the restored container so later switcher calls toggle the right divs.
//   - The active sheet is captured as the sheetName of the first container
//     whose inline display is not "none" while at least one sibling is
//     hidden; when every container is visible (the pristine state) there is
//     no selection to restore. A sheet missing from the new body (renamed or
//     deleted) restores nothing. On the first shell build, the
//     aspose-active-sheet meta marker stamped by PreviewExporter selects the
//     workbook's saved ActiveSheetIndex instead of accepting the export
//     script's hard-coded zero.
//
// The Excel-style shell: buildShell wraps every served document in view
// chrome — a top bar (file name, connection dot, revision, evaluation
// badge), a formula bar, the scroll host that adopts the original body
// children, a self-drawn sheet tab strip and a status bar — styled by
// /live/shell.css, which the server links into every composed page (no
// link means no shell, so a page without the stylesheet stays the bare
// document). Each refresh swaps the whole body and discards the shell with
// it, so applyDocument rebuilds the shell around every fresh document, and
// all shell interactions ride document-level event delegation instead of
// listeners on swapped nodes. The chrome contains no <table> on purpose:
// resolveTargetTable falls back to the body's first table for bare
// single-sheet exports, and a table in the chrome would hijack that
// fallback.
//
// Cell addressing: the engine exports with CellNameAttribute switched on
// (alongside row/column headers and gridlines), so every data <td> carries
// its sheet-relative A1 address as data-cell='A1'. The focus spotlight
// depends on that attribute to locate cells; collectCells documents the
// exact contract.
//
// Revision bookkeeping: two counters, because failed renders burn revision
// numbers. `currentRevision` is the content on screen (badge text, refresh
// decisions); `streamRevision` is the last revision any event reported —
// status events announce the numbers of failed attempts, so the update that
// follows a recovery stays contiguous and keeps its in-place refresh (a
// genuine hole still falls back to a full reload).
//
// Focus spotlights: a `focus` event (sent after the update of the same
// revision) lists the sheet/range targets an edit touched. A focus for
// content still being fetched is parked until the refresh lands its
// revision; applying switches to the first target's sheet, overlays fading
// selection boxes on the located cells and centers the first one, and any
// locating failure degrades to a sheet jump plus a floating label. Under
// window.__asposePreviewFx === "demo" a synthetic cursor flies in first.
(function () {
  'use strict';

  var CLIENT_SCRIPT_SUFFIX = '/live/client.js';
  // Sheet containers of the Aspose single-file export (see the notes above).
  // The attribute is authored as sheetName; the HTML parser lower-cases it.
  var SHEET_CONTAINER_SELECTOR = "div[id^='table_'][sheetname]";
  var SHEET_ID_PREFIX = 'table_';
  var BADGE_ATTRIBUTE = 'data-aspose-preview-badge';
  var FX_STYLE_ID = 'aspose-preview-fx-style';
  // The spotlight green, the generic spreadsheet-selection visual; the same
  // brand green the shell stylesheet builds its chrome from.
  var FOCUS_COLOR = '#1f8a52';
  // The translucent fill of sheet-level flashes; the formula-bar selection
  // box carries the same fill through shell.css.
  var FOCUS_FILL = 'rgba(31,138,82,0.10)';
  // The scroll host's id doubles as the shell's already-built check and as
  // the key under which the existing per-element scroll capture/restore
  // carries the main scroll position across body swaps.
  var SCROLL_HOST_ID = 'aspose-scroll-host';
  // The stylesheet link the server injects only while /live/shell.css is
  // being served. Without it the shell would render as unstyled text mixed
  // into the document, so its absence disables the shell entirely.
  var SHELL_STYLESHEET_SELECTOR = 'link[href="/live/shell.css"]';
  var ACTIVE_SHEET_META_SELECTOR = 'meta[name="aspose-active-sheet"]';

  // The revision shown on screen (or optimistically being fetched), seeded by
  // the metadata pin the server injected into this very page.
  var currentRevision = typeof previewMeta().revision === 'number'
    ? previewMeta().revision
    : 0;
  // The last revision any event reported, failed attempts included (see the
  // revision bookkeeping notes above).
  var streamRevision = currentRevision;
  var refreshing = false;
  var refreshQueued = false;
  var badge = null;
  // Focus payload waiting for the refresh that lands its revision.
  var pendingFocus = null;
  // Live focus visuals (overlay nodes, timers, the demo cursor's rAF handle),
  // registered so the next update or body swap can drop them all at once.
  var focusFx = { nodes: [], timers: [], frame: 0 };
  // The formula-bar selection: the sheet/address anchor plus the persistent
  // overlay node. The anchor outlives a body swap (restoreViewState
  // re-locates the cell in the fresh document); the node dies with the swap.
  // Deliberately not part of focusFx — spotlights come and go around a
  // standing selection without tearing it down.
  var selection = null;
  var selectionNode = null;
  // Shell indicator state, kept outside the DOM because the shell is torn
  // down and rebuilt from scratch around every swapped-in body.
  var connectionState = 'live';
  var lastRenderMs = null;
  // Spreadsheet scale selection survives in-place live refreshes because
  // this client instance outlives every body swap.
  var scaleMode = '100';

  var runtime = window.AsposePreviewRuntime;

  runtime.events.onopen = function () {
    // Fires on the first connection and again after every automatic
    // reconnect; the shell's connection indicators track it.
    setConnectionState('live');
  };

  runtime.events.addEventListener('hello', function (event) {
    var payload = runtime.parse(event);
    if (!payload || typeof payload.revision !== 'number') {
      return;
    }
    // The server may have re-rendered while this client was disconnected
    // (first connect, or an automatic reconnect): converge on its revision.
    // hello carries the served snapshot's revision, a content revision.
    if (payload.revision > streamRevision) {
      streamRevision = payload.revision;
    }
    if (payload.revision > currentRevision) {
      currentRevision = payload.revision;
      refresh();
    }
  });

  runtime.events.addEventListener('update', function (event) {
    // Fresh content is on its way; any "showing last good" notice is stale,
    // and focus overlays point at cells the coming swap will replace.
    hideBadge();
    clearFocusFx();
    var payload = runtime.parse(event);
    if (!payload || typeof payload.revision !== 'number') {
      window.location.reload();
      return;
    }
    // The render duration rides the update payload; an update without one
    // leaves the status bar's timing slot empty rather than showing a
    // number that belongs to an earlier render.
    lastRenderMs = typeof payload.renderMs === 'number' ? payload.renderMs : null;
    syncShellIndicators();
    if (payload.revision === streamRevision + 1 || payload.revision === streamRevision) {
      // Contiguous with the event stream. An update at or below the content
      // revision is the echo of content an earlier refresh already fetched
      // (its pin ran ahead of the events), so only newer content refreshes.
      streamRevision = payload.revision;
      if (payload.revision > currentRevision) {
        currentRevision = payload.revision;
        refresh();
      }
    } else {
      // A hole (or reordering) in the revision sequence means missed events;
      // a full page load is the reliable way back in sync.
      window.location.reload();
    }
  });

  runtime.events.addEventListener('status', function (event) {
    // A render problem keeps the last good revision on screen; the badge
    // says so, and the console carries the full status payload.
    var payload = runtime.parse(event);
    if (payload && typeof payload.revision === 'number' && payload.revision > streamRevision) {
      // The failed attempt burnt this revision number. Adopting it keeps the
      // recovery update contiguous, so it refreshes in place instead of
      // falling back to a state-losing full reload.
      streamRevision = payload.revision;
    }
    if (payload && payload.state === 'error') {
      showBadge('showing last good (rev ' + currentRevision + ') — '
        + (typeof payload.code === 'string' ? payload.code : 'ERROR'));
    } else {
      hideBadge();
    }
    if (window.console && window.console.warn) {
      window.console.warn('preview status: ' + event.data);
    }
  });

  runtime.events.addEventListener('focus', function (event) {
    // Sent after the update of the same revision: spotlight what it touched.
    var payload = runtime.parse(event);
    if (!payload || typeof payload.revision !== 'number'
        || !Array.isArray(payload.targets) || payload.targets.length === 0) {
      return;
    }
    if (payload.revision < currentRevision) return; // Stale; already moved past.
    if (payload.revision > currentRevision || refreshing || refreshQueued) {
      // The matching content is not on screen yet (its update normally lands
      // first and starts a refresh); park it until that refresh completes.
      pendingFocus = payload;
      return;
    }
    applyFocus(payload);
  });

  runtime.events.onerror = function () {
    // EventSource reconnects on its own; the hello event of the next
    // connection resynchronizes the revision, so no reload happens here —
    // the shell's connection indicators are the only visible change.
    setConnectionState('reconnecting');
  };

  // The shell wraps the very first document too. The client script is
  // injected at the end of the body with `defer`, so the DOM is normally
  // complete by the time this runs; the readiness check covers any other
  // injection point.
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { buildShell(); });
  } else {
    buildShell();
  }

  // Every shell interaction rides document-level delegation: each refresh
  // replaces the whole body (shell included), so listeners must live above
  // the swap. Registered once for the page's lifetime.
  document.addEventListener('click', function (event) {
    var target = event.target;
    if (!target || typeof target.closest !== 'function') return;
    var tab = target.closest('.aspose-shell-tab');
    if (tab) {
      activateSheet(tab.getAttribute('data-aspose-sheet'));
      return;
    }
    var fit = target.closest('.aspose-shell-fit');
    if (fit) {
      setScaleMode(fit.getAttribute('data-aspose-fit'));
      return;
    }
    // Any addressed cell of the export can anchor the formula bar; the
    // chrome contains no <td>, so this can never match shell nodes.
    var cell = target.closest('td[data-cell]');
    if (cell) selectCell(cell);
  });

  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') clearSelection();
  });

  window.addEventListener('resize', function () { applyScaleMode(); });

  // Fetch the current document and swap it into the live page without a
  // navigation, carrying the sheet selection and scroll positions across the
  // swap. Anything unexpected falls back to a full reload.
  function refresh() {
    if (refreshing) {
      // Coalesce: one more round after the in-flight one covers every
      // revision accepted in the meantime.
      refreshQueued = true;
      return;
    }
    refreshing = true;
    setShellBusy(true);
    runtime.fetchDocument(currentRevision)
      .then(function (next) {
        // Captured at swap time, not at fetch time: the user may have
        // scrolled or switched sheets while the fetch was in flight.
        var viewState = captureViewState();
        applyDocument(next);
        restoreViewState(viewState);
        refreshing = false;
        setShellBusy(false);
        if (refreshQueued) {
          refreshQueued = false;
          refresh();
        } else {
          deliverPendingFocus();
        }
      })
      .catch(function () {
        window.location.reload();
      });
  }

  function applyDocument(next) {
    if (!next || !next.body) {
      throw new Error('preview refresh produced an empty document');
    }
    // The swap invalidates every located cell; drop the focus visuals first.
    clearFocusFx();

    // Swap the style-bearing head nodes; the rest of the head is static.
    // The adopted shell stylesheet is exempt (see adoptShellStylesheet):
    // removing it would unstyle the chrome for the rest of the swap.
    var staleStyles = document.head.querySelectorAll('style, link[rel="stylesheet"]');
    var freshStyles = next.head ? next.head.querySelectorAll('style, link[rel="stylesheet"]') : [];
    var i;
    for (i = 0; i < staleStyles.length; i += 1) {
      if (staleStyles[i].matches(SHELL_STYLESHEET_SELECTOR)) continue;
      staleStyles[i].parentNode.removeChild(staleStyles[i]);
    }
    for (i = 0; i < freshStyles.length; i += 1) {
      document.head.appendChild(document.importNode(freshStyles[i], true));
    }

    // Swap the whole body, then force its scripts to run: markup-injected
    // scripts are inert, but the rendered document needs its own scripts
    // (e.g. the sheet tab switcher), so each one is rebuilt as a fresh,
    // executable element. The live client itself is skipped — this very
    // code is already running.
    document.documentElement.replaceChild(document.importNode(next.body, true), document.body);
    reviveScripts(document.body);

    // Prefer the metadata pin the server injected into the fetched page
    // (its inline script re-executed just above): the response may already
    // be newer than the update event that triggered this refresh.
    if (typeof previewMeta().revision === 'number') {
      currentRevision = Math.max(currentRevision, previewMeta().revision);
    }

    // The swap discarded the previous shell together with the old body.
    // Rebuild it around the fresh content right here: restoreViewState runs
    // next and looks the scroll host up by id, so the host must already
    // exist, and reviveScripts has already re-executed the metadata pin, so
    // the shell reads the new page's values.
    buildShell();
  }

  // What the user is looking at, captured right before the body swap: the
  // selected sheet, the formula-bar selection, the window scroll, and
  // per-element scroll positions (keyed by id — the only handle that
  // survives the swap; the shell's scroll host is one of those elements,
  // which is how the main scroll position crosses the swap now that the
  // body itself is clipped).
  function captureViewState() {
    var state = {
      activeSheet: null,
      scaleMode: scaleMode,
      // The selection anchor is safe to capture by reference: selections
      // are replaced wholesale, never mutated in place.
      selection: selection,
      scrollX: window.pageXOffset || 0,
      scrollY: window.pageYOffset || 0,
      scrolled: []
    };

    var containers = document.querySelectorAll(SHEET_CONTAINER_SELECTOR);
    var firstVisible = null;
    var anyHidden = false;
    var i;
    for (i = 0; i < containers.length; i += 1) {
      if (containers[i].style.display === 'none') {
        anyHidden = true;
      } else if (!firstVisible) {
        firstVisible = containers[i];
      }
    }
    // Only a page where some container is hidden carries a selection; the
    // pristine export shows every sheet and needs no restore.
    if (anyHidden && firstVisible) {
      state.activeSheet = firstVisible.getAttribute('sheetname');
    }

    var candidates = document.body ? document.body.querySelectorAll('[id]') : [];
    for (i = 0; i < candidates.length; i += 1) {
      if (candidates[i].scrollTop || candidates[i].scrollLeft) {
        state.scrolled.push({
          id: candidates[i].id,
          top: candidates[i].scrollTop,
          left: candidates[i].scrollLeft
        });
      }
    }
    return state;
  }

  function restoreViewState(state) {
    if (state.activeSheet !== null) {
      // A missing sheet (renamed or deleted) restores nothing rather than
      // guessing at a replacement.
      activateSheet(state.activeSheet);
    }

    setScaleMode(state.scaleMode || '100');

    for (var s = 0; s < state.scrolled.length; s += 1) {
      var element = document.getElementById(state.scrolled[s].id);
      if (element) {
        element.scrollTop = state.scrolled[s].top;
        element.scrollLeft = state.scrolled[s].left;
      }
    }
    window.scrollTo(state.scrollX, state.scrollY);

    // Last, so the selected cell is measured with sheet visibility already
    // settled. (The overlay's coordinates are content-space, so the scroll
    // restores above do not affect it either way.)
    restoreSelection(state.selection);
  }

  function sheetContainerByName(name) {
    var containers = document.querySelectorAll(SHEET_CONTAINER_SELECTOR);
    for (var i = 0; i < containers.length; i += 1) {
      if (containers[i].getAttribute('sheetname') === name) return containers[i];
    }
    return null;
  }

  // Show the named sheet by re-applying the containers' inline display state
  // (see the view-state notes up top) and re-pointing the export's own
  // head-scoped activeSheetIndex, or the next fnSetActiveSheet call would
  // toggle the wrong divs. Shared by restore, the focus spotlight and the
  // shell's tab strip.
  function activateSheet(name) {
    var target = sheetContainerByName(name);
    if (!target) return null;
    var containers = document.querySelectorAll(SHEET_CONTAINER_SELECTOR);
    for (var i = 0; i < containers.length; i += 1) {
      containers[i].style.display = containers[i] === target ? 'block' : 'none';
    }
    var index = parseInt(target.id.slice(SHEET_ID_PREFIX.length), 10);
    if (!isNaN(index) && typeof window.activeSheetIndex === 'number') {
      window.activeSheetIndex = index;
    }
    // A selection anchored on another sheet would leave its overlay floating
    // over the newly shown one — the box lives in the scroll host, which the
    // containers' display toggling does not hide — so drop it here.
    if (selection && selection.sheet !== name) clearSelection();
    // Every sheet switch funnels through here — tab clicks, view-state
    // restore and the focus spotlight — so this is the one place that keeps
    // the tab strip's highlight in step with the visible sheet.
    syncTabHighlight(name);
    applyScaleMode();
    return target;
  }

  function savedActiveSheet() {
    var marker = document.head
      ? document.head.querySelector(ACTIVE_SHEET_META_SELECTOR)
      : null;
    return marker ? marker.getAttribute('content') : null;
  }

  // The metadata pin the server injects into every served page (revision,
  // view, file, eval). Read through this helper — never cached — because a
  // body swap re-executes the inline pin script, replacing the object
  // wholesale; a page without the pin reads as an empty object.
  function previewMeta() {
    var meta = window.__asposePreview;
    return meta && typeof meta === 'object' ? meta : {};
  }

  // Wrap the served document in the Excel-style shell: a top bar (file
  // name, connection dot, revision, evaluation badge), a formula bar, the
  // scroll host that adopts every original body child, a self-drawn sheet
  // tab strip and a status bar. The sheet view keeps only the top bar, the
  // (dark, centering) host and the status bar. The chrome is built from
  // Keep the shell stylesheet in the head, across body swaps. The server
  // injects the <link> before </body>, so a swap would replace it with a
  // fresh copy whose sheet applies asynchronously — and every overlay or
  // scroll-parent decision made in that window sees pre-stylesheet layout
  // (the restored selection box landed 27px off exactly this way). Moving
  // the already-loaded node keeps its sheet applied without a reload; later
  // body copies are dropped as duplicates, and applyDocument's head-style
  // swap leaves the adopted link alone.
  function adoptShellStylesheet() {
    var links = document.querySelectorAll(SHELL_STYLESHEET_SELECTOR);
    for (var i = 0; i < links.length; i += 1) {
      if (links[i].parentNode === document.head) continue;
      if (document.head.querySelector(SHELL_STYLESHEET_SELECTOR)) {
        links[i].parentNode.removeChild(links[i]);
      } else {
        document.head.appendChild(links[i]);
      }
    }
  }

  // div/span/header/footer exclusively — never <table> — because
  // resolveTargetTable's bare-export fallback grabs the body's first table
  // and must keep finding the document's own. Safe to call twice on the
  // same body: the host's id marks a built shell.
  function buildShell() {
    if (!document.body || document.getElementById(SCROLL_HOST_ID)) return;
    // No stylesheet link means /live/shell.css is not being served; an
    // unstyled shell would only bury the document in stray text, so the
    // page stays the bare export instead.
    if (!document.querySelector(SHELL_STYLESHEET_SELECTOR)) return;
    adoptShellStylesheet();
    var meta = previewMeta();
    var isImage = meta.view === 'sheet';
    document.body.classList.add('aspose-shell-body');

    // The host adopts every original body child — the rendered document,
    // its scripts and the injected bootstrap, none of which care where they
    // sit once executed — and becomes the page's only scrolling element.
    var host = document.createElement('div');
    host.id = SCROLL_HOST_ID;
    host.className = 'aspose-shell-host' + (isImage ? ' aspose-shell-host-image' : '');
    while (document.body.firstChild) {
      host.appendChild(document.body.firstChild);
    }

    var top = document.createElement('header');
    top.className = 'aspose-shell-top';
    var file = document.createElement('span');
    file.className = 'aspose-shell-file';
    file.textContent = typeof meta.file === 'string' ? meta.file : '';
    var right = document.createElement('span');
    right.className = 'aspose-shell-top-right';
    var dot = document.createElement('span');
    dot.className = 'aspose-shell-dot';
    var revision = document.createElement('span');
    revision.className = 'aspose-shell-rev';
    right.appendChild(dot);
    right.appendChild(revision);
    if (meta.eval === true) {
      var evalBadge = document.createElement('span');
      evalBadge.className = 'aspose-shell-eval';
      evalBadge.textContent = 'EVALUATION';
      right.appendChild(evalBadge);
    }
    top.appendChild(file);
    top.appendChild(right);
    document.body.appendChild(top);

    if (!isImage) {
      var formula = document.createElement('div');
      formula.className = 'aspose-shell-formula';
      var namebox = document.createElement('span');
      namebox.className = 'aspose-shell-namebox';
      var fx = document.createElement('span');
      fx.className = 'aspose-shell-fx';
      fx.textContent = 'fx';
      var value = document.createElement('span');
      value.className = 'aspose-shell-value';
      formula.appendChild(namebox);
      formula.appendChild(fx);
      formula.appendChild(value);
      document.body.appendChild(formula);
    }

    document.body.appendChild(host);

    if (!isImage) {
      var strip = buildTabs(host);
      if (strip) document.body.appendChild(strip);
    }

    var status = document.createElement('footer');
    status.className = 'aspose-shell-status';
    var statusText = document.createElement('span');
    statusText.className = 'aspose-shell-status-text';
    status.appendChild(statusText);
    if (!isImage) {
      var zoom = document.createElement('span');
      zoom.className = 'aspose-shell-zoom';
      var modes = [
        ['fit-content', 'Fit content'],
        ['fit-width', 'Fit width'],
        ['100', '100%']
      ];
      for (var z = 0; z < modes.length; z += 1) {
        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'aspose-shell-fit';
        button.setAttribute('data-aspose-fit', modes[z][0]);
        button.textContent = modes[z][1];
        zoom.appendChild(button);
      }
      status.appendChild(zoom);
    }
    document.body.appendChild(status);

    if (!isImage) {
      var tables = gridTables(host);
      for (var t = 0; t < tables.length; t += 1) {
        tagGrid(tables[t]);
      }

      // A pristine export shows every sheet stacked vertically; with a tab
      // strip of its own the shell starts on the first sheet instead. A
      // view-state restore that follows may immediately re-activate another
      // sheet — that is the ordering working as intended. When something
      // already hid a container, only the highlight needs seeding.
      var containers = document.querySelectorAll(SHEET_CONTAINER_SELECTOR);
      var anyHidden = false;
      var firstVisible = null;
      for (var i = 0; i < containers.length; i += 1) {
        if (containers[i].style.display === 'none') anyHidden = true;
        else if (!firstVisible) firstVisible = containers[i];
      }
      if (containers.length > 0 && !anyHidden) {
        var saved = savedActiveSheet();
        activateSheet(saved && sheetContainerByName(saved)
          ? saved
          : containers[0].getAttribute('sheetname'));
      } else if (firstVisible) {
        syncTabHighlight(firstVisible.getAttribute('sheetname'));
      }
    }

    syncShellIndicators();
    applyScaleMode();
  }

  // The sheet tab strip, one tab per sheet container in workbook order (the
  // export ships no tab UI of its own). A bare single-sheet export has no
  // containers and gets no strip. Clicks arrive through the document-level
  // delegate, because the strip is replaced together with the body.
  function buildTabs(host) {
    var containers = host.querySelectorAll(SHEET_CONTAINER_SELECTOR);
    if (containers.length === 0) return null;
    var strip = document.createElement('div');
    strip.className = 'aspose-shell-tabs';
    for (var i = 0; i < containers.length; i += 1) {
      var name = containers[i].getAttribute('sheetname');
      var tab = document.createElement('span');
      tab.className = 'aspose-shell-tab';
      tab.setAttribute('data-aspose-sheet', name);
      tab.textContent = name;
      strip.appendChild(tab);
    }
    return strip;
  }

  // Scale only the displayed worksheet content, leaving the shell chrome at
  // normal size. CSS zoom is intentional here: unlike transform it changes
  // the scrollable layout bounds, so fit-width never leaves an unreachable
  // right edge in Chromium-based preview surfaces.
  function setScaleMode(mode) {
    if (mode !== 'fit-content' && mode !== 'fit-width' && mode !== '100') return;
    scaleMode = mode;
    applyScaleMode();
  }

  function applyScaleMode() {
    var host = document.getElementById(SCROLL_HOST_ID);
    if (!host || host.classList.contains('aspose-shell-host-image')) return;

    var previous = host.querySelectorAll('[data-aspose-preview-scaled]');
    for (var i = 0; i < previous.length; i += 1) {
      previous[i].style.zoom = '';
      previous[i].removeAttribute('data-aspose-preview-scaled');
    }

    var target = null;
    var containers = host.querySelectorAll(SHEET_CONTAINER_SELECTOR);
    for (i = 0; i < containers.length; i += 1) {
      if (containers[i].style.display !== 'none') {
        target = containers[i];
        break;
      }
    }
    if (!target) target = host.querySelector('table');
    if (!target) return;

    var factor = 1;
    var naturalWidth = Math.max(target.scrollWidth, target.offsetWidth, 1);
    var naturalHeight = Math.max(target.scrollHeight, target.offsetHeight, 1);
    var widthFactor = Math.max(0.1, (host.clientWidth - 24) / naturalWidth);
    if (scaleMode === 'fit-width') {
      factor = widthFactor;
    } else if (scaleMode === 'fit-content') {
      factor = Math.min(widthFactor, Math.max(0.1, (host.clientHeight - 24) / naturalHeight));
    }
    // Avoid extreme enlargement of small sheets while still allowing a
    // compact table to make useful use of the viewport.
    factor = Math.min(2, Math.max(0.1, factor));
    target.style.zoom = String(factor);
    target.setAttribute('data-aspose-preview-scaled', scaleMode);

    var buttons = document.querySelectorAll('.aspose-shell-fit');
    for (i = 0; i < buttons.length; i += 1) {
      var active = buttons[i].getAttribute('data-aspose-fit') === scaleMode;
      buttons[i].className = active
        ? 'aspose-shell-fit aspose-shell-fit-active'
        : 'aspose-shell-fit';
      buttons[i].setAttribute('aria-pressed', active ? 'true' : 'false');
    }
  }

  // The main grid table of every sheet: the first table inside each sheet
  // container, or — for the bare single-sheet export — the first table in
  // the host. "First in document order" is the same assumption
  // resolveTargetTable already makes; nested tables (charts render as
  // tables inside cells) open later in document order and are never picked.
  function gridTables(host) {
    var containers = host.querySelectorAll(SHEET_CONTAINER_SELECTOR);
    var tables = [];
    if (containers.length === 0) {
      var bare = host.querySelector('table');
      if (bare) tables.push(bare);
      return tables;
    }
    for (var i = 0; i < containers.length; i += 1) {
      var table = containers[i].querySelector('table');
      if (table) tables.push(table);
    }
    return tables;
  }

  // Mark a sheet's main grid for the shell stylesheet: the table gets
  // aspose-shell-grid (zero-specificity view gridlines) and the exported
  // row/column header cells get aspose-shell-heading. Headers are
  // recognised structurally — the export gives every data cell a data-cell
  // address, so the first row's leading address-less run is the column
  // header row (corner plus letters) and an address-less first cell of any
  // other row is its row header. Only this table's own rows are walked;
  // nested chart tables keep their cells untouched.
  function tagGrid(table) {
    table.classList.add('aspose-shell-grid');
    var rows = table.rows;
    for (var r = 0; r < rows.length; r += 1) {
      var cells = rows[r].cells;
      for (var c = 0; c < cells.length; c += 1) {
        if (cells[c].hasAttribute('data-cell')) break;
        cells[c].classList.add('aspose-shell-heading');
        if (r > 0) break;
      }
    }
  }

  // Keep the tab strip's highlight in step with the visible sheet; tabs are
  // matched by sheet name, the same key activateSheet works with. A page
  // without a strip (sheet view, bare export) is a no-op.
  function syncTabHighlight(name) {
    var tabs = document.querySelectorAll('.aspose-shell-tab');
    for (var i = 0; i < tabs.length; i += 1) {
      tabs[i].className = tabs[i].getAttribute('data-aspose-sheet') === name
        ? 'aspose-shell-tab aspose-shell-tab-active'
        : 'aspose-shell-tab';
    }
  }

  // The connection state feeds the top-bar dot and the status line. It
  // lives in module state so a shell rebuilt mid-outage starts amber.
  function setConnectionState(state) {
    if (connectionState === state) return;
    connectionState = state;
    syncShellIndicators();
  }

  // The status bar turns deep green while a refresh is fetching and
  // swapping. The swapped-in shell is born non-busy, which is accurate by
  // then: after the swap only the synchronous view-state restore remains.
  function setShellBusy(busy) {
    var status = document.querySelector('.aspose-shell-status');
    if (!status) return;
    if (busy) status.classList.add('aspose-shell-busy');
    else status.classList.remove('aspose-shell-busy');
  }

  // Repaint the shell's live indicators — top-bar revision, connection dot,
  // the status line — from module state. Cheap and idempotent: state
  // changes call it directly and buildShell calls it to seed every rebuilt
  // shell; without a shell each lookup misses and nothing happens.
  function syncShellIndicators() {
    var live = connectionState !== 'reconnecting';
    var revision = document.querySelector('.aspose-shell-rev');
    if (revision) revision.textContent = 'rev ' + currentRevision;
    var dot = document.querySelector('.aspose-shell-dot');
    if (dot) {
      dot.className = 'aspose-shell-dot' + (live ? '' : ' aspose-shell-dot-reconnecting');
      dot.title = live ? 'Live' : 'Reconnecting';
    }
    var text = document.querySelector('.aspose-shell-status-text');
    if (text) {
      var meta = previewMeta();
      var parts = [typeof meta.file === 'string' && meta.file.length > 0
        ? 'Watching ' + meta.file
        : 'Watching'];
      parts.push('rev ' + currentRevision);
      if (typeof lastRenderMs === 'number') parts.push(lastRenderMs + 'ms');
      parts.push(live ? 'Live' : 'Reconnecting');
      text.textContent = parts.join(' · '); // Middle-dot separators.
    }
  }

  // Anchor the formula-bar selection on a data cell: the name box shows the
  // sheet-relative address, the value area the cell's text, and a
  // persistent (non-fading) selection box wraps the cell — the same
  // content-space overlay mechanism the focus spotlight uses, so it tracks
  // the cell through scrolling and stands until replaced or cleared.
  function selectCell(cell) {
    clearSelection();
    var bounds = unionRect([cell]);
    if (!bounds) return; // On a hidden sheet, or sized away by the export.
    var container = cell.closest(SHEET_CONTAINER_SELECTOR);
    selection = {
      sheet: container ? container.getAttribute('sheetname') : null,
      cell: cell.getAttribute('data-cell')
    };
    selectionNode = overlayBox(cell, bounds);
    selectionNode.className = 'aspose-shell-select';
    setFormulaBar(selection.cell, (cell.textContent || '').trim());
  }

  // Drop the formula-bar selection: the overlay, the stored anchor and the
  // bar text. Safe to call when nothing is selected, or when the overlay
  // already died with a swapped-out body.
  function clearSelection() {
    if (selectionNode && selectionNode.parentNode) {
      selectionNode.parentNode.removeChild(selectionNode);
    }
    selectionNode = null;
    selection = null;
    setFormulaBar('', '');
  }

  // Re-anchor a captured selection in the fresh document, after the sheet
  // restore has settled visibility. A cell the new document does not have
  // (or no longer shows) clears the selection rather than guessing; a
  // re-located cell also refreshes the value area with its new content.
  function restoreSelection(captured) {
    clearSelection();
    if (!captured || typeof captured.cell !== 'string') return;
    var scope = typeof captured.sheet === 'string'
      ? sheetContainerByName(captured.sheet)
      : document.body;
    if (!scope) return;
    var cell = scope.querySelector('td[data-cell="' + captured.cell + '"]');
    if (cell) selectCell(cell);
  }

  // The formula bar's two text slots. The sheet view builds no formula bar,
  // which turns these writes into no-ops.
  function setFormulaBar(address, value) {
    var namebox = document.querySelector('.aspose-shell-namebox');
    if (namebox) namebox.textContent = address;
    var valueArea = document.querySelector('.aspose-shell-value');
    if (valueArea) valueArea.textContent = value;
  }

  // A parked focus is applied once a refresh settles with nothing queued:
  // the page now shows exactly its revision (apply), moved past it (drop),
  // or is still behind (keep waiting for the next update's refresh).
  function deliverPendingFocus() {
    if (!pendingFocus || pendingFocus.revision > currentRevision) return;
    var payload = pendingFocus;
    pendingFocus = null;
    if (payload.revision === currentRevision) applyFocus(payload);
  }

  // Spotlight the targets of a focus payload: switch to the first target's
  // sheet, overlay a selection box on every locatable target and bring the
  // first one into view. Best-effort by design — any failure degrades to a
  // sheet jump plus a floating label, and never throws into the stream.
  function applyFocus(payload) {
    clearFocusFx();
    var targets = payload.targets.slice(0, 16);
    try {
      ensureFxStyle();
      var boxes = [];
      for (var i = 0; i < targets.length; i += 1) {
        var target = targets[i] || {};
        var table = resolveTargetTable(target.sheet, i === 0);
        if (!table) throw new Error('sheet not found');
        // A target without a range is sheet-level: flash the whole table.
        var light = !(typeof target.range === 'string' && target.range.length > 0);
        var cells = [table];
        if (!light) {
          var rect = parseA1Rect(target.range);
          if (!rect) throw new Error('range is not A1');
          cells = collectCells(table, rect);
          if (cells.length === 0) throw new Error('range is outside the table');
        }
        var bounds = unionRect(cells);
        if (!bounds && i === 0) throw new Error('target is not visible');
        if (!bounds) continue; // A secondary target on a non-visible sheet.
        boxes.push({ node: drawFocusBox(table, bounds, light), light: light });
      }
      if (boxes.length === 0) throw new Error('nothing to spotlight');
      if (window.__asposePreviewFx === 'demo') {
        // Jump ahead of the animation, fly the cursor in, then reveal.
        scrollToBox(boxes[0], 'auto');
        playDemoCursor(boxes[0].node, function () { revealBoxes(boxes); });
      } else {
        revealBoxes(boxes);
        scrollToBox(boxes[0], 'smooth');
      }
    } catch (ignored) {
      degradeFocus(targets[0] || {});
    }
  }

  // Resolve the <table> that hosts a target. Multi-sheet exports wrap each
  // sheet in a container div; a single-sheet export attaches one bare
  // <table> to the body, which is the only sheet and matches any sheet name.
  // The shell moves that bare table inside its scroll host, but it stays
  // the body's first table — the chrome is deliberately table-free so this
  // fallback cannot land on chrome. Only the batch's first target may
  // switch the visible sheet.
  function resolveTargetTable(sheetName, allowSwitch) {
    var containers = document.querySelectorAll(SHEET_CONTAINER_SELECTOR);
    if (containers.length === 0) {
      return document.body ? document.body.querySelector('table') : null;
    }
    var container = null;
    if (typeof sheetName === 'string' && sheetName.length > 0) {
      container = allowSwitch ? activateSheet(sheetName) : sheetContainerByName(sheetName);
    } else {
      // No sheet named: stay on (and search) the sheet currently showing.
      for (var i = 0; i < containers.length && !container; i += 1) {
        if (containers[i].style.display !== 'none') container = containers[i];
      }
    }
    return container ? container.querySelector('table') : null;
  }

  // Parse "B2" or "B2:D4" (absolute $ tolerated) into a 1-based rectangle.
  function parseA1Rect(text) {
    var match = /^\$?([A-Za-z]{1,3})\$?(\d+)(?::\$?([A-Za-z]{1,3})\$?(\d+))?$/
      .exec(String(text).replace(/\s+/g, ''));
    if (!match) return null;
    var c1 = columnLetterIndex(match[1]);
    var r1 = parseInt(match[2], 10);
    var c2 = match[3] ? columnLetterIndex(match[3]) : c1;
    var r2 = match[4] ? parseInt(match[4], 10) : r1;
    if (r1 < 1 || r2 < 1) return null;
    return { left: Math.min(c1, c2), top: Math.min(r1, r2),
      right: Math.max(c1, c2), bottom: Math.max(r1, r2) };
  }

  function columnLetterIndex(letters) {
    var value = 0;
    for (var i = 0; i < letters.length; i += 1) {
      value = value * 26 + (letters.charCodeAt(i) & 31); // 'A' and 'a' -> 1
    }
    return value;
  }

  // Locate the cells an A1 rectangle covers through their data-cell anchors:
  // the export (CellNameAttribute, see the notes up top) writes every data
  // cell's sheet-relative address into a data-cell attribute, the single
  // source of truth for addressing here. Row/column header cells and the
  // supportMisalignedColumns fixup row carry no such attribute, so scanning
  // [data-cell] excludes them by construction. Only anchors exist in the
  // DOM — a merged region renders just its top-left cell, and text overflow
  // absorbs the blank cells to its right into the absorber's colspan — so a
  // cell is selected when the rectangle it spans (anchor address extended by
  // colSpan/rowSpan) intersects `rect`, which covers the swallowed cells.
  // Addresses repeat across sheets (no sheet prefix), hence the scan stays
  // scoped to the hosting table; and it is one pass over that table's cells,
  // never one query per target address, so a large `rect` costs no more
  // than a small one.
  function collectCells(table, rect) {
    var cells = [];
    var anchored = table.querySelectorAll('td[data-cell]');
    for (var i = 0; i < anchored.length; i += 1) {
      var cell = anchored[i];
      // parseA1Rect reads a single address as a degenerate rectangle (and
      // tolerates a stray $); a value it cannot parse skips its cell.
      var anchor = parseA1Rect(cell.getAttribute('data-cell'));
      if (!anchor) continue;
      var colSpan = cell.colSpan > 0 ? cell.colSpan : 1;
      var rowSpan = cell.rowSpan > 0 ? cell.rowSpan : 1;
      if (anchor.left <= rect.right && anchor.left + colSpan - 1 >= rect.left
          && anchor.top <= rect.bottom && anchor.top + rowSpan - 1 >= rect.top) {
        cells.push(cell);
      }
    }
    return cells;
  }

  // Union of the elements' viewport rects; zero-size cells (rows the export
  // hides) keep their grid position but add nothing. Null when none visible.
  function unionRect(elements) {
    var box = null;
    for (var i = 0; i < elements.length; i += 1) {
      var rect = elements[i].getBoundingClientRect();
      if (rect.width === 0 && rect.height === 0) continue;
      if (!box) {
        box = { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom };
      } else {
        box.left = Math.min(box.left, rect.left);
        box.top = Math.min(box.top, rect.top);
        box.right = Math.max(box.right, rect.right);
        box.bottom = Math.max(box.bottom, rect.bottom);
      }
    }
    return box;
  }

  // An absolutely positioned overlay parked inside the anchor element's
  // scroll container so it tracks the content when that container (or the
  // window) scrolls; left/top are content-space coordinates, so later
  // scrolling needs no repositioning. Positioning only — callers add their
  // own look. Shared by the focus spotlight and the formula-bar selection.
  function overlayBox(anchor, bounds) {
    var parent = scrollParent(anchor);
    if (window.getComputedStyle(parent).position === 'static') {
      parent.style.position = 'relative'; // Transient: the next swap resets it.
    }
    var origin = parent.getBoundingClientRect();
    var box = document.createElement('div');
    box.style.cssText = 'position:absolute;box-sizing:border-box;pointer-events:none;'
      + 'left:' + (bounds.left - origin.left - parent.clientLeft + parent.scrollLeft) + 'px;'
      + 'top:' + (bounds.top - origin.top - parent.clientTop + parent.scrollTop) + 'px;'
      + 'width:' + (bounds.right - bounds.left) + 'px;'
      + 'height:' + (bounds.bottom - bounds.top) + 'px;';
    parent.appendChild(box);
    return box;
  }

  // An Excel-style selection box (green border, corner handles) around a
  // viewport rectangle. Boxes are born invisible; revealBoxes starts their
  // pulse-hold-fade life cycle.
  function drawFocusBox(table, bounds, light) {
    var box = overlayBox(table, bounds);
    box.setAttribute('data-aspose-focus', light ? 'sheet' : 'range');
    box.style.zIndex = '2147483646';
    box.style.opacity = '0';
    box.style.border = '2px solid ' + FOCUS_COLOR;
    if (light) box.style.background = FOCUS_FILL;
    var corners = light ? [] : ['left:-4px;top:-4px', 'right:-4px;top:-4px',
      'left:-4px;bottom:-4px', 'right:-4px;bottom:-4px'];
    for (var i = 0; i < corners.length; i += 1) {
      var handle = document.createElement('div');
      handle.style.cssText = 'position:absolute;width:6px;height:6px;background:'
        + FOCUS_COLOR + ';border:1px solid #fff;' + corners[i];
      box.appendChild(handle);
    }
    return fxNode(box);
  }

  function scrollParent(element) {
    for (var node = element.parentNode; node && node !== document.body; node = node.parentNode) {
      if (node.nodeType !== 1) break;
      var style = window.getComputedStyle(node);
      if (/auto|scroll/.test(style.overflow + style.overflowX + style.overflowY)) {
        return node;
      }
    }
    return document.body;
  }

  // Pulse the green ring twice, hold, fade out — the spotlight's life
  // cycle. Sheet-level flashes ("light") skip the hold and pass quickly.
  function revealBoxes(boxes) {
    for (var i = 0; i < boxes.length; i += 1) {
      var node = boxes[i].node;
      if (!node.parentNode) continue; // Already swept by a body swap.
      node.style.opacity = '';
      node.style.animation = 'asposePreviewPulse 0.8s ease-out 2';
      fadeOutLater(node, boxes[i].light ? 1300 : 3900);
    }
  }

  // Fade a fx node after `delay` ms, then remove it.
  function fadeOutLater(node, delay) {
    fxTimer(function () {
      node.style.transition = 'opacity 0.4s ease';
      node.style.opacity = '0';
      fxTimer(function () {
        if (node.parentNode) node.parentNode.removeChild(node);
      }, 450);
    }, delay);
  }

  // Center the first target; sheet-level flashes just come into view. Some
  // environments silently drop smooth programmatic scrolling (disabled smooth
  // scrolling, embedded panes), so a moment later a still fully off-screen
  // box is snapped into place instantly.
  function scrollToBox(box, behavior) {
    var align = box.light ? 'nearest' : 'center';
    try {
      box.node.scrollIntoView({ behavior: behavior, block: align, inline: align });
      if (behavior !== 'smooth') return;
      fxTimer(function () {
        var rect = box.node.getBoundingClientRect();
        var view = document.documentElement;
        if (rect.bottom < 0 || rect.right < 0
            || rect.top > view.clientHeight || rect.left > view.clientWidth) {
          box.node.scrollIntoView({ block: align, inline: align });
        }
      }, 700);
    } catch (ignored) { /* Scrolling is cosmetic; never let it break focus. */ }
  }

  // The demo cursor (only under window.__asposePreviewFx === "demo"): an
  // inline-SVG pointer flies from outside the bottom-right corner along a
  // quadratic Bezier onto the spotlight, clicks with a ripple, fades, then
  // hands over to `done`. Sessions without the flag never reach this code.
  function playDemoCursor(boxNode, done) {
    var view = document.documentElement;
    var rect = boxNode.getBoundingClientRect();
    var to = { x: Math.min(Math.max((rect.left + rect.right) / 2, 16), view.clientWidth - 16),
      y: Math.min(Math.max((rect.top + rect.bottom) / 2, 16), view.clientHeight - 16) };
    var from = { x: view.clientWidth + 30, y: view.clientHeight + 30 };
    var mid = { x: (from.x + to.x) / 2 - view.clientWidth * 0.12,
      y: (from.y + to.y) / 2 - view.clientHeight * 0.18 };
    var cursor = document.createElement('div');
    cursor.style.cssText = 'position:fixed;left:0;top:0;z-index:2147483647;pointer-events:none;'
      + 'filter:drop-shadow(1px 2px 2px rgba(0,0,0,0.35));will-change:transform;';
    cursor.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"'
      + ' viewBox="0 0 24 24"><path d="M3 1 L3 18 L7.8 14 L10.6 20.4 L13.4 19.2 L10.6 12.9'
      + ' L16.4 12.4 Z" fill="#fff" stroke="#1c1c1c" stroke-width="1.4" stroke-linejoin="round"/></svg>';
    document.body.appendChild(fxNode(cursor));
    var started = null;
    function fly(now) {
      if (started === null) started = now;
      var t = Math.min((now - started) / 700, 1);
      var e = t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2; // ease in-out
      var u = 1 - e;
      var x = u * u * from.x + 2 * u * e * mid.x + e * e * to.x;
      var y = u * u * from.y + 2 * u * e * mid.y + e * e * to.y;
      cursor.style.transform = 'translate(' + (x - 3) + 'px,' + (y - 1) + 'px)'; // tip at 3,1
      if (t < 1) {
        focusFx.frame = window.requestAnimationFrame(fly);
        return;
      }
      focusFx.frame = 0;
      var ripple = document.createElement('div');
      ripple.style.cssText = 'position:fixed;pointer-events:none;z-index:2147483647;'
        + 'width:28px;height:28px;border-radius:50%;border:2px solid ' + FOCUS_COLOR + ';'
        + 'left:' + (to.x - 14) + 'px;top:' + (to.y - 14) + 'px;'
        + 'animation:asposePreviewRipple 0.3s ease-out forwards;';
      document.body.appendChild(fxNode(ripple));
      fxTimer(function () {
        cursor.style.transition = 'opacity 0.25s';
        cursor.style.opacity = '0';
      }, 120);
      fxTimer(function () {
        if (ripple.parentNode) ripple.parentNode.removeChild(ripple);
        if (cursor.parentNode) cursor.parentNode.removeChild(cursor);
        done();
      }, 400);
    }
    focusFx.frame = window.requestAnimationFrame(fly);
  }

  // Degraded focus: locating failed somewhere, so switch to the target
  // sheet, bring its table into view and float a label naming the intended
  // destination instead. Best-effort all the way down — never throws.
  function degradeFocus(target) {
    try {
      var table = resolveTargetTable(target.sheet, true);
      if (table) {
        try {
          table.scrollIntoView({ behavior: 'smooth', block: 'start' });
        } catch (ignored) {
          table.scrollIntoView(true);
        }
      }
      var sheet = typeof target.sheet === 'string' ? target.sheet : '';
      var range = typeof target.range === 'string' ? target.range : '';
      var text = sheet && range ? sheet + '!' + range : sheet || range;
      if (!text || !document.body) return;
      var label = document.createElement('div');
      // Floats one step above the stale-revision badge; both offsets clear
      // the shell's status bar at the bottom of the viewport.
      label.style.cssText = 'position:fixed;right:12px;bottom:104px;z-index:2147483647;'
        + 'background:rgba(20,20,20,0.82);color:#fff;font:12px/1.5 system-ui,sans-serif;'
        + 'padding:6px 10px;border-radius:4px;pointer-events:none;max-width:60vw;';
      label.textContent = '→ ' + text;
      document.body.appendChild(fxNode(label));
      fadeOutLater(label, 2000);
    } catch (ignored) {
      // Out of fallbacks; stay silent rather than throw into the stream.
    }
  }

  // The pulse and ripple keyframes live in a head <style>; a refresh's style
  // purge removes it, so it is re-injected before every focus application.
  // They stay JS-injected instead of moving into shell.css because the
  // spotlight must keep working even when the shell stylesheet is not
  // being served.
  function ensureFxStyle() {
    if (document.getElementById(FX_STYLE_ID)) return;
    var style = document.createElement('style');
    style.id = FX_STYLE_ID;
    // The pulse keeps a thin inner ring on the box while an outer glow
    // expands and dissolves; two runs read as a heartbeat, after which the
    // box holds its plain border until fadeOutLater retires it.
    style.textContent = '@keyframes asposePreviewPulse'
      + '{0%{box-shadow:inset 0 0 0 1.5px rgba(31,138,82,0.85),0 0 0 0 rgba(31,138,82,0.55)}'
      + '100%{box-shadow:inset 0 0 0 1.5px rgba(31,138,82,0.85),0 0 0 12px rgba(31,138,82,0)}}'
      + '@keyframes asposePreviewRipple{from{transform:scale(0.25);opacity:0.85}'
      + 'to{transform:scale(1.7);opacity:0}}';
    document.head.appendChild(style);
  }

  function fxNode(node) {
    focusFx.nodes.push(node);
    return node;
  }

  function fxTimer(callback, delay) {
    focusFx.timers.push(window.setTimeout(callback, delay));
  }

  // Drop every focus visual at once: timers, the cursor's animation frame
  // and the nodes themselves (a body swap orphans the nodes anyway, but
  // scheduled callbacks must never outlive their targets).
  function clearFocusFx() {
    for (var i = 0; i < focusFx.timers.length; i += 1) {
      window.clearTimeout(focusFx.timers[i]);
    }
    if (focusFx.frame) window.cancelAnimationFrame(focusFx.frame);
    for (var n = 0; n < focusFx.nodes.length; n += 1) {
      var node = focusFx.nodes[n];
      if (node.parentNode) node.parentNode.removeChild(node);
    }
    focusFx = { nodes: [], timers: [], frame: 0 };
  }

  // A small fixed badge in the bottom-right corner — raised clear of the
  // shell's status bar — while the page shows a stale revision.
  // Inline-styled and pointer-transparent so it can never interfere with
  // the rendered document; a body swap discards it with the old body, and
  // hideBadge covers every other path.
  function showBadge(text) {
    if (!badge) {
      badge = document.createElement('div');
      badge.setAttribute(BADGE_ATTRIBUTE, '');
      badge.style.cssText = 'position:fixed;right:12px;bottom:72px;z-index:2147483647;'
        + 'background:rgba(20,20,20,0.82);color:#fff;font:12px/1.5 system-ui,sans-serif;'
        + 'padding:6px 10px;border-radius:4px;pointer-events:none;max-width:60vw;';
    }
    badge.textContent = text;
    if (document.body) {
      // appendChild also re-attaches a badge orphaned by a body swap.
      document.body.appendChild(badge);
    }
  }

  function hideBadge() {
    if (badge && badge.parentNode) {
      badge.parentNode.removeChild(badge);
    }
  }

  function reviveScripts(root) {
    var scripts = root.querySelectorAll('script');
    for (var i = 0; i < scripts.length; i += 1) {
      var inert = scripts[i];
      var src = inert.getAttribute('src') || '';
      if (src.length >= CLIENT_SCRIPT_SUFFIX.length
          && src.lastIndexOf(CLIENT_SCRIPT_SUFFIX) === src.length - CLIENT_SCRIPT_SUFFIX.length) {
        continue;
      }
      var executable = document.createElement('script');
      for (var a = 0; a < inert.attributes.length; a += 1) {
        executable.setAttribute(inert.attributes[a].name, inert.attributes[a].value);
      }
      executable.textContent = inert.textContent;
      if (executable.src && !executable.hasAttribute('async')) {
        // Dynamically inserted scripts default to async; keep source order
        // for external scripts, like the parser would have.
        executable.async = false;
      }
      inert.parentNode.replaceChild(executable, inert);
    }
  }
}());
