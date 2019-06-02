// Learn more about F# at http://fsharp.org
module Erinome

open System
open FSharp.Data
open System.IO
open System.Text.RegularExpressions

type Ipynb = JsonProvider<"https://drive.google.com/uc?export=download&id=1034rXX-xPwDbY-yvgmmXWGBBa3pE7-Wf">


type CodeCell = {
    Lines: string list
}

type Endpoint =
    | Get of string
    | Post of string

type SliderEl = {
    min: int
    max: int
    step: int
    defaultValue: int
}


type ColabFormElement =
    | Slider of SliderEl

let getTempDir() =
   let tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
   Directory.CreateDirectory tempDirectory |> ignore
   tempDirectory


let (|Prefix|_|) (p:string) (s:string) =
    if s.StartsWith(p) then
        Some(s.Substring(p.Length))
    else
        None

let parameterRegex =
    Regex(@"^\s*(?<VariableName>\w*)\s*=\s*(?<DefaultValue>.*)\s*#@param(?<Options>.*)$",
        (RegexOptions.Compiled ||| RegexOptions.IgnoreCase))

let (|ParamRegex|_|) input =
        let m = parameterRegex.Match(input)
        if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
        else None

let lineEndpoint (line:string) =
    match line.TrimStart() with
    | Prefix "#@GET" path -> Some (Get (path.Trim()))
    | Prefix "#@POST" path -> Some (Post (path.Trim()))
    | _ -> None

let colabElementOptionsParser variableName defaultValue options =
    Some 0

let colabFormLine optionsParser line =
    match line with
    | ParamRegex [ variableName; defaultValue; options ] ->
        optionsParser (variableName.Trim()) (defaultValue.Trim()) (options.Trim())
    | _ ->
        None


let generateEndpointCode endpoint cell =
    let paramss =
        cell.Lines
        |> List.map (colabFormLine colabElementOptionsParser)
    printfn "Params: %A" paramss
    String.Join('\n', cell.Lines)

let cellEndpoint cell =
    cell.Lines
    |> List.choose lineEndpoint
    |> List.tryLast

let generateCode cell =
    match cellEndpoint cell with
    | None ->
        String.Join('\n', cell.Lines)
    | Some endpoint ->
        generateEndpointCode endpoint cell


let generateServer (cells:CodeCell list) =
    cells
    |> List.map generateCode
    |> fun x -> String.Join '\n', x


let useNotebook uri = async {
    printfn "Loading notebook from uri: %s" uri
    let! np = Ipynb.AsyncLoad(uri)
    let name = np.Metadata.Colab.Name
    printfn "Notebook name: %s" name

    let cells =
        List.ofArray np.Cells
        |> List.where (fun c -> c.CellType = "code")
        |> List.map (fun c -> { Lines = List.ofArray c.Source })

    let server = generateServer cells
    //printf "Cells: %A" cells


    return 0
}




let main argv =
    let ret =
        match List.ofArray argv with
        | [] ->
            printfn "Please provide file name of notebook"
            1
        | uri::[] ->
            Async.RunSynchronously <| useNotebook uri
        | _ ->
            printfn "Should only have one argument"
            2

    printfn "Return code: %A - Press any key to exit" ret
    System.Console.ReadKey() |> ignore
    ret

let beHappy a = a + 5