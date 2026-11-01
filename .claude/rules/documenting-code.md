---
paths:
  - "**/*.cs"
---

# Documenting Code

Documentation in code is read by developers as-is, in the editor, not rendered by another tool. So:

- Use XML tags sparingly - `<summary>`, `<param>`, `<remarks>`, `<see cref>` cover almost everything.
- Format width and whitespace for a human reader.
- Keep `<summary>` short - a few sentences at most. Put a longer explanation in `<remarks>`.
- Document truly complex ideas, algorithms or procedures in a Markdown page under `docs/` (and `docs/SUMMARY.md`),
  and link to it from the code. The byte format, for example, lives in `docs/03-layout.md`, not in XML comments.

When to write a comment at all is covered in `docs/09-code-style.md` ("Documentation and comments").
