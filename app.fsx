#r "packages/Suave/lib/net40/Suave.dll"

open Suave
open Suave.Operators  // Fish operator >=>
open Suave.Filters    // GET, POST, Put, ...
open Suave.Successful

let serverDateTime = warbler (fun _ -> System.DateTime.UtcNow.ToString() |> OK)

let app = 
    choose
      [ GET >=> choose 
            [ path "/" >=> OK "Faster APIs with Suave.IO"
              path "/api/time" >=> serverDateTime ]]

startWebServer defaultConfig app
