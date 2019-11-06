namespace GameLib
open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading
open MBrace.FsPickler

type ConnectResult = |Ok |Timeout
type ClientReadResult = 
    |Ok of msg : MsgServerToClient 
    |Timeout
    |Failed

type SocketReadResult = 
    |Ok of data : byte[]
    |Timeout
    |Failed

type ClientConnection(gwtoclient) =
    let GwToClient : IMsgTaker<MsgServerToClient> = gwtoclient
    let binarySerializer = FsPickler.CreateBinarySerializer()
    let mutable Socket : TcpClient option = None
    let mutable NetStream : NetworkStream option = None
    let mutable IpConnected  = ""
    let mutable ctx : CancellationTokenSource option = None

    member x.IsConnected = NetStream.IsSome

    member x.Connect (ip : string) (port : int) = async{
        let new_socket = new TcpClient(AddressFamily.InterNetwork)
        let task_connect = async{
            do! new_socket.ConnectAsync(ip, port) |> Async.AwaitTask
            return Some ConnectResult.Ok}
        let task_wait = async{
            do! Async.Sleep(5000)
            return Some ConnectResult.Timeout}
        let task = Async.Choice [task_connect; task_wait]
        let! ret = Async.Catch (task)
        let bok = 
            match ret with
            |Choice1Of2 (Some ConnectResult.Ok) ->  true
            |Choice1Of2 _ ->  false
            |Choice2Of2 (exc : Exception) -> false
        if not bok then 
            return false
        else
        Socket <- Some new_socket
        NetStream <- Some (new_socket.GetStream())
        ctx <- Some (new CancellationTokenSource())
        let reader = async{
            let! r = x.Reader NetStream.Value; 
            x.CloseAndNotify()
            return ()}
        Async.Start(reader, ctx.Value.Token)
        return true
    }

    member x.IsClosed = Socket.IsNone || NetStream.IsNone

    member private x.CloseAndNotify() =
        try 
            if (not x.IsClosed) then 
                GwToClient.TakeMessage MsgServerToClient.Disconnect
        finally x.Close()

    member x.Isclosed = Socket.IsNone || NetStream.IsNone

    member x.Close() =
        if ctx.IsSome then 
            try ctx.Value.Cancel()
            finally ctx <- None
        if NetStream.IsSome then
            try
                NetStream.Value.Close()
                NetStream.Value.Dispose()
            finally NetStream <- None
        if Socket.IsSome then
            try
                Socket.Value.Close()
                Socket.Value.Dispose()
            finally Socket <- None

    member x.Send (msg : MsgClientToServer) = async {
        if Socket.IsNone || not Socket.Value.Connected || 
            NetStream.IsNone || not NetStream.Value.CanWrite then
            return false
        else
        let stream = NetStream.Value
        let data = binarySerializer.Pickle msg
        let len = MyConverter.IntToByte (data.Length)
        do! stream.AsyncWrite len
        do! stream.AsyncWrite data
        return true
    }

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

    member private x.Reader (stream : NetworkStream) = 
        let rec readloop() = async {
            let! bmsg = x.ReadMsg stream
            match bmsg with 
            |ClientReadResult.Ok msg ->
                try 
                    GwToClient.TakeMessage msg
                    return! readloop() 
                with _-> return false
            |ClientReadResult.Timeout
            |ClientReadResult.Failed -> return true
        }
        readloop()

    member private x.ReadMsg (stream : NetworkStream) = async {
        let task_readlen = x.SocketRead(stream, 4, -1)
        let! ret = Async.Catch (task_readlen)
        let blen = 
            match ret with
            |Choice1Of2 (SocketReadResult.Ok m) -> Some m
            |Choice1Of2 _ -> None
            |Choice2Of2 (exc : Exception) -> None
        if blen.IsNone then 
            return ClientReadResult.Failed
        else
        let blen = blen.Value
        let len = MyConverter.ByteToInt blen
        if len > 20000 then 
            return ClientReadResult.Failed
        else

        let task = async{
            let! ret = x.SocketRead(stream, len, 5000)
            match ret with
            |SocketReadResult.Ok data -> 
                let msg = binarySerializer.UnPickle<MsgServerToClient> data
                return (ClientReadResult.Ok msg)
            |SocketReadResult.Failed -> return ClientReadResult.Failed
            |SocketReadResult.Timeout -> return ClientReadResult.Timeout}

        let! ret = Async.Catch (task)
        let ret = 
            match ret with
            |Choice1Of2 m -> m
            |Choice2Of2 (exc : Exception) -> ClientReadResult.Failed
        
        return ret
    }



    interface IClientConnection with
        member x.Connect ip port = x.Connect ip port
        member x.Send(msg) = x.Send(msg)
        member x.Close() = x.Close()
        member x.IsClosed = x.IsClosed




