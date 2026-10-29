// rva-to-method.fsx — mapeia RVA de GameAssembly.dll -> método (Assembly-CSharp etc.)
//
// Uso:
//   dotnet fsi docs/re/tools/rva-to-method.fsx 0xF72955 0x4525346
//
// Como funciona: o Il2CppInterop grava, na geração do interop, o arquivo
// `BepInEx\interop\MethodAddressToToken.db` (magic "UMTM", versão 1). Ele contém, para cada
// método do jogo, o RVA do ponteiro nativo (relativo à imagem, igual ao offset do WER) + o
// MetadataToken da assembly interop correspondente. Ordenado por RVA, então a busca binária
// devolve o método que *contém* o endereço de falha.
//
// Formato (Il2CppInterop.Common.Maps.MethodAddressToTokenMapBase):
//   int32 Magic; int32 Version; int32 NumAssemblies; int32 NumMethods; int32 DataOffset;
//   [NumAssemblies] string (7-bit length + UTF8)                     <- nomes das assemblies
//   [NumMethods]    int64 rva                                        <- ordenado
//   [NumMethods]    (int32 token, int32 assemblyIndex)
//
// Limitação importante: o DB é um mapa de *ponteiros de método*, não de layout de código.
// Vários métodos podem compartilhar o mesmo endereço (stub/thunk compartilhado). O script
// imprime `aliases` exatamente para expor isso: aliases > 1 significa que patchar esse
// endereço afeta todos os métodos listados.

open System
open System.IO
open System.Text
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.Reflection.PortableExecutable
open System.Collections.Generic

let interopDir =
    let env = Environment.GetEnvironmentVariable "ASKA_INTEROP"
    if String.IsNullOrWhiteSpace env
    then @"C:\Program Files (x86)\Steam\steamapps\common\ASKA\BepInEx\interop"
    else env

let dbPath = Path.Combine(interopDir, "MethodAddressToToken.db")
if not (File.Exists dbPath) then
    eprintfn "MethodAddressToToken.db não encontrado em %s (defina ASKA_INTEROP)" interopDir
    exit 2

let bytes = File.ReadAllBytes dbPath
let numAsm = BitConverter.ToInt32(bytes, 8)
let numMethods = BitConverter.ToInt32(bytes, 12)
let dataOffset = BitConverter.ToInt32(bytes, 16)

let mutable pos = 20
let readStr () =
    let mutable len = 0
    let mutable shift = 0
    let mutable b = 0uy
    let mutable cont = true
    while cont do
        b <- bytes.[pos]
        pos <- pos + 1
        len <- len ||| ((int b &&& 0x7F) <<< shift)
        shift <- shift + 7
        if b &&& 0x80uy = 0uy then cont <- false
    let s = Encoding.UTF8.GetString(bytes, pos, len)
    pos <- pos + len
    s
let asms = [| for _ in 1 .. numAsm -> readStr () |]

let ptrBase = dataOffset
let valBase = dataOffset + numMethods * 8
let addressAt i = BitConverter.ToInt64(bytes, ptrBase + i * 8)
let tokenAt i = BitConverter.ToInt32(bytes, valBase + i * 8)
let asmAt i = BitConverter.ToInt32(bytes, valBase + i * 8 + 4)

let readers = Dictionary<int, MetadataReader>()
let readerFor asmIdx =
    match readers.TryGetValue asmIdx with
    | true, md -> Some md
    | _ ->
        let simple = asms.[asmIdx].Split(',') |> Array.head
        let p = Path.Combine(interopDir, simple + ".dll")
        if not (File.Exists p) then None
        else
            let fs = File.OpenRead p
            let pe = new PEReader(fs)
            let md = pe.GetMetadataReader()
            readers.[asmIdx] <- md
            Some md

let nameAt i =
    let token = tokenAt i
    match readerFor (asmAt i) with
    | None -> sprintf "<%s?>" (asms.[asmAt i].Split(',') |> Array.head)
    | Some md when (token >>> 24) <> 0x06 -> sprintf "<non-MethodDef 0x%08X>" token
    | Some md ->
        try
            let mdef = md.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(token &&& 0xFFFFFF))
            let tdef = md.GetTypeDefinition(mdef.GetDeclaringType())
            let ns = md.GetString tdef.Namespace
            let tn = md.GetString tdef.Name
            let mn = md.GetString mdef.Name
            (if ns = "" then tn else ns + "." + tn) + "::" + mn
        with ex -> sprintf "<token 0x%08X: %s>" token ex.Message

let indexContaining (rva: int64) =
    let mutable lo = 0
    let mutable hi = numMethods - 1
    let mutable ans = -1
    while lo <= hi do
        let mid = (lo + hi) / 2
        if addressAt mid <= rva then (ans <- mid; lo <- mid + 1) else hi <- mid - 1
    ans

let nextDistinct (i: int) =
    let start = addressAt i
    let mutable j = i + 1
    while j < numMethods && addressAt j = start do j <- j + 1
    j

let aliasesAt (rva: int64) =
    let mutable i = indexContaining rva
    if i < 0 then 0
    else
        while i > 0 && addressAt (i - 1) = rva do i <- i - 1
        let mutable n = 0
        while i + n < numMethods && addressAt (i + n) = rva do n <- n + 1
        n

let describe (rva: int64) =
    let i = indexContaining rva
    if i < 0 then printfn "0x%X: antes do primeiro método (min 0x%X)" rva (addressAt 0)
    else
        let start = addressAt i
        let j = nextDistinct i
        let stop = if j < numMethods then addressAt j else -1L
        let span = if stop > 0L then sprintf "0x%X" (stop - start) else "?"
        let aliases = aliasesAt start
        let methodLabel =
            if aliases > 1 then sprintf "(um de %d — ver lista abaixo)" aliases
            else nameAt i
        printfn ""
        printfn "RVA 0x%X" rva
        printfn "  método    : %s" methodLabel
        printfn "  start     : 0x%X   offset dentro do método: +0x%X   tamanho~%s" start (rva - start) span
        printfn "  aliases   : %d método(s) compartilham 0x%X%s" aliases start (if aliases > 1 then "  <-- patch por endereço afeta todos" else "")
        if aliases > 1 then
            let mutable k = i
            while k > 0 && addressAt (k - 1) = start do k <- k - 1
            for n in k .. k + aliases - 1 do printfn "      - %s" (nameAt n)

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length = 0 then
    eprintfn "uso: dotnet fsi rva-to-method.fsx <rva-hex> [rva-hex ...]"
    exit 2
for a in args do
    let s = a.Trim()
    let s = if s.StartsWith "0x" || s.StartsWith "0X" then s.Substring 2 else s
    match Int64.TryParse(s, Globalization.NumberStyles.HexNumber, null) with
    | true, rva -> describe rva
    | _ -> eprintfn "não é hex: %s" a
