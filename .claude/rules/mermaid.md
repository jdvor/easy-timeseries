---
paths:
  - "**/*.md"
---

# Mermaid Charts

Mermaid parses label and message text; it does not treat it as an opaque string. Some ordinary punctuation characters
are grammar tokens and silently split a line into two statements. Check text against the table below **before**
writing the diagram, not after the parse error.

## Characters that break text

| Character           | Where it breaks                        | Fix                                   |
| ------------------- | -------------------------------------- | ------------------------------------- |
| semicolon           | anywhere - it is a statement separator | `#59;` (entity code)                  |
| hash                | anywhere - it starts an entity code    | `#35;`                                |
| double quote        | inside a quoted label                  | `#quot;`                              |
| brackets and braces | flowchart node labels                  | wrap the whole label in double quotes |
| angle brackets      | labels, when parsed as HTML            | `#lt;` and `#gt;`                     |

Entity codes are the general escape and work in every diagram type: `#59;` `#35;` `#quot;` `#lt;` `#gt;`, plus named
and decimal HTML entities such as `#infin;` and `#9829;`.

Generic C# types are the common trap here: write `IMaterializer#lt;T#gt;`, or quote the label.

## Reserved words

These break renderers when used as bare node identifiers in flowcharts:

`STYLE`, `LINKSTYLE`, `CLASSDEF`, `CLASS`, `CLICK`, `GRAPH`, `DIR`, `subgraph`, `end`, `DOWN`, `UP`

Lowercase `end` is the dangerous one: it terminates `subgraph`, `loop`, `alt`, `opt` and `box`, so a node called `end`
closes a block that is still open. Capitalize it (`End`) or quote it.

A node id starting with `o` or `x` immediately after an edge is read as a circle or cross edge type
(`A---oB` is an edge, not a node named `oB`). Put a space before it or capitalize the id.

## Line breaks

| Diagram                           | How                                                                            |
| --------------------------------- | ------------------------------------------------------------------------------ |
| Sequence messages, notes, aliases | `<br/>`                                                                        |
| Flowchart labels                  | markdown strings - double quotes wrapping backticks, with real newlines inside |
