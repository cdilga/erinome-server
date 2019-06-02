module IPynbTests

open Expecto
open Erinome

[<Tests>]
let tests =
  testList "Jupyter Notebook Tests" [
    yield testList "Line Endpoints" [
        testCase "Normal comment line not endpoint" <| fun _ ->
          let line = "# hello world"
          let endpoint = lineEndpoint line
          Expect.isNone endpoint "Normal comments are not endpoints"

        testCase "GET endpoint" <| fun _ ->
          let line = "  #@GET   /path   "
          let endpoint = lineEndpoint line
          let expected = Some (Get "/path")
          Expect.equal endpoint expected "Should be /path endpoint"

        testCase "POST endpoint" <| fun _ ->
          let line = "  #@POST/api/path.thing"
          let endpoint = lineEndpoint line
          let expected = Some (Post "/api/path.thing")
          Expect.equal endpoint expected "Should get post endpoint"
    ]

    let mockOptionsParser variableName defaultValue options =
        Some (variableName, defaultValue, options)

    yield testList "Colab form parameters" [
        testCase "No parameter" <| fun _ ->
            let line = "myVar = 145 #@thing"
            let formEl = colabFormLine mockOptionsParser line
            Expect.isNone formEl "Should not parse a form element when no #@param"

        testCase "With whitespace" <| fun _ ->
            let line = "  myVar  =    145    #@param  paramoptions  "
            let actual = colabFormLine mockOptionsParser line
            let expected = Some ("myVar", "145", "paramoptions")
            Expect.equal actual expected "Should handle trimming whitespace"
    ]
  ]
