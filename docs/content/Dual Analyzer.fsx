(**
---
category: end-users
categoryindex: 1
index: 2
---

# Writing an analyzer for both console and editor

With a little orchestration it is possible to easily write two analyzer functions that share a common implementation.

*)

(*** hide ***)
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Analyzers.SDK.dll"
#r "../../src/FSharp.Analyzers.Cli/bin/Release/net6.0/FSharp.Compiler.Service.dll"
(** *)

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax

/// This analyzer function will try and detect if any `System.*` open statement was found after any non System open.
/// See https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/conventions#sort-open-statements-topologically
/// Note that this implementation is not complete and only serves as an illustration.
/// Nested modules are not taken into account.
let private topologicallySortedOpenStatementsAnalyzer (untypedTree: ParsedInput) : Async<Message list> =
    async {
        let allOpenStatements =
            let allOpenStatements = ResizeArray<string * range>()

            let (|LongIdentAsString|) (lid: SynLongIdent) =
                lid.LongIdent |> List.map (fun ident -> ident.idText) |> String.concat "."

            let rec visitSynModuleSigDecl (decl: SynModuleSigDecl) =
                match decl with
                | SynModuleSigDecl.Open(SynOpenDeclTarget.ModuleOrNamespace(longId = LongIdentAsString value), mOpen) ->
                    allOpenStatements.Add(value, mOpen)
                | _ -> ()

            let rec visitSynModuleDecl (decl: SynModuleDecl) =
                match decl with
                | SynModuleDecl.Open(SynOpenDeclTarget.ModuleOrNamespace(longId = LongIdentAsString value), mOpen) ->
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

        let isOpenStatement (openStatement: string, _) = openStatement.StartsWith("System")

        let nonSystemOpens = allOpenStatements |> List.skipWhile isOpenStatement

        return
            nonSystemOpens
            |> List.filter isOpenStatement
            |> List.map (fun (openStatement, mOpen) ->
                {
                    Type = "Unsorted System open statement"
                    Message = $"%s{openStatement} was found after non System namespaces where opened!"
                    Code = "SOT001"
                    Severity = Warning
                    Range = mOpen
                    Fixes = []
                }
            )
    }

[<CliAnalyzer "Topologically sorted open statements">]
let cliAnalyzer (ctx: CliContext) : Async<Message list> =
    topologicallySortedOpenStatementsAnalyzer ctx.ParseFileResults.ParseTree

[<EditorAnalyzer "Topologically sorted open statements">]
let editorAnalyzer (ctx: EditorContext) : Async<Message list> =
    match ctx.ParseFileResults with
    // The editor might not have any parse results for a given file. So we don't return any messages.
    | None -> async.Return []
    | Some parseResults -> topologicallySortedOpenStatementsAnalyzer parseResults.ParseTree

(**
Both analyzers will follow the same code path: the console application will always have the required data, while the editor needs to be more careful.  
⚠️ Please do not be tempted by calling `.Value` on the `EditorContext` 😉.

[Previous]({{fsdocs-previous-page-link}})
[Next]({{fsdocs-next-page-link}})
*)
