namespace BugDb

module Models = 
  open System
  type Bug = { Id : int; Details : string; Closed : DateTime option }

module Db =
  open System
  open FSharp.Core
  open Models

  type DbQuery = 
    | OpenBugs of AsyncReplyChannel<Bug list>
    | Bug of AsyncReplyChannel<Bug option> * int
    | Update of AsyncReplyChannel<Bug> * Bug
    | Create of AsyncReplyChannel<Bug> * string

  let db = MailboxProcessor.Start(fun inbox -> 
    let rec loop ((cid, bugs) as oldState) = 
      async {
        let! msg = inbox.Receive ()
        let newState = 
               match msg with
               | OpenBugs c -> 
                 c.Reply (bugs |> List.filter (fun {Closed = c} -> c.IsNone ))
                 oldState
               | Bug (c, id) ->
                 c.Reply (bugs |> List.filter (fun {Id = id'} -> id = id') |> function | [] -> None | h :: _ -> Some h)
                 oldState
               | Update (c, b) ->
                 c.Reply b
                 let bugs' = b :: (bugs |> List.filter (fun {Id = id} -> id <> b.Id))
                 (cid, bugs')
               | Create (c, d) ->
                  let b = {Id = cid; Details = d; Closed = None}
                  c.Reply b
                  (cid + 1, b :: bugs)
        return! loop newState
      }
    loop (4,[{Id = 1; Details = "Nothing works"; Closed = None}
             {Id = 2; Details = "Can't add bugs."; Closed = None}
             {Id = 3; Details = "Only Bob can close bugs"; Closed = None}])
  )

module Access = 
  open Db

  let GetOpenBugs () = Db.db.PostAndAsyncReply(fun c -> OpenBugs c) |> Async.RunSynchronously
  let GetBug id = Db.db.PostAndAsyncReply(fun c -> Bug (c,id)) |> Async.RunSynchronously
  let UpdateBug b = Db.db.PostAndAsyncReply(fun c -> Update (c, b)) |> Async.RunSynchronously
  let CreateBug d = Db.db.PostAndAsyncReply(fun c -> Create (c, d)) |> Async.RunSynchronously
