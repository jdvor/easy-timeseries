@../AGENTS.md

## Claude Code specifics

- For C# navigation, prefer the `cwm-roslyn-navigator` MCP tools (`find_symbol`, `find_references`,
  `get_diagnostics`, `get_project_graph`), when configured, over reading whole files or running `dotnet build`.
- For external library docs, use the `context7` MCP first, and `microsoft-docs` MCP for Microsoft and Azure APIs,
  when configured.
