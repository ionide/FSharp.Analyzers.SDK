module FSharp.Analyzers.Cli.Tests.OutputTests

open System
open System.IO
open NUnit.Framework
open FSharp.Analyzers.Cli.Output

let private relativeTo (codeRoot: string) (file: string) =
    (codeRootUri codeRoot).MakeRelativeUri(Uri file).OriginalString

[<Test>]
let ``a path is relative to a code root without a trailing separator`` () =
    let root = Path.Combine(Path.GetTempPath(), "repo")

    Assert.That(relativeTo root (Path.Combine(root, "src", "A.fs")), Is.EqualTo "src/A.fs")

[<Test>]
let ``a path is relative to a code root with a trailing separator`` () =
    let root =
        Path.Combine(Path.GetTempPath(), "repo")
        + string Path.DirectorySeparatorChar

    Assert.That(relativeTo root (Path.Combine(root, "src", "A.fs")), Is.EqualTo "src/A.fs")
