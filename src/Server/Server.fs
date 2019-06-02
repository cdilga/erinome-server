open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Hopac
open Saturn
open Shared
open Giraffe.Core
open Microsoft.AspNetCore.Http
open FSharp.Data
open Giraffe.GiraffeViewEngine
open System.Text
open System.IO
open System.Text
open HttpFs.Client


let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"

let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let getInitCounter() : Task<Counter> = task { return { Value = 42 } }

type RequestBody = JsonProvider<"response.json", SampleIsList = true>
type DeployBody = JsonProvider<"response-deploy.json">

let page = tag "Page"
let link = tag "Link"
let p = tag "P"
let button = tag "Button"
let input = tag "Input"
let box = tag "Box"
let img = tag "Image"
let _display = attr "display"

let doDeployment token = job {
    use! request =
        Request.createUrl Post "https://api.zeit.co/v9/now/deployments"
        |> Request.setHeader (Custom ("Authorization", sprintf "Bearer %s" token))
        |> Request.body (BodyString """{
  "name": "f-deployment-test",
  "public": true,
  "version": 2,
  "files": [
    { "file": "index.html", "data": "<!doctype html>\n<html>\n  <head>\n    <title>A litma.af deployment with the Now API!</title>\n    <link rel=\"stylesheet\" href=\"style.css\"> \n </head>\n  <body>\n    <h1>Welcome to a simple static file</h1>\n    <p>Deployed with <a href=\"https://zeit.co/docs/api\">ZEIT&apos;s Now API</a>!</p>\n    <p>This deployment includes three files. A static index.html file as the homepage, a static style.css file for styling, and a date.js serverless function that returns the date on invocation. <img src=\"https://api.checkface.ml/api/erinome\"/> Try <a href=\"/date.js\">getting the date here.</a></p> \n   </body>\n</html>" },
    { "file": "style.css", "data": "h1 {\n margin-top: 70px; \n text-align: center; \n font-size: 45px; \n} \n h1, p {\n font-family: Helvetica; \n} \n a {\n color: #0076FF; \n text-decoration: none; \n} \n p {\n text-align: center; \n font-size: 30px; \n} \n p:nth-child(3) { \n font-size: 25px; \n margin-left: 15%; \n margin-right: 15%; \n}" },
    { "file": "date.js", "data": "module.exports = (req, res) => {\n  res.end(`The time is ${new Date()}`)\n}" }
  ],
  "builds": [
    { "src": "*.js", "use": "@now/node" },
    { "src": "*.html", "use": "@now/static" },
    { "src": "*.css", "use": "@now/static" }
  ]
}"""        )
        |> getResponse

    let! bodyStr = Response.readBodyAsString request
    let body = DeployBody.Parse bodyStr
    printfn "Deploy response: %A" body
    return body
}


let handleRequest : HttpHandler = fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
        let! bodyjson = ctx.ReadBodyFromRequestAsync()
        let body = RequestBody.Parse(bodyjson)
        let counterVal = Option.defaultValue 15 body.ClientState.Counter
        printfn "%A" body.ClientState.Counter
        let! deploymentResponse = task {
            if body.Action = "deploy"
            then
                let! b =  startAsTask <| doDeployment body.Token
                return Some b
            else
                return None
        }

        let link =
            deploymentResponse
            |> Option.map (fun b -> sprintf "https://%s" b.Url)
            |> Option.map (fun url -> p [] [link [ _href url] [str url]])
            |> Option.defaultWith (fun _ -> p [] [ str "Click button to make a new deployment"])

        printfn "Body: %A" body
        let response = page [] [
            p [] [str (sprintf "Counter: %i" counterVal)]
            button [_action "deploy"] [str (sprintf "Deploy a thing")]
            button [_action "counter"] [str (sprintf "Count a thing")]
            p [] [str (sprintf "Email: %s" body.User.Email)]
            p [] [str (sprintf "ID: %s" body.User.Id)]
            p [] [str (sprintf "Username: %s" body.User.Username)]
            link
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

    router { get "/api/init" (fun next ctx ->
        task {
            let! counter = getInitCounter()
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
