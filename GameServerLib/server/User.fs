namespace GameServerLib
open System
open System.Collections.Immutable
open GameLib


type UserStatus = 
    |Offline |Connecting |Connected |LogIn |InLobby 
    |WaitingForGame |InGame |Disconnected |Failed of msg : string

type UserState = {
    Status : UserStatus
    GameId : int
    GameInitData : UserGameInitData option
} with
    static member InitState = {
        UserState.Status = UserStatus.Offline;
        GameId = -1;
        GameInitData = None}
    member x.RetFailure (msg : string) = {x with Status = UserStatus.Failed msg}
   

type private UserStateVar = {
    Reader : UserState -> Async<MsgToUser>
    State : UserState
    Worker : UserStateVar -> Async<UserStateVar>
    Flag : StateVarFlag
} with
    member x.ShouldExit() = 
        x.Flag <> StateVarFlag.OK ||
        match x.State.Status with
        |Disconnected |Failed _ -> true 
        |_ -> false

    member x.Ret(state : UserState) = {x with State = state}

    member x.RetFailure(state : UserState, msg : string) = 
        let state = state.RetFailure msg
        {x with State = state; Flag = StateVarFlag.Failed(msg)}

    member x.RetFailure(state : UserState, ret : MyRet) = 
        let msg = match ret with |MyRet.OK -> "" |Error msg -> msg
        let state = state.RetFailure msg
        {x with State = state; Flag = StateVarFlag.Failed(msg)}


