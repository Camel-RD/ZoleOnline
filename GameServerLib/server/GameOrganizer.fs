namespace GameServerLib
open System
open System.Collections.Immutable
open GameLib

type GameOrganizerStatus = |Offline |Working

type GameOrganizerState = {
    Status : GameOrganizerStatus
    Games : Set<GameNew>
    Users : ImmutableList<IUser>
}
with
    static member InitState = {
        GameOrganizerState.Status = GameOrganizerStatus.Offline;
        Games = Set.empty;
        Users = ImmutableList.Empty}

type private GameOrganizerStateVar = {
    Reader : GameOrganizerState -> Async<MsgToGameOrganizer>
    State : GameOrganizerState
    Worker : GameOrganizerStateVar -> Async<GameOrganizerStateVar>
    Flag : StateVarFlag} with
    member x.ShouldExit() = x.Flag <> StateVarFlag.OK


type GameOrganizer(toServer) as this =
    let MailBox = new AutoCancelAgent<MsgToGameOrganizer>(this.DoInbox)
    let ToServer : IGameOrganizerToServer = toServer
    let IdGenerator = IdGenerator(0)
    let InitState = GameOrganizerState.InitState

    let MessageTaker = 
        {new IMsgTakerX<MsgToGameOrganizer> with
            member x.TakeMessage msg = this.TakeMessageSafe msg
            member x.TakeMessageGetReply<'Reply> (builder, ?timeout) = this.TakeMessageGetReply<'Reply> (builder, timeout)
            member x.TakeMessageGetReplyX (msg, ?timeout) = this.TakeMessageGetReplyX(msg, timeout) }

    member val To = MPToGameOrganizer(MessageTaker) :> IToGameOrganizer

    member x.Start() = MailBox.Start()

    member private x.UserCanceled (state : GameOrganizerState) (user : IUser) =
        let new_users = state.Users.Remove user
        let game = state.Games |> Seq.tryFind  (fun g -> g.Users.Contains user)
        let new_games = 
            match game with
            |Some game ->
                game.Users.Remove user |> ignore
                for user2 in game.Users do 
                    user2.FromGameOrganizer.UserLeftNewGame user.Name
                if game.Users.Count = 0 
                then 
                    //ToServer.NewGameCancel game.id -1 -1
                    state.Games.Remove game
                else state.Games
            |None -> state.Games
        {state with Users = new_users; Games = new_games}

    member private x.StartGame (state : GameOrganizerState) (game : GameNew) =
        ToServer.GotUsersForGame game.id game.Users.[0].Id game.Users.[1].Id game.Users.[2].Id
        let new_users = state.Users.RemoveRange game.Users
        let new_games = state.Games.Remove game
        {state with Users = new_users; Games = new_games}
        
    member private x.UserStartWaitForNewGame (state : GameOrganizerState) (user : IUser) =
        if state.Users.Contains user then
            let msg = "Lietotājs jau ir pieteicies spēlei"
            let state = x.UserCanceled state user
            user.FromGameOrganizer.Kick msg
            (state, Error msg)
        else
        let ngames = 
            state.Games 
            |> Set.filter (fun g -> not g.IsPrivate)
        let new_games, game = 
            if ngames.IsEmpty then
                let new_game = GameNew(IdGenerator.GetNext())
                (state.Games.Add new_game, new_game)
            else state.Games, (state.Games |> Seq.head)
        user.FromGameOrganizer.UserJoinedGame user.Name ""
        for user2 in game.Users do
            user.FromGameOrganizer.UserJoinedGame user2.Name ""
            user2.FromGameOrganizer.UserJoinedGame user.Name ""
        game.Users.Add user
        let new_users = state.Users.Add user
        let state = {state with Games = new_games; Users = new_users}
        let state =
            if game.Users.Count = 3 then
                x.StartGame state game
            else state
        (state, MyRet.OK)
    
    member private x.UserStartWaitForPrivateGame (state : GameOrganizerState) (user : IUser) 
            (gamename : string) (gamepsw : string) =

        if state.Users.Contains user then
            let msg = "Lietotājs jau ir pieteicies spēlei"
            let state = x.UserCanceled state user
            user.FromGameOrganizer.Kick msg
            (state, Error msg)
        else
        let ngames = 
            state.Games 
            |> Set.filter (fun g -> g.IsPrivate && g.GameName = gamename)
        let new_games, game = 
            if ngames.IsEmpty then
                let new_game = GameNew(IdGenerator.GetNext())
                new_game.IsPrivate <- true
                new_game.GameName <- gamename
                new_game.GamePsw <- gamepsw
                (state.Games.Add new_game, new_game)
            else state.Games, (state.Games |> Seq.head)
        if game.GamePsw <> gamepsw then
            user.FromGameOrganizer.CancelNewGame()
            (state, MyRet.OK)
        else
        user.FromGameOrganizer.UserJoinedGame user.Name ""
        for user2 in game.Users do
            user.FromGameOrganizer.UserJoinedGame user2.Name ""
            user2.FromGameOrganizer.UserJoinedGame user.Name ""
        game.Users.Add user
        let new_users = state.Users.Add user
        let state = {state with Games = new_games; Users = new_users}
        let state =
            if game.Users.Count = 3 then
                x.StartGame state game
            else state
        (state, MyRet.OK)

    member private x.Stop (state : GameOrganizerState) =
        for game in state.Games do
            let userid1 = if game.Users.Count = 0 then -1 else game.Users.[0].Id
            let userid2 = if game.Users.Count = 1 then -1 else game.Users.[1].Id
            for user in game.Users do user.FromGameOrganizer.CancelNewGame()
            ToServer.NewGameCancel game.id userid1 userid2
        {state with Games = Set.empty; Users = ImmutableList.Empty; Status = GameOrganizerStatus.Offline}

    member private x.DoMsg (statevar : GameOrganizerStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader(cur_state)
        let cur_state =
            match msg, cur_state.Status with
            |MsgToGameOrganizer.Control MsgControl.Start, GameOrganizerStatus.Offline ->
                {cur_state with Status = GameOrganizerStatus.Working}
            
            |MsgToGameOrganizer.Control MsgControl.Stop, GameOrganizerStatus.Working  ->
                x.Stop cur_state
            
            |MsgToGameOrganizer.FromServer (UserStartWaitForNewGame user), GameOrganizerStatus.Working ->
                let cur_state, ret = x.UserStartWaitForNewGame cur_state user
                cur_state
            
            |MsgToGameOrganizer.FromServer (UserStartWaitForPrivateGame m), GameOrganizerStatus.Working ->
                let cur_state, ret = x.UserStartWaitForPrivateGame cur_state m.user m.name m.psw
                cur_state

            |MsgToGameOrganizer.FromServer (UserStartWaitForNewGame user), GameOrganizerStatus.Offline ->
                Logger.WriteLine "Got msg UserStartWaitForNewGame when GameOrganizerStatus.Offline";
                cur_state
            
            |MsgToGameOrganizer.FromServer (UserStartWaitForPrivateGame _), GameOrganizerStatus.Offline ->
                Logger.WriteLine "Got msg UserStartWaitForNewGame when GameOrganizerStatus.Offline";
                cur_state

            |MsgToGameOrganizer.FromServer (UserCancelWaitForNewGame user), GameOrganizerStatus.Working ->
                let cur_state = x.UserCanceled cur_state user
                cur_state

            |MsgToGameOrganizer.FromServer (UserClosed userid), GameOrganizerStatus.Working ->
                let user = cur_state.Users |> Seq.tryFind (fun u -> u.Id = userid)
                match user with
                |Some user -> x.UserCanceled cur_state user
                |None -> cur_state

            |_-> cur_state
        return {statevar with State = cur_state}
    }

    member private x.DoInit (statevar : GameOrganizerStateVar) = async{
        let cur_state = {statevar.State with Status = GameOrganizerStatus.Working}
        return {statevar with State = cur_state; Worker = x.DoMsg}
    }

    member private x.DoInbox(inbox : MailboxProcessor<MsgToGameOrganizer>) = 
        let rec loop (statevar : GameOrganizerStateVar) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 (new_state : GameOrganizerStateVar) -> 
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
            {GameOrganizerStateVar.Reader = x.MsgReader(inbox); 
                State = init_state; 
                Worker = x.DoInit;
                Flag = StateVarFlag.OK}
        loop(init_statevar)

    member private x.MsgReader (inbox : MailboxProcessor<MsgToGameOrganizer>) (state : GameOrganizerState) =
        let rec loop() = async{
            let! msg = inbox.Receive()

            Logger.WriteLine("GameOrganizer: <- {0}", msg)

            match msg with
            |MsgToGameOrganizer.Control MsgControl.KillPill -> return msg
            |MsgToGameOrganizer.Control (MsgControl.GetState channel) ->
                channel.Reply state
                return! loop()
            | _ -> return msg}
        loop()

    member x.GetState() = 
            let msg channel = MsgToGameOrganizer.Control (GetReply (MsgControl.GetState, channel))
            let state = MailBox.PostAndReply(msg)
            state

    member private x.TakeMessage(msg : MsgToGameOrganizer) = MailBox.Post msg

    member private x.TakeMessageSafe(msg : MsgToGameOrganizer) = 
        if not (x.IsClosed || x.IsDisposed) then 
            try MailBox.Post msg finally ()

    member private x.TakeMessageGetReply<'Reply>(builder : AsyncReplyChannel<'Reply> -> MsgToGameOrganizer, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout 10000
        if x.IsClosed || x.IsDisposed then 
            async{return None}
        else
        try MailBox.PostAndTryAsyncReply(builder, timeout)
        with |_-> async{return None}

    member private x.TakeMessageGetReplyX<'Reply>(msg : MsgToGameOrganizer, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout 10000
        if x.IsClosed || x.IsDisposed then 
            async{return None}
        else
        let fmsg ch = MsgToGameOrganizer.Control (GetReply (msg, ch))
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