namespace GameServerLib
open System
open System.Collections.Immutable
open GameLib

type IServerConnection =
    abstract member Start : gwtouser : IMsgTaker<MsgClientToServer> -> bool
    abstract member Send : msg : MsgServerToClient -> Async<bool>
    abstract member Close : unit -> unit
    abstract member IsClosed : bool

type IServerToUser =
    abstract member Kill : unit -> unit
    abstract member Kick : msg : string -> unit
    abstract member InitConnection : gw : IMsgTaker<MsgServerToClient> -> unit
    abstract member AddPoints : gamesplayed : int -> points : int -> unit

type IGameOrganizerToUser =
    abstract member Kick : msg : string -> unit
    abstract member UserJoinedGame : name : string -> extrainfo : string -> unit
    abstract member UserLeftNewGame : name : string -> unit
    abstract member CancelNewGame : unit -> unit

type IGamePlayingToUser =
    abstract member InitGame : data : UserGameInitData -> unit
    abstract member StartGame : unit -> unit
    abstract member StopGame : unit -> unit

type ILobbyToUser =
    abstract member LobbyData : data : LobbyData -> unit
    abstract member LobbyUpdate : data : LobbyUpdateData -> unit
    abstract member UpdatePlayer : data : LobbyUpdateData -> unit

type IUser =
    abstract member Id : int
    abstract member Name : string
    abstract member GamesPlayed : int
    abstract member Points : int
    abstract member Kill : unit -> unit
    abstract member GetState : ?timeout : int -> Async<obj option>
    abstract member SetUserData : userid : int * name : string * psw : string -> Unit
    abstract member AddPoints : points : int * gamesplayed : int -> unit
    abstract member SetPoints : points : int * gamesplayed : int -> unit
    abstract member FromServer : IServerToUser
    abstract member FromGameOrganizer : IGameOrganizerToUser
    abstract member FromGamePlaying : IGamePlayingToUser
    abstract member FromClient : IMsgTaker<MsgClientToServer>
    abstract member FromGM : IMsgTaker<MsgGameMasterToRemote>
    abstract member FromLobby : ILobbyToUser
    inherit IComparable
    inherit IDisposable

type IToGameOrganizer =
    abstract member Kill : unit -> unit
    abstract member GetState : ?timeout : int -> Async<obj option>
    abstract member UserStartWaitForNewGame : user : IUser -> unit
    abstract member UserStartWaitForPrivateGame : user : IUser -> name : string -> psw : string -> unit
    abstract member UserCancelWaitForNewGame : user : IUser -> unit
    abstract member UserClosed : userid : int -> unit

type IUserToServer =
    abstract member GetRegCode : userid : int -> name : string -> psw : string -> email : string -> Async<NewOrLoginUserReply option>
    abstract member Register : userid : int -> name : string -> psw : string -> regcode : string -> Async<NewOrLoginUserReply option>
    abstract member LoginUser : userid : int -> name : string -> psw : string -> Async<NewOrLoginUserReply option>
    abstract member LoginUserAsGuest : userid : int -> name : string -> Async<NewOrLoginUserReply option>
    abstract member UserClosed : userid : int -> unit
    abstract member EnterLobby : userid : int -> unit
    abstract member GetCalendarData : userid : int -> name : string -> Async<string option>
    abstract member GetCalendarTagData : userid : int -> name : string -> tag : string -> Async<string option>
    abstract member SetCalendarData : userid : int -> data : string -> unit
    abstract member UserStartWaitForNewGame : userid : int -> unit
    abstract member UserStartWaitForPrivateGame : userid : int -> name : string -> psw : string -> unit
    abstract member UserCancelWaitForNewGame : userid : int -> unit
    abstract member GameStopped : userid : int -> gameid : int -> unit

type IGameOrganizerToServer =
    abstract member NewGameCancel : gameid : int -> userid1 : int -> userid2 : int -> unit
    abstract member GotUsersForGame : gameid : int -> userid1 : int -> userid2 : int -> userid3 : int -> unit

type IGamePlayingToServer =
    abstract member GameStopped : gameid : int -> unit
    abstract member AddGamePoints : gamepoints : GamePoints -> unit

type IToServer =
    abstract member Kill : unit -> unit
    abstract member GetState : ?timeout : int -> Async<obj option>
    abstract member Start : unit -> unit
    abstract member Stop : unit -> unit
    abstract member AddRawUser : gw : IMsgTaker<MsgServerToClient> -> Async<IUser option>
    abstract member AddRawUserR : gw : IMsgTaker<MsgServerToClient> -> IUser
    abstract member FromUser : IUserToServer
    abstract member FromGameOrganizer : IGameOrganizerToServer
    abstract member FromGamePlaying : IGamePlayingToServer

type IToGamePlaying =
    abstract member Kill : unit -> unit
    abstract member GetState : ?timeout : int -> Async<obj option>
    abstract member Stop : unit -> unit


type IToLobby =
    abstract member EnterLobby : user : IUser -> unit
    abstract member LeaveLobby : user : IUser -> unit
    abstract member EnterServer : user : IUser -> unit
    abstract member LeaveServer : user : IUser -> unit
    abstract member UpdateUser : user : IUser -> unit



