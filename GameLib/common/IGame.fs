namespace GameLib
open System
open System.Collections.Immutable

type PlayerType = |NotSet = 0 | Local = 1 | PcAI = 2 | RemoteServer = 3 | RemoteClient = 4 | Aborted = 5
type PlayerGameType = |NotSet = 0 | Little = 1 | Big = 2 | Table = 3 | Aborted = 4

type IGameToPlayer = 
    abstract member ClearAll : unit -> unit
    abstract member ClearBeforeNewGame : unit -> unit
    abstract member AskStartNewGame : unit -> unit
    abstract member StartedNewGame : firstPlayerNr : int -> unit
    abstract member AskBeBig : unit -> unit
    abstract member AskedBeBig : playerNr : int -> unit
    abstract member AskBuryCards : unit -> unit
    abstract member AskedBuryCards : playerNr : int -> unit
    abstract member AskMakeMove : unit -> unit
    abstract member AskTick : unit -> unit
    abstract member AskSimpleTick : unit -> unit
    abstract member PlayerStopped : playernr : int-> unit
    abstract member GameStop : unit -> unit
    abstract member DoAddCards : cards : CardSet -> unit
    abstract member DoAddCardsByIds : cards : ImmutableArray<int> -> unit
    abstract member RepliedBeBig : playernr : int -> bebig : bool -> zole : bool -> unit
    abstract member DoTakeGameData : gameType : GameType -> bigPlayerNr : int -> firstPlayerNr : int -> unit
    abstract member RepliedBury : playernr : int-> unit
    abstract member WillMove : playernr : int -> unit
    abstract member GotMove : playernr : int -> card : Card ->  unit
    abstract member DoAfter3 : card1 : Card -> card2 : Card -> card3 : Card -> winnernr : int -> points : int -> unit
    abstract member DoAfterZGame : points : int -> unit
    abstract member SetPoints : pts1 : int -> pts2 : int -> pts3 : int -> unit

type IXToPlayer = 
    abstract member ReplyStartNewGame : yesorno : bool -> unit
    abstract member ReplyBeBig : bebig : bool -> zole : bool -> unit
    abstract member ReplyBuryCards : card1 : Card -> card2 : Card -> unit
    abstract member ReplyMakeMove : card : Card -> unit
    abstract member ReplyTick : unit -> unit
    abstract member PlayerStop : unit -> unit
    abstract member PlayerStopped : playernr : int -> unit
    abstract member IsValidMove : card : Card -> bool

type XToPlayerEmpty() = 
    static member val Empty = XToPlayerEmpty()
    interface IXToPlayer with
        override x.ReplyStartNewGame(yesorno : bool) = ()
        override x.ReplyBeBig(bebig : bool) (zole : bool) = ()
        override x.ReplyBuryCards(card1 : Card) (card2 : Card) = ()
        override x.ReplyMakeMove(card : Card) = ()
        override x.ReplyTick() = ()
        override x.PlayerStop() = ()
        override x.PlayerStopped (playernr : int) = ()
        override x.IsValidMove(card : Card) = false

type IPlayer =
    inherit IDisposable
    abstract member PlayerType : PlayerType
    abstract member Name : string
    abstract member PlayerNr : int
    abstract member UUID : Guid
    abstract member GetState : unit -> obj
    abstract member Start : unit -> unit
    abstract member TakeMessage : msg : MsgToPlayer -> unit
    abstract member FromGame : IGameToPlayer
    abstract member FromX : IXToPlayer

