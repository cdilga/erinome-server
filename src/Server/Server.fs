open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Saturn
open Shared
open Giraffe.Core
open Microsoft.AspNetCore.Http
open FSharp.Data
open Giraffe.GiraffeViewEngine
open System.Text


let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"

let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 5005us

let getInitCounter() : Task<Counter> = task { return { Value = 42 } }

type RequestBody = JsonProvider<"response.json", SampleIsList = true>

let page = tag "Page"
let link = tag "Link"
let p = tag "P"
let button = tag "Button"
let input = tag "Input"
let box = tag "Box"
let img = tag "Image"
let _display = attr "display"

let handleRequest : HttpHandler = fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
        let! bodyjson = ctx.ReadBodyFromRequestAsync()
        let body = RequestBody.Parse(bodyjson)
        let counterVal = Option.defaultValue 15 body.ClientState.Counter
        printfn "%A" body.ClientState.Counter
        printfn "Body: %A" body
        let response = page [] [
            p [] [str (sprintf "Counter: %i" counterVal)]
            button [_action "count"] [str (sprintf "Deploy a thing")]
            p [] [str (sprintf "Email: %s" body.User.Email)]
            p [] [str (sprintf "ID: %s" body.User.Id)]
            p [] [str (sprintf "Username: %s" body.User.Username)]
            p [] [link [ _href (sprintf "https://%s" body.InstallationUrl)] [str (sprintf "%s" body.InstallationUrl)]]
            box [_display "none"] [input [_name "counter"; _type "hidden"; _value (string (counterVal + 1))] []]
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
]
// ] {
//     get "/api/init" (fun next ctx ->
//         task {
//             let! counter = getInitCounter()
//             return! json counter next ctx
//         })
//     get "/" (fun next ctx ->
//         task {
//             let counter = "hello world"
//             return! json counter next ctx
//         })
//     post "/" (fun next ctx ->
//         task {

//         })
// }

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    //use_static publicPath
    use_json_serializer(Thoth.Json.Giraffe.ThothSerializer())
    use_gzip
}

run app
