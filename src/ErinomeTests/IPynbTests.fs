module IPynbTests

open Expecto
open Erinome

[<Tests>]
let tests =
  testList "Jupyter Notebook Tests" [
    testList "Line Endpoints" [
        testCase "Normal comment line not endpoint" <| fun _ ->
          let line = "# hello world"
          let endpoint = lineEndpoint line
          Expect.isNone endpoint "Normal comments are not endpoints"

        testCase "GET endpoint" <| fun _ ->
          let line = "  #@GET   /path   "
          let endpoint = lineEndpoint line
          let expected = Some (Get "/path")
          Expect.equal endpoint expected "Should be /path endpoint"
    ]
  ]
