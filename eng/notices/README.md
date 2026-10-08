# Release notices

`release-notices.ps1` reads the actual published `.deps.json`, including the
self-contained runtime pack. It verifies restored NuGet archive hashes and copies
original license, notice and package metadata files without reformatting them.
The output `notices/index.json` records the package identities and file hashes;
the package's `SHA256SUMS` covers the complete directory.

Packages declaring only an SPDX expression use the corresponding full text here:

- `MIT.txt`: standard MIT permission and disclaimer text. Publication prepends
  the individual package copyright from its original NuGet metadata; packages
  without that attribution require explicit review.
- `Apache-2.0.txt`: the Apache License 2.0 text.

The original package metadata accompanies each fallback text.
These are license texts, not a second dependency roster. New license expressions
without supplied text fail publication and require an explicit legal-file review.
Review original upstream notices when changing dependencies; do not translate,
reflow or shorten them.
