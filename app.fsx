#I "packages/Suave/lib/net40/"
#I "packages/fsharpx.extras/lib/40/"
#I "packages/NewtonSoft.Json/lib/net45/"
#r "Suave.dll"
#r "fsharpx.extras.dll"
#r "newtonsoft.json.dll"
#load "db.fsx"

open Suave
open Suave.Operators  // Fish operator >=>
open Suave.Filters    // GET, POST, Put, ...
open Suave.Successful
open Suave.RequestErrors
open System
open BugDb.Access
open BugDb.Models
open Newtonsoft.Json

let ifFound = Option.map
let getOrElse = FSharpx.Option.getOrElse
let hasDetails = FSharpx.Choice.choice
let bugNotFound = NOT_FOUND "No bug"
let jsonMime = Writers.setMimeType "application/json"

type JBug = { Id : int; Details : string; Closed : Nullable<DateTime> }
let toJbug (bug : Bug) = 
  { Id = bug.Id; Details = bug.Details; Closed = (Option.toNullable bug.Closed) }

let getOpenBugs = 
  warbler (fun _ -> 
    GetOpenBugs () |> Seq.map toJbug
    |> JsonConvert.SerializeObject
    |> OK)

let okBug b = jsonMime >=> OK (b |> toJbug |> JsonConvert.SerializeObject)

let createBug = 
  request (fun r -> r.formData "details" |> hasDetails (NewBug >> okBug) BAD_REQUEST)

let updateBug b = 
  request (fun r -> r.formData "details" |> hasDetails ((fun d -> UpdateBug { b with Details = d }) >> okBug) BAD_REQUEST)
    
let handleBug b = choose [ GET  >=> okBug b 
                           POST >=> updateBug b ]

let closeBug b = UpdateBug { b with Closed = Some DateTime.UtcNow } |> okBug

let app = 
  choose
    [ pathScan "/api/bugs/%d" (GetBug >> ifFound handleBug >> getOrElse bugNotFound)
      POST >=> choose
        [ path "/api/bugs/create" >=> createBug 
          pathScan "/api/bugs/%d/close" (GetBug >> ifFound closeBug >> getOrElse bugNotFound) ] 
      GET >=> choose 
        [ path "/" >=> OK "Faster APIs with Suave.IO"
          path "/api/bugs/open" >=> jsonMime >=> getOpenBugs ]]