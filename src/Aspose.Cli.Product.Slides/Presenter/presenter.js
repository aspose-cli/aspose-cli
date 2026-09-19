/* Presentations read like a slide editor: a numbered slide rail, one slide
 * on the stage with its speaker notes, and a full-screen slideshow. */
AsposeViewer.definePresenter('slides', {
  kind: 'Presentation',
  glyph: 'S',
  views: {
    slides: {
      layout: 'deck',
      noun: 'Slide',
      sidebar: 'thumbnails',
      notes: function (part) { return part.properties && part.properties.notes; }
    }
  }
});
