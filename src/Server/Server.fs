open System.IO
open System.Threading.Tasks
open FSharp.Control.Tasks.V2
open Giraffe
open Hopac
open Saturn
open Shared
open Microsoft.AspNetCore.Http
open FSharp.Data
open Giraffe.GiraffeViewEngine
open Erinome
open ZeitAPI
open System.IO

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"

let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let getInitCounter() : Task<Counter> = task { return { Value = 42 } }
let getInitCounterHappy() : Task<Counter> = task { return { Value = beHappy 16 } }

type RequestBody = JsonProvider<"response.json", SampleIsList = true>

let page = tag "Page"
let link = tag "Link"
let p = tag "P"
let h1 = tag "H1"
let button = tag "Button"
let input = tag "Input"
let box = tag "Box"
let img = voidTag "Img"
let container = tag "Container"
let _display = attr "display"
let linkHeadTag = voidTag "link"

type ClickDeployResponse<'a> =
    | Response of 'a
    | Failure of string

let generateIndexHtml name paths =
    let markup = html [] [
        head [] [
            title [] [ str name ]
            linkHeadTag [ _rel "stylesheet"; _href "style.css" ]
        ]
        body [] [
            h1 [] [ str name ]
            p [] [str "Deployed with ZEIT Now using Erinome"]
            p [] [
                for path in paths do
                    yield br []
                    yield a [ _href path ] [ str path ]
            ]
        ]
    ]

    { file = "index.html"; data = renderHtmlDocument markup }



let cssFile = { file = "style.css"; data = "h1 {\n margin-top: 70px; \n text-align: center; \n font-size: 45px; \n} \n h1, p {\n font-family: Helvetica; \n} \n a {\n color: #0076FF; \n text-decoration: none; \n} \n p {\n text-align: center; \n font-size: 30px; \n} \n p:nth-child(3) { \n font-size: 25px; \n margin-left: 15%; \n margin-right: 15%; \n}" }

let generateRequestForNotebook nbUrl = async {
    let! deploymentData = generateServerFromShareUrl nbUrl
    match deploymentData with
    | Ok (name, pyFile, paths) ->
        //let reqs = { file = "requirements.txt"; data = File.ReadAllText("pythontestserver/requirements.txt") }
        //let cows = { file = "cowsay.py"; data = File.ReadAllText("pythontestserver/cowsay.py") }
        let server = { file = "server.py"; data = pyFile }
        let indexFile = generateIndexHtml name paths
        printfn "PyFile: %s" pyFile
        let routes = paths |> List.map (fun path -> { src = path; dest = "server.py" })
        let request = deploymentRequest name ([cssFile; indexFile; server]) normalBuilds routes
        return Ok request
    | Error er ->
        return Error er
}

let handleRequest : HttpHandler = fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
        let! bodyjson = ctx.ReadBodyFromRequestAsync()
        let body = RequestBody.Parse(bodyjson)



        let! deploymentResponse = task {
            match body.Action, body.ClientState.NotebookUrl with
            | "deploy", Some nbUrl ->
                    let! request = generateRequestForNotebook nbUrl
                    match request with
                    | Ok req ->
                        if false
                        then return Error "Just testing"
                        else
                            let! b =  startAsTask <| doDeployment body.Token req
                            return Ok b
                    | Error er ->
                        return Error er
            | _ ->
                return Error "Click to make a deployment"
        }

        let link =
            match deploymentResponse with
            | Ok response ->
                printfn "Response to deploying: %A" response
                let url = sprintf "https://%s" response.Url
                p [] [link [ _href url] [str url]]
            | Error er ->
                 p [] [ str er]

        let driveUrl = body.ClientState.NotebookUrl |> Option.defaultValue ""

        printfn "Body: %A" body
        let response = page [] [
            input [_label "Colaboratory Google Drive share url"; _name "notebookUrl"; _value driveUrl ] []
            button [_action "deploy"] [str (sprintf "Deploy Notebook")]
            link
            container [] [
                h1 [] [str "To get your colab share url" ]
                p [] [str "Make sure you have saved and have the notebook open in DRIVE"]
                rawText "<Img src=\"https://erinome.appspot.com/images/click_share.jpg\" />"
                p [] [str "Click the share button"]
                rawText "<Img src=\"https://erinome.appspot.com/images/click_share.jpg\" />"
                p [] [str "Share, and copy link. It should look similar to https://colab.research.google.com/drive/1034rXX-xPwDbY-yvgmmXWGBBa3pE7-Wf#scrollTo=65QaPYCYBRfm"]
                rawText "<Img src=\"https://erinome.appspot.com/images/copy_link.jpg\" />"

            ]
        ]

        return! ctx.WriteTextAsync <| renderHtmlNode response
    }

let uiHook =
    setHttpHeader "Access-Control-Allow-Origin" "*"
    >=> setHttpHeader "Access-Control-Allow-Methods" "GET, POST, DELETE, OPTIONS"
    >=> setHttpHeader "Access-Control-Allow-Headers" "Authorization, Accept, Content-Type"
    >=> choose [
        POST
            >=> setStatusCode 200
            >=> handleRequest
        OPTIONS
            >=> setStatusCode 200
    ]


let webApp = choose [
    route "/api/uihook" >=> uiHook

    router { get "/api/init" (fun next ctx ->
        task {
            let! counter = getInitCounter()
            return! json counter next ctx
        })
    }

    router { get "/api/behappy" (fun next ctx ->
        task {
            let! counter = getInitCounterHappy()
            return! json counter next ctx
        })
    }
]

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    use_static publicPath
    use_json_serializer(Thoth.Json.Giraffe.ThothSerializer())
    use_gzip
}

run app
