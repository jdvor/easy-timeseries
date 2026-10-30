---
paths:
  - "**/*.md"
---

# Markdown for Humans

Documents under `docs/`, the READMEs and these agent instructions are read by people as files - in an editor, a
terminal, a git diff - and as the rendered mdbook.

- Keep line width at 130 characters at most. Tables and mermaid graphs are exempt: leave their rows as they are and
  do not pad them to the same width.
- Put the important things - most impactful changes, prerequisites - at the top of the document.
- Don't pack inline code densely: several backticked names side by side on one line are hard to read.
- Don't list several types or properties one after another in a sentence - use a bullet list or a table.
- Don't leave decision history in a document that went through several iterations ("previously considered X, now
  Y"). It distracts a reader who wants the big picture; put it in a separate decision log if it is worth keeping.
- Use mermaid graphs to explain complex relations or interactions (see `mermaid.md`).
- A new page under `docs/` also needs an entry in `docs/SUMMARY.md`, or mdbook will not show it.

## Rendered targets are not wrapped

The 130-character limit is for files. It does not apply to Markdown submitted to something that renders it and
controls its own width - GitHub PR descriptions, issue bodies and review comments. There, write each paragraph and
each list item as **one long line**: a hard-wrapped paragraph cannot widen and looks ragged in a narrow column.
Turning a local file into a PR body means unwrapping it first.
