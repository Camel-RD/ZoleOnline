namespace GameServerLib
open System
open System.Collections.Immutable
open GameLib


type MPHelper() =
    static member val MyTimeOut = -1
    static member GetReply<'Tmsg, 'Treply> (msgtaker : IMsgTakerX<'Tmsg>, msg : 'Tmsg, ?timeout : int) : Async<Option<'Treply>> = 
        let timeout = defaultArg timeout MPHelper.MyTimeOut
        msgtaker.TakeMessageGetReplyX (msg, timeout)


type MPToUser(msgtaker : IMsgTakerX<MsgToUser>) =

    let ftakemessage_fromserver msg = MsgToUser.FromServer msg |> msgtaker.TakeMessage
    let ftakemessage_fromgameorganizer msg = MsgToUser.FromGameOrganizer msg |> msgtaker.TakeMessage
    
    member this.GetState ?timeout = 
        let timeout = defaultArg timeout MPHelper.MyTimeOut
        let fmsg ch = MsgToUser.Control (MsgControl.GetState ch)
        msgtaker.TakeMessageGetReply(fmsg, timeout)
    
    interface IServerToUser with
        member this.Kill() = 
            MsgToUser.Control MsgControl.KillPill |> msgtaker.TakeMessage

        member this.Kick(msg) = 
            MsgServerToUser.Kick msg |> ftakemessage_fromserver
        
        member this.InitConnection(gw) = 
            MsgServerToUser.InitConnection gw |> ftakemessage_fromserver

        member this.AddPoints gamesplayed points = 
            MsgServerToUser.AddPoints {|gamesplayed = gamesplayed; points = points|} |> ftakemessage_fromserver

    interface IGameOrganizerToUser with

        member this.Kick(msg) = 
            MsgGameOrganizerToUser.Kick msg |> ftakemessage_fromgameorganizer

        member this.UserJoinedGame name extrainfo = 
            MsgGameOrganizerToUser.UserJoinedNewGame {|name = name; extrainfo = extrainfo|} |> ftakemessage_fromgameorganizer

        member this.UserLeftNewGame(name) = 
            MsgGameOrganizerToUser.UserLeftNewGame {|name = name|} |> ftakemessage_fromgameorganizer

        member this.CancelNewGame() = 
            MsgGameOrganizerToUser.CancelNewGame |> ftakemessage_fromgameorganizer

    interface IGamePlayingToUser with

        member this.InitGame data = 
            MsgToUser.FromGamePlaying (MsgGamePlayingToUser.InitGame data) |> msgtaker.TakeMessage

        member this.StartGame() = 
            MsgToUser.FromGamePlaying MsgGamePlayingToUser.StartGame |> msgtaker.TakeMessage

        member this.StopGame() = 
            MsgToUser.FromGamePlaying MsgGamePlayingToUser.StopGame |> msgtaker.TakeMessage

    interface ILobbyToUser with

        member this.LobbyData(data) = 
            MsgToUser.FromLobby (MsgLobbyToUser.LobbyData data) |> msgtaker.TakeMessage

        member this.LobbyUpdate(data) = 
            MsgToUser.FromLobby (MsgLobbyToUser.LobbyUpdate data) |> msgtaker.TakeMessage

        member this.UpdatePlayer(data) = 
            MsgToUser.FromLobby (MsgLobbyToUser.LobbyUpdate data) |> msgtaker.TakeMessage


