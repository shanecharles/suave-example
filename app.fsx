#I "packages/Suave/lib/net40/"
#I "packages/NewtonSoft.Json/lib/net45/"

#r "Suave.dll"
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
open Newtonsoft.Json.Linq

let nullableDate : DateTime option -> Nullable<DateTime> = function
  | None -> Nullable<DateTime> () 
  | Some d -> Nullable d

type JBug = { Id : int; Details : string; Closed : Nullable<DateTime> }
let toJbug (bug : Bug) = 
  { Id = bug.Id; Details = bug.Details; Closed = (nullableDate bug.Closed) }

let jsonMime = Writers.setMimeType "application/json"
let getOpenBugs = 
  warbler (fun _ -> 
    GetOpenBugs () |> Seq.map toJbug
    |> JsonConvert.SerializeObject
    |> OK)

let bugNotFound = sprintf "Bug id %d is not found." >> RequestErrors.NOT_FOUND
let okBug b = jsonMime >=> OK (b |> toJbug |> JsonConvert.SerializeObject)

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

