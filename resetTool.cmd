dotnet tool uninstall -g fsharp-analyzers
fake build -t Pack
dotnet tool install --add-source ./out -g fsharp-analyzers