type PlayerEmpty() =
    static member val Empty = new PlayerEmpty()
    interface IPlayer with
        override x.PlayerType = PlayerType.NotSet
        override x.Name : string = ""
        override x.PlayerNr : int = -1
        override x.UUID : Guid = Guid.Empty
        override x.GetState () : obj = obj()
        override x.Start() = ()
        override x.TakeMessage (msg : MsgToPlayer) = ()
        override x.FromGame = x :> IGameToPlayer
        override x.FromX = x :> IXToPlayer
    interface IGameToPlayer with
        override x.ClearAll() = ()
        override x.ClearBeforeNewGame() = ()
        override x.AskStartNewGame() = ()
        override x.StartedNewGame (firstPlayerNr : int) = ()
        override x.AskBeBig() = ()
        override x.AskedBeBig playerNr = ()
        override x.AskBuryCards() = ()
        override x.AskedBuryCards playerNr = ()
        override x.AskMakeMove() = ()
        override x.AskTick() = ()
        override x.AskSimpleTick() = ()
        override x.PlayerStopped (playernr : int) = ()
        override x.GameStop() = ()
        override x.DoAddCards(cards : CardSet) = ()
        override x.DoAddCardsByIds(cards : ImmutableArray<int>) = ()
        override x.RepliedBeBig (playernr : int) (bebig : bool) (zole : bool) = ()
        override x.DoTakeGameData (gameType : GameType) (bigPlayerNr : int) (firstPlayerNr : int) = ()
        override x.RepliedBury (playernr : int) = ()
        override x.WillMove (playernr : int) = () 
        override x.GotMove (playernr : int) (card : Card) = ()
        override x.DoAfter3 (card1 : Card) (card2 : Card) (card3 : Card) (winnernr : int) (points : int)= ()
        override x.DoAfterZGame (points : int)= ()
        override x.SetPoints (pts1 : int) (pts2 : int) (pts3 : int) = ()
    interface IXToPlayer with
        override x.ReplyStartNewGame(yesorno : bool) = ()
        override x.ReplyBeBig(bebig : bool) (zole : bool) = ()
        override x.ReplyBuryCards(card1 : Card) (card2 : Card) = ()
        override x.ReplyMakeMove(card : Card) = ()
        override x.ReplyTick() = ()
        override x.PlayerStop() = ()
        override x.PlayerStopped (playernr : int) = ()
        override x.IsValidMove(card : Card) = false
    interface IDisposable with
        override x.Dispose() = ()

type IGameMasterToRemote =
    abstract member Stop : unit -> unit
    inherit IGameToPlayer

type IRemoteToGameMaster =
    inherit IXToPlayer

type IUIToClient =
    abstract member PlayOffline : name : string -> unit
    abstract member Connect : ip : string -> port : int -> unit
    abstract member Disconnect : unit -> unit
    abstract member AppClosing : unit -> unit
    abstract member LogIn : name : string -> psw : string -> unit
    abstract member LogInAsGuest : name : string -> unit
    abstract member GetRegCode : name : string -> psw : string -> email : string -> unit
    abstract member Register : name : string -> psw : string -> regcode : string -> unit
    abstract member EnterLobby : unit -> unit
    abstract member GetCalendarData : unit -> unit
    abstract member GetCalendarTagData : tag : string -> unit
    abstract member SetCalendarData : data : string -> unit
    abstract member JoinGame : unit -> unit
    abstract member JoinPrivateGame : name : string -> psw : string -> unit
    abstract member CancelNewGame : unit -> unit

type IClientToServer =
    abstract member Connect : greeting : string -> unit
    abstract member Disconnect : unit -> unit
    abstract member LogIn : name : string -> psw : string -> unit
    abstract member LogInAsGuest : name : string -> unit
    abstract member GetRegCode : name : string -> psw : string -> email : string -> unit
    abstract member Register : name : string -> psw : string -> regcode : string -> unit
    abstract member EnterLobby : unit -> unit
    abstract member GetCalendarData : unit -> unit
    abstract member GetCalendarTagData : tag : string -> unit
    abstract member SetCalendarData : data : string -> unit
    abstract member JoinGame : unit -> unit
    abstract member JoinPrivateGame : name : string -> psw : string -> unit
    abstract member CancelNewGame : unit -> unit
    abstract member GameStopped : unit -> unit 

type IClientToServerAsync =
    abstract member Connect : greeting : string -> Async<bool>
    abstract member Disconnect : unit -> Async<bool>
    abstract member LogIn : name : string -> psw : string -> Async<bool>
    abstract member LogInAsGuest : name : string -> Async<bool>
    abstract member GetRegCode : name : string -> psw : string -> email : string -> Async<bool>
    abstract member Register : name : string -> psw : string -> regcode : string -> Async<bool>
    abstract member EnterLobby : unit -> Async<bool>
    abstract member GetCalendarData : unit -> Async<bool>
    abstract member GetCalendarTagData : tag : string -> Async<bool>
    abstract member SetCalendarData : data : string -> Async<bool>
    abstract member JoinGame : unit -> Async<bool>
    abstract member JoinPrivateGame : name : string -> psw : string -> Async<bool>
    abstract member CancelNewGame : unit -> Async<bool>
    abstract member GameStopped : unit -> Async<bool> 