type User(toServer, serverconnection) as this =
    let MailBox = new AutoCancelAgent<MsgToUser>(this.DoInbox)
    let InitState = UserState.InitState
    let ToServer : IUserToServer = toServer
    let ServerConnection : IServerConnection = serverconnection

    let mutable _Id = -1 
    let mutable _Name = ""
    let mutable _GamesPlayed = 0
    let mutable _Points = 0
    let mutable ToGM : IMsgTaker<MsgDataFromRemote> option = None
    
    let MessageTaker = 
        {new IMsgTakerX<MsgToUser> with
            member x.TakeMessage msg = this.TakeMessageSafe msg
            member x.TakeMessageGetReply<'Reply> (builder, ?timeout) = this.TakeMessageGetReply<'Reply> (builder, timeout)
            member x.TakeMessageGetReplyX (msg, ?timeout) = this.TakeMessageGetReplyX(msg, timeout) }

    let _To = MPToUser(MessageTaker)
    let _FromServer = _To :> IServerToUser
    let _FromGameOrganizer = _To :> IGameOrganizerToUser
    let _FromGamePlaying = _To :> IGamePlayingToUser
    let _FromLobby = _To :> ILobbyToUser

    let _FromGM =
        {new IMsgTaker<MsgGameMasterToRemote> with
            member x.TakeMessage msg = this.TakeMessageSafe msg}

    let _FromClient =
        {new IMsgTaker<MsgClientToServer> with
            member x.TakeMessage msg = this.TakeMessageSafe msg}

    let _GwToClient =
        {new IMsgTakerAsync<MsgServerToClient> with
             member x.TakeMessage(msg) = this.SendMessageToClient msg }

    let _ToClient = MPServerToClientAsync(_GwToClient) :> IServerToClientAsync

    let BadMsg (tag, exp, msg) = 
        Logger.BadMsg ("User[" + _Id.ToString() + ":" + _Name + "]." + tag, exp, msg)

    member x.Id = _Id
    member x.Name = _Name
    member x.GamesPlayed = _GamesPlayed
    member x.Points = _Points

    member val FromServer = _FromServer
    member val FromGM = _FromGM
    member val FromGamePlaying = _FromGamePlaying
    member val FromClient = _FromClient

    member x.SetUserData (id, name, psw) =
        _Id <- id
        _Name <- name

    member x.AddPoints (points, gamesplayed) =
        _Points <- _Points + points
        _GamesPlayed <- _GamesPlayed + gamesplayed
  
    member x.SetPoints (points, gamesplayed) =
        _Points <-  points
        _GamesPlayed <- gamesplayed

    member x.Start() = MailBox.Start()


    member private x.DoConnect (statevar : UserStateVar) = async{
        let cur_state = {statevar.State with Status = UserStatus.Connecting}
        let! msg = statevar.Reader(cur_state)
        let (greeting, ret : MyRet) =
            match msg with
            |MsgToUser.FromClient (MsgClientToServer.Connect greeting) -> (Some greeting, MyRet.OK)
            |_ -> BadMsg ("DoConnect", "Connect", msg); (None ,Error "Bad msg")

        if not ret.isok then return statevar.RetFailure(cur_state, ret) else

        if greeting.Value <> AppData.Greeting then
            let msg = "Nekorekta programmas versija"
            let! q =_ToClient.ConnectionRefused msg
            return statevar.RetFailure(cur_state, msg)
        else 

        let cur_state = {cur_state with Status = UserStatus.Connected}
        let! q = _ToClient.Connected ""
        return {statevar with State = cur_state; Worker = x.DoLogIn}
    }

    member private x.DoLogIn (statevar : UserStateVar) = async{
        let cur_state = {statevar.State with Status = UserStatus.LogIn}
        let! msg = statevar.Reader(cur_state)
        match msg with
        |MsgToUser.FromClient (MsgClientToServer.LogIn m) -> 
            let! ret = ToServer.LoginUser x.Id m.name m.psw
            match ret with
            |Some (NewOrLoginUserReply.OK id) -> 
                _Id <- id
                let! q = _ToClient.LogInOK()
                return {statevar with State = cur_state; Worker = x.DoInLobby}
            |Some (NewOrLoginUserReply.Failed msg) ->
                let! q = _ToClient.LogInFailed msg
                return {statevar with State = cur_state; Worker = x.DoLogIn}
            |None ->
                let msg = "Servera kļūda"
                let! q = _ToClient.LogInFailed msg
                return statevar.RetFailure(cur_state, "Bad msg")

        |MsgToUser.FromClient (MsgClientToServer.LogInAsGuest name) -> 
            let! ret = ToServer.LoginUserAsGuest x.Id name
            match ret with
            |Some (NewOrLoginUserReply.OK id) -> 
                _Id <- id
                let! q = _ToClient.LogInOK()
                return {statevar with State = cur_state; Worker = x.DoInLobby}
            |Some (NewOrLoginUserReply.Failed msg) ->
                let! q = _ToClient.LogInFailed msg
                return {statevar with State = cur_state; Worker = x.DoLogIn}
            |None ->
                let msg = "Servera kļūda"
                let! q = _ToClient.LogInFailed msg
                return statevar.RetFailure(cur_state, "Bad msg")

        |MsgToUser.FromClient (MsgClientToServer.GetRegCode m) -> 
            let! ret = ToServer.GetRegCode x.Id m.name m.psw m.email
            match ret with
            |Some (NewOrLoginUserReply.OK id) -> 
                _Id <- id
                let! q = _ToClient.GetRegCodeOk()
                return {statevar with State = cur_state; Worker = x.DoLogIn}
            |Some (NewOrLoginUserReply.Failed msg) ->
                let! q = _ToClient.GetRegCodeFailed msg
                return {statevar with State = cur_state; Worker = x.DoLogIn}
            |None ->
                let msg = "Servera kļūda"
                let! q = _ToClient.RegisterFailed msg
                return statevar.RetFailure(cur_state, "Bad msg")

        |MsgToUser.FromClient (MsgClientToServer.Register m) -> 
            let! ret = ToServer.Register x.Id m.name m.psw m.regcode
            match ret with
            |Some (NewOrLoginUserReply.OK id) -> 
                _Id <- id
                let! q = _ToClient.RegisterOk()
                return {statevar with State = cur_state; Worker = x.DoInLobby}
            |Some (NewOrLoginUserReply.Failed msg) ->
                let! q = _ToClient.RegisterFailed msg
                return {statevar with State = cur_state; Worker = x.DoLogIn}
            |None ->
                let msg = "Servera kļūda"
                let! q = _ToClient.RegisterFailed msg
                return statevar.RetFailure(cur_state, "Bad msg")
            
        |_ -> 
            BadMsg ("DoLogin", "MsgLogIn/MsgRegister", msg); 
            return statevar.RetFailure(cur_state, "Bad msg")
        
    }


    member private x.DoInLobby (statevar : UserStateVar) = async{
        let cur_state = {statevar.State with Status = UserStatus.InLobby}
        let! msg = statevar.Reader(cur_state)
        match msg with
        |MsgToUser.FromClient MsgClientToServer.EnterLobby -> 
            ToServer.EnterLobby x.Id
            return {statevar with State = cur_state; Worker = x.DoInLobbyA}
        |MsgToUser.FromClient (MsgClientToServer.FromClientGame _)
        |MsgToUser.FromClient MsgClientToServer.GameStopped
        |MsgToUser.FromGamePlaying _
        |MsgToUser.FromGameOrganizer _ ->
            return {statevar with State = cur_state} //ignore
        |_ -> 
            BadMsg ("DoInLobby", "MsgEnterLobby", msg)
            return statevar.RetFailure(cur_state, "Bad msg")
    }

    member private x.DoInLobbyA (statevar : UserStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader(cur_state)
        match msg with
        |MsgToUser.FromLobby (MsgLobbyToUser.LobbyData m) -> 
            let! q = _ToClient.LobbyData m
            return {statevar with State = cur_state; Worker = x.DoInLobbyA}

        |MsgToUser.FromLobby (MsgLobbyToUser.LobbyUpdate m) -> 
            let! q = _ToClient.LobbyUpdate m
            return {statevar with State = cur_state; Worker = x.DoInLobbyA}

        |MsgToUser.FromClient (MsgClientToServer.GetCalendarData) -> 
            let! ret = ToServer.GetCalendarData x.Id _Name
            if ret.IsSome && ret.Value <> "" then
                let! q = _ToClient.CalendarData(ret.Value) in ()
            return {statevar with State = cur_state; Worker = x.DoInLobbyA}

        |MsgToUser.FromClient (MsgClientToServer.GetCalendarTagData tag) -> 
            let! ret = ToServer.GetCalendarTagData x.Id _Name tag
            if ret.IsSome && ret.Value <> "" then
                let! q = _ToClient.CalendarTagData(ret.Value) in ()
            return {statevar with State = cur_state; Worker = x.DoInLobbyA}

        |MsgToUser.FromClient (MsgClientToServer.SetCalendarData data) -> 
            ToServer.SetCalendarData x.Id data
            return {statevar with State = cur_state; Worker = x.DoInLobbyA}

        |MsgToUser.FromClient MsgClientToServer.JoinGame -> 
            ToServer.UserStartWaitForNewGame x.Id
            return {statevar with State = cur_state; Worker = x.DoJoinGame}

        |MsgToUser.FromClient (MsgClientToServer.JoinPrivateGame m) -> 
            ToServer.UserStartWaitForPrivateGame x.Id m.name m.psw
            return {statevar with State = cur_state; Worker = x.DoJoinGame}

        |MsgToUser.FromClient (MsgClientToServer.FromClientGame _)
        |MsgToUser.FromClient MsgClientToServer.GameStopped
        |MsgToUser.FromGameOrganizer _
        |MsgToUser.FromGamePlaying _ ->
            return {statevar with State = cur_state} //ignore
        
        |_ -> 
            BadMsg ("DoInLobbyA", "LobbyData/MsgJoinGame", msg)
            return statevar.RetFailure(cur_state, "Bad msg")
    }

    member private x.DoJoinGame (statevar : UserStateVar) = async{
        let cur_state = {statevar.State with Status = UserStatus.WaitingForGame}
        let! msg = statevar.Reader(cur_state)
        match msg with
        |MsgToUser.FromLobby (MsgLobbyToUser.LobbyData _) -> 
            return {statevar with State = cur_state; Worker = x.DoJoinGame}

        |MsgToUser.FromClient MsgClientToServer.CancelNewGame -> 
            ToServer.UserCancelWaitForNewGame(x.Id)
            return {statevar with State = cur_state; Worker = x.DoInLobby}

        |MsgToUser.FromGameOrganizer (UserJoinedNewGame m) ->
            let! q = _ToClient.GotNewPlayer m.name m.extrainfo
            return {statevar with State = cur_state; Worker = x.DoJoinGame}

        |MsgToUser.FromGameOrganizer (UserLeftNewGame m) ->
            let! q = _ToClient.LostNewPlayer m.name ""
            return {statevar with State = cur_state; Worker = x.DoJoinGame}

        |MsgToUser.FromGameOrganizer MsgGameOrganizerToUser.CancelNewGame ->
            let! q = _ToClient.CancelNewGame()
            return {statevar with State = cur_state; Worker = x.DoInLobby}

        |MsgToUser.FromGamePlaying (MsgGamePlayingToUser.InitGame m) ->
            let! q = _ToClient.GotNewGame m.PlayerNames.[0] m.PlayerNames.[1] m.PlayerNames.[2]
            let cur_state = {cur_state with GameInitData = Some m}
            return {statevar with State = cur_state; Worker = x.DoInGame}

        |_ -> 
            BadMsg ("DoJoinGame", "UserJoinedNewGame/...", msg); 
            return statevar.RetFailure(cur_state, "Bad msg")
        
    }

    member private x.DoInGame (statevar : UserStateVar) = async{
        let cur_state = {statevar.State with Status = UserStatus.InGame}
        let! msg = statevar.Reader(cur_state)
        match msg with 
        |MsgToUser.FromGamePlaying MsgGamePlayingToUser.StartGame ->
            return {statevar with State = cur_state; Worker = x.DoInGameA}
        |MsgToUser.FromGamePlaying MsgGamePlayingToUser.StopGame
        //|MsgToUser.FromClient MsgClientToServer.GameStopped -> 
        //    return {statevar with State = cur_state; Worker = x.DoStopFromClient}
        |MsgToUser.FromClient MsgClientToServer.Disconnect
        |MsgToUser.NotConnected -> 
            let cur_state = {cur_state with Status = UserStatus.Disconnected}
            return {statevar with State = cur_state}
        |_ -> 
            BadMsg ("DoInGame", "FromGamePlaying StartGame", msg)
            return statevar.RetFailure(cur_state, "Bad msg")
    }

    member private x.DoInGameA (statevar : UserStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader(cur_state)
        match msg with 
        |MsgToUser.FromGamePlaying MsgGamePlayingToUser.StopGame 
        |MsgToUser.FromGM MsgGameMasterToRemote.Stop -> 
            return {statevar with Worker = x.DoStopFromServer} 

        |MsgToUser.FromGamePlaying (MsgGamePlayingToUser.FromGM mdata) -> 
            let! q = MsgServerToClient.FromGM mdata |> _GwToClient.TakeMessage
            return statevar

        |MsgToUser.FromGM mdata -> 
            let! q = MsgServerToClient.FromGM mdata |> _GwToClient.TakeMessage
            return statevar

        |MsgToUser.FromClient (MsgClientToServer.FromClientGame mdata) -> 
            cur_state.GameInitData.Value.MessageGateWay.TakeMessage mdata
            match mdata.msg with
            |MsgPlayerToGame.PlayerStopped _ 
            |MsgPlayerToGame.PlayerFailed _ ->
                return {statevar with Worker = x.DoStopFromClient} 
            |MsgPlayerToGame.ReplyStartNewGame m when (not m.yesorno) ->
                return {statevar with Worker = x.DoStopFromClient} 
            |_ ->
                return statevar

        |MsgToUser.FromClient MsgClientToServer.GameStopped -> 
            return {statevar with Worker = x.DoStopFromClient} 

        |MsgToUser.FromClient MsgClientToServer.Disconnect 
        |MsgToUser.NotConnected -> 
            if cur_state.GameInitData.IsSome then
                let plnr = cur_state.GameInitData.Value.PlayerNr
                let msg = MsgPlayerToGame.PlayerStopped plnr
                let msg = MsgDataFromRemote(plnr, msg)
                cur_state.GameInitData.Value.MessageGateWay.TakeMessage msg
            let cur_state = {cur_state with Status = UserStatus.Disconnected}
            return {statevar with State = cur_state}

        |MsgToUser.FromLobby _ -> return statevar // ignore

        |_ -> 
            BadMsg ("DoInGameA", "User.DoInGame ...", msg) 
            let cur_state = {cur_state with Status = UserStatus.Disconnected}
            return {statevar with State = cur_state}

    }

    member private x.DoStopFromClient (statevar : UserStateVar) = async{
        return {statevar with Worker = x.DoInLobby}
    }

    member private x.DoStopFromServer (statevar : UserStateVar) = async{
        let cur_state = statevar.State
        let! q = MsgServerToClient.FromGM MsgGameMasterToRemote.Stop |> _GwToClient.TakeMessage
        return {statevar with Worker = x.DoInLobby}
    }

    member private x.DoInit (statevar : UserStateVar) = async{
        let cur_state = statevar.State
        return {statevar with State = cur_state; Worker = x.DoConnect}
    }


    member private x.DoInbox(inbox : MailboxProcessor<MsgToUser>) = 
        let rec loop (statevar : UserStateVar) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 (new_state : UserStateVar) -> 
                if new_state.ShouldExit()
                then
                    x.Close()
                    return () 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                Logger.WriteLine("User[{0}:{1}]: exc: {2}", x.Id, x.Name, exc.ToString())
                x.Close()
                return () }
        let init_state = InitState
        let init_statevar = 
            {UserStateVar.Reader = x.MsgReader(inbox); 
                State = init_state; 
                Worker = x.DoInit;
                Flag = StateVarFlag.OK}
        loop(init_statevar)


    member private x.MsgReader (inbox : MailboxProcessor<MsgToUser>) (state : UserState) =
        let rec loop() = async{
            let bclose =
                match state.Status with
                |UserStatus.Failed _ -> true
                |_-> false
            if bclose then
                x.Close();
                return MsgToUser.Control MsgControl.KillPill
            else            
            
            let! msg = inbox.Receive()

            Logger.WriteLine("User[{0}:{1}]: <- {2}", x.Id, x.Name, msg)

            match msg with
            |MsgToUser.Control MsgControl.KillPill -> return msg
            |MsgToUser.Control (MsgControl.GetState channel) -> 
                channel.Reply state
                return! loop()
            |MsgToUser.SendToClient msgin ->
                let! q = x.SendMessageToClient msgin
                if not q then return MsgToUser.Control MsgControl.KillPill
                else return! loop()
            |MsgToUser.FromServer (MsgServerToUser.AddPoints m) ->
                _GamesPlayed <- _GamesPlayed + m.gamesplayed
                _Points <- _Points + m.points
                return! loop()
            | _ -> return msg}
        loop()

    member x.GetState() = 
        let msg channel = MsgToUser.Control (GetReply (MsgControl.GetState, channel))
        let state = MailBox.PostAndReply(msg)
        state

    member private x.SendMessageToClient (msg : MsgServerToClient) = async{
        try
            Logger.WriteLine("User[{0}]{1}.SendMessageToClient: msg: {2}", _Id, _Name, msg)
            return! ServerConnection.Send msg
        with 
        | exc -> 
            MsgToUser.Control MsgControl.KillPill |> x.TakeMessage
            Logger.WriteLine("User[{0}]{1}.SendMessageToClient: failed ex:{2}", _Id, _Name, exc.ToString())
            return false
    }

    member private x.TakeMessage(msg : MsgToUser) = x.TakeMessageSafe msg

    member private x.TakeMessageSafe(msg : MsgToUser) = 
        if not (x.IsClosed || x.IsDisposed) then 
            try 
                MailBox.Post msg 
            with exc -> 
                Logger.WriteLine("User[{0}]: EXC TakeMessageSafe: {1}", exc.Message)

    member private x.TakeMessage(msg : MsgGameMasterToRemote) = 
        let msg = MsgToUser.FromGM msg
        x.TakeMessage msg
    
    member private x.TakeMessageSafe(msg : MsgGameMasterToRemote) = 
        let msg = MsgToUser.FromGM msg
        x.TakeMessageSafe msg

    member private x.TakeMessage(msg : MsgClientToServer) = 
        let msg = MsgToUser.FromClient msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgClientToServer) = 
        let msg = MsgToUser.FromClient msg
        x.TakeMessageSafe msg


    member private x.TakeMessageGetReply<'Reply>(builder : AsyncReplyChannel<'Reply> -> MsgToUser, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout 10000
        if x.IsClosed || x.IsDisposed then 
            async{return None}
        else
        try MailBox.PostAndTryAsyncReply(builder, timeout)
        with |_-> async{return None}

    member private x.TakeMessageGetReplyX<'Reply>(msg : MsgToUser, timeout : int option) : Async<'Reply option>=  
        let timeout = defaultArg timeout 10000
        if x.IsClosed || x.IsDisposed then 
            async{return None}
        else
        let fmsg ch = MsgToUser.Control (GetReply (msg, ch))
        async{
            let! ret = Async.Catch (MailBox.PostAndTryAsyncReply(fmsg, timeout))
            match ret with
            |Choice1Of2 ret -> 
                match ret with
                |Some (:? 'Reply as mm) -> return Some mm
                |_ -> return None 
            |Choice2Of2 (exc : Exception) -> 
                return None }


    member val private _IsClosed = false with get,set
    member x.IsClosed = x._IsClosed

    member private x.Close() = 
        if not (x._IsClosed || x.IsDisposed) then
            Logger.WriteLine("User[{0}]: Closed")
            try ToServer.UserClosed x.Id with _ -> ()
            try x.Dispose()
            finally x._IsClosed <- true    


    member val private _IsDisposed = false with get, set
    member x.IsDisposed = x._IsDisposed

    member x.Dispose() =
        if not x.IsDisposed then
            try ServerConnection.Close() with _ -> ()
            try (MailBox :> IDisposable).Dispose() 
            finally x._IsDisposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()


    override x.Equals(obj) =
        match obj with
        | :? User as y -> x.Id = y.Id
        | _-> false
    
    override x.GetHashCode() = x.Id.GetHashCode()
    
    interface System.IComparable with
        member x.CompareTo(obj: obj): int = 
            match obj with
            | :? User as y -> compare y.Id x.Id
            | _ -> invalidArg "obj" "cannot compare values of different types"        


    interface IUser with
        member x.Id = x.Id
        member x.Name = x.Name
        member x.GamesPlayed = x.GamesPlayed
        member x.Points = x.Points
        member x.Kill() = MsgToUser.Control MsgControl.KillPill |> x.TakeMessage
        member x.GetState(timeout) = raise (System.NotImplementedException())
        member x.SetUserData (userid, name, psw) = x.SetUserData (userid, name, psw)
        member x.AddPoints (points, gamesplayed) = x.AddPoints (points, gamesplayed)
        member x.SetPoints (points, gamesplayed) = x.SetPoints (points, gamesplayed)
        member x.FromGameOrganizer = _FromGameOrganizer
        member x.FromGamePlaying = _FromGamePlaying
        member x.FromServer = _FromServer
        member x.FromClient = _FromClient
        member x.FromGM = _FromGM
        member x.FromLobby = _FromLobby
