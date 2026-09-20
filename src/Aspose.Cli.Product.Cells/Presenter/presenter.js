/* Workbooks read like a spreadsheet.
 *
 * The sheet view is one image per sheet behind the shared tab strip. The
 * workbook view is the product's own HTML grid: text stays selectable, cells
 * keep their exact geometry, and an edit is patched into the grid cell by
 * cell, so the scroll position, the sheet in view and everything the edit did
 * not touch stay exactly as they were. */
(function () {
  'use strict';

  var CHANGED_CLASS = 'aspose-cell-changed';
  var FLASH_MS = 1800;
  var GRID_STYLE =
    '.' + CHANGED_CLASS + '{animation:aspose-cell-flash 1.8s ease-out forwards}'
    + '@keyframes aspose-cell-flash{0%{background:rgba(20,122,82,.32)}'
    + '70%{background:rgba(20,122,82,.32)}100%{background:transparent}}'
    + '@media (prefers-reduced-motion: reduce){.' + CHANGED_CLASS + '{animation:none}}';

  AsposeViewer.definePresenter('cells', {
    kind: 'Workbook',
    glyph: 'C',
    views: {
      sheets: { layout: 'tabs', noun: 'Sheet', zoom: 1 },
      workbook: { layout: 'grid', create: grid, noun: 'Sheet', zoom: 1 }
    }
  });

  /** The product's grid, with the sheet tabs and the cell-level updates. */
  function grid(ctx) {
    var root = ctx.el('div', 'cells-grid');
    var frame = ctx.el('iframe', 'cells-grid-frame');
    frame.title = 'Workbook grid';
    var strip = ctx.el('div', 'av-tabstrip');
    strip.setAttribute('role', 'tablist');
    strip.setAttribute('aria-label', 'Sheets');
    root.append(frame, strip);
    var sheets = [];
    var tabs = [];
    var active = 0;
    var scale = 1;
    var loaded = null;

    strip.addEventListener('keydown', function (event) {
      var next = event.key === 'ArrowLeft' ? active - 1
        : event.key === 'ArrowRight' ? active + 1
          : event.key === 'Home' ? 0
            : event.key === 'End' ? tabs.length - 1
              : -1;
      if (next < 0 || next >= tabs.length) {
        return;
      }
      event.preventDefault();
      showSheet(next);
      tabs[next].focus();
    });

    return {
      element: root,
      focus: frame,
      // The sheet tabs below the grid navigate the workbook.
      pager: false,
      show: function () {
        var url = ctx.url(ctx.parts[0]);
        if (url === loaded) {
          return undefined;
        }
        loaded = url;
        frame.addEventListener('load', mount);
        frame.src = url;
        return undefined;
      },
      update: function () {
        var url = ctx.url(ctx.parts[0]);
        if (url === loaded || !frame.contentDocument) {
          return;
        }
        var previous = loaded;
        loaded = url;
        fetch(url)
          .then(function (response) { return response.text(); })
          .then(function (html) { patch(html); })
          .catch(function () {
            // The grid keeps what it shows; the next revision tries again.
            loaded = previous;
          });
      },
      rescale: function () {
        var zoom = ctx.zoom();
        scale = typeof zoom === 'number' ? zoom : 1;
        if (frame.contentDocument) {
          frame.contentDocument.documentElement.style.zoom = scale;
        }
      },
      mark: function () {
        // The grid marks the cells it patched, which is finer than the part.
      },
      pointOf: function () {
        var cell = frame.contentDocument
          && frame.contentDocument.querySelector('.' + CHANGED_CLASS);
        if (!cell) {
          return null;
        }
        var box = cell.getBoundingClientRect();
        var host = frame.getBoundingClientRect();
        return {
          x: host.left + (box.left + box.width / 2) * scale,
          y: host.top + (box.top + box.height / 2) * scale
        };
      },
      scale: function () { return scale; }
    };

    /** Indexes the sheets of a freshly loaded grid and shows the active one. */
    function mount() {
      frame.removeEventListener('load', mount);
      var document = frame.contentDocument;
      if (!document) {
        return;
      }
      var style = document.createElement('style');
      style.textContent = GRID_STYLE;
      document.head.appendChild(style);
      document.documentElement.style.zoom = scale;
      sheets = sheetsOf(document);
      labelTabs();
      showSheet(indexOf(sheets, activeName(document)));
    }

    /**
     * Takes in a new grid: sheets that are still there keep their element and
     * only the cells that differ are replaced, so the view does not move.
     */
    function patch(html) {
      var document = frame.contentDocument;
      var next = new DOMParser().parseFromString(html, 'text/html');
      var arrived = sheetsOf(next);
      var changed = [];
      var name = sheets[active] ? sheets[active].name : null;
      arrived.forEach(function (sheet, index) {
        var mine = sheets[indexOf(sheets, sheet.name)];
        if (!mine) {
          insert(document, sheet, index);
          return;
        }
        patchSheet(document, mine, sheet, changed);
      });
      sheets.slice().forEach(function (sheet) {
        if (indexOf(arrived, sheet.name) < 0) {
          sheet.element.remove();
        }
      });
      sheets = sheetsOf(document);
      labelTabs();
      showSheet(Math.max(0, indexOf(sheets, name)));
      flash(changed);
    }

    function patchSheet(document, mine, theirs, changed) {
      var was = cellsOf(mine.element);
      var now = cellsOf(theirs.element);
      var addresses = Object.keys(now);
      if (addresses.length !== Object.keys(was).length
          || addresses.some(function (address) { return !was[address]; })) {
        // Rows or columns moved: the sheet is replaced and marked whole.
        var replacement = document.importNode(theirs.element, true);
        mine.element.replaceWith(replacement);
        changed.push(replacement);
        return;
      }
      addresses.forEach(function (address) {
        if (was[address].outerHTML === now[address].outerHTML) {
          return;
        }
        var cell = document.importNode(now[address], true);
        was[address].replaceWith(cell);
        changed.push(cell);
      });
    }

    function insert(document, sheet, index) {
      var element = document.importNode(sheet.element, true);
      var before = sheets[index] ? sheets[index].element : null;
      if (before) {
        before.parentNode.insertBefore(element, before);
      } else {
        document.body.appendChild(element);
      }
    }

    function flash(cells) {
      cells.forEach(function (cell) {
        cell.classList.remove(CHANGED_CLASS);
        void cell.offsetWidth;
        cell.classList.add(CHANGED_CLASS);
        setTimeout(function () { cell.classList.remove(CHANGED_CLASS); }, FLASH_MS);
      });
      if (cells.length) {
        cells[0].scrollIntoView({ block: 'nearest', inline: 'nearest' });
      }
    }

    function showSheet(index) {
      active = Math.max(0, Math.min(index, sheets.length - 1));
      sheets.forEach(function (sheet, at) {
        if (sheet.element !== sheet.element.ownerDocument.body) {
          sheet.element.style.display = at === active ? '' : 'none';
        }
      });
      tabs.forEach(function (tab, at) {
        tab.setAttribute('aria-selected', String(at === active));
        tab.tabIndex = at === active ? 0 : -1;
      });
    }

    function labelTabs() {
      while (tabs.length > sheets.length) {
        tabs.pop().remove();
      }
      sheets.forEach(function (sheet, index) {
        var tab = tabs[index];
        if (!tab) {
          tab = ctx.el('button', 'av-tab');
          tab.type = 'button';
          tab.setAttribute('role', 'tab');
          tab.addEventListener('click', function () { showSheet(index); });
          tabs[index] = tab;
          strip.appendChild(tab);
        }
        tab.textContent = sheet.name;
      });
    }
  }

  /**
   * The sheets of an exported workbook. Every sheet is one element carrying
   * its name; a workbook with a single sheet exports as a bare body.
   */
  function sheetsOf(document) {
    var blocks = Array.prototype.slice.call(document.querySelectorAll('[sheetname]'));
    if (blocks.length) {
      return blocks.map(function (element) {
        return { name: element.getAttribute('sheetname'), element: element };
      });
    }
    return document.body ? [{ name: activeName(document) || 'Sheet', element: document.body }] : [];
  }

  /** Cells of one sheet, by their A1 address. */
  function cellsOf(element) {
    var cells = Object.create(null);
    Array.prototype.forEach.call(element.querySelectorAll('td[data-cell]'), function (cell) {
      cells[cell.getAttribute('data-cell')] = cell;
    });
    return cells;
  }

  /** The sheet the workbook was saved on; the product stamps it into the head. */
  function activeName(document) {
    var meta = document.querySelector('meta[name="aspose-active-sheet"]');
    return meta ? meta.getAttribute('content') : null;
  }

  function indexOf(sheets, name) {
    for (var i = 0; i < sheets.length; i++) {
      if (sheets[i].name === name) {
        return i;
      }
    }
    return -1;
  }
}());
