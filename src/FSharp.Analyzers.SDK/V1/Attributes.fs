namespace FSharp.Analyzers.SDK.V1

open System
open System.Runtime.InteropServices

type Analyzer = CliContext -> Async<Message list>
type EditorAnalyzer = CliContext -> Async<Message list>

[<AttributeUsage(AttributeTargets.Method
                 ||| AttributeTargets.Property
                 ||| AttributeTargets.Field)>]
type CliAnalyzerAttribute
    (
        [<Optional; DefaultParameterValue "Analyzer">] name: string,
        [<Optional; DefaultParameterValue("" :> obj)>] shortDescription: string,
        [<Optional; DefaultParameterValue("" :> obj)>] helpUri: string
    )
    =
    inherit Attribute()

    member val Name: string = name

    member val ShortDescription: string option =
        if String.IsNullOrWhiteSpace shortDescription then
            None
        else
            Some shortDescription

    member val HelpUri: string option =
        if String.IsNullOrWhiteSpace helpUri then
            None
        else
            Some helpUri

[<AttributeUsage(AttributeTargets.Method
                 ||| AttributeTargets.Property
                 ||| AttributeTargets.Field)>]
type EditorAnalyzerAttribute
    (
        [<Optional; DefaultParameterValue "Analyzer">] name: string,
        [<Optional; DefaultParameterValue("" :> obj)>] shortDescription: string,
        [<Optional; DefaultParameterValue("" :> obj)>] helpUri: string
    )
    =
    inherit Attribute()

    member val Name: string = name

    member val ShortDescription: string option =
        if String.IsNullOrWhiteSpace shortDescription then
            None
        else
            Some shortDescription

    member val HelpUri: string option =
        if String.IsNullOrWhiteSpace helpUri then
            None
        else
            Some helpUri
