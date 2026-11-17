module FSharp.Analyzers.Cli.Tests.ASTCollectingTests

open NUnit.Framework
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

let private checker = FSharpChecker.Create()

let private parse (source: string) =
    let fileName = "Test.fs"

    let options =
        { FSharpParsingOptions.Default with
            SourceFiles = [| fileName |]
        }

    let result =
        checker.ParseFile(fileName, SourceText.ofString source, options)
        |> Async.RunSynchronously

    Assert.That(result.Diagnostics, Is.Empty, sprintf "parse diagnostics: %A" result.Diagnostics)
    result.ParseTree

type private IdentCollector() =
    inherit ASTCollecting.SyntaxCollectorBase()

    member val Exprs = ResizeArray<string>()
    member val Types = ResizeArray<string>()

    override this.WalkExpr(_, expr) =
        match expr with
        | SynExpr.Ident ident -> this.Exprs.Add ident.idText
        | _ -> ()

    override this.WalkType(_, ty) =
        match ty with
        | SynType.LongIdent(SynLongIdent(id = ids)) ->
            ids
            |> List.iter (fun ident -> this.Types.Add ident.idText)
        | _ -> ()

let private collect source =
    let collector = IdentCollector()
    ASTCollecting.walkAst collector (parse source)
    collector

[<Test>]
let ``walkAst visits the expression of a record expression spread`` () =
    let collector =
        collect
            """
module M
type A = { X: int }
type B = { X: int; Y: int }
let spreadSource = { X = 1 }
let fieldValue = 2
let b = { ...spreadSource; Y = fieldValue }
"""

    Assert.That(collector.Exprs, Does.Contain "spreadSource")
    Assert.That(collector.Exprs, Does.Contain "fieldValue")

[<Test>]
let ``walkAst visits the type of a record type spread`` () =
    let collector =
        collect
            """
module M
type SpreadSource = { X: int }
type R = { ...SpreadSource; Y: FieldType }
"""

    Assert.That(collector.Types, Does.Contain "SpreadSource")
    Assert.That(collector.Types, Does.Contain "FieldType")