type MPToServer(msgtaker : IMsgTakerX<MsgToServer>) =
    
    let ftakemessage_fromuser msg = MsgToServer.FromUser msg |> msgtaker.TakeMessage

    member this.Kill() = 
        MsgToServer.Control MsgControl.KillPill |> msgtaker.TakeMessage
    
    member this.GetState ?timeout = 
        let timeout = defaultArg timeout MPHelper.MyTimeOut
        let fmsg ch = MsgToServer.Control (MsgControl.GetState ch)
        msgtaker.TakeMessageGetReply(fmsg, timeout)

    member this.Start() = 
        MsgToServer.Start |> msgtaker.TakeMessage

    member this.Stop() = 
        MsgToServer.Stop |> msgtaker.TakeMessage
    
    member this.AddRawUser(gw : IMsgTaker<MsgServerToClient>) = 
        let fmsg ch = MsgToServer.AddRawUser {|gw = gw; ch = ch|}
        msgtaker.TakeMessageGetReply(fmsg, MPHelper.MyTimeOut)

    member this.AddRawUserR(gw : IMsgTaker<MsgServerToClient>) = 
        let fmsg ch = MsgToServer.AddRawUser {|gw = gw; ch = ch|}
        let ret = msgtaker.TakeMessageGetReply(fmsg, MPHelper.MyTimeOut)
        let retv = Async.RunSynchronously(ret)
        retv.Value
        
    interface IUserToServer with
        
        member this.GetRegCode userid name psw email = 
            let fmsg ch = MsgToServer.FromUser (MsgUserToServer.GetRegCode {|userid = userid; name = name; psw = psw; email = email; ch = ch|})
            msgtaker.TakeMessageGetReply(fmsg, MPHelper.MyTimeOut)

        member this.Register userid name psw regcode = 
            let fmsg ch = MsgToServer.FromUser (MsgUserToServer.Register {|userid = userid; name = name; psw = psw; regcode = regcode; ch = ch|})
            msgtaker.TakeMessageGetReply(fmsg, MPHelper.MyTimeOut)

        member this.LoginUser userid name psw =
            let fmsg ch = MsgToServer.FromUser (LoginUser {|userid = userid; name = name; psw = psw; ch = ch|}) 
            msgtaker.TakeMessageGetReply(fmsg, MPHelper.MyTimeOut)

        member this.LoginUserAsGuest userid name =
            let fmsg ch = MsgToServer.FromUser (LoginUserAsGuest {|userid = userid; name = name; ch = ch|}) 
            msgtaker.TakeMessageGetReply(fmsg, MPHelper.MyTimeOut)

        member this.EnterLobby userid = 
            MsgUserToServer.EnterLobby userid |> ftakemessage_fromuser

        member this.GetCalendarData userid name =
            let fmsg ch = MsgToServer.FromUser (MsgUserToServer.GetCalendarData {|userid = userid; name = name; ch = ch|}) 
            msgtaker.TakeMessageGetReply(fmsg, MPHelper.MyTimeOut)

        member this.GetCalendarTagData userid name tag =
            let fmsg ch = MsgToServer.FromUser (MsgUserToServer.GetCalendarTagData {|userid = userid; name = name; tag = tag; ch = ch|}) 
            msgtaker.TakeMessageGetReply(fmsg, MPHelper.MyTimeOut)

        member this.SetCalendarData userid data =
            MsgUserToServer.SetCalendarData {|userid = userid; data = data|} |> ftakemessage_fromuser

        member this.UserStartWaitForNewGame(userid) =
            MsgUserToServer.UserStartWaitForNewGame {|userid = userid|} |> ftakemessage_fromuser

        member this.UserStartWaitForPrivateGame userid name psw =
            MsgToServer.FromUser (MsgUserToServer.UserStartWaitForPrivateGame 
                    {|userid = userid; name = name; psw = psw|}) 
            |> msgtaker.TakeMessage

        member this.UserCancelWaitForNewGame(userid) = 
            MsgUserToServer.UserCancelWaitForNewGame {|userid = userid|} |> ftakemessage_fromuser

        member this.UserClosed(userid) = 
            MsgUserToServer.UserClosed {|userid = userid|} |> ftakemessage_fromuser

        member this.GameStopped userid gameid = 
            MsgUserToServer.GameStopped {|userid = userid; gameid = gameid|} |> ftakemessage_fromuser

    interface IGameOrganizerToServer with

        member this.NewGameCancel gameid userid1 userid2 = 
            MsgToServer.FromGameOrganizer (MsgGameOrganizerToServer.NewGameCancel {|gameid = gameid; userid1 = userid1; userid2 = userid2|}) |> msgtaker.TakeMessage

        member this.GotUsersForGame gameid userid1 userid2 userid3 = 
            MsgToServer.FromGameOrganizer (MsgGameOrganizerToServer.GotUsersForGame 
                {|gameid = gameid; userid1 = userid1; userid2 = userid2; userid3 = userid3|}) |> msgtaker.TakeMessage

    interface IGamePlayingToServer with

        member this.AddGamePoints(gamepoints) = 
            MsgToServer.FromGamePlaying (MsgGamePlayingToServer.AddGamePoints gamepoints) |> msgtaker.TakeMessage
    
        member this.GameStopped(gameid) = 
            MsgToServer.FromGamePlaying (MsgGamePlayingToServer.GameStopped gameid) |> msgtaker.TakeMessage



