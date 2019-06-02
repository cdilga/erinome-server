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


let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"

let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 5005us

let getInitCounter() : Task<Counter> = task { return { Value = 42 } }

type RequestBody = JsonProvider<"response.json", SampleIsList = true>

let handleRequest : HttpHandler = fun (next : HttpFunc) (ctx : HttpContext) ->
    task {
        let! bodyjson = ctx.ReadBodyFromRequestAsync()
        let body = RequestBody.Parse(bodyjson)
        let counterVal = Option.defaultValue 15 body.ClientState.Counter

        printf "Body: %A" bodyjson
        let response = """<Page>
              <P>Counter: 0</P>
              <Button>deploy all the things</Button>
              <P>Email: c</P>
              <P>ID: undefined</P>
              <P>Username: c</P>
              <P>Name: undefined</P>
              <Img src="https://api.checkface.ml/api/c?dim=300" />
              <P><Link href="https://"></Link></P>
              </Page>"""
        return! Successful.OK response next ctx
    }

let rootApp =
    route "/"

    >=> setHttpHeader "Access-Control-Allow-Origin" "*"
    >=> setHttpHeader "Access-Control-Allow-Methods" "GET, POST, DELETE, OPTIONS"
    >=> setHttpHeader "Access-Control-Allow-Headers" "Authorization, Accept, Content-Type"
    >=> choose [
        POST
            >=> setStatusCode 200
            >=> handleRequest
        OPTIONS
            >=> setStatusCode 200
    ]
    //>=>


let webApp = choose [
    route "/foo" >=> text "Foo"
    rootApp
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
