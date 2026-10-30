---
name: dotnet-test
description: >
  Run .NET tests with `dotnet test`. Covers detecting whether a repository uses MTP
  (Microsoft.Testing.Platform) or VSTest mode via global.json, the incompatible
  command-line options for each mode, how to read the "Zero tests ran" / exit code 5
  failure signature, cross-checking results by running the test executable directly,
  and the full MTP exit code table. Load this skill whenever running `dotnet test`,
  diagnosing a failed or empty test run, or investigating "zero tests ran" in a .NET
  project.
---

# Running Tests with `dotnet test`

`dotnet test` has two runner modes with **different and incompatible command-line options**. Check which mode the
repository uses before composing the command.

## 1. Detect the mode first

Read `global.json` in the repository root:

```json
{
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

| `global.json` `test.runner`  | Mode   | Notes                                         |
| ---------------------------- | ------ | --------------------------------------------- |
| `Microsoft.Testing.Platform` | MTP    | .NET 10 SDK and later only                    |
| `VSTest`, or key absent      | VSTest | The default; the only mode before .NET 10 SDK |

## 2. MTP mode - the option list is short

Only these are recognized by `dotnet test` itself. **Anything else is forwarded to the test application**, which
usually rejects it.

- Target selection (pick exactly one): `--project`, `--solution`, `--test-modules` (plus `--root-directory`)
- Build: `-c|--configuration`, `-f|--framework`, `-a|--arch`, `--os`, `-r|--runtime`, `--no-build`, `--no-restore`,
  `--property:<NAME>=<VALUE>` (`-p`)
- Output: `-v|--verbosity`, `--output <Normal|Detailed>`, `--no-ansi`, `--no-progress`
- Results: `--results-directory`, `--diagnostic-output-directory`, `--config-file`, `--minimum-expected-tests`,
  `--max-parallel-test-modules`
- Launch profile: `--no-launch-profile`, `--no-launch-profile-arguments`

**DO:**

- Select a project with `--project <path>`, a solution with `--solution <path>`, a dll with `--test-modules <glob>`.
- Put any test-application arguments after a literal `--`, e.g. `dotnet test -- --report-trx`.
- Reach for `--diagnostic-output-directory <dir>` when a run behaves strangely; it writes a `.diag` trace per module.

**DON'T:**

- Don't pass `--nologo`. It is a VSTest-mode option. In MTP mode it is forwarded to the test app, which fails to
  parse it and exits before discovery.
- Don't pass `--logger`, `--filter`, `--collect`, `--blame`, or `--settings`. Those are VSTest-mode options too.
- Don't pass a bare positional path (`dotnet test Foo.csproj`); use `--project Foo.csproj`.

## 3. The failure signature to recognize

An unrecognized forwarded argument produces **no error message naming the argument**. It looks like this:

```
Foo.Tests.dll (net10.0) Zero tests ran
Exit code: 5

Test run summary: Zero tests ran
  error: 1
  total: 0
```

The wording is misleading. **Exit code 5 means the command-line arguments were invalid**, not that discovery found
nothing - that would be exit code 8. The test host rejected an argument and exited before discovery ever started.
Duration is a few tens of milliseconds and the `.diag` trace stops early during host startup.

**When you see this, suspect your own command line first.** Before reporting a tooling defect or an environment
problem:

1. Re-run with no options at all: `dotnet test --project <path>`. If that works, an option was the problem.
2. Only if the bare form also fails, investigate further.

Do not describe this as the tool "misreporting" - the run genuinely did not happen.

The same exit code 5 also appears for a _valid_ option that only some projects understand, in a solution mixing
test frameworks or extensions. Route those per project with the `TestingPlatformCommandLineArguments` MSBuild
property rather than passing them on the command line.

### Suppressing the banner

`--nologo` is the wrong tool in MTP mode. Use any of:

- `--no-banner`, passed to the test application: `dotnet test -- --no-banner`
- the `DOTNET_NOLOGO=1` environment variable
- the `TESTINGPLATFORM_NOBANNER=1` environment variable

## 4. Cross-checking a result

Every MTP test project builds to a self-contained executable that can be run directly:

```shell
./tests/Foo.Tests/bin/Debug/net10.0/Foo.Tests
```

This bypasses `dotnet test` entirely and takes the platform's own options (`--list-tests`, `--filter`, ...). It is
a useful second opinion, but a discrepancy between it and `dotnet test` almost always means the `dotnet test`
command line was wrong, not that the results differ.

## 5. Exit codes

| Code | Meaning                                                                |
| ---- | ---------------------------------------------------------------------- |
| 0    | Success                                                                |
| 1    | Unknown error - catch all; read the output                             |
| 2    | At least one test failed                                               |
| 3    | Session aborted, for example by Ctrl+C                                 |
| 4    | Extension setup is invalid; the session cannot run                     |
| 5    | **Invalid command-line arguments passed to the test app** - see above  |
| 7    | Session could not complete; likely crashed                             |
| 8    | The session ran zero tests                                             |
| 9    | Minimum execution policy violated (`--minimum-expected-tests`)         |
| 10   | The test framework failed for an infrastructure reason, e.g. a fixture |
| 13   | Stopped after reaching `--maximum-failed-tests`                        |

Codes can be suppressed with `--ignore-exit-code 8` or the `TESTINGPLATFORM_EXITCODE_IGNORE` environment variable.
A project whose tests are all skipped otherwise exits 8.

## References

- [dotnet test with MTP](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp)
- [dotnet test with VSTest](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-vstest)
- [Migrate from VSTest to MTP](https://learn.microsoft.com/dotnet/core/testing/migrating-vstest-microsoft-testing-platform)
