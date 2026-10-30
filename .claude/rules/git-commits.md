# Git Commits

## Commit message format

Follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/) for every new commit.

```
{type}{!}: {header}

{optional body}

{optional footer}
```

**Types:** `feat`, `fix`, `refactor`, `test`, `doc`, `perf`, `chore`, `ci`, `build`, `revert`

The `publish.yml` workflow derives the release version from these prefixes (`paulhatch/semantic-version`), so the
type is not cosmetic:

| Element                                                                    | Bump  |
| -------------------------------------------------------------------------- | ----- |
| `fix`                                                                      | PATCH |
| `feat`                                                                     | MINOR |
| `!` after type/scope, or a `BREAKING CHANGE:` footer                       | MAJOR |
| Any other type (`refactor`, `test`, `doc`, `perf`, `chore`, `ci`, `build`) | none  |

`!` and `BREAKING CHANGE:` combine with any type, not just `feat`/`fix`, e.g. `refactor!: drop legacy reader`.

**DO:**

- Header: max 89 chars, imperative mood, lowercase, no trailing period.
- Use `!` immediately before the `:` to signal a breaking change: `feat!: replace the storage contract`.
- Add a `BREAKING CHANGE: <description>` footer when the header alone does not explain the break.
- Add `[skip ci]` to the body when a CI run is not needed.
- Write the body to explain *why*, not *what* - the diff shows what changed.
- Keep the body short: at most 130 chars per line, ideally under 1000 characters in total.
- Make one logical change per commit; include tests and documentation in the same commit as the change they cover.
- On an unmerged branch, fix defects of the branch's own earlier commits as `git commit --fixup <sha>` and squash them
  (`git rebase --autosquash`) before opening the PR. Use `fix:` only for behavior that has been released.

**DON'T:**

- Don't write vague messages like "fix bug" or "update code".
- Don't bundle unrelated changes in one commit.
- Don't split a feature into separate `feat:`, `test:` and `doc:` commits - each commit should build, pass its tests
  and document itself.
- Don't force-push to `master`.
- Don't skip pre-commit hooks with `--no-verify`.
- Don't add AI attribution trailers such as `Co-Authored-By: <model>`.

## When to commit

Shape the commits once, from the whole body of work, just before the PR - not step by step while implementing.
Only then is it clear which changes belong together and which later changes were corrections of earlier ones.

**DO:**

- Leave the work uncommitted while implementing, including across the steps of an implementation plan.
- When a checkpoint is worth protecting, make a local commit and treat it as disposable.
- Before the PR, regroup the branch into logical commits (scripted rebase with `pick`/`fixup`, or a soft reset and
  new commits). Check that the final tree is unchanged and that every commit builds and passes its tests.
- Keep a backup branch of the pre-rewrite history until the PR is merged.

**DON'T:**

- Don't commit after each plan step unless asked to.
- Don't rewrite history that has already been pushed and shared without asking first.

## Legacy flags in history

Older commits carry `[BREAKING]` or `[minor]` in the body. They predate Conventional Commits in this repository:
`[BREAKING]` meant what `!` means now, `[minor]` what `feat` means now. Do not use them in new commits.

## Branch naming

Use prefixed names: `feature/<topic>`, `fix/<topic>`. No personal or opaque names like `my-branch` or `wip`.
