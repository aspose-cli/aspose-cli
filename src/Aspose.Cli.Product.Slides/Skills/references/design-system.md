# Presentation design system

A deck is one visual system. The template carries it: theme colors, theme
fonts (including an East Asian font for CJK text), masters and layouts. Content
fills placeholders; it does not restyle them.

## Choosing the template

1. The user's brand template, when supplied.
2. Otherwise omit `--template`: `slides create` uses the built-in 16:9 design
   with a dark title slide, clean white content slides, a restrained teal
   accent, and Calibri with Microsoft YaHei for CJK text.
3. To change the look, edit the template in PowerPoint once and reuse it;
   never compensate slide by slide with shape styling. `slides create base.pptx`
   writes the built-in design as a starting point.

## Content rules

- One claim per slide; put the claim in the title.
- At most six bullets and two levels per slide. Split rather than shrink.
- Prefer a chart or a small table over dense prose; label data directly only
  when it improves comprehension; keep legends and gridlines quiet.
- Keep charts in the theme's accent colors so every slide agrees.
- Keep speaker notes short and in `set_notes`, not on the slide.

## What to look for in review

Title hierarchy, text overflowing its placeholder, text shrunk below about
14 pt, image crops and distortion, chart labels, footer and slide-number
placement, contrast, and missing CJK glyphs. For CJK content run
`aspose-cli fonts check deck.pptx` before rendering.
