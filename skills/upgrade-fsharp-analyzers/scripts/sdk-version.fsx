// Inspects analyzer assemblies without loading them.
// For each DLL it prints the FSharp.Analyzers.SDK version it was compiled against and the
// names of the analyzers it registers (the names used by --exclude-analyzers / --include-analyzers).
//
// Usage: dotnet fsi sdk-version.fsx <analyzer.dll> [more dlls...]
// Typical location after `dotnet restore`:
//   ~/.nuget/packages/<package-id>/<version>/analyzers/dotnet/fs/*.dll
open System
open System.IO
open System.Reflection.Metadata
open System.Reflection.PortableExecutable

let paths =
    fsi.CommandLineArgs
    |> Array.skip 1

if Array.isEmpty paths then
    eprintfn "Usage: dotnet fsi sdk-version.fsx <analyzer.dll> [more dlls...]"
    exit 2

let analyzerAttributes =
    set
        [
            "CliAnalyzerAttribute"
            "EditorAnalyzerAttribute"
        ]

/// Name of the type that declares the constructor of a custom attribute.
let attributeTypeName (md: MetadataReader) (ca: CustomAttribute) =
    match ca.Constructor.Kind with
    | HandleKind.MemberReference ->
        let mr = md.GetMemberReference(MemberReferenceHandle.op_Explicit ca.Constructor)

        match mr.Parent.Kind with
        | HandleKind.TypeReference ->
            Some(md.GetString(md.GetTypeReference(TypeReferenceHandle.op_Explicit mr.Parent).Name))
        | HandleKind.TypeDefinition ->
            Some(
                md.GetString(md.GetTypeDefinition(TypeDefinitionHandle.op_Explicit mr.Parent).Name)
            )
        | _ -> None
    | HandleKind.MethodDefinition ->
        let m = md.GetMethodDefinition(MethodDefinitionHandle.op_Explicit ca.Constructor)
        Some(md.GetString(md.GetTypeDefinition(m.GetDeclaringType()).Name))
    | _ -> None

/// First fixed argument of the attribute blob, which is the analyzer name.
let firstStringArgument (md: MetadataReader) (ca: CustomAttribute) =
    let mutable reader = md.GetBlobReader ca.Value

    if reader.ReadUInt16() <> 1us then
        None
    else
        match reader.ReadSerializedString() with
        | null -> None
        | s -> Some s

let analyzerNames (md: MetadataReader) =
    let fromAttributes (handles: CustomAttributeHandleCollection) =
        handles
        |> Seq.map md.GetCustomAttribute
        |> Seq.choose (fun ca ->
            match attributeTypeName md ca with
            | Some n when analyzerAttributes.Contains n -> firstStringArgument md ca
            | _ -> None
        )

    md.TypeDefinitions
    |> Seq.map md.GetTypeDefinition
    |> Seq.collect (fun td ->
        Seq.concat
            [
                td.GetMethods()
                |> Seq.collect (fun h ->
                    fromAttributes (md.GetMethodDefinition(h).GetCustomAttributes())
                )
                td.GetProperties()
                |> Seq.collect (fun h ->
                    fromAttributes (md.GetPropertyDefinition(h).GetCustomAttributes())
                )
                td.GetFields()
                |> Seq.collect (fun h ->
                    fromAttributes (md.GetFieldDefinition(h).GetCustomAttributes())
                )
            ]
    )
    |> Seq.distinct
    |> Seq.sort
    |> Seq.toList

for path in paths do
    use fs = File.OpenRead path
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()

    let sdkRef =
        md.AssemblyReferences
        |> Seq.map md.GetAssemblyReference
        |> Seq.tryFind (fun r -> md.GetString r.Name = "FSharp.Analyzers.SDK")

    match sdkRef with
    | None ->
        printfn
            "%s: no reference to FSharp.Analyzers.SDK, not an analyzer assembly"
            (Path.GetFileName path)
    | Some r ->
        let names = analyzerNames md
        printfn "%s" (Path.GetFileName path)
        printfn "  FSharp.Analyzers.SDK: %O" r.Version
        printfn "  analyzers (%d):" names.Length

        for n in names do
            printfn "    %s" n
