#r "packages/Suave/lib/net40/Suave.dll"
#r "packages/NewtonSoft.Json/lib/net45/newtonsoft.json.dll"
#load "db.fsx"

open Suave
open Suave.Operators  // Fish operator >=>
open Suave.Filters    // GET, POST, Put, ...
open Suave.Successful
open Suave.Model.Binding
open Suave.RequestErrors
open System
open BugDb.Access

open Newtonsoft.Json

let serverDateTime = warbler (fun _ -> System.DateTime.UtcNow.ToString() |> OK)

let jsonMime = Writers.setMimeType "application/json"

let getOpenBugs = warbler (fun _ -> 
                        let bugs = AsyncGetOpenBugs () |> Async.RunSynchronously
                        bugs |> JsonConvert.SerializeObject |> OK)

let bugDetails id = 
    match AsyncGetBug id |> Async.RunSynchronously with 
    | None   -> id |> sprintf "Bug id %d is not found." |> RequestErrors.NOT_FOUND
    | Some b -> jsonMime >=> OK (JsonConvert.SerializeObject(b))

let app = 
    choose
      [ GET >=> choose 
            [ path "/" >=> OK "Faster APIs with Suave.IO"
              path "/api/bugs/open" >=> jsonMime >=> getOpenBugs
              pathScan "/api/bugs/%d" bugDetails
              path "/api/time" >=> serverDateTime ]]

startWebServer defaultConfig app
