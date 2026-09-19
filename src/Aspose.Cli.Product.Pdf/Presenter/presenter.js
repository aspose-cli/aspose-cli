/* PDF documents read like a PDF reader: a continuous run of pages with a
 * thumbnail sidebar and the size of the page in view. */
AsposeViewer.definePresenter('pdf', {
  kind: 'PDF document',
  glyph: 'PDF',
  views: {
    pages: {
      layout: 'pages',
      noun: 'Page',
      sidebar: 'thumbnails',
      status: function (part) { return part.properties && part.properties.size; }
    }
  }
});
