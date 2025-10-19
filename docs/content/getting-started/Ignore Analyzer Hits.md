---
category: getting-started
categoryindex: 1
index: 5
---

# Ignoring Analyzer Hits

The FSharp.Analyzers.SDK supports suppressing analyzer warnings through special comments which define ignore ranges. This allows you to disable specific analyzers for certain code sections without modifying the analyzer configuration globally.

## Comment Format

The comment format follows this pattern: `prefix: command [codes]`. You can specify multiple codes with one comment by delimiting the codes with commas. For example: `fsharpanalyzer: ignore-line CODE1, CODE2`.

## Current Line Ignore

To ignore analyzer warnings on a single line, use a comment with the analyzer code:

```fsharp
let someFunction () =
    let option = Some 42
    option.Value // fsharpanalyzer: ignore-line OV001
```

## Next Line Ignore

To ignore analyzer warnings on a single line, use a comment with the analyzer code:

```fsharp
let someFunction () =
    let option = Some 42
    // fsharpanalyzer: ignore-line-next OV001
    option.Value
```

## Region Ignore

To ignore analyzer warnings for a block of code, use start and end comments:

```fsharp
// fsharpanalyzer: ignore-region-start OV001
let someFunction () =
    let option = Some 42
    option.Value
// fsharpanalyzer: ignore-region-end
```

## Ignore File

To ignore all analyzer warnings in a file, place the following comment at the top of the file:

```fsharp
// fsharpanalyzer: ignore-file OV001
let someFunction () =
    let option = Some 42
    option.Value
```

[Previous]({{fsdocs-previous-page-link}})
[Next]({{fsdocs-next-page-link}})