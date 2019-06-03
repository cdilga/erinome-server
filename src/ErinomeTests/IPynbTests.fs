module IPynbTests

open Expecto
open Erinome
open System

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

    let mockOptionsParser options =
        Some options

    yield testList "Colab form parameters" [
        testCase "No parameter" <| fun _ ->
            let line = "myVar = 145 #@thing"
            let formEl = colabFormLine mockOptionsParser line
            Expect.isNone formEl "Should not parse a form element when no #@param"

        testCase "With whitespace" <| fun _ ->
            let line = "  myVar  =    145    #@param  paramoptions  "
            let actual = colabFormLine mockOptionsParser line
            let expected = Some { variableName = "myVar"; defaultValue = "145"; elementOptions = "paramoptions" }
            Expect.equal actual expected "Should handle trimming whitespace"

        testCase "Slider options parser" <| fun _ ->
            let options = "{type:\"slider\", min:4, max:12, step:1}"
            let actual = colabElementOptionsParser options
            let expected = Some <| Slider ({ min = 4; max = 12; step = 1 })
            Expect.equal actual expected "Should parse slider element correctly"
    ]

    yield testCase "generate server test case" <| fun _ ->
        let cell1 = {
            Lines = [
                "c = 17 #@param {type:\"slider\", min:4, max:12, step:1}"
            ]
            CellNumber = 1
        }
        let cell2 = {
            Lines = [
                "#@GET /add5"
                "a = 5"
                "b = 16 #@param {type:\"slider\", min:10, max:100, step:4}"
                "display(a + b)"
            ]
            CellNumber = 2
        }

        let code = generateServer [cell1; cell2]
        let expected = String.Join('\n', [
            "#Cell 1"
            "c = 17 #@param {type:\"slider\", min:4, max:12, step:1}"
            ""
            "#Cell 2:endpoint"
            "#@GET /add5"
            "a = 5"
            "b = 16 #@param {type:\"slider\", min:10, max:100, step:4}"
            "display(a + b)"
        ])


        Expect.equal code expected "Should be able to parse multiple cells to full code"
  ]
