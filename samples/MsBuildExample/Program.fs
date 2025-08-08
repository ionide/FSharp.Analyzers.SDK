// For more information see https://aka.ms/fsharp-console-apps

let value = Some 42

printfn "The value is: %d" value.Value // This will cause a warning from the OptionAnalyzer