type IServerToClient =
    abstract member Connected : greeting : string -> unit
    abstract member Disconnect : unit -> unit
    abstract member ConnectionRefused : msg : string -> unit
    abstract member LogInFailed : msg : string -> unit
    abstract member LogInOK : unit -> unit
    abstract member GetRegCodeFailed : msg : string -> unit
    abstract member GetRegCodeOk : unit -> unit
    abstract member RegisterFailed : msg : string -> unit
    abstract member RegisterOk : unit -> unit
    abstract member LobbyData : data : LobbyData -> unit
    abstract member LobbyUpdate : data : LobbyUpdateData -> unit
    abstract member CalendarData : data : string -> unit
    abstract member CalendarTagData : data : string -> unit
    abstract member CancelNewGame : unit -> unit
    abstract member GotNewPlayer : name : string -> extrainfo : string -> unit
    abstract member LostNewPlayer : name : string -> extrainfo : string -> unit
    abstract member GotNewGame : user1 : string -> user2 : string -> user3 : string -> unit

type IServerToClientAsync =
    abstract member Connected : greeting : string -> Async<bool>
    abstract member Disconnect : unit -> Async<bool>
    abstract member ConnectionRefused : msg : string -> Async<bool>
    abstract member LogInFailed : msg : string -> Async<bool>
    abstract member LogInOK : unit -> Async<bool>
    abstract member GetRegCodeFailed : msg : string -> Async<bool>
    abstract member GetRegCodeOk : unit -> Async<bool>
    abstract member RegisterFailed : msg : string -> Async<bool>
    abstract member RegisterOk : unit -> Async<bool>
    abstract member LobbyData : data : LobbyData -> Async<bool>
    abstract member LobbyUpdate : data : LobbyUpdateData -> Async<bool>
    abstract member CalendarData : data : string -> Async<bool>
    abstract member CalendarTagData : data : string -> Async<bool>
    abstract member CancelNewGame : unit -> Async<bool>
    abstract member GotNewPlayer : name : string -> extrainfo : string -> Async<bool>
    abstract member LostNewPlayer : name : string -> extrainfo : string -> Async<bool>
    abstract member GotNewGame : user1 : string -> user2 : string -> user3 : string -> Async<bool>

type IPlayerToGame =
    abstract member ReplyStartNewGame : playernr : int -> yesorno : bool -> unit
    abstract member ReplyBeBig : playernr : int -> bebig : bool -> zole : bool -> unit
    abstract member ReplyBuryCards : playernr : int -> card1 : Card -> card2 : Card -> unit
    abstract member ReplyMakeMove : playernr : int -> card : Card -> unit
    abstract member ReplyTick : playernr : int -> unit
    abstract member PlayerStopped : playernr : int -> unit
    abstract member PlayerFailed : playernr : int -> msg : string -> unit
    abstract member SendMessage : playernr : int -> msg : MsgGameToPlayer -> unit

       

type IUserToX =
    abstract member ReplyStartNewGame : yesorno : bool -> unit
    abstract member ReplyBeBig : bebig : bool -> zole : bool -> unit
    abstract member ReplyBuryCards : card1 : Card -> card2 : Card -> unit
    abstract member ReplyMakeMove : card : Card -> unit
    abstract member ReplyTick : unit -> unit
    abstract member StopGame : unit -> unit
    abstract member UIFailed : msg : string -> unit
    abstract member IsValidMove : card : Card -> bool

type private UserToXEmpty() =
    static member val Empty = UserToXEmpty()
    interface IUserToX with
        member x.ReplyStartNewGame(yesorno : bool) = ()
        member x.ReplyBeBig(bebig : bool) (zole : bool) = ()
        member x.ReplyBuryCards(card1 : Card) (card2 : Card) = ()
        member x.ReplyMakeMove(card : Card) = ()
        member x.ReplyTick() = ()
        member x.StopGame() = ()
        member x.IsValidMove(card : Card) = false
        member x.UIFailed(msg) = ()


type IPlayerToUI =
    abstract member AskStartGame : unit -> unit
    abstract member DoStartGame : unit -> unit
    abstract member AskBeBig : unit -> unit
    abstract member AskBuryCards : unit -> unit
    abstract member AskMakeMove : unit -> unit
    abstract member AskTick : unit -> unit
    abstract member AskSimpleTick : unit -> unit
    abstract member ShowText : s : string -> unit

    abstract member ShowCards : cards : CardSet -> 
                                cardsondesk : CardSet -> 
                                firstplayernr : int ->
                                localplayernr : int -> unit

    abstract member ShowCards2 : cards : ImmutableArray<CardSet> -> 
                                    cardsondesk : CardSet -> 
                                    firstplayernr : int -> 
                                    localplayernr : int -> unit

    abstract member ShowPoints : points : int -> unit

    abstract member ShowNames : s1 : string -> s2 : string -> s3 : string -> 
                                highlight : int -> localplayernr : int ->  unit

    abstract member HideThings : unit -> unit    
    abstract member AddRowToStats : v1 : int -> v2 : int -> v3 : int -> localplayernr : int ->  unit
    abstract member ShowStats : b : bool -> unit

