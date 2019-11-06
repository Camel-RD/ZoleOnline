namespace GameServerLib
open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading
open MBrace.FsPickler
open GameLib

type ServerListener(port : int, ffornew : MsgListenerToServer -> unit) =
    let mutable listener : TcpListener option = None

    member x.Start() =
        x.Close()
        let new_listener = new TcpListener(IPAddress.Any, port)
        listener <- Some new_listener
        new_listener.Start()
        let task_handle = async{
            do! x.Handler(new_listener)
            try ffornew MsgListenerToServer.Closed
            finally x.Close()
            return ()
        }
        Async.Start(task_handle)
    
    member x.Close() =
        if listener.IsSome then
            try listener.Value.Stop()
            finally listener <- None

    member private x.Handler(listener : TcpListener) =
        let rec loop () : Async<unit> = async {
            let! new_client = listener.AcceptTcpClientAsync() |> Async.AwaitTask
            let new_sc = ServerConnection new_client
            let msg = MsgListenerToServer.NewConnection new_sc
            ffornew msg
            return! loop()
        }
        try 
            loop()
        with exc ->
            Logger.WriteLine("ServerListener: exc: {0}", exc.Message)
            async{return ()}