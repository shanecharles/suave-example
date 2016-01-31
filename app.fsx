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

let bugNotFound = sprintf "Bug id %d is not found." >> RequestErrors.NOT_FOUND
let returnBug b = jsonMime >=> OK (b |> JsonConvert.SerializeObject)

let createBug = 
    request (fun r -> 
            match r.formData "details" with
            | Choice1Of2 d -> 
                d |> AsyncCreateBug 
                |> Async.RunSynchronously
                |> returnBug
            | Choice2Of2 _ -> BAD_REQUEST "There were no details for the developers.")

let updateBug b = 
    request (fun r -> 
            match r.formData "details" with 
            | Choice1Of2 d -> 
                let b' = AsyncUpdateBug { b with Details = d } |> Async.RunSynchronously
                returnBug b'
            | Choice2Of2 m -> BAD_REQUEST m)

let handleBug id = 
    match AsyncGetBug id |> Async.RunSynchronously with
    | None   -> id |> bugNotFound
    | Some b ->
        choose [ GET  >=> returnBug b 
                 POST >=> updateBug b ]

let closeBug id =
    match AsyncGetBug id |> Async.RunSynchronously with
    | None   -> id |> bugNotFound
    | Some b -> 
        AsyncUpdateBug { b with Closed = Some DateTime.UtcNow } |> Async.RunSynchronously
        |> returnBug

let app = 
    choose
      [ pathScan "/api/bugs/%d" handleBug 
        POST >=> choose
            [ path "/api/bugs/create" >=> createBug ] 
        GET >=> choose 
            [ path "/" >=> OK "Faster APIs with Suave.IO"
              path "/api/bugs/open" >=> jsonMime >=> getOpenBugs
              path "/api/time" >=> serverDateTime ]
        Authentication.authenticateBasic ((=) ("bob","1password")) <|
            choose [ POST >=> pathScan "/api/bugs/%d/close" closeBug ]]

startWebServer defaultConfig app
