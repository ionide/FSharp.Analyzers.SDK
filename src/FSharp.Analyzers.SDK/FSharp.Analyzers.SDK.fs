namespace FSharp.Analyzers.SDK

open System
open FSharp.Compiler
open FSharp.Compiler.Syntax
open FSharp.Compiler.Symbols
open FSharp.Compiler.EditorServices
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
    { FromRange : Text.Range
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
      Range: Text.Range
      Fixes: Fix list }

type Analyzer = Context -> Message list