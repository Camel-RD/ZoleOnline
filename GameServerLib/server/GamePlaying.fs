namespace GameServerLib
open System
open System.Collections.Immutable
open System.Collections.Generic
open GameLib

type GamePlayingStatus = |Init |InGame |Stopped |Failed of msg : string

type GamePlayingState = {
    Status : GamePlayingStatus
} with
    static member InitState = {GamePlayingState.Status = GamePlayingStatus.Init}

type private GamePlayingStateVar = {
    Reader : GamePlayingState -> Async<MsgToGamePlaying>
    State : GamePlayingState
    Worker : GamePlayingStateVar -> Async<GamePlayingStateVar>
    Flag : StateVarFlag} with
    member x.ShouldExit() = 
        x.Flag <> StateVarFlag.OK ||
        match x.State.Status with
        |GamePlayingStatus.Stopped |GamePlayingStatus.Failed _ -> true |_-> false


type GamePlaying(id, toServer, users) as this =
    let MailBox = new AutoCancelAgent<MsgToGamePlaying>(this.DoInbox)
    let ToServer : IGamePlayingToServer = toServer
    let _Users : IUser [] =  users
    let InitState = GamePlayingState.InitState

    let mutable OnlineGame : GameServer option = None

    let gwToRemote = 
        {new IMsgTaker<MsgDataToRemote> with
            member x.TakeMessage msg = MsgToGamePlaying.FromGM msg |> this.TakeMessageSafe}

    let gwFromRemote = 
        {new IMsgTaker<MsgDataFromRemote> with
            member x.TakeMessage msg = MsgToGamePlaying.FromRemote msg |> this.TakeMessageSafe}

    let GameToOwner =
        {new IGameToOwner with
             member x.GameStopped(msg) = 
                MsgToGamePlaying.FromGame (MsgGameToOwner.GameStopped msg) |> this.TakeMessageSafe
             member x.GameClosed() = 
                MsgToGamePlaying.FromGame MsgGameToOwner.GameClosed |> this.TakeMessageSafe
             member x.AddPoints userpoints = 
                MsgToGamePlaying.AddPoints userpoints |> this.TakeMessageSafe}
    
    let MessageTaker = 
        {new IMsgTakerX<MsgToGamePlaying> with
            member x.TakeMessage msg = this.TakeMessageSafe msg
            member x.TakeMessageGetReply<'Reply> (builder, ?timeout) = this.TakeMessageGetReply<'Reply> (builder, timeout)
            member x.TakeMessageGetReplyX (msg, ?timeout) = this.TakeMessageGetReplyX(msg, timeout) }
    
    member x.id : int = id
    member val Created : DateTime = DateTime.Now
    member val Users =  _Users

    member val To = MPToGamePlaying(MessageTaker) :> IToGamePlaying

    member x.Start() = 
        MailBox.Start()

    member x.StartGameServer() =
        let playernames = [|x.Users.[0].Name; x.Users.[1].Name; x.Users.[2].Name|]
        let playernamesa = playernames |> ImmutableArray.CreateRange
        let playertypes = [PlayerType.RemoteServer; PlayerType.RemoteServer; PlayerType.RemoteServer] |> ImmutableArray.CreateRange
        let game = new GameServer(GameFormEmpty.Empty, playernamesa, 0, playertypes, false, Some gwToRemote, Some GameToOwner)
        for i in 0..2 do
            let initdata = 
                {UserGameInitData.GameId = x.id; 
                 MessageGateWay = gwFromRemote;
                 PlayerNr = i;
                 PlayerNames = playernames}
            let touser = _Users.[i].FromGamePlaying
            touser.InitGame initdata
            touser.StartGame()
        game.InitGame()
        OnlineGame <- Some game

    member private x.DoStartGame (statevar : GamePlayingStateVar) = async{
        x.StartGameServer()
        let cur_state = {statevar.State with Status = GamePlayingStatus.InGame}
        return {statevar with State = cur_state; Worker = x.DoGame}
    }

    member private x.DoGame (statevar : GamePlayingStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader(cur_state)
        let cur_state =
            match msg with
            |MsgToGamePlaying.Control MsgControl.Stop ->
                x.StopGame cur_state

            |MsgToGamePlaying.FromGame (MsgGameToOwner.GameStopped _) ->
                x.StopGame cur_state

            |MsgToGamePlaying.FromGame MsgGameToOwner.GameClosed ->
                x.StopGame cur_state
            
            |MsgToGamePlaying.AddPoints mdata ->
                for i in 0..2 do
                    mdata.UserIds.[i] <- _Users.[i].Id
                ToServer.AddGamePoints mdata
                cur_state
            
            |MsgToGamePlaying.FromGM mdata ->
                let touser = _Users.[mdata.playerNr]
                touser.FromGM.TakeMessage mdata.msg
                cur_state

            |MsgToGamePlaying.FromRemote mdata ->
                OnlineGame.Value.FromRemote.TakeMessage mdata
                cur_state

            |_ -> cur_state
        
        if cur_state.Status <> GamePlayingStatus.InGame then
            x.Close()
        return {statevar with State = cur_state}
    }

    member private x.StopGame(state : GamePlayingState) =
        if not (x._IsClosed || state.Status = GamePlayingStatus.Stopped) then
            x.Close()
        {state with Status = GamePlayingStatus.Stopped}

    member private x.DoInbox(inbox : MailboxProcessor<MsgToGamePlaying>) = 
        let rec loop (statevar : GamePlayingStateVar) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 (new_state : GamePlayingStateVar) -> 
                if new_state.ShouldExit()
                then
                    x.Close()
                    return () 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                x.StopGame(statevar.State) |> ignore
                return () }
        let init_state = InitState
        let init_statevar = 
            {GamePlayingStateVar.Reader = x.MsgReader(inbox); 
                State = init_state; 
                Worker = x.DoStartGame;
                Flag = StateVarFlag.OK}
        loop(init_statevar)

    member private x.MsgReader (inbox : MailboxProcessor<MsgToGamePlaying>) (state : GamePlayingState) =
        let rec loop() = async{
            let! msg = inbox.Receive()
            let bstopped = 
                match state.Status with
                |GamePlayingStatus.Stopped |GamePlayingStatus.Failed _ -> true 
                |_ -> false
            if bstopped then 
                x.Close()
                return MsgToGamePlaying.Control MsgControl.KillPill
            else
            
            Logger.WriteLine("GamePlaying[{0}]: <- {1}", x.id, msg)

            match msg with
            |MsgToGamePlaying.Control MsgControl.KillPill -> return msg
            |MsgToGamePlaying.Control (MsgControl.GetState channel) ->
                channel.Reply state
                return! loop()
            | _ -> return msg}
        loop()

    member x.GetState() = 
            let msg channel = MsgToGamePlaying.Control (GetReply (MsgControl.GetState, channel))
            let state = MailBox.PostAndReply(msg)
            state


    member private x.TakeMessage(msg : MsgToGamePlaying) = x.TakeMessageSafe msg

    member private x.TakeMessageSafe(msg : MsgToGamePlaying) = 
        if not (x.IsClosed || x.IsDisposed) then 
            try MailBox.Post msg finally ()

    member private x.TakeMessageGetReply<'Reply>(builder : AsyncReplyChannel<'Reply> -> MsgToGamePlaying, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout 10000
        if x.IsClosed || x.IsDisposed then 
            async{return None}
        else
        try MailBox.PostAndTryAsyncReply(builder, timeout)
        with |_-> async{return None}

    member private x.TakeMessageGetReplyX<'Reply>(msg : MsgToGamePlaying, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout 10000
        if x.IsClosed || x.IsDisposed then 
            async{return None}
        else
        let fmsg ch = MsgToGamePlaying.Control (GetReply (msg, ch))
        async{
            let! ret = Async.Catch (MailBox.PostAndTryAsyncReply(fmsg, timeout))
            match ret with
            |Choice1Of2 ret -> 
                match ret with
                |Some (:? 'Reply as mm) -> return Some mm
                |_ -> return None 
            |Choice2Of2 (exc : Exception) -> 
                return None }


    override x.Equals(obj) =
        match obj with
        | :? GameNew as y -> x.id = y.id
        | _-> false
    override x.GetHashCode() = x.id.GetHashCode()
    interface IComparable with
        member x.CompareTo(obj: obj): int = 
            match obj with
            | :? GameNew as y -> compare y.id x.id
            | _ -> invalidArg "obj" "cannot compare values of different types"    



    member val private _IsClosed = false with get,set
    member x.IsClosed = x._IsClosed

    member private x.Close() = 
        if not x._IsClosed then
            for user in _Users do
                try 
                    user.FromGamePlaying.StopGame()
                with _-> ()
            try
                if OnlineGame.IsSome then
                    MsgToGame.GameStop
                    |> OnlineGame.Value.TakeMessageSafe
            with _-> ()
            try
                ToServer.GameStopped x.id
            with _-> ()
            try x.Dispose()
            finally x._IsClosed <- true    


    member val private _IsDisposed = false with get, set
    member x.IsDisposed = x._IsDisposed

    member x.Dispose() =
        if not x.IsDisposed then
            try
                (MailBox :> IDisposable).Dispose()
                if OnlineGame.IsSome then
                    OnlineGame.Value.Dispose()
                    OnlineGame <- None
            finally
                x._IsDisposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()

