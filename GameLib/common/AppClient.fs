namespace GameLib
open System
open System.Collections.Immutable

type AppClientStatus = 
    |NotSet |Initialized |StartUp |OfflineGame |Connecting |Connected 
    |LoggingIn |InLobby |JoiningGame |OnlineGame |Failed

type AppClientState = {
    Status : AppClientStatus
    Name : string
    Regdata : {|psw : string; regcode : string; email : string|}
    PlayerNames : string list
    ExcMsg : string
}
with
    static member InitState = {
        AppClientState.Status = AppClientStatus.NotSet;
        Name = "";
        Regdata = {|psw = ""; regcode = ""; email = ""|};
        PlayerNames = [];
        ExcMsg = ""}
    member x.Fail (msg : string) = {x with Status = AppClientStatus.Failed; ExcMsg = msg}


type private AppClientStateVar = {
    Reader : AppClientState  -> int -> Async<MsgToClient>
    State : AppClientState
    Worker : AppClientStateVar -> Async<AppClientStateVar>
    Flag : StateVarFlag} with
    member x.ShouldExit() = x.Flag <> StateVarFlag.OK
    member x.WithSt state = {x with State = state}
    member x.WithStW (state, worker) = {x with State = state; Worker = worker}

type AppClient(gameform) as this =
    let MailBox = new AutoCancelAgent<MsgToClient>(this.DoInbox)
    let GameForm : IGameForm = gameform
    let InitState = AppClientState.InitState
    let mutable LocalPlayerNr = -1
    let mutable LocalPlayerName = ""
    let mutable ServerIP = ""
    let mutable ServerPort = 0
    let mutable ClientGame : GameClient option = None
    let mutable OfflineGame : GameServer option = None
    let mutable GWToServer : IMsgTaker<MsgClientToServer> option = None

    let ToGameUI = GameForm :> IPlayerToUI 
    let ToClientUI = GameForm :> IClientToUI

    (*
    let _GWToServerWrapper =
        {new IMsgTaker<MsgClientToServer> with
             member x.TakeMessage(msg) = MsgToClient.SendMessageToServer msg |> this.TakeMessage}
    let _ToServer = MPClientToServer(_GWToServerWrapper) :> IClientToServer
    *)
    
    let _GWToServerWrapper =
        {new IMsgTakerAsync<MsgClientToServer> with
             member x.TakeMessage(msg) = this.SendMessageToServer msg}

    let _ToServer = MPClientToServerAsync(_GWToServerWrapper) :> IClientToServerAsync

    let SelectGameUITarget() =
        let state = this.GetStateEx()
        match state.Status, OfflineGame, ClientGame  with
        |AppClientStatus.OfflineGame, Some game, _-> game.FromUser
        |AppClientStatus.OnlineGame, _, Some game -> game.FromUser 
        |_-> UserToXEmpty.Empty :> IUserToX

    let IsValidMove (card : Card) = SelectGameUITarget().IsValidMove(card)

    //let FromGameUI = SelectGameUITarget()
    let FromGameUI = MPGameUIToClient(this.TakeMessage, IsValidMove) :> IUserToX
    let FromClientUI = MPUIToClient(this.TakeMessage) :> IUIToClient

    let FromGM =
        {new IMsgTaker<MsgGameMasterToRemote> with
            member x.TakeMessage msg = this.TakeMessageSafe msg}

    let FromServer =
        {new IMsgTaker<MsgServerToClient> with
            member x.TakeMessage msg = this.TakeMessageSafe msg}

    let FromClientGame =
        {new IMsgTaker<MsgDataFromRemote> with
            member x.TakeMessage msg = MsgToClient.FromClientGame msg |> this.TakeMessageSafe}

    let GameToOwner =
        {new IGameToOwner with
             member x.GameStopped(msg) = 
                MsgToClient.FromGame (MsgGameToOwner.GameStopped msg) |> this.TakeMessageSafe
             member x.GameClosed() = 
                MsgToClient.FromGame MsgGameToOwner.GameClosed |> this.TakeMessageSafe
             member x.AddPoints userpoints = ()}

    let GWToGameMaster =
        {new IMsgTaker<MsgDataFromRemote> with
             member x.TakeMessage(msg) = FromClientGame.TakeMessage msg}

    let Connection = ClientConnection(FromServer)


    member x.InitAppClient() =
        MailBox.Start()
        let msg = MsgToClient.InitGame
        x.TakeMessage msg
    
    member x.SetGWToServer(gw : IMsgTaker<MsgClientToServer>) = 
        GWToServer <- Some gw

    member x.StartLocalGame(playrename : string) =
        let playernames = [playrename; "Askolds"; "Haralds"] |> ImmutableArray.CreateRange
        let playertypes = [PlayerType.Local; PlayerType.PcAI; PlayerType.PcAI] |> ImmutableArray.CreateRange
        let game = new GameServer(GameForm, playernames, 0, playertypes, false, None, Some GameToOwner)
        LocalPlayerName <- playrename
        LocalPlayerNr <- 0
        game.InitGame()
        OfflineGame <- Some game

    member x.StartOnlineGame(localplayrename : string, names : string list) =
        let playernames = ImmutableArray.CreateRange names
        LocalPlayerNr <- playernames.IndexOf(localplayrename)
        LocalPlayerName <- localplayrename
        let game = new GameClient(ToGameUI, playernames, 0, LocalPlayerNr, PlayerType.Local, 
                    false, Some FromClientGame, Some GameToOwner)
        game.InitGame()
        ClientGame <- Some game
    
    member private x.StopLocalGame() = 
        if OfflineGame.IsSome then
            try OfflineGame.Value.Dispose()
            finally OfflineGame <- None
        Logger.WriteLine("AppClient: OfflineGame closed")

    member private x.StopOnlineGame() = 
        if ClientGame.IsSome then
            try ClientGame.Value.Dispose()
            finally ClientGame <- None
        Logger.WriteLine("AppClient: OnlineGame closed")
    
    member private x.DoOfflineGame (statevar : AppClientStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader statevar.State -1
        match msg with
        |MsgToClient.FromGameUI msgin -> 
            MsgToGame.FromGameUI msgin
            |> OfflineGame.Value.TakeMessage 
            return statevar
        
        |MsgToClient.FromGame (MsgGameToOwner.GameStopped msgin) -> 
            x.StopLocalGame()
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |MsgToClient.FromGame MsgGameToOwner.GameClosed -> 
            x.StopLocalGame()
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoOfflineGame: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
    }

    member private x.DoOnlineGame (statevar : AppClientStateVar) = 
        let cur_state = {statevar.State with Status = AppClientStatus.OnlineGame}
        x.StartOnlineGame(cur_state.Name, cur_state.PlayerNames)
        statevar.WithStW (cur_state, x.DoOnlineGameA)
        |> x.DoOnlineGameA

    member private x.DoOnlineGameA (statevar : AppClientStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader cur_state 30000
        match msg with
        |MsgToClient.FromServer (MsgServerToClient.LobbyData _) -> 
            return statevar.WithStW (cur_state, x.DoOnlineGameA)
        
        |MsgToClient.FromServer (MsgServerToClient.LobbyUpdate mdata) -> 
            return statevar.WithStW (cur_state, x.DoOnlineGameA)

        |MsgToClient.FromServer (MsgServerToClient.FromGM MsgGameMasterToRemote.Stop) -> 
            x.StopOnlineGame()
            return statevar.WithStW (cur_state, x.DoLobby)
        
        |MsgToClient.FromGame (MsgGameToOwner.GameStopped msgin) -> 
            x.StopOnlineGame()
            return statevar.WithStW (cur_state, x.DoLobby)
        
        |MsgToClient.FromGame MsgGameToOwner.GameClosed -> 
            x.StopOnlineGame()
            return statevar.WithStW (cur_state, x.DoLobby)

        |MsgToClient.FromServer (MsgServerToClient.FromGM mdata) -> 
            ClientGame.Value.FromGM.TakeMessage mdata
            return statevar.WithStW (cur_state, x.DoOnlineGameA)
        
        |MsgToClient.FromGameUI mdata -> 
            ClientGame.Value.FromGameUI.TakeMessage mdata
            return statevar.WithStW (cur_state, x.DoOnlineGameA)
        
        |MsgToClient.FromClientGame mdata -> 
            let msgout = MsgClientToServer.FromClientGame mdata 
            let! q = x.SendMessageToServer msgout
            if not q then 
                GameForm.ShowMessage "Nav savienojuma ar sarveri"
                return statevar.WithStW (cur_state, x.DoStartUp)
            else return statevar.WithStW (cur_state, x.DoOnlineGameA)
        
        |MsgToClient.NoConnection -> 
            GameForm.ShowMessage "Nav savienojuma ar sarveri"
            return statevar.WithStW (cur_state, x.DoStartUp)

        |MsgToClient.Control MsgControl.TimeOut ->
            GameForm.ShowMessage "Kādu laiku nekas nav darīts ..."
            do! Async.Sleep(5000)
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoOnlineGameA: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
    }

    member private x.DoJoinGame (statevar : AppClientStateVar) = async{
        let cur_state = {statevar.State with Status = AppClientStatus.JoiningGame}
        ToClientUI.GoToNewGame()
        let! q = _ToServer.JoinGame()
        return statevar.WithStW (cur_state, x.DoJoinGameA)
    }
    
    member val private _gamename = "" with get,set
    member val private _gamepsw = "" with get,set

    member private x.DoJoinPrivateGame (statevar : AppClientStateVar) =  async{
        let cur_state = {statevar.State with Status = AppClientStatus.JoiningGame}
        ToClientUI.GoToNewGame()
        let! q = _ToServer.JoinPrivateGame x._gamename x._gamepsw
        return statevar.WithStW (cur_state, x.DoJoinGameA)
    }

    member private x.DoJoinGameA (statevar : AppClientStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader cur_state -1
        match msg with
        |MsgToClient.FromServer (MsgServerToClient.LobbyData _) 
        |MsgToClient.FromServer (MsgServerToClient.LobbyUpdate _) 
        |MsgToClient.FromServer (MsgServerToClient.CalendarData _) 
        |MsgToClient.FromServer (MsgServerToClient.CalendarTagData _) ->  
            return statevar.WithStW (cur_state, x.DoJoinGameA) //ignore

        |MsgToClient.FromServer (MsgServerToClient.GotNewPlayer mdata) -> 
            ToClientUI.GotPlayerForNewGame mdata.name mdata.extrainfo
            return statevar.WithStW (cur_state, x.DoJoinGameA)
        
        |MsgToClient.FromServer (MsgServerToClient.LostNewPlayer mdata) -> 
            ToClientUI.LostPlayerForNewGame mdata.name
            return statevar.WithStW (cur_state, x.DoJoinGameA)
        
        |MsgToClient.FromServer (MsgServerToClient.CancelNewGame) -> 
            ToClientUI.CancelNewGame("")
            return statevar.WithStW (cur_state, x.DoJoinGameA)
        
        |MsgToClient.FromServer (MsgServerToClient.GotNewGame mdata) -> 
            let playernames = [mdata.user1; mdata.user2; mdata.user3]
            let cur_state = {cur_state with PlayerNames = playernames}
            return statevar.WithStW (cur_state, x.DoOnlineGame)
        
        |MsgToClient.FromClientUI MsgUIToClient.CancelNewGame -> 
            let! q = _ToServer.CancelNewGame()
            return statevar.WithStW (cur_state, x.DoLobby)
        
        |MsgToClient.NoConnection -> 
            GameForm.ShowMessage "Nav savienojuma ar sarveri"
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoJoinGameA: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
    }

    member private x.DoLobby (statevar : AppClientStateVar) = async{
        let cur_state = {statevar.State with Status = AppClientStatus.InLobby}
        ToClientUI.GoToLobby()
        let! q = _ToServer.EnterLobby()
        return statevar.WithStW (cur_state, x.DoLobbyA)
    }

    member private x.DoLobbyA (statevar : AppClientStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader cur_state -1
        match msg with
        |MsgToClient.FromServer (MsgServerToClient.LobbyData mdata) -> 
            ToClientUI.SetLobbyData mdata
            return statevar.WithStW (cur_state, x.DoLobbyA)
        
        |MsgToClient.FromServer (MsgServerToClient.LobbyUpdate mdata) -> 
            match mdata with
            |LobbyUpdateData.NewPlayer m -> ToClientUI.AddLobbyData m
            |LobbyUpdateData.LostPlayer m -> ToClientUI.RemoveLobbyData m
            |LobbyUpdateData.UpdatePlayer m -> ToClientUI.UpdateLobbyData m
            return statevar.WithStW (cur_state, x.DoLobbyA)

        |MsgToClient.FromServer (MsgServerToClient.CalendarData mdata) -> 
            ToClientUI.CalendarData mdata
            return statevar.WithStW (cur_state, x.DoLobbyA)

        |MsgToClient.FromServer (MsgServerToClient.CalendarTagData mdata) -> 
            ToClientUI.CalendarTagData mdata
            return statevar.WithStW (cur_state, x.DoLobbyA)

        |MsgToClient.FromClientUI MsgUIToClient.GetCalendarData -> 
            let msg = MsgClientToServer.GetCalendarData 
            let! q = x.SendMessageToServer msg
            return statevar.WithStW (cur_state, x.DoLobbyA)

        |MsgToClient.FromClientUI (MsgUIToClient.GetCalendarTagData tag) -> 
            let msg = MsgClientToServer.GetCalendarTagData tag
            let! q = x.SendMessageToServer msg
            return statevar.WithStW (cur_state, x.DoLobbyA)

        |MsgToClient.FromClientUI (MsgUIToClient.SetCalendarData data) -> 
            let msg = MsgClientToServer.SetCalendarData data
            let! q = x.SendMessageToServer msg
            return statevar.WithStW (cur_state, x.DoLobbyA)

        |MsgToClient.FromClientUI MsgUIToClient.JoinGame -> 
            return statevar.WithStW (cur_state, x.DoJoinGame)
        
        |MsgToClient.FromClientUI (MsgUIToClient.JoinPrivateGame m) -> 
            x._gamename <- m.name
            x._gamepsw <- m.psw
            return statevar.WithStW (cur_state, x.DoJoinPrivateGame)

        |MsgToClient.NoConnection -> 
            GameForm.ShowMessage "Nav savienojuma ar sarveri"
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |MsgToClient.FromGM _
        |MsgToClient.FromClientGame _
        |MsgToClient.FromGameUI _ -> 
            Logger.WriteLine("AppClient[{0}].DoLobbyA ignoring:{1}", LocalPlayerNr, msg)
            return statevar.WithSt cur_state
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoLobbyA: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
    }

    member private x.DoRegister (statevar : AppClientStateVar) = async{
        let cur_state = statevar.State
        let regdata = cur_state.Regdata
        let! q = _ToServer.Register cur_state.Name regdata.psw regdata.regcode
        ToClientUI.Wait "Gaidam atbildi no servera"
        let! msg = statevar.Reader cur_state 10000
        if msg = (MsgToClient.Control MsgControl.TimeOut) then 
            Logger.WriteLine("AppClient[{0}].GetRegCode: MsgReader timeout", cur_state.Name)
            ToClientUI.ShowMessage2 "Neizdevās sagaidīt atbildi no servera"
            return statevar.WithStW (cur_state, x.DoLogIn)
        else
        match msg with
        |MsgToClient.FromServer (MsgServerToClient.RegisterOk) -> 
            return statevar.WithStW (cur_state, x.DoLobby)
        
        |MsgToClient.FromServer (MsgServerToClient.RegisterFailed mdata) -> 
            ToClientUI.ShowMessage2 mdata
            return statevar.WithStW (cur_state, x.DoLogIn)
        
        |MsgToClient.NoConnection -> 
            ToClientUI.ConnectionFailed "Nav savienojuma ar sarveri"
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoRegister: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
    }

    member private x.DoGetRegCode (statevar : AppClientStateVar) = async{
        let cur_state = statevar.State
        let regdata = cur_state.Regdata
        let! q = _ToServer.GetRegCode cur_state.Name regdata.psw regdata.email
        ToClientUI.Wait "Gaidam atbildi no servera"
        let! msg = statevar.Reader cur_state 10000
        if msg = (MsgToClient.Control MsgControl.TimeOut) then 
            Logger.WriteLine("AppClient[{0}].GetRegCode: MsgReader timeout", cur_state.Name)
            ToClientUI.ShowMessage2 "Neizdevās sagaidīt atbildi no servera"
            return statevar.WithStW (cur_state, x.DoLogIn)
        else
        match msg with
        |MsgToClient.FromServer (MsgServerToClient.GetRegCodeOk) -> 
            ToClientUI.ShowMessage2 "Reģistrācijas kods nosūtōts uz norādīto e-pastu\nReģistrāciju var turpināt ar nosūtīto kodu"
            return statevar.WithStW (cur_state, x.DoLogIn)
        
        |MsgToClient.FromServer (MsgServerToClient.GetRegCodeFailed mdata) -> 
            ToClientUI.ShowMessage2 mdata
            return statevar.WithStW (cur_state, x.DoLogIn)
        
        |MsgToClient.NoConnection -> 
            ToClientUI.ConnectionFailed "Nav savienojuma ar sarveri"
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoGetRegCode: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
    }

    member private x.DoLogInA (statevar : AppClientStateVar) = async{
        let cur_state = statevar.State
        let regdata = cur_state.Regdata
        let! q = 
            if regdata.psw ="" then 
                _ToServer.LogInAsGuest cur_state.Name
            else
                _ToServer.LogIn cur_state.Name regdata.psw
        ToClientUI.Wait "Mēģinam pierakstīties serverī"
        let! msg = statevar.Reader cur_state 10000
        if msg = (MsgToClient.Control MsgControl.TimeOut) then 
            Logger.WriteLine("AppClient[{0}].DoLogInA: MsgReader timeout", cur_state.Name)
            ToClientUI.ShowMessage2 "Neizdevās sagaidīt atbildi no servera"
            return statevar.WithStW (cur_state, x.DoLogIn)
        else
        match msg with
        |MsgToClient.FromServer (MsgServerToClient.LogInOk) -> 
            return statevar.WithStW (cur_state, x.DoLobby)
        
        |MsgToClient.FromServer (MsgServerToClient.LogInFailed mdata) -> 
            ToClientUI.ShowMessage2 mdata
            ToClientUI.GoToLoginPage()
            return statevar.WithStW (cur_state, x.DoLogIn)
        
        |MsgToClient.NoConnection -> 
            ToClientUI.ConnectionFailed "Nav savienojuma ar sarveri"
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoLogInA: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
    }

    member private x.DoLogIn (statevar : AppClientStateVar) = async{
        let cur_state = {statevar.State with Status = AppClientStatus.LoggingIn}
        ToClientUI.GoToLoginPage()
        let! msg = statevar.Reader cur_state -1
        match msg with
        |MsgToClient.FromClientUI (MsgUIToClient.LogIn mdata)  -> 
            let regdata = {|psw = mdata.psw; regcode = ""; email = ""|}
            let cur_state = {cur_state with Regdata = regdata; Name = mdata.name}
            LocalPlayerName <- mdata.name
            return statevar.WithStW (cur_state, x.DoLogInA)
        
        |MsgToClient.FromClientUI (MsgUIToClient.LogInAsGuest name)  -> 
            let regdata = {|psw = ""; regcode = ""; email = ""|}
            let cur_state = {cur_state with Regdata = regdata; Name = name}
            LocalPlayerName <- name
            return statevar.WithStW (cur_state, x.DoLogInA)

        |MsgToClient.FromClientUI (MsgUIToClient.GetRegCode mdata)  -> 
            let regdata = {|psw = mdata.psw; regcode = ""; email = mdata.email|}
            let cur_state = {cur_state with Regdata = regdata; Name = mdata.name}
            LocalPlayerName <- mdata.name
            return statevar.WithStW (cur_state, x.DoGetRegCode)
        
        |MsgToClient.FromClientUI (MsgUIToClient.Register mdata)  -> 
            let regdata = {|psw = mdata.psw; regcode = mdata.regcode; email = ""|}
            let cur_state = {cur_state with Regdata = regdata; Name = mdata.name}
            LocalPlayerName <- mdata.name
            return statevar.WithStW (cur_state, x.DoRegister)
        
        |MsgToClient.NoConnection -> 
            ToClientUI.ShowMessage2 "Nav savienojuma ar sarveri"
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoLogIn: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
           
    }

    member private x.DoConnect (statevar : AppClientStateVar) = async{
        let cur_state = {statevar.State with Status = AppClientStatus.Connecting}
        ToClientUI.Wait "Mēģinam pieslēgties serverim"
        let! bcon = Connection.Connect ServerIP ServerPort
        if not bcon then
            return statevar.WithStW (cur_state, x.DoStartUp)
        else
        let! q = _ToServer.Connect AppData.Greeting
        if not q then
            return statevar.WithStW (cur_state, x.DoStartUp)
        else
        let! msg = statevar.Reader cur_state -1
        match msg with
        |MsgToClient.FromServer (MsgServerToClient.Connected _) -> 
            let cur_state = {statevar.State with Status = AppClientStatus.Connected}
            return statevar.WithStW (cur_state, x.DoLogIn)
        
        |MsgToClient.FromServer (MsgServerToClient.ConnectionRefused mdata) -> 
            ToClientUI.ConnectionFailed mdata
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |MsgToClient.NoConnection -> 
            ToClientUI.ConnectionFailed "Nav savienojuma ar sarveri"
            return statevar.WithStW (cur_state, x.DoStartUp)
        
        |_-> 
            Logger.WriteLine("AppClient[{0}].DoConnect: bad msg {1}", LocalPlayerName, msg)
            return statevar.WithStW (cur_state, x.DoStartUp)
    }

    member private x.SilentCloseConnection() = async{
        if Connection.Isclosed then
            return true
        else
        Connection.Close()
        let scanner msg = 
            match msg with
            |MsgToClient.FromServer MsgServerToClient.Disconnect -> 
                Some (async{return true})
            |_-> None
        let scanner_takeall msg = Some (async{return true})
        let! b = MailBox.TryScan (scanner_takeall, 300)
        return true
    }

    member private x.DoStartUp (statevar : AppClientStateVar) = async{
        let cur_state = {statevar.State with Status = AppClientStatus.StartUp}
        let! q = x.SilentCloseConnection() 
        GameForm.DoStartUp()
        let! msg = statevar.Reader cur_state -1
        match msg with
        |MsgToClient.FromClientUI (MsgUIToClient.PlayOffline name) -> 
            x.StartLocalGame name
            let cur_state = 
                {cur_state with 
                    Status = AppClientStatus.OfflineGame; 
                    Name = name}
            LocalPlayerName <- name
            return statevar.WithStW (cur_state, x.DoOfflineGame)
        
        |MsgToClient.FromClientUI (MsgUIToClient.Connect m) -> 
            ServerIP <- m.ip
            ServerPort <- m.port
            return statevar.WithStW (cur_state, x.DoConnect)
        
        |_ -> 
            Logger.WriteLine("AppClient[{0}].DoStartUp: DoStartUp ignoring msg:{1}", LocalPlayerName, msg)
            return statevar.WithSt cur_state
    }

    member private x.DoMsgInit (statevar : AppClientStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader statevar.State -1
        let cur_state =
            match msg with
            |MsgToClient.InitGame -> cur_state
            |_ -> 
                Logger.BadMsg ("AppClient.DoMsgInit", "InitGame", msg)
                cur_state.Fail "Bad state"

        if cur_state.Status = AppClientStatus.Failed then 
            return {statevar with State = cur_state; Flag = StateVarFlag.Failed ""}
        else 

        return statevar.WithStW (cur_state, x.DoStartUp) 
    }


    member private x.OnCloseAppClient() =
        if not (x.IsClosed || x.IsDisposed) then
            try Connection.Close()
            finally x.Close()


    member private x.DoInbox(inbox : MailboxProcessor<MsgToClient>) = 
        let rec loop (statevar : AppClientStateVar) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 new_state -> 
                if new_state.ShouldExit() || new_state.Flag = StateVarFlag.Return
                then 
                    x.OnCloseAppClient()
                    return () 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                x.OnCloseAppClient()
                return () }
        let init_state = InitState
        let init_statevar = 
            {Reader = x.MsgReader inbox; 
            State = init_state; 
            Worker = x.DoMsgInit;
            Flag = StateVarFlag.OK}
        loop(init_statevar)


    member private x.MsgReader (inbox : MailboxProcessor<MsgToClient>) (state : AppClientState) (timeout : int)  =
        let rec loop() = async{
            let! ret = Async.Catch (inbox.Receive(timeout))
            let msg = 
                match ret with
                |Choice1Of2 msg -> msg
                |Choice2Of2 _ -> MsgToClient.Control MsgControl.TimeOut 

            Logger.WriteLine("AppClient[{0}]: <- {1}",state.Name, msg.ToString())

            match msg with
            |MsgToClient.Control MsgControl.KillPill -> return msg
            
            |MsgToClient.Control (MsgControl.GetState channel) -> 
                channel.Reply state
                return! loop()
            
            |MsgToClient.FromServer MsgServerToClient.Disconnect ->
                return MsgToClient.NoConnection
            
            |MsgToClient.FromClientUI MsgUIToClient.Disconnect ->
                Connection.Close()
                return MsgToClient.NoConnection
            
            |MsgToClient.FromClientUI MsgUIToClient.AppClosing ->
                x.OnCloseAppClient()
                return MsgToClient.Control MsgControl.KillPill

            |MsgToClient.SendMessageToServer msgin ->
                let! q = x.SendMessageToServer msgin 
                if not q then 
                    return MsgToClient.NoConnection
                else return! loop()
            
            | _ -> return msg}
        loop()


    member x.GetState() = 
        let msg channel = MsgToClient.Control (MsgControl.GetState channel)
        let state = MailBox.PostAndReply(msg)
        state
    
    member x.GetStateEx() = x.GetState() :?> AppClientState


    member private x.SendMessageToServer (msg : MsgClientToServer) = async{
        if Connection.IsConnected then
            try
                Logger.WriteLine("AppClient[{0}]: SendMessageToServer msg:{1}", LocalPlayerName, msg)
                let! q = Connection.Send msg
                if not q then
                    Logger.WriteLine("AppClient[{0}]: SendMessageToServer failed", LocalPlayerName)
                return q
            with 
            | exc -> 
                Logger.WriteLine("AppClient[{0}]: SendMessageToServer failed ex:{1}", LocalPlayerName, exc.ToString())
                return false
        else
            Logger.WriteLine("AppClient[{0}]: SendMessageToServer GWToServer = None ", LocalPlayerName)
            return false
    }


    member private x.TakeMessage(msg : MsgToClient) = x.TakeMessageSafe msg

    member private x.TakeMessageSafe(msg : MsgToClient) = 
        if not (x.IsClosed || x.IsDisposed) then 
            try MailBox.Post msg finally ()

    member private x.TakeMessageGetReply<'Reply>(builder : AsyncReplyChannel<'Reply> -> MsgToClient, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout 10000
        if not (x.IsClosed || x.IsDisposed) then 
            async{return Option.None}
        else
        try MailBox.PostAndTryAsyncReply(builder, timeout)
        with |_-> async{return Option.None}

    member private x.TakeMessage(msg : MsgGameMasterToRemote) = 
        let msg = MsgToClient.FromGM msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgGameMasterToRemote) = 
        let msg = MsgToClient.FromGM msg
        x.TakeMessageSafe msg

    member private x.TakeMessage(msg : MsgServerToClient) = 
        let msg = MsgToClient.FromServer msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgServerToClient) = 
        let msg = MsgToClient.FromServer msg
        x.TakeMessageSafe msg

    member private x.TakeMessage(msg : MsgUIToX) = 
        let msg = MsgToClient.FromGameUI msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgUIToX) = 
        let msg = MsgToClient.FromGameUI msg
        x.TakeMessageSafe msg



    member val private _IsClosed = false with get,set
    member x.IsClosed = x._IsClosed

    member private x.Close() = 
        if not (x.IsClosed || x.IsDisposed) then
            try
                x.Dispose()
            finally
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

    interface IClient with
        member x.SendMessageToServer(msg) = x.SendMessageToServer(msg)
        member x.FromGameUI = FromGameUI
        member x.FromClientUI = FromClientUI
        member x.FromClientGame = FromClientGame
        member x.FromServer = FromServer
        member x.FromGM = FromGM