type MPToGameOrganizer(msgtaker : IMsgTakerX<MsgToGameOrganizer>) =
    interface IToGameOrganizer with
        member this.Kill() = 
            MsgToGameOrganizer.Control MsgControl.KillPill |> msgtaker.TakeMessage

        member this.GetState ?timeout = 
            let timeout = defaultArg timeout MPHelper.MyTimeOut
            let fmsg ch = MsgToGameOrganizer.Control (MsgControl.GetState ch)
            msgtaker.TakeMessageGetReply(fmsg, timeout)

        member this.UserStartWaitForNewGame(user) = 
            MsgToGameOrganizer.FromServer (MsgFromServerToGameOrganizer.UserStartWaitForNewGame  user) |> msgtaker.TakeMessage

        member this.UserStartWaitForPrivateGame user name psw =
            MsgToGameOrganizer.FromServer (MsgFromServerToGameOrganizer.UserStartWaitForPrivateGame 
                    {|user = user; name = name; psw = psw|}) 
            |> msgtaker.TakeMessage

        member this.UserCancelWaitForNewGame(user) = 
            MsgToGameOrganizer.FromServer (MsgFromServerToGameOrganizer.UserCancelWaitForNewGame user) |> msgtaker.TakeMessage

        member this.UserClosed(userid) = 
            MsgToGameOrganizer.FromServer (MsgFromServerToGameOrganizer.UserClosed userid) |> msgtaker.TakeMessage


type MPToGamePlaying(msgtaker : IMsgTakerX<MsgToGamePlaying>) =
    interface IToGamePlaying with
        member this.Kill() = 
            MsgToGamePlaying.Control MsgControl.KillPill |> msgtaker.TakeMessage

        member this.GetState ?timeout = 
            let timeout = defaultArg timeout MPHelper.MyTimeOut
            let fmsg ch = MsgToGamePlaying.Control (MsgControl.GetState ch)
            msgtaker.TakeMessageGetReply(fmsg, timeout)

        member this.Stop() = 
            MsgToGamePlaying.Control MsgControl.Stop |> msgtaker.TakeMessage


type private MPServerToClient (msgtaker : IMsgTaker<MsgServerToClient>) =
    interface IServerToClient with
        member x.Connected(greeting) = MsgServerToClient.Connected greeting |> msgtaker.TakeMessage
        member x.Disconnect() = MsgServerToClient.Disconnect |> msgtaker.TakeMessage
        member x.ConnectionRefused(msg) = MsgServerToClient.ConnectionRefused msg|> msgtaker.TakeMessage
        member x.LogInFailed(msg) = MsgServerToClient.LogInFailed msg |> msgtaker.TakeMessage
        member x.LogInOK() = MsgServerToClient.LogInOk |> msgtaker.TakeMessage
        member x.GetRegCodeFailed(msg) = MsgServerToClient.GetRegCodeFailed msg |> msgtaker.TakeMessage
        member x.GetRegCodeOk() = MsgServerToClient.GetRegCodeOk |> msgtaker.TakeMessage
        member x.RegisterFailed(msg) = MsgServerToClient.RegisterFailed msg |> msgtaker.TakeMessage
        member x.RegisterOk() = MsgServerToClient.RegisterOk |> msgtaker.TakeMessage
        member x.LobbyData(data) = MsgServerToClient.LobbyData data |> msgtaker.TakeMessage
        member x.LobbyUpdate(data) = MsgServerToClient.LobbyUpdate data |> msgtaker.TakeMessage
        member x.CalendarData(data) = MsgServerToClient.CalendarData data |> msgtaker.TakeMessage
        member x.CalendarTagData(data) = MsgServerToClient.CalendarTagData data |> msgtaker.TakeMessage
        member x.CancelNewGame() = MsgServerToClient.CancelNewGame |> msgtaker.TakeMessage
        member x.GotNewPlayer name extrainfo = MsgServerToClient.GotNewPlayer {|name = name; extrainfo = extrainfo|} |> msgtaker.TakeMessage
        member x.LostNewPlayer name extrainfo =  MsgServerToClient.LostNewPlayer {|name = name; extrainfo = extrainfo|} |> msgtaker.TakeMessage
        member x.GotNewGame user1 user2 user3 = MsgServerToClient.GotNewGame {|user1 = user1; user2 = user2; user3 = user3|} |> msgtaker.TakeMessage

