/* Word documents read as a continuous run of pages, navigated by the
 * outline of the headings the page view places on each page. */
AsposeViewer.definePresenter('words', {
  kind: 'Word document',
  glyph: 'W',
  views: {
    pages: { layout: 'pages', noun: 'Page', sidebar: 'outline' }
  }
});
