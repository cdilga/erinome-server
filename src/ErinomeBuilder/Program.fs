// Learn more about F# at http://fsharp.org
module Erinome
open Maybe

open System
open FSharp.Data
open FSharpPlus
open System.IO
open System.Text.RegularExpressions

type Ipynb = JsonProvider<"https://drive.google.com/uc?export=download&id=1034rXX-xPwDbY-yvgmmXWGBBa3pE7-Wf">

type CodeCell = {
    Lines: string list
    CellNumber: int
}

type EndpointKind = Get | Post

type Endpoint = {
    path: string
    kind: EndpointKind
}

type SliderEl = {
    min: int
    max: int
    step: int
}

type ColabFormElementOptions =
    | Slider of SliderEl

type ColabFormElement<'a> = {
    defaultValue: string
    variableName: string
    elementOptions: 'a
}

let getTempDir() =
   let tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
   Directory.CreateDirectory tempDirectory |> ignore
   tempDirectory


let (|Prefix|_|) (p:string) (s:string) =
    if s.StartsWith(p) then
        Some(s.Substring(p.Length))
    else
        None

let compileRegex str = Regex(str, RegexOptions.Compiled)

let parameterRegex = compileRegex @"^\s*(?<VariableName>\w*)\s*=\s*(?<DefaultValue>.*)\s*#@param(?<Options>.*)$"
let sliderRegex = compileRegex @"{\s*type\s*:\s*""slider""\s*,\s*min\s*:\s*(?<min>[-|+|\d]\d*)\s*,\s*max\s*:\s*(?<max>[-|+|\d]\d*)\s*,\s*step\s*:\s*(?<step>\d+)\s*}"

let (|Regex|_|) (regex:Regex) input =
        let m = regex.Match(input)
        if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
        else None

let lineEndpoint (line:string) =
    match line.TrimStart() with
    | Prefix "#@GET" path -> Some { path = (path.Trim()); kind = Get }
    | Prefix "#@POST" path -> Some { path = (path.Trim()); kind = Post }
    | _ -> None

let colabElementOptionsParser options =
    match options with
    | Regex sliderRegex [min; max; step] ->
        maybe {
            let! mini = tryParse min
            let! maxi = tryParse max
            let! stepi = tryParse step
            return Slider { min = mini; max = maxi; step = stepi }
        }
    | _ -> None

let colabFormLine optionsParser line =
    match line with
    | Regex parameterRegex [ variableName; defaultValue; options ] ->
        maybe {
            let! elOptions = optionsParser (options.Trim())
            return { variableName = variableName.Trim(); defaultValue = defaultValue.Trim(); elementOptions = elOptions }
        }
    | _ ->
        None


let generateEndpointCode endpoint cell =
    let paramss =
        cell.Lines
        |> List.map (colabFormLine colabElementOptionsParser)
    let cellComment = sprintf "#Cell %d:endpoint" cell.CellNumber
    cellComment::cell.Lines

let cellEndpoint cell =
    cell.Lines
    |> List.choose lineEndpoint
    |> List.tryLast

let generateCode cell =
    match cellEndpoint cell with
    | None ->
        let cellComment = sprintf "#Cell %d" cell.CellNumber
        cellComment::cell.Lines
    | Some endpoint ->
        generateEndpointCode endpoint cell

let getPaths cells =
    cells
    |> List.choose cellEndpoint
    |> List.map (fun e -> e.path)

let generateHandlerClass endpoints =
    "asdf"


let generateServer (cells:CodeCell list) =
    cells
    |> List.map generateCode
    |> List.mapi (fun i x -> if i > 0 then ""::x else x)
    |> List.collect id
    |> fun x -> String.Join('\n', x)

let shareUrlRegex = compileRegex @"drive\/(.*?)[?#$]"

let getNotebookDownloadUri shareUrl =
    match shareUrl with
    | Regex shareUrlRegex [ nbId ] -> Some <| sprintf "https://drive.google.com/uc?export=download&id=%s" nbId
    | _ -> None

let generateServerFromShareUrl nbUrl = async {
    let downloadUri = getNotebookDownloadUri nbUrl
    match downloadUri with
    | Some uri ->
        let! np = Ipynb.AsyncLoad(uri)
        let name = np.Metadata.Colab.Name

        let cells =
            List.ofArray np.Cells
            |> List.where (fun c -> c.CellType = "code")
            |> List.mapi (fun i c -> { Lines = List.ofArray c.Source; CellNumber = i })

        let server = generateServer cells
        let paths = getPaths cells

        return Ok (name, server, paths)
    | None ->
        return Error "Invalid drive share url - make sure it is saved on google drive"
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