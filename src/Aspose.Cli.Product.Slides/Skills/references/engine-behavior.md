# Engine behavior

The CLI makes the engine's calls and reports what the engine did. These are behaviors of
Aspose.Slides an edit runs into; the CLI passes each on unchanged.

## replace_text

- It covers the text of shapes on slides, in groups, in tables and in SmartArt, and of notes
  only with `includeNotes: true`. It leaves the text of charts, layouts and masters as it is.
- A match does not span two paragraphs. Text split into several runs matches, and the
  replacement takes the format of the first run.
- No match is no error: `returned` is an empty list.
- An empty `oldText` is the engine's `ArgumentException`, reported as `ENGINE_ERROR`.

## Footers, slide numbers and dates

- A slide shows a footer, a slide number or a date only where its layout has that placeholder.
- Setting `footerVisible`, `slideNumberVisible` or `dateTimeVisible` copies the placeholder from
  the layout again, so text set on it before is lost: set the visibility first, then the text of
  `placeholder/Footer`.
- Setting the text of a date or slide number placeholder replaces its field with plain text.

## Charts

- A point's value that comes from the chart's workbook names its cell in `value.source`; setting
  the cell (`…/data/<worksheet>/<row>/<column>`) changes the point. A series' name is read only:
  set the cell its `name.source` names.
- A chart added with `initWithSample: false` and one series loses its categories on save.
- A category axis set to `Date` reads back as `Text` after the save.

## Positions

An address with a position (`paragraph/0`, `run/1`, `cell/1/2`, `series/0`, `point/2`) names
whatever is at that position. An edit reads each address after the whole batch, from the saved
file: when a later operation inserted or deleted before it, the entry shows the object now there.

## Evaluation mode

Without a license the engine reads text longer than five characters as its first characters and a
notice, and adds a watermark text box to every slide of every file it saves; reads and counts show
the box as a shape.
