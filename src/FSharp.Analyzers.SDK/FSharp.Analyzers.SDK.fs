namespace FSharp.Analyzers.SDK

open System
open FSharp.Compiler
open FSharp.Compiler.SyntaxTree
open FSharp.Compiler.SourceCodeServices
open System.Runtime.InteropServices

/// Marks an analyzer for scanning
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type AnalyzerAttribute([<Optional; DefaultParameterValue "Analyzer">] name: string) =
  inherit Attribute()

  member _.Name = name

type Context =
    { FileName: string
      Content: string[]
      ParseTree: ParsedInput
      TypedTree: FSharpImplementationFileContents
      Symbols: FSharpEntity list
      GetAllEntities: bool -> AssemblySymbol list}

type Fix =
    { FromRange : Range.range
      FromText : string
      ToText : string }

type Severity =
    | Info
    | Warning
    | Error

type Message =
    { Type: string
      Message: string
      Code: string
      Severity: Severity
      Range: Range.range
      Fixes: Fix list }

type Tooltip =
    { Message: string
      Code: string
      Range: Range.range
    }

type AnalyzerOutput = {
  Messages: Message list
  Tooltips: Tooltip list
}

type Analyzer = Context -> AnalyzerOutput