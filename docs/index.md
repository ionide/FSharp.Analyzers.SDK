# Ionide FSharp.Analyzers.SDK

Library used for building custom analyzers for FSAC / F# editors.

F# analyzers are live, real-time, project based plugins that enables to diagnose source code and surface custom errors, warnings and code fixes into editor. Read more about analyzers here - https://medium.com/lambda-factory/introducing-f-analyzers-772487889429

## How to build

1. Install the .NET SDK version specified in `global.json`
2. `dotnet tool restore`
3. Open and build in your favorite IDE, or use `dotnet build`

## How to run sample
1. `dotnet build -c Release`
2. Run the console application:

```shell
dotnet run --project src\FSharp.Analyzers.Cli\FSharp.Analyzers.Cli.fsproj -- --project ./samples/OptionAnalyzer/OptionAnalyzer.fsproj --analyzers-path ./samples/OptionAnalyzer/bin/Release --verbosity d
```

You can also set up a run configuration of FSharp.Analyzers.Cli in your favorite IDE using similar arguments. This also allows you to debug FSharp.Analyzers.Cli.

## Using Analyzers

Checkout our [Getting Started](https://ionide.io/FSharp.Analyzers.SDK/content/Getting%20Started%20Using.html) guide for analyzer users!

## Writing Analyzers

Checkout our [Getting Started](https://ionide.io/FSharp.Analyzers.SDK/content/Getting%20Started%20Writing.html) guide for analyzer authors!

## How to contribute

*Imposter syndrome disclaimer*: I want your help. No really, I do.

There might be a little voice inside that tells you you're not ready; that you need to do one more tutorial, or learn another framework, or write a few more blog posts before you can help me with this project.

I assure you, that's not the case.

This project has some clear Contribution Guidelines and expectations that you can [read here](https://github.com/Krzysztof-Cieslak/FSharp.Analyzers.SDK/blob/main/CONTRIBUTING.md).

The contribution guidelines outline the process that you'll need to follow to get a patch merged. By making expectations and process explicit, I hope it will make it easier for you to contribute.

And you don't just have to write code. You can help out by writing documentation, tests, or even by giving feedback about this work. (And yes, that includes giving feedback about the contribution guidelines.)

Thank you for contributing!


## Contributing and copyright

The project is hosted on [GitHub](https://github.com/Krzysztof-Cieslak/FSharp.Analyzers.SDK) where you can [report issues](https://github.com/Krzysztof-Cieslak/FSharp.Analyzers.SDK/issues), fork
the project and submit pull requests.

The library is available under [MIT license](https://github.com/Krzysztof-Cieslak/FSharp.Analyzers.SDK/blob/main/LICENSE.md), which allows modification and redistribution for both commercial and non-commercial purposes.

[Next]({{fsdocs-next-page-link}})
