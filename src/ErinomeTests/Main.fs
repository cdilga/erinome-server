// Learn more about F# at http://fsharp.org

open System
open Expecto

[<EntryPoint>]
let main argv =
    //clear the console if running dotnet watch run to keep it easy to see results
    let args =
        match List.ofArray argv with
        | "WatchTests"::exArgs ->
            Console.Clear()
            List.toArray exArgs
        | _ ->
            argv

    //run tests with expecto
    Tests.runTestsInAssembly defaultConfig args