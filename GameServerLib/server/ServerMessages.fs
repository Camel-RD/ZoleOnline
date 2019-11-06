namespace GameServerLib
open System
open GameLib
open System.Net.Sockets


type MsgServerToUser =
    |Kick of msg : string
    |StartGame
    |InitConnection of IMsgTaker<MsgServerToClient>
    |LeftGame of {|gameid : int|}
    |AddPoints of {|gamesplayed : int; points : int|}

type MsgGameOrganizerToUser =
    |Kick of msg : string
    |UserJoinedNewGame of {|name : string; extrainfo : string|}
    |UserLeftNewGame of {|name : string|}
    |CancelNewGame

type MsgGamePlayingToUser =
    |InitGame of UserGameInitData
    |StartGame 
    |StopGame
    |FromGM of MsgGameMasterToRemote
    |FromRemote of MsgDataFromRemote

type MsgLobbyToUser =
    |LobbyData of data : LobbyData
    |LobbyUpdate of data : LobbyUpdateData

type MsgToUser =
    |Control of msg : MsgControl
    |NotConnected
    |SendToClient of MsgServerToClient
    |FromClient of MsgClientToServer
    |FromServer of MsgServerToUser
    |FromGameOrganizer of MsgGameOrganizerToUser
    |FromGamePlaying of MsgGamePlayingToUser
    |FromGM of MsgGameMasterToRemote
    |FromLobby of MsgLobbyToUser

type MsgUserToServer =
    |GetRegCode of {|userid : int; name : string; psw : string; email : string; ch : (AsyncReplyChannel<NewOrLoginUserReply>)|}
    |Register of {|userid : int; name : string; psw : string; regcode : string; ch : (AsyncReplyChannel<NewOrLoginUserReply>)|}
    |LoginUser of {|userid : int; name : string; psw : string; ch : (AsyncReplyChannel<NewOrLoginUserReply>)|}
    |LoginUserAsGuest of {|userid : int; name : string; ch : (AsyncReplyChannel<NewOrLoginUserReply>)|}
    |UserClosed of {|userid : int|}
    |EnterLobby of userid : int
    |GetCalendarData of {|userid : int; name : string; ch : (AsyncReplyChannel<string>)|}
    |GetCalendarTagData of {|userid : int; name : string; tag : string; ch : (AsyncReplyChannel<string>)|}
    |SetCalendarData of {|userid : int; data : string|}
    |UserStartWaitForNewGame of {|userid : int|}
    |UserStartWaitForPrivateGame of {|userid : int; name : string; psw : string|}
    |UserCancelWaitForNewGame of {|userid : int|}
    |GameStopped of {|userid : int; gameid : int|}

type MsgGameOrganizerToServer =
    |NewGameCancel of {|gameid : int; userid1 : int; userid2 : int|}
    |GotUsersForGame of {|gameid : int; userid1 : int; userid2 : int; userid3 : int|}

type MsgGamePlayingToServer =
    |GameStopped of gameid : int
    |AddGamePoints of GamePoints

type MsgListenerToServer =
    |NewConnection of client : obj
    |Closed

type MsgToServer =
    |Control of msg : MsgControl
    |Start 
    |Stop
    |AddRawUser of {|gw : IMsgTaker<MsgServerToClient>; ch : (AsyncReplyChannel<IUser>)|}
    |FromUser of MsgUserToServer
    |FromGameOrganizer of MsgGameOrganizerToServer
    |FromGamePlaying of MsgGamePlayingToServer
    |FromListener of MsgListenerToServer

type MsgFromServerToGameOrganizer =
    |UserStartWaitForNewGame of user : IUser
    |UserStartWaitForPrivateGame of {|user : IUser; name : string; psw : string|}
    |UserCancelWaitForNewGame of user : IUser
    |UserClosed of userid : int

type MsgToGameOrganizer =
    |Control of msg : MsgControl
    |FromServer of MsgFromServerToGameOrganizer

type MsgToGamePlaying =
    |Control of msg : MsgControl
    |AddPoints of userpoints : GamePoints
    |FromGame of MsgGameToOwner 
    |FromGM of MsgDataToRemote
    |FromRemote of MsgDataFromRemote

type MsgToLobby =
    |Control of msg : MsgControl
    |EnterLobby of user : IUser
    |LeaveLobby of user : IUser
    |EnterServer of user : IUser
    |LeaveServer of user : IUser
    |UpdateUser of user : IUser

module MsgHelper =
    let Get<'a> (input : MsgControl) = 
        match input with
        |GetReply (:? 'a as msgin, channel) -> Some (msgin, channel)
        | _ -> None

    let GetIf<'a> (ftest : 'a -> bool) (input : MsgControl) = 
        match input with
        |GetReply (:? 'a as msgin, channel) when ftest(msgin) -> Some (msgin, channel)
        | _ -> None


    let (|ToServer_Register|_|) (input : MsgToServer) = 
        match input with
        |MsgToServer.Control m-> 
            match Get<MsgUserToServer>(m) with
            |Some(MsgUserToServer.Register msgin, ch) -> Some (msgin, ch)
            |_ -> None
        |_ -> None

    let (|ToServer_LogInUser|_|) (input : MsgToServer) = 
        match input with
        |MsgToServer.Control m-> 
            match Get<MsgUserToServer>(m) with
            |Some(MsgUserToServer.LoginUser msgin, ch) -> Some (msgin, ch)
            |_ -> None
        |_ -> None