type IClientToUI = 
    abstract member Wait: msg : string -> unit    
    abstract member DoStartUp : unit -> unit    
    abstract member ConnectionFailed : msg : string -> unit    
    abstract member ShowMessage2 : msg : string -> unit    
    abstract member GoToLoginPage : unit -> unit    
    abstract member GoToRegisterPage : unit -> unit    
    abstract member GoToLobby : unit -> unit    
    abstract member SetLobbyData : data : LobbyData -> unit    
    abstract member AddLobbyData : data : LobbyPlayerInfo -> unit    
    abstract member RemoveLobbyData : name : string -> unit    
    abstract member UpdateLobbyData : data : LobbyPlayerInfo -> unit    
    abstract member CalendarData : data : string -> unit    
    abstract member CalendarTagData : data : string -> unit    
    abstract member GotPlayerForNewGame : name : string -> info : string -> unit    
    abstract member LostPlayerForNewGame : name : string -> unit    
    abstract member GoToNewGame : unit -> unit    
    abstract member CancelNewGame : msg : string -> unit    

type IGameForm =
    inherit IPlayerToUI
    inherit IClientToUI
    abstract member ShowMessage : msg : string -> unit
    abstract member SetMyPlayerNr : nr : int -> unit
    abstract member IsClosing : unit -> bool
    abstract member SetNames : plnm1 : string -> plnm2 : string -> plnm3 : string -> localplayernr : int -> unit


type GameFormEmpty() =
    static member val Empty = new GameFormEmpty()
    interface IGameForm with
        member this.ShowMessage(msg) = ()

        member this.AskBeBig() = ()
        member this.AskBuryCards() = ()
        member this.AskMakeMove() = ()
        member this.AskSimpleTick() = ()
        member this.AskStartGame() = ()
        member this.AskTick() = ()
        member this.DoStartGame() = ()
        member this.HideThings() = ()
        member this.IsClosing() = false
        member this.SetMyPlayerNr nr = ()
        member this.SetNames plnm1 plnm2 plnm3 localplayernr = ()
        member this.ShowCards cards cardsondesk firstplayernr localplayernr = ()
        member this.ShowCards2 cards cardsondesk firstplayernr localplayernr = ()
        member this.ShowNames s1 s2 s3 highlight localplayernr = ()
        member this.ShowPoints points = ()
        member this.ShowStats b = ()
        member this.ShowText s = ()
        member this.AddRowToStats v1 v2 v3 localplayernr = ()

        member this.Wait(msg) = ()
        member this.DoStartUp() = ()
        member this.ConnectionFailed(msg) = ()
        member this.GoToLoginPage() = ()
        member this.GoToRegisterPage() = ()
        member this.ShowMessage2(msg) = ()
        member this.GoToLobby() = ()
        member this.SetLobbyData(data) = ()
        member this.AddLobbyData(data) = ()
        member this.RemoveLobbyData(data) = ()
        member this.UpdateLobbyData(data) = ()
        member this.CalendarData data = ()
        member this.CalendarTagData data = ()
        member this.GoToNewGame() = ()
        member this.GotPlayerForNewGame name info = ()
        member this.LostPlayerForNewGame(name) = ()
        member this.CancelNewGame(msg) = ()

type IGameSolver =
    abstract member WantBeBig : cards : CardSet -> bool
    abstract member CardsToBury : cards : CardSet -> (Card * Card)
    abstract member FindMove : cards : CardSet -> cardsOnDesk : CardSet -> 
        goneCards : CardSet -> isTableGaem : bool -> isBig : bool -> 
        isafterbig : bool -> Card


type IGameToOwner =
    abstract member GameStopped : msg : string -> unit
    abstract member AddPoints : userpoints : GamePoints -> unit
    abstract member GameClosed : unit -> unit

type IClientConnection =
    abstract member Connect :ip : string -> port : int -> Async<bool>
    abstract member Send : msg : MsgClientToServer -> Async<bool>
    abstract member Close : unit -> unit
    abstract member IsClosed : bool

type IClient =
    inherit IDisposable
    abstract member SendMessageToServer : msg : MsgClientToServer -> Async<bool>
    abstract member FromGameUI : IUserToX
    abstract member FromClientUI : IUIToClient
    abstract member FromClientGame : IMsgTaker<MsgDataFromRemote>
    abstract member FromServer : IMsgTaker<MsgServerToClient>
    abstract member FromGM : IMsgTaker<MsgGameMasterToRemote>


