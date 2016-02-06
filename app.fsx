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
open BugDb.Models
open Newtonsoft.Json
open Newtonsoft.Json.Linq

let nullableDate : DateTime option -> Nullable<DateTime> = function
    | None -> Nullable<DateTime> () 
    | Some d -> Nullable d

let serializeBug (b : Bug) = JObject(JProperty("Id",b.Id),JProperty("Details",b.Details),JProperty("Closed", nullableDate(b.Closed)))
let serializeBugs bugs = JObject(JProperty("Bugs",JArray(bugs |> List.map serializeBug)))

let jsonMime = Writers.setMimeType "application/json"
let getOpenBugs = warbler (fun _ -> 
                        GetOpenBugs () |> serializeBugs 
                        |> fun s -> s.ToString()
                        |> OK)

let bugNotFound = sprintf "Bug id %d is not found." >> RequestErrors.NOT_FOUND
let okBug b = jsonMime >=> OK (b |> serializeBug |> fun s -> s.ToString())

let createBug = 
    request (fun r -> 
            match r.formData "details" with
            | Choice1Of2 d -> d |> CreateBug |> okBug
            | Choice2Of2 _ -> BAD_REQUEST "There were no details for the developers.")

let updateBug b = 
    request (fun r -> 
            match r.formData "details" with 
            | Choice1Of2 d -> 
                UpdateBug { b with Details = d } |> okBug
            | Choice2Of2 m -> BAD_REQUEST m)

let handleBug id = 
    match GetBug id with
    | None   -> id |> bugNotFound
    | Some b ->
        choose [ GET  >=> okBug b 
                 POST >=> updateBug b ]

let closeBug id =
    match GetBug id with
    | None   -> id |> bugNotFound
    | Some b -> UpdateBug { b with Closed = Some DateTime.UtcNow }
                |> okBug

let app = 
    choose
      [ pathScan "/api/bugs/%d" handleBug 
        POST >=> choose
            [ path "/api/bugs/create" >=> createBug 
              pathScan "/api/bugs/%d/close" closeBug ] 
        GET >=> choose 
            [ path "/" >=> OK "Faster APIs with Suave.IO"
              path "/api/bugs/open" >=> jsonMime >=> getOpenBugs ]]

startWebServer defaultConfig app