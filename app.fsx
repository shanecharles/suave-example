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
open BugDb
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

let serializeBugs = Seq.map toJbug >> JsonConvert.SerializeObject >> OK

let getAllBugs = warbler (fun _ -> Db.GetAllBugs () |> serializeBugs)
let serverTime = DateTimeOffset.Now.ToString() |> sprintf "Server time is always: %s" |> OK

let getOpenBugs = warbler (fun _ -> Db.GetOpenBugs () |> serializeBugs)
let getClosedBugs = warbler (fun _ -> Db.GetClosedBugs () |> serializeBugs)

let okBug b = jsonMime >=> OK (b |> toJbug |> JsonConvert.SerializeObject)

let createBug = 
  request (fun r -> r.formData "details" |> hasDetails (Db.NewBug >> okBug) BAD_REQUEST)

let updateBug b = 
  request (fun r -> r.formData "details" |> hasDetails ((fun d -> Db.UpdateBug { b with Details = d }) >> okBug) BAD_REQUEST)
    
let getOrUpdateBug b = choose [ GET  >=> okBug b 
                                POST >=> updateBug b ]

let closeBug b = Db.UpdateBug { b with Closed = Some DateTime.UtcNow } |> okBug

let getBugsByStatus = function
  | "open"   -> getOpenBugs
  | "closed" -> getClosedBugs
  | _        -> getAllBugs

let app = 
  choose
    [ GET  >=> path "/" >=> OK "<html><body><h1>Faster APIs with Suave.IO</h1></body></html>"          
      pathScan "/api/bugs/%d" (Db.GetBug >> ifFound getOrUpdateBug >> getOrElse bugNotFound)
      GET  >=> pathScan "/api/bugs/%s" getBugsByStatus 
      POST >=> path "/api/bugs/create" >=> createBug 
      POST >=> pathScan "/api/bugs/%d/close" (Db.GetBug >> ifFound closeBug >> getOrElse bugNotFound)
      GET  >=> path "/api/bugs" >=> jsonMime >=> getAllBugs
      GET  >=> path "/api/time" >=> serverTime ]