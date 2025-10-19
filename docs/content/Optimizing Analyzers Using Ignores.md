---
category: end-users
categoryindex: 2
index: 6
---

# Optimizing Analyzers Using Ignores

## Overview

When writing analyzers, we often seen code which looks like this:

```fsharp
[<EditorAnalyzerAttribute "YourAnalyzer">]
let analyzerEditorContext (ctx: EditorContext) =
    handler ctx.TypedTree
```

The handler code will typically dig into walking the AST/TAST to analyze code and see if the conditions of the current analyzer are being met.

## Using Ignore Ranges

We can optimize our analyzer by checking if there are any ignore ranges with a File scope for the current analyzer. This allows us to skip running the analyzer entirely for files which have been marked to ignore all hits from this analyzer. We cannot skip analyzing files which have more granular ignore ranges (like line or region), since we need to walk the tree to see if any hits fall outside of those ranges.

```fsharp
[<EditorAnalyzerAttribute "YourAnalyzer">]
let analyzerEditorContext (ctx: EditorContext) =
    let ignoreRanges = ctx.AnalyzerIgnoreRanges |> Map.tryFind "your code here"
    
    match ignoreRanges with
    | Some ranges -> 
        if ranges |> List.contains File then
            async { return [] }
        else
            handler ctx.TypedTree
    | None -> handler ctx.TypedTree
```

