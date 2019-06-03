module ZeitAPI


open Hopac
open System
open FSharp.Data
open FSharp.Json
open HttpFs.Client

type DeployBody = JsonProvider<"response-deploy.json">

let file name data =
    sprintf "{ \"file\": \"%s\", \"data\": \"%s\" }" name data

type InlineFile = {
    file: string
    data: string
}

type BuildObj = {
    src: string
    [<JsonField("use")>]
    useBuilder: string
}

type DeploymentRequest = {
    name: string
    version: int
    files: InlineFile list
    builds: BuildObj list
}

let deploymentRequest name files builds = {
        name = name
        version = 2
        files = files
        builds = builds
    }

let defaultBuilds = [
    { src = "*.js"; useBuilder = "@now/node" }
    { src = "*.html"; useBuilder = "@now/static" }
    { src = "*.css"; useBuilder = "@now/static" }
    { src = "*.py"; useBuilder = "@now/python" }
]

let testFiles = [
    { file = "index.html"; data = "<!doctype html>\n<html>\n  <head>\n    <title>A litma.af deployment with the Now API!</title>\n    <link rel=\"stylesheet\" href=\"style.css\"> \n </head>\n  <body>\n    <h1>Welcome to a simple static file</h1>\n    <p>Deployed with <a href=\"https://zeit.co/docs/api\">ZEIT&apos;s Now API</a>!</p>\n    <p>This deployment includes three files. A static index.html file as the homepage, a static style.css file for styling, and a date.js and date.py serverless function that returns the date on invocation. <img src=\"https://api.checkface.ml/api/erinome\"/> Try <a href=\"/date.js\">getting the date here (node).</a> <a href=\"/date.py\">Or here (python).</a></p> \n   </body>\n</html>" }
    { file = "style.css"; data = "h1 {\n margin-top: 70px; \n text-align: center; \n font-size: 45px; \n} \n h1, p {\n font-family: Helvetica; \n} \n a {\n color: #0076FF; \n text-decoration: none; \n} \n p {\n text-align: center; \n font-size: 30px; \n} \n p:nth-child(3) { \n font-size: 25px; \n margin-left: 15%; \n margin-right: 15%; \n}" }
    { file = "date.py"; data = "import datetime\nfrom http.server import BaseHTTPRequestHandler\n\nclass handler(BaseHTTPRequestHandler):\n\n    def do_GET(self):\n        self.send_response(200)\n        self.send_header('Content-type','text/plain')\n        self.end_headers()\n        self.wfile.write(str(datetime.datetime.now()).encode())\n        return" }
    { file = "date.js"; data = "module.exports = (req, res) => {\n  res.end(`The time is ${new Date()}`)\n}" }
]

let doDeployment token name files builds = job {
    let request = deploymentRequest name files builds
    let bodyStr = Json.serialize request
    use! response =
        Request.createUrl Post "https://api.zeit.co/v9/now/deployments"
        |> Request.setHeader (Custom ("Authorization", sprintf "Bearer %s" token))
        |> Request.body (BodyString bodyStr)
        |> getResponse

    let! bodyStr = Response.readBodyAsString response
    let body = DeployBody.Parse bodyStr
    printfn "Deploy response: %A" body
    return body
}