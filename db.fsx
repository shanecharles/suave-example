namespace BugDb

module Db =

    open System

    type Bug = { Id : int; Details : string; Closed : DateTime option }

    type DbQuery = 
        | OpenBugs of AsyncReplyChannel<Bug list>
        | Bug of AsyncReplyChannel<Bug list> * int

    let db = MailboxProcessor.Start(fun inbox -> 
        let rec loop ((id, bugs) as oldState) = 
            async {
                let! msg = inbox.Receive ()
                let newState = 
                    match msg with
                    | OpenBugs c -> 
                        c.Reply (bugs)
                        oldState
                    | Bug (c, id) ->
                        c.Reply (bugs |> List.filter (fun {Id = id'} -> id = id'))
                        oldState
                return! loop newState
            }
        loop (2,[{Id = 1; Details = "Nothing works"; Closed = None}])
        )

module Access = 
    open Db

    let AsyncGetOpenBugs = Db.db.PostAndAsyncReply(fun c -> OpenBugs c)
    let AsyncGetBug id = Db.db.PostAndAsyncReply(fun c -> Bug (c,id))