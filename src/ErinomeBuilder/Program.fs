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

type ErinomeFunctionVariant = {
    colabFunction: string
    erinomeFunction: string
    functionDefinition: string list
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

let erinomeFunctionVariants = [
    {
        colabFunction = "display"
        erinomeFunction = "erinomeDisplay"
        functionDefinition = [
            "def erinomeDisplay(x):"
            "    global erinomeBuffer"
            "    erinomeBuffer = erinomeBuffer + str(x)"
            "    return"
        ]
    }
]

let makeGlobalParam line =
    let param = colabFormLine colabElementOptionsParser line
    match param with
    | Some { variableName = name; } -> sprintf "global %s" name
    | _ -> line


let generateEndpointCode endpoint cell =
    let paramss =
        cell.Lines
        |> List.map (colabFormLine colabElementOptionsParser)

    let cellComment = sprintf "#Cell %d:endpoint" cell.CellNumber
    let pp = endpoint.path.Replace("/", "")
    let defin = sprintf "def %s():" pp
    let lines =
        cell.Lines
        |> List.map makeGlobalParam
        |> List.map (fun l -> "    " + l)
    cellComment::defin::lines @ ["    return"]

let cellEndpoint cell =
    let ep =
        cell.Lines
        |> List.choose lineEndpoint
        |> List.tryLast
    match ep with
    | Some endpoint -> Some (cell, endpoint)
    | None -> None

let generateCode cell =
    match cellEndpoint cell with
    | None ->
        let cellComment = sprintf "#Cell %d" cell.CellNumber
        cellComment::cell.Lines
    | Some (cell, endpoint) ->
        generateEndpointCode endpoint cell

let getPaths cells =
    cells
    |> List.choose cellEndpoint
    |> List.map (fun (_, ep) -> ep.path)

let genEndpoints cell ep = [
    let p = ep.path
    yield sprintf "       if urlPath == '%s':" p
    let paramss =
        cell.Lines
        |> List.choose (colabFormLine colabElementOptionsParser)
    for ps in paramss do
        let name = ps.variableName
        yield sprintf "           global %s" name
        yield sprintf "           %s = %s" name ps.defaultValue
        yield sprintf "           if '%s' in parsedQuery: %s = int(parsedQuery['%s'][0])" name name name
    let pp = p.Replace("/", "")
    yield sprintf "           %s()" pp
    yield ""
]

let generateHandlerClass endpoints =
    [
        yield ""
        yield "#GeneratedHandler"
        yield "from http.server import BaseHTTPRequestHandler"
        yield "import urllib.parse as urlparse"
        yield "class handler(BaseHTTPRequestHandler):"
        yield ""
        yield "   def do_GET(self):"
        yield "       self.send_response(200)"
        yield "       self.send_header('Content-type','text/plain')"
        yield "       self.end_headers()"
        yield "       parsed = urlparse.urlparse(self.path)"
        yield "       parsedQuery = urlparse.parse_qs(parsed.query)"
        yield "       urlPath = parsed.path"
        yield "       global erinomeBuffer"
        yield "       erinomeBuffer = '' # reset output"
        for cell, ep in endpoints do
            for s in genEndpoints cell ep do
                yield s
        yield "       message = erinomeBuffer"
        yield "       self.wfile.write(message.encode())"
        yield "       return"
    ]

let useErinomeVariants line:string =
    List.fold (fun l f -> l.Replace(f.colabFunction, f.erinomeFunction)) line erinomeFunctionVariants

let erinomeFuncSrc =
    let source =
        erinomeFunctionVariants
        |> List.collect (fun f -> ""::f.functionDefinition)
    "#Erinome Function Variants"::"erinomeBuffer = ''"::source

let generateServer (cells:CodeCell list) =
    let userCode =
        cells
        |> List.map (generateCode >> (fun x -> ""::x))
        |> List.collect id
        |> List.map useErinomeVariants
    let handler = (generateHandlerClass <| List.choose cellEndpoint cells)

    Seq.concat [erinomeFuncSrc; userCode; handler] |> fun x -> String.Join('\n', x)

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

let beHappy a = a + 5