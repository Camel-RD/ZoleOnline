namespace GameServerLib
open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading
open MBrace.FsPickler
open GameLib

type ServerReadResult = 
    |Ok of msg : MsgClientToServer
    |Timeout
    |Failed

type ServerConnection(Socket : TcpClient) =
    let binarySerializer = FsPickler.CreateBinarySerializer()
    let mutable GwToUser : IMsgTaker<MsgClientToServer> option = None
    let mutable NetStream : NetworkStream option = None
    let mutable IpConnected  = ""
    let mutable ctx : CancellationTokenSource option = None

    member x.Start (gwtouser) = 
        GwToUser <- Some gwtouser
        NetStream <- Some (Socket.GetStream())
        ctx <- Some (new CancellationTokenSource())
        let reader = async{
            let! r = x.Reader NetStream.Value; 
            x.CloseAndNotify()
            return ()}
        Async.Start(reader, ctx.Value.Token)
        true

    member x.IsClosed = NetStream.IsNone

    member private x.CloseAndNotify() =
        try 
            if (not x.IsClosed) && GwToUser.IsSome then  
                GwToUser.Value.TakeMessage MsgClientToServer.Disconnect
        finally x.Close()

    member x.Close() =
        if ctx.IsSome then 
            try
                ctx.Value.Cancel()
            finally
                ctx <- None
        if NetStream.IsSome then
            try
                NetStream.Value.Close()
                NetStream.Value.Dispose()
            finally
                NetStream <- None
        try
            Socket.Close()
            Socket.Dispose()
        finally ()
            

    member x.Send (msg : MsgServerToClient) = async {
        if not Socket.Connected || NetStream.IsNone || not NetStream.Value.CanWrite then
            return false
        else
        let stream = NetStream.Value
        let data = binarySerializer.Pickle msg
        let len = MyConverter.IntToByte (data.Length)
        do! stream.AsyncWrite len
        do! stream.AsyncWrite data
        return true
    }

    member private x.Reader (stream : NetworkStream) = 
        let rec readloop() = async {
            let! bmsg = x.ReadMsg stream
            match bmsg with 
            |ServerReadResult.Ok msg ->
                let rt = 
                    try 
                        GwToUser.Value.TakeMessage msg
                        true
                    with _-> false
                if rt 
                then return! readloop() 
                else return false
            |ServerReadResult.Timeout
            |ServerReadResult.Failed -> return false
        }
        readloop()

    member private x.SocketRead (stream : NetworkStream, count : int, timeout : int) = async{
        let buffer : byte[] = Array.zeroCreate count
        let rec loop (pos: int) = async{
            let read_task = stream.ReadAsync(buffer, pos, count-pos) |> Async.AwaitTask
            let! ret = Async.Catch (read_task)
            match ret with
            |Choice1Of2 len -> 
                if len = 0 then
                    return Some SocketReadResult.Failed
                else
                if pos + len = count then 
                    return Some (SocketReadResult.Ok buffer)
                else return! loop (pos + count)
            |Choice2Of2 (exc : Exception) -> 
                return Some SocketReadResult.Failed
        }
        let task_read = loop 0
        let task_wait = async{
                do! Async.Sleep(timeout)
                return Some SocketReadResult.Timeout}
        
        let task = 
            if timeout = -1 
            then task_read
            else Async.Choice [task_read; task_wait]

        let! ret = Async.Catch (task)
        let ret = 
            match ret with
            |Choice1Of2 (Some m) -> m
            |Choice1Of2 None -> SocketReadResult.Timeout
            |Choice2Of2 (exc : Exception) -> SocketReadResult.Failed
        return ret
    }

    member private x.ReadMsg (stream : NetworkStream) = async {
        let task_readlen = x.SocketRead(stream, 4, 3600*1000)
        let! ret = Async.Catch (task_readlen)
        let blen = 
            match ret with
            |Choice1Of2 (SocketReadResult.Ok m) -> Some m
            |Choice1Of2 _ -> None
            |Choice2Of2 (exc : Exception) -> None
        if blen.IsNone then 
            return ServerReadResult.Failed
        else
        let blen = blen.Value
        let len = MyConverter.ByteToInt blen
        if len > 10000 then 
            return ServerReadResult.Failed
        else
        let task = async{
            let! ret = x.SocketRead(stream, len, 5000)
            match ret with
            |SocketReadResult.Ok data -> 
                let msg = binarySerializer.UnPickle<MsgClientToServer> data
                return (ServerReadResult.Ok msg)
            |SocketReadResult.Failed -> return ServerReadResult.Failed
            |SocketReadResult.Timeout -> return ServerReadResult.Timeout}

        let! ret = Async.Catch (task)
        let ret = 
            match ret with
            |Choice1Of2 m -> m
            |Choice2Of2 (exc : Exception) -> ServerReadResult.Failed
        return ret
    }

    interface IServerConnection with
        member x.Start(gwtouser) = x.Start(gwtouser)
        member x.Send(msg) = x.Send(msg)
        member x.Close() = x.Close()
        member x.IsClosed = x.IsClosed



