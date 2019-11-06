namespace GameServerLib
open System
open System.Collections.Generic
open GameLib

type LobbyStatus = |Working

type LobbyState = {
    Status : LobbyStatus
    UsersOnline : Set<IUser>
    UsersInLobby : Set<IUser>
}
with
    static member InitState = {
        LobbyState.Status = LobbyStatus.Working;
        UsersOnline = Set.empty;
        UsersInLobby = Set.empty}

type private LobbyStateVar = {
    Reader : LobbyState -> Async<MsgToLobby>
    State : LobbyState
    Worker : LobbyStateVar -> Async<LobbyStateVar>
    Flag : StateVarFlag} with
    member x.ShouldExit() = x.Flag <> StateVarFlag.OK

type Lobby() as this =
    let MailBox = new AutoCancelAgent<MsgToLobby>(this.DoInbox)
    let InitState = LobbyState.InitState

    let MessageTaker = 
        {new IMsgTakerX<MsgToLobby> with
            member x.TakeMessage msg = this.TakeMessageSafe msg
            member x.TakeMessageGetReply<'Reply> (builder, ?timeout) = this.TakeMessageGetReply<'Reply> (builder, timeout)
            member x.TakeMessageGetReplyX (msg, ?timeout) = this.TakeMessageGetReplyX(msg, timeout) }
    
    let _To = MPToLobby(MessageTaker) :> IToLobby

    member val To = _To


    member x.Start() = MailBox.Start()

    member private x.FormatInfo (user : IUser) =
        sprintf "%A (%A)" user.Points user.GamesPlayed
    
    member private x.MakeInfo (user : IUser) =
        {LobbyPlayerInfo.name = user.Name; info = x.FormatInfo user}


    member private x.GetFullList (state : LobbyState) =
        let ret = List<LobbyPlayerInfo>()
        for uo in state.UsersOnline do
            let info = x.MakeInfo uo
            ret.Add info
        {LobbyData.playerCount = state.UsersOnline.Count; players = ret}

    member x.OnEnterLobby (state : LobbyState) (user : IUser) =
        let data = x.GetFullList state
        user.FromLobby.LobbyData data
        let new_users_inlobby = state.UsersInLobby.Add user
        {state with UsersInLobby = new_users_inlobby}

    member x.OnLeaveLobby (state : LobbyState) (user : IUser) =
        let new_users_inlobby = state.UsersInLobby.Remove user
        {state with UsersInLobby = new_users_inlobby}

    member x.OnEnterServer (state : LobbyState) (user : IUser) =
        let info = x.MakeInfo user
        let data = LobbyUpdateData.NewPlayer info
        for user in state.UsersInLobby do
            user.FromLobby.LobbyUpdate data
        let new_users = state.UsersOnline.Add user
        {state with UsersOnline = new_users}

    member x.OnLeaveServer (state : LobbyState) (user : IUser) =
        let data = LobbyUpdateData.LostPlayer user.Name
        let new_users = state.UsersOnline.Remove user
        let new_users_inlobby = state.UsersInLobby.Remove user
        for user1 in new_users_inlobby do
            user1.FromLobby.LobbyUpdate data
        {state with UsersOnline = new_users; UsersInLobby = new_users_inlobby}

    member x.OnUpdateUser (state : LobbyState) (user : IUser) =
        let info = x.MakeInfo user
        let data = LobbyUpdateData.UpdatePlayer info
        for user in state.UsersInLobby do
            user.FromLobby.LobbyUpdate data
        state

    member private x.DoMsgA (state : LobbyState) (msg : MsgToLobby) = 
        let cur_state = state
        let cur_state =
            match msg with
            |MsgToLobby.EnterLobby user ->
                if not (cur_state.UsersOnline.Contains(user)) then cur_state
                else x.OnEnterLobby cur_state user

            |MsgToLobby.LeaveLobby user ->
                if not (cur_state.UsersOnline.Contains(user)) then cur_state
                else x.OnLeaveLobby cur_state user

            |MsgToLobby.EnterServer user ->
                if cur_state.UsersOnline.Contains(user) then cur_state
                else x.OnEnterServer cur_state user

            |MsgToLobby.LeaveServer user -> 
                if not (cur_state.UsersOnline.Contains(user)) then cur_state
                else x.OnLeaveServer cur_state user

            |MsgToLobby.UpdateUser user -> 
                if not (cur_state.UsersOnline.Contains(user)) then cur_state
                else x.OnUpdateUser cur_state user

            |_-> cur_state
        cur_state


    member private x.DoMsg (statevar : LobbyStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader(cur_state)
        let cur_state = x.DoMsgA cur_state msg
        return {statevar with State = cur_state}
    }

    member private x.DoInbox(inbox : MailboxProcessor<MsgToLobby>) = 
        let rec loop (statevar : LobbyStateVar) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 (new_state : LobbyStateVar) -> 
                if new_state.ShouldExit()
                then
                    x.Close()
                    return () 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                x.Close()
                return () }
        let init_state = InitState
        let init_statevar = 
            {LobbyStateVar.Reader = x.MsgReader(inbox); 
                State = init_state; 
                Worker = x.DoMsg;
                Flag = StateVarFlag.OK}
        loop(init_statevar)


    member private x.MsgReader (inbox : MailboxProcessor<MsgToLobby>) (state : LobbyState) =
        let rec loop() = async{
            let! msg = inbox.Receive()

            Logger.WriteLine("Lobby: <- {0}", msg)

            match msg with
            |MsgToLobby.Control MsgControl.KillPill -> return msg
            |MsgToLobby.Control (MsgControl.GetState channel) ->
                channel.Reply state
                return! loop()
            | _ -> return msg}
        loop()

    member x.GetState() = 
            let msg channel = MsgToLobby.Control (GetReply (MsgControl.GetState, channel))
            let state = MailBox.PostAndReply(msg)
            state

    member private x.TakeMessage(msg : MsgToLobby) = x.TakeMessageSafe msg

    member private x.TakeMessageSafe(msg : MsgToLobby) = 
        if not (x.IsClosed || x.IsDisposed) then 
            try MailBox.Post msg finally ()

    member private x.TakeMessageGetReply<'Reply>(builder : AsyncReplyChannel<'Reply> -> MsgToLobby, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout MPHelper.MyTimeOut
        if x.IsClosed || x.IsDisposed then 
            async{return Option.None}
        else
        try 
            let ret = MailBox.PostAndTryAsyncReply(builder, timeout)
            ret
        with |_-> async{return Option.None}

    member private x.TakeMessageGetReplyX<'Reply>(msg : MsgToLobby, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout MPHelper.MyTimeOut
        if x.IsClosed || x.IsDisposed then 
            async{return None}
        else
        let fmsg ch = MsgToLobby.Control (GetReply (msg, ch))
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
        if not x._IsClosed then
            x.Dispose()
            x._IsClosed <- true    


    member val private _IsDisposed = false with get, set
    member x.IsDisposed = x._IsDisposed

    member x.Dispose() =
        if not x.IsDisposed then
            try
                (MailBox :> IDisposable).Dispose()
            finally
                x._IsDisposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()