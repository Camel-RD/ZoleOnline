namespace GameLib
open System

type MsgControl =
    |Start |Stop |KillPill 
    |TimeOut
    |Failure of msg : string
    |GetState of AsyncReplyChannel<obj>
    |GetReply of obj * AsyncReplyChannel<obj>

type MsgGameToPlayer =
    |KillPill
    |AskTick |AskSimpleTick 
    |RepliedTick of playerNrWho : int
    |InitGame
    |AskStartNewGame 
    |StartedNewGame of firstPlayerNr : int
    |GameStop 
    |PlayerStopped of playerNrWho : int
    |Clear
    |AddCards of {|cardIds : (list<int>)|}
    |AskBeBig
    |AskedBeBig of playerNr : int
    |AskBury 
    |AskedBury of playerNr : int
    |RepliedBeBig of {|playerNrWho : int; beBig : bool; zole : bool|}
    |Dug of playerNrWho : int
    |GameData of {|gameType : GameType; bigPlayerNr : int; firstPlayerNr : int|}
    |Before3 of firstPlayerNr : int
    |AskMove
    |MillMove of playerNrWho : int
    |GotMove of {|playerNrWho : int; card : int|}
    |After3 of {|card1 : int; card2 : int; card3 : int; winnernr : int; points : int|}
    |AfterZGame of pointsForLittle : int
    |SetPoints of {|pts1 : int; pts2 : int; pts3 : int|}

type MsgPlayerToGame =
    |ReplyTick of playerIdWho : int
    |Failed of {|playerWho : int; msg : string|}
    |StopGameFromUser //from user
    |ReplyStartNewGame of {|playerNrWho : int; yesorno : bool|}
    |ReplyBeBig of {|playerNrWho : int; beBig : bool; zole : bool|}
    |ReplyBury of {|playerNrWho : int; card1 : int; card2 : int|}
    |ReplyMove of {|playerNrWho : int; card : int|}
    |PlayerFailed of {|playerNr : int; msg : string|}
    |PlayerStopped of playerNr : int
    |SendToRemote of {|playerNr : int; msg : MsgGameToPlayer|}

type MsgUIToX =
    |ReplyTick
    |StopGame
    |ReplyStartNewGame of yesorno : bool
    |ReplyBeBig of {|beBig : bool; zole : bool|}
    |ReplyBury of {|card1 : int; card2 : int|}
    |ReplyMove of card : int
    |UIFailed of msg : string


type MsgToPlayer =
    |KillPill
    |GetState of AsyncReplyChannel<obj>
    |FromGame of MsgGameToPlayer
    |FromUser of MsgUIToX
    |FromRemote of MsgPlayerToGame //in game we do MsgUIToX -> MsgPlayerToGame


type MsgRemoteToGameMaster = MsgPlayerToGame

type MsgGameMasterToRemote = 
    |Stop 
    |ToPlayer of MsgGameToPlayer

//Remote = ClientGame
//playerNr is present in MsgPlayerToGame, but we will take it from MsgDataFromRemote
type MsgDataFromRemote(playerNr : int, msg : MsgRemoteToGameMaster) = 
    member val playerNr = playerNr
    member val msg = msg
    member x.IsGood() = playerNr >= 0 && playerNr <= 2
    override x.ToString() = 
        sprintf "MsgDataFromRemote: {playerNr:%A; msg:%A}" playerNr (msg.ToString())

type MsgDataToRemote(playerNr : int, msg : MsgGameMasterToRemote) = 
    member val playerNr = playerNr
    member val msg = msg
    member x.IsGood() = playerNr >= 0 && playerNr <= 2
    override x.ToString() = 
        sprintf "MsgDataToRemote: {playerNr:%A; msg:%A}" playerNr (msg.ToString())

type MsgToGame = 
    |KillPill
    |GetState of AsyncReplyChannel<obj>
    |GameStop 
    |InitGame
    |FromGameUI of MsgUIToX
    |FromPleyer of MsgPlayerToGame
    |FromRemote of MsgDataFromRemote

type MsgToClientGame = 
    |KillPill
    |GetState of AsyncReplyChannel<obj>
    |GameStop 
    |InitGame
    |FromGameUI of MsgUIToX
    |FromPleyer of MsgPlayerToGame
    |FromGM of MsgGameMasterToRemote
    |FromGMToPlayer of MsgGameToPlayer // unwrapped FromGameMaster


type MsgClientToServer = 
    |Connect of greeting : string
    |Disconnect
    |LogIn of {|name : string; psw : string|}
    |LogInAsGuest of name : string
    |GetRegCode of {|name : string; psw : string; email : string|}
    |Register of {|name : string; psw : string; regcode : string|}
    |EnterLobby
    |GetCalendarData
    |GetCalendarTagData of tag : string
    |SetCalendarData of data : string
    |JoinGame
    |JoinPrivateGame of {|name : string; psw : string|}
    |CancelNewGame
    |GameStopped
    |FromClientGame of MsgDataFromRemote

type MsgServerToClient =
    |Connected of greeting : string
    |Disconnect
    |ConnectionRefused of msg : string
    |LogInFailed of msg : string
    |LogInOk
    |GetRegCodeFailed of msg : string
    |GetRegCodeOk
    |RegisterFailed of msg : string
    |RegisterOk
    |LobbyData of data : LobbyData
    |LobbyUpdate of data : LobbyUpdateData
    |CalendarData of string
    |CalendarTagData of string
    |GotNewPlayer of {|name : string; extrainfo : string|}
    |LostNewPlayer of {|name : string; extrainfo : string|}
    |CancelNewGame
    |GotNewGame of {|user1 : string; user2 : string; user3 : string|}
    |FromGM of MsgGameMasterToRemote

type MsgUIToClient = 
    |PlayOffline of name : string
    |Connect of {|ip : string; port : int|}
    |Disconnect
    |AppClosing
    |LogIn of {|name : string; psw : string|}
    |LogInAsGuest of name : string
    |GetRegCode of {|name : string; psw : string; email : string|}
    |Register of {|name : string; psw : string; regcode : string|}
    |EnterLobby
    |GetCalendarData
    |GetCalendarTagData of tag : string
    |SetCalendarData of data : string
    |JoinGame
    |JoinPrivateGame of {|name : string; psw : string|}
    |CancelNewGame

type MsgGameToOwner =
    |GameStopped of msg : string
    |AddPoints of userpoints : GamePoints
    |GameClosed

type MsgToClient =
    |Control of MsgControl
    |InitGame
    |NoConnection
    |SendMessageToServer of MsgClientToServer
    |FromGameUI of MsgUIToX
    |FromGame of MsgGameToOwner 
    |FromClientUI of MsgUIToClient
    |FromClientGame of MsgDataFromRemote
    |FromServer of MsgServerToClient
    |FromGM of MsgGameMasterToRemote


