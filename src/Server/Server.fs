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


        let reqs = { file = "requirements.txt"; data = File.ReadAllText("pythontestserver/requirements.txt") }
        let cows = { file = "cowsay.py"; data = File.ReadAllText("pythontestserver/cowsay.py") }


        let! deploymentResponse = task {
            if body.Action = "deploy"
            then
                let routes = [
                    { src = "/saycow"; dest = "/cowsay.py" }
                    { src = "/cowsay"; dest = "/cowsay.py" }
                    { src = "/cowsay/stuff/([^/]+)"; dest = "/cowsay.py" }

                ]
                let request = deploymentRequest "g-deployment-test" (reqs::cows::testFiles) defaultBuilds routes
                let! b =  startAsTask <| doDeployment body.Token request
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
