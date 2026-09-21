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
  // A mark is meant to be looked at; past this many, the sheet as a whole
  // is the honest answer, and it is one flash instead of hundreds.
  var MAX_MARKS = 128;
  var MAX_ALIGNED_ROWS = 2000;
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
      sheets: { layout: 'tabs', noun: 'Sheet', zoom: 1, label: 'Sheet images' },
      workbook: { layout: 'grid', create: grid, noun: 'Sheet', zoom: 1, label: 'Grid' }
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
      // The grid zooms like a spreadsheet, whatever the part is made of.
      zoomable: function () { return true; },
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
      if (addresses.length === Object.keys(was).length
          && addresses.every(function (address) { return was[address]; })) {
        // The same cells are there: only the ones that differ are replaced,
        // so the view does not move and the mark lands on the edit itself.
        addresses.forEach(function (address) {
          if (cellSignature(was[address]) === cellSignature(now[address])) {
            return;
          }
          var cell = document.importNode(now[address], true);
          was[address].replaceWith(cell);
          changed.push(cell);
        });
        return;
      }
      // The grid grew, shrank or moved, so the sheet is swapped in whole.
      // What is marked is still only what changed: rows are aligned by what
      // they contain, so an inserted row highlights itself rather than
      // everything the insert pushed down.
      var before = rowsOf(mine.element);
      var replacement = document.importNode(theirs.element, true);
      mine.element.replaceWith(replacement);
      var marks = rowDifferences(before, rowsOf(replacement));
      if (marks.length === 0 || marks.length > MAX_MARKS) {
        changed.push(replacement);
        return;
      }
      changed.push.apply(changed, marks);
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
  function rowsOf(element) {
    return Array.prototype.slice.call(element.querySelectorAll('tr'));
  }

  function cellsIn(row) {
    return Array.prototype.slice.call(row.querySelectorAll('td[data-cell]'));
  }

  /**
   * The cells worth marking after a structural change. Rows the alignment
   * matched are compared cell by cell. Rows it could not match arrive in
   * runs -- what was there, and what is there now -- and those runs are
   * zipped in order, so a row whose value changed marks only the cell that
   * changed, while a row with nothing to pair against is new and marks
   * itself.
   */
  function rowDifferences(before, after) {
    var marks = [];
    var dropped = [];
    var arrived = [];

    function settle() {
      arrived.forEach(function (row, index) {
        marks.push.apply(marks, changedCells(dropped[index] || null, row));
      });
      dropped.length = 0;
      arrived.length = 0;
    }

    align(before, after).forEach(function (pair) {
      if (pair.before && pair.after) {
        settle();
        marks.push.apply(marks, changedCells(pair.before, pair.after));
        return;
      }
      if (pair.after) {
        arrived.push(pair.after);
      } else {
        dropped.push(pair.before);
      }
    });
    settle();
    return marks;
  }

  /**
   * The cells of one row that are new or no longer look the same. A cell
   * that is new and empty is not one of them: a sheet grows by padding every
   * row out to its widest, and nobody edited those.
   */
  function changedCells(before, after) {
    var left = before ? cellsIn(before) : [];
    var marks = [];
    cellsIn(after).forEach(function (cell, index) {
      if (!left[index]) {
        if (cell.textContent !== '') {
          marks.push(cell);
        }
        return;
      }
      if (cellSignature(left[index]) !== cellSignature(cell)) {
        marks.push(cell);
      }
    });
    return marks;
  }

  /**
   * Pairs the rows of two renders by their text, longest common subsequence
   * first. A row only one side has is reported with nothing on the other, so
   * the caller can tell an edited row from an inserted one.
   */
  function align(before, after) {
    if (before.length > MAX_ALIGNED_ROWS || after.length > MAX_ALIGNED_ROWS) {
      return after.map(function (row, index) {
        return { before: before[index] || null, after: row };
      });
    }
    var left = before.map(signature);
    var right = after.map(signature);
    var table = [];
    for (var i = 0; i <= left.length; i++) {
      table.push(new Array(right.length + 1).fill(0));
    }
    for (var row = left.length - 1; row >= 0; row--) {
      for (var column = right.length - 1; column >= 0; column--) {
        table[row][column] = left[row] === right[column]
          ? table[row + 1][column + 1] + 1
          : Math.max(table[row + 1][column], table[row][column + 1]);
      }
    }
    var pairs = [];
    var a = 0;
    var b = 0;
    while (a < left.length && b < right.length) {
      if (left[a] === right[b]) {
        pairs.push({ before: before[a], after: after[b] });
        a++;
        b++;
      } else if (table[a + 1][b] >= table[a][b + 1]) {
        pairs.push({ before: before[a], after: null });
        a++;
      } else {
        pairs.push({ before: null, after: after[b] });
        b++;
      }
    }
    for (; a < left.length; a++) {
      pairs.push({ before: before[a], after: null });
    }
    for (; b < right.length; b++) {
      pairs.push({ before: null, after: after[b] });
    }
    return pairs;
  }

  /**
   * What a row says, read from its data cells alone. A sheet that grows adds
   * empty cells to every row, and a row whose text is taken whole would then
   * look like a new row to the alignment below.
   */
  function signature(row) {
    var texts = cellsIn(row).map(function (cell) { return cell.textContent; });
    // A sheet that grows pads every row with empty cells; that is not content.
    while (texts.length && texts[texts.length - 1] === '') {
      texts.pop();
    }
    return texts.join('\u0001');
  }

  /**
   * What a cell looks like, without saying where it is. A row pushed down by
   * an insert keeps its contents and its formatting, and only its address
   * moves, so comparing addresses would mark the whole sheet below the edit.
   * Attributes are normalized because the exporter writes an empty class on
   * every cell once a sheet grows, which is not something anyone can see.
   */
  function cellSignature(cell) {
    return cell.innerHTML
      + '|' + attribute(cell, 'style')
      + '|' + attribute(cell, 'class')
      + '|' + attribute(cell, 'colspan')
      + '|' + attribute(cell, 'rowspan')
      + '|' + attribute(cell, 'width')
      + '|' + attribute(cell, 'height');
  }

  function attribute(element, name) {
    return (element.getAttribute(name) || '').trim();
  }

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
