# Pull Requests

## Follow the template

The PR template is `.github/pull_request_template.md`.

**DO:**

- Follow the template's sections, their order and their headings exactly.
- Treat HTML comments in the template as instructions for writing the body. Leave them out of the PR body, and delete
  optional sections that do not apply.
- Fill in checklists truthfully: tick an item only when it was actually done or verified.

**DON'T:**

- Don't use an earlier PR as the template. It shows tone and level of detail, nothing more.
- Don't add sections the template does not have. Put a note a reviewer needs into the closest template section.

## Write for the reviewer

The reviewer sees the commit list, the diff, the file list and the CI result in GitHub already.

**DO:**

- Explain why the change is needed, what changed in behavior, breaking changes with migration, and risks.
- Mention testing only when it is not visible from CI: manual steps, an environment the tests need (Docker for
  `Easy.TimeSeries.AzureBlobs.Tests`), or an area that is deliberately left untested.
- Write each paragraph and list item as one line - GitHub reflows it (see `markdown.md`).

**DON'T:**

- Don't list the commits, the changed files or the test counts.
- Don't paste test runner output.

## GitHub CLI

Use `gh` for GitHub operations (`gh pr create`, `gh pr view`, `gh pr diff`, `gh api ...`). Pass multi-line bodies
through `--body-file -` with a heredoc, so Markdown and quotes survive the shell.
