/* Workbooks read like a spreadsheet: one sheet at a time at its actual size
 * behind a sheet tab strip. */
AsposeViewer.definePresenter('cells', {
  kind: 'Workbook',
  glyph: 'C',
  views: {
    sheets: { layout: 'tabs', noun: 'Sheet', zoom: 1 }
  }
});
