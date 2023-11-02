(**
---
category: end-users
categoryindex: 1
index: 3
---

# Writing an analyzer for both console and editor

With a little orchestration it is possible to easily write two analyzer functions that share a common implementation.

*)

(*** hide ***)
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Analyzers.SDK.dll"
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Compiler.Service.dll"
(** *)

open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax

/// This analyzer function will try and detect if any `System.*` open statement was found after any non System open.
/// See https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/conventions#sort-open-statements-topologically
/// Note that this implementation is not complete and only serves as an illustration.
/// Nested modules are not taken into account.
let private topologicallySortedOpenStatementsAnalyzer
    (sourceText: ISourceText)
    (untypedTree: ParsedInput)
    (checkResults: FSharpCheckFileResults)
    : Async<Message list>
    =
    async {
        let allOpenStatements =
            let allOpenStatements = ResizeArray<string list * range>()

            let (|LongIdentAsString|) (lid: SynLongIdent) =
                lid.LongIdent |> List.map (fun ident -> ident.idText)

            let rec visitSynModuleSigDecl (decl: SynModuleSigDecl) =
                match decl with
                | SynModuleSigDecl.Open(
                    target = SynOpenDeclTarget.ModuleOrNamespace(longId = LongIdentAsString value; range = mOpen)) ->
                    allOpenStatements.Add(value, mOpen)
                | _ -> ()

            let rec visitSynModuleDecl (decl: SynModuleDecl) =
                match decl with
                | SynModuleDecl.Open(
                    target = SynOpenDeclTarget.ModuleOrNamespace(longId = LongIdentAsString value; range = mOpen)) ->
                    allOpenStatements.Add(value, mOpen)
                | _ -> ()

            match untypedTree with
            | ParsedInput.SigFile(ParsedSigFileInput(contents = contents)) ->
                for SynModuleOrNamespaceSig(decls = decls) in contents do
                    for decl in decls do
                        visitSynModuleSigDecl decl

            | ParsedInput.ImplFile(ParsedImplFileInput(contents = contents)) ->
                for SynModuleOrNamespace(decls = decls) in contents do
                    for decl in decls do
                        visitSynModuleDecl decl

            allOpenStatements |> Seq.toList

        let isSystemOpenStatement (openStatement: string list, mOpen: range) =
            let isFromBCL () =
                let line = sourceText.GetLineString(mOpen.EndLine - 1)

                match checkResults.GetSymbolUseAtLocation(mOpen.EndLine, mOpen.EndColumn, line, openStatement) with
                | Some symbolUse ->
                    match symbolUse.Symbol.Assembly.FileName with
                    | None -> false
                    | Some assemblyPath ->
                        // This might not be an airtight check
                        assemblyPath.ToLower().Contains "microsoft.netcore.app.ref"
                | _ -> false

            openStatement.[0].StartsWith("System") && isFromBCL ()

        let nonSystemOpens = allOpenStatements |> List.skipWhile isSystemOpenStatement

        return
            nonSystemOpens
            |> List.filter isSystemOpenStatement
            |> List.map (fun (openStatement, mOpen) ->
                let openStatementText = openStatement |> String.concat "."

                {
                    Type = "Unsorted System open statement"
                    Message = $"%s{openStatementText} was found after non System namespaces where opened!"
                    Code = "SOT001"
                    Severity = Warning
                    Range = mOpen
                    Fixes = []
                }
            )
    }

[<CliAnalyzer "Topologically sorted open statements">]
let cliAnalyzer (ctx: CliContext) : Async<Message list> =
    topologicallySortedOpenStatementsAnalyzer ctx.SourceText ctx.ParseFileResults.ParseTree ctx.CheckFileResults

[<EditorAnalyzer "Topologically sorted open statements">]
let editorAnalyzer (ctx: EditorContext) : Async<Message list> =
    match ctx.CheckFileResults with
    // The editor might not have any check results for a given file. So we don't return any messages.
    | None -> async.Return []
    | Some checkResults ->
        topologicallySortedOpenStatementsAnalyzer ctx.SourceText ctx.ParseFileResults.ParseTree checkResults

(**
Both analyzers will follow the same code path: the console application will always have the required data, while the editor needs to be more careful.  
⚠️ Please do not be tempted by calling `.Value` on the `EditorContext` 😉.

To enable a wide range of analyzers, both context types give access to very detailed information about the source code.  
Among this information is the full untyped abstract syntax tree (AST) and the typed abstract syntax tree (TAST).
As you can deduce from the example above, processing these trees is a very common task in an analyzer. But writing your own tree traversal code can be daunting and can also get quite repetitive over many analyzers.  
That's why the SDK offers the `ASTCollecting` and `TASTCollecting` modules. In there, you'll find facility types and functions to make your analyzers author life easier.
For both trees, a type is defined, `SyntaxCollectorBase` and `TypedTreeCollectorBase` respectively, with members you can override to have easy access to the tree elements you want to process.  
Just pass an instance with your overriden members to the `walkAst` or `walkTast` function.  

The `allOpenStatements` function from above, rewritten to make use of `walkAst`, could look like this:
*)

let allOpenStatementsWithWalker untypedTree =
    let allOpenStatements = ResizeArray<string list * range>()

    let (|LongIdentAsString|) (lid: SynLongIdent) =
        lid.LongIdent |> List.map (fun ident -> ident.idText)

    let walker =
        { new ASTCollecting.SyntaxCollectorBase() with
            override _.WalkSynModuleSigDecl(decl: SynModuleSigDecl) =
                match decl with
                | SynModuleSigDecl.Open(
                    target = SynOpenDeclTarget.ModuleOrNamespace(longId = LongIdentAsString value; range = mOpen)) ->
                    allOpenStatements.Add(value, mOpen)
                | _ -> ()

            override _.WalkSynModuleDecl(decl: SynModuleDecl) =
                match decl with
                | SynModuleDecl.Open(
                    target = SynOpenDeclTarget.ModuleOrNamespace(longId = LongIdentAsString value; range = mOpen)) ->
                    allOpenStatements.Add(value, mOpen)
                | _ -> ()
        }

    ASTCollecting.walkAst walker untypedTree

    allOpenStatements |> Seq.toList

(**

Because we want to process the `SynModuleSigDecl` and `SynModuleDecl` elements of the AST, we just override the two appropriate members of the `SyntaxCollectorBase` type in an [object expression](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/object-expressions) and pass the instance to `walkAst`.  
Much simpler and shorter than doing the traversal ourselves.

[Previous]({{fsdocs-previous-page-link}})
[Next]({{fsdocs-next-page-link}})
*)