type private MPServerToClientAsync (msgtaker : IMsgTakerAsync<MsgServerToClient>) =
    interface IServerToClientAsync with
        member x.Connected(greeting) = MsgServerToClient.Connected greeting |> msgtaker.TakeMessage
        member x.Disconnect() = MsgServerToClient.Disconnect |> msgtaker.TakeMessage
        member x.ConnectionRefused(msg) = MsgServerToClient.ConnectionRefused msg|> msgtaker.TakeMessage
        member x.LogInFailed(msg) = MsgServerToClient.LogInFailed msg |> msgtaker.TakeMessage
        member x.LogInOK() = MsgServerToClient.LogInOk |> msgtaker.TakeMessage
        member x.GetRegCodeFailed(msg) = MsgServerToClient.GetRegCodeFailed msg |> msgtaker.TakeMessage
        member x.GetRegCodeOk() = MsgServerToClient.GetRegCodeOk |> msgtaker.TakeMessage
        member x.RegisterFailed(msg) = MsgServerToClient.RegisterFailed msg |> msgtaker.TakeMessage
        member x.RegisterOk() = MsgServerToClient.RegisterOk |> msgtaker.TakeMessage
        member x.LobbyData(data) = MsgServerToClient.LobbyData data |> msgtaker.TakeMessage
        member x.LobbyUpdate(data) = MsgServerToClient.LobbyUpdate data |> msgtaker.TakeMessage
        member x.CalendarData(data) = MsgServerToClient.CalendarData data |> msgtaker.TakeMessage
        member x.CalendarTagData(data) = MsgServerToClient.CalendarTagData data |> msgtaker.TakeMessage
        member x.CancelNewGame() = MsgServerToClient.CancelNewGame |> msgtaker.TakeMessage
        member x.GotNewPlayer name extrainfo = MsgServerToClient.GotNewPlayer {|name = name; extrainfo = extrainfo|} |> msgtaker.TakeMessage
        member x.LostNewPlayer name extrainfo =  MsgServerToClient.LostNewPlayer {|name = name; extrainfo = extrainfo|} |> msgtaker.TakeMessage
        member x.GotNewGame user1 user2 user3 = MsgServerToClient.GotNewGame {|user1 = user1; user2 = user2; user3 = user3|} |> msgtaker.TakeMessage

type private MPToLobby (msgtaker : IMsgTakerX<MsgToLobby>) =
    interface IToLobby with
        member x.EnterLobby data = MsgToLobby.EnterLobby data |> msgtaker.TakeMessage
        member x.LeaveLobby data = MsgToLobby.LeaveLobby data |> msgtaker.TakeMessage
        member x.EnterServer data = MsgToLobby.EnterServer data |> msgtaker.TakeMessage
        member x.LeaveServer data = MsgToLobby.LeaveServer data |> msgtaker.TakeMessage
        member x.UpdateUser data = MsgToLobby.UpdateUser data |> msgtaker.TakeMessage



