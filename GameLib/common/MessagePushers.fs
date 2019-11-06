namespace GameLib
open System
open System.Collections.Immutable

type private MPGameToPlayer(ftakemsg : MsgGameToPlayer -> unit) =
    interface IGameToPlayer with
        override x.ClearAll() = MsgGameToPlayer.Clear |> ftakemsg
        override x.ClearBeforeNewGame() = MsgGameToPlayer.Clear |> ftakemsg
        override x.AskStartNewGame() = MsgGameToPlayer.AskStartNewGame |> ftakemsg
        override x.StartedNewGame firstPlayerNr = MsgGameToPlayer.StartedNewGame firstPlayerNr |> ftakemsg
        override x.AskBeBig() = MsgGameToPlayer.AskBeBig |> ftakemsg
        override x.AskedBeBig playerNr = MsgGameToPlayer.AskedBeBig playerNr |> ftakemsg
        override x.AskBuryCards() = MsgGameToPlayer.AskBury |> ftakemsg
        override x.AskedBuryCards playerNr = MsgGameToPlayer.AskedBury playerNr |> ftakemsg
        override x.AskMakeMove() = MsgGameToPlayer.AskMove |> ftakemsg
        override x.GameStop() = MsgGameToPlayer.GameStop |> ftakemsg
        override x.AskTick() = MsgGameToPlayer.AskTick |> ftakemsg
        override x.AskSimpleTick() = MsgGameToPlayer.AskSimpleTick |> ftakemsg
        override x.PlayerStopped playernr = MsgGameToPlayer.PlayerStopped playernr |> ftakemsg

        override x.DoAddCards(cards : CardSet) =
            let idslist = 
                cards.Cards
                |> Seq.map (fun c -> c.IndexInFullDeck)
                |> Seq.toList
            MsgGameToPlayer.AddCards {|cardIds = idslist|} 
            |> ftakemsg

        override x.DoAddCardsByIds(cards : ImmutableArray<int>) =
            let idslist = Seq.toList cards
            MsgGameToPlayer.AddCards {|cardIds = idslist|}
            |> ftakemsg

        override x.RepliedBeBig playernr bebig zole = 
            MsgGameToPlayer.RepliedBeBig
                {|playerNrWho = playernr; 
                beBig = bebig;
                zole = zole|}
            |> ftakemsg

        override x.DoTakeGameData gameType bigPlayerNr firstPlayerNr = 
            MsgGameToPlayer.GameData
                {|gameType = gameType; 
                bigPlayerNr = bigPlayerNr;
                firstPlayerNr = firstPlayerNr|}
            |> ftakemsg

        override x.RepliedBury playernr = MsgGameToPlayer.Dug playernr |> ftakemsg
        override x.WillMove playernr = MsgGameToPlayer.MillMove playernr |> ftakemsg
        override x.GotMove playernr card = 
            MsgGameToPlayer.GotMove
                {|playerNrWho = playernr; 
                card = card.IndexInFullDeck|}
            |> ftakemsg

        override x.DoAfter3 card1 card2 card3 winnernr points =
            MsgGameToPlayer.After3
                {|card1 = card1.IndexInFullDeck;
                card2 = card2.IndexInFullDeck;
                card3 = card3.IndexInFullDeck;
                winnernr = winnernr;
                points = points|}
            |> ftakemsg

        override x.DoAfterZGame points = MsgGameToPlayer.AfterZGame points |> ftakemsg

        override x.SetPoints pts1 pts2 pts3 = 
            MsgGameToPlayer.SetPoints {|pts1 = pts1; pts2 = pts2; pts3 = pts3|} 
            |> ftakemsg


type private MPPlayerToGameA(ftarget : MsgPlayerToGame -> unit) =

    interface IPlayerToGame with
        override x.ReplyStartNewGame playernr yesorno = 
            MsgPlayerToGame.ReplyStartNewGame {|playerNrWho = playernr; yesorno = yesorno|} 
            |> ftarget

        override x.ReplyBeBig playernr bebig zole = 
            MsgPlayerToGame.ReplyBeBig
                {|playerNrWho = playernr; 
                beBig = bebig;
                zole = zole|}
            |> ftarget

        override x.ReplyBuryCards playernr card1 card2 = 
            MsgPlayerToGame.ReplyBury
                {|playerNrWho = playernr; 
                card1 = card1.IndexInFullDeck;
                card2 = card2.IndexInFullDeck|}
            |> ftarget
    
        override x.ReplyMakeMove playernr card = 
            MsgPlayerToGame.ReplyMove
                {|playerNrWho = playernr; 
                card = card.IndexInFullDeck|}
            |> ftarget

        override x.PlayerFailed playernr msg = 
            MsgPlayerToGame.Failed({|playerWho = playernr; msg = msg|}) |> ftarget

        override x.ReplyTick(playernr) = 
            MsgPlayerToGame.ReplyTick playernr |> ftarget
        
        override x.PlayerStopped playernr = 
            MsgPlayerToGame.PlayerStopped playernr |> ftarget

        member x.SendMessage playernr msg = 
            MsgPlayerToGame.SendToRemote({|playerNr = playernr; msg = msg|}) |> ftarget


type IMPUIToGameTarget =
    abstract member PlayerNr : int
    abstract member IsValidMove : Card -> bool
    abstract member TakeMessage : MsgPlayerToGame -> unit

type private MPPlayerToGame(target : IMPUIToGameTarget) =
    interface IXToPlayer with
        override x.ReplyStartNewGame(yesorno : bool) = 
            MsgPlayerToGame.ReplyStartNewGame {|playerNrWho = target.PlayerNr; yesorno = yesorno|} 
            |> target.TakeMessage

        override x.ReplyBeBig bebig zole = 
            MsgPlayerToGame.ReplyBeBig
                {|playerNrWho = target.PlayerNr; 
                beBig = bebig;
                zole = zole|}
            |> target.TakeMessage

        override x.ReplyBuryCards card1 card2 = 
            MsgPlayerToGame.ReplyBury
                {|playerNrWho = target.PlayerNr; 
                card1 = card1.IndexInFullDeck;
                card2 = card2.IndexInFullDeck|}
            |> target.TakeMessage
    
        override x.ReplyMakeMove card = 
            MsgPlayerToGame.ReplyMove
                {|playerNrWho = target.PlayerNr; 
                card = card.IndexInFullDeck|}
            |> target.TakeMessage

        override x.ReplyTick() = MsgPlayerToGame.ReplyTick target.PlayerNr |> target.TakeMessage
        override x.PlayerStop() = MsgPlayerToGame.StopGameFromUser |> target.TakeMessage
        override x.PlayerStopped playernr = MsgPlayerToGame.PlayerStopped target.PlayerNr |> target.TakeMessage
        override x.IsValidMove card = target.IsValidMove card


type private MPUserToGame(target : IMPUIToGameTarget) =
    inherit MPPlayerToGame(target)
    interface IXToPlayer with
        override x.PlayerStopped playernr = ()


type private MPGameUIToClient(ftakemessage : MsgUIToX -> unit, fisvalidmove : Card -> bool) =
    interface IUserToX with
        override x.ReplyStartNewGame(yesorno : bool) = 
            MsgUIToX.ReplyStartNewGame yesorno
            |> ftakemessage

        override x.ReplyBeBig bebig zole = 
            MsgUIToX.ReplyBeBig
                {|beBig = bebig; 
                  zole = zole|}
            |> ftakemessage

        override x.ReplyBuryCards card1 card2 = 
            MsgUIToX.ReplyBury
                {|card1 = card1.IndexInFullDeck;
                  card2 = card2.IndexInFullDeck|}
            |> ftakemessage
    
        override x.ReplyMakeMove card = 
            MsgUIToX.ReplyMove card.IndexInFullDeck
            |> ftakemessage

        override x.ReplyTick() = MsgUIToX.ReplyTick |> ftakemessage
        override x.StopGame() = MsgUIToX.StopGame |> ftakemessage
        override x.IsValidMove card = fisvalidmove card
        override x.UIFailed(msg) = MsgUIToX.UIFailed msg |> ftakemessage


type private UserToXWrapper(ftarget : unit -> IUserToX) =
    interface IUserToX with
        member x.ReplyStartNewGame(yesorno : bool) = ftarget().ReplyStartNewGame(yesorno)
        member x.ReplyBeBig(bebig : bool) (zole : bool) = ftarget().ReplyBeBig bebig zole
        member x.ReplyBuryCards(card1 : Card) (card2 : Card) = ftarget().ReplyBuryCards card1 card2
        member x.ReplyMakeMove(card : Card) = ftarget().ReplyMakeMove card
        member x.ReplyTick() = ftarget().ReplyTick()
        member x.StopGame() = ftarget().StopGame()
        member x.IsValidMove(card : Card) = ftarget().IsValidMove card
        member x.UIFailed(msg) = ftarget().UIFailed(msg)


type private UserToPlayerWrapper(ftarget : unit -> IXToPlayer) =
    interface IUserToX with
        member x.ReplyStartNewGame(yesorno : bool) = ftarget().ReplyStartNewGame(yesorno)
        member x.ReplyBeBig(bebig : bool) (zole : bool) = ftarget().ReplyBeBig bebig zole
        member x.ReplyBuryCards(card1 : Card) (card2 : Card) = ftarget().ReplyBuryCards card1 card2
        member x.ReplyMakeMove(card : Card) = ftarget().ReplyMakeMove card
        member x.ReplyTick() = ftarget().ReplyTick()
        member x.StopGame() = ftarget().PlayerStop()
        member x.IsValidMove(card : Card) = ftarget().IsValidMove card
        member x.UIFailed(msg) = ftarget().PlayerStop()
    

type private MPToServer (messaggeReceiver) =
    member val TakeMessage : (MsgClientToServer -> unit) = messaggeReceiver
    interface IClientToServer with
        member x.Connect(greeting) = MsgClientToServer.Connect greeting |> x.TakeMessage
        member x.Disconnect() = MsgClientToServer.Disconnect |> x.TakeMessage
        member x.LogIn name psw = MsgClientToServer.LogIn {|name = name; psw = psw|} |> x.TakeMessage
        member x.LogInAsGuest name = MsgClientToServer.LogInAsGuest name |> x.TakeMessage
        member x.GetRegCode name psw email = MsgClientToServer.GetRegCode {|name = name; psw = psw; email = email|} |> x.TakeMessage
        member x.Register name psw regcode = MsgClientToServer.Register {|name = name; psw = psw; regcode = regcode|} |> x.TakeMessage
        member x.EnterLobby() = MsgClientToServer.EnterLobby |> x.TakeMessage
        member x.JoinGame() = MsgClientToServer.JoinGame |> x.TakeMessage
        member x.GetCalendarData() = MsgClientToServer.GetCalendarData |> x.TakeMessage
        member x.GetCalendarTagData tag = MsgClientToServer.GetCalendarTagData tag |> x.TakeMessage
        member x.SetCalendarData data = MsgClientToServer.SetCalendarData data |> x.TakeMessage
        member x.JoinPrivateGame name psw = MsgClientToServer.JoinPrivateGame {|name = name; psw = psw|} |> x.TakeMessage
        member x.CancelNewGame() = MsgClientToServer.CancelNewGame |> x.TakeMessage
        member x.GameStopped() = MsgClientToServer.GameStopped |> x.TakeMessage


type private MPUIToClient(ftakemsg : MsgToClient -> unit) =
    let ftakemsg msg = MsgToClient.FromClientUI msg |> ftakemsg
    interface IUIToClient with
        member this.PlayOffline(name) = MsgUIToClient.PlayOffline name |> ftakemsg
        member this.Connect ip port = MsgUIToClient.Connect {|ip = ip; port = port|} |> ftakemsg
        member this.Disconnect() = MsgUIToClient.Disconnect |> ftakemsg
        member this.AppClosing() = MsgUIToClient.AppClosing |> ftakemsg
        member this.LogIn name psw = MsgUIToClient.LogIn {|name = name; psw = psw|} |> ftakemsg
        member this.LogInAsGuest name = MsgUIToClient.LogInAsGuest name |> ftakemsg
        member this.GetRegCode name psw email = MsgUIToClient.GetRegCode {|name = name; psw = psw; email = email|} |> ftakemsg
        member this.Register name psw regcode = MsgUIToClient.Register {|name = name; psw = psw; regcode = regcode|} |> ftakemsg
        member this.EnterLobby() = MsgUIToClient.EnterLobby |> ftakemsg
        member this.GetCalendarData() = MsgUIToClient.GetCalendarData |> ftakemsg
        member this.GetCalendarTagData tag = MsgUIToClient.GetCalendarTagData tag |> ftakemsg
        member this.SetCalendarData data = MsgUIToClient.SetCalendarData data |> ftakemsg
        member this.JoinGame() = MsgUIToClient.JoinGame |> ftakemsg
        member this.JoinPrivateGame name psw = MsgUIToClient.JoinPrivateGame {|name = name; psw = psw|} |> ftakemsg
        member this.CancelNewGame() = MsgUIToClient.CancelNewGame |> ftakemsg


type private MsgConverter(playernr : int) =
    member x.Convert (msg : MsgUIToX) : MsgPlayerToGame =
        match msg with
        |ReplyTick -> MsgPlayerToGame.ReplyTick playernr
        |StopGame -> MsgPlayerToGame.StopGameFromUser
        |ReplyStartNewGame yesorno -> MsgPlayerToGame.ReplyStartNewGame {|playerNrWho = playernr; yesorno = yesorno|}
        |ReplyBeBig m -> MsgPlayerToGame.ReplyBeBig {|playerNrWho = playernr; beBig = m.beBig; zole = m.zole|}
        |ReplyBury m -> MsgPlayerToGame.ReplyBury {|playerNrWho = playernr; card1 = m.card1; card2 = m.card2|}
        |ReplyMove card-> MsgPlayerToGame.ReplyMove {|playerNrWho = playernr; card = card|}
        |UIFailed msg -> MsgPlayerToGame.Failed {|playerWho = playernr; msg = msg|}

type private MPClientToServer (msgtaker : IMsgTaker<MsgClientToServer>) =
    interface IClientToServer with
        member this.Connect(greeting) = MsgClientToServer.Connect greeting |> msgtaker.TakeMessage
        member this.Disconnect() = MsgClientToServer.Disconnect |> msgtaker.TakeMessage
        member this.GetRegCode name psw email = MsgClientToServer.GetRegCode {|name = name; psw = psw; email = email|} |> msgtaker.TakeMessage
        member this.Register name psw regcode = MsgClientToServer.Register {|name = name; psw = psw; regcode = regcode|} |> msgtaker.TakeMessage
        member this.LogIn name psw = MsgClientToServer.LogIn {|name = name; psw =psw|} |> msgtaker.TakeMessage
        member this.LogInAsGuest name = MsgClientToServer.LogInAsGuest name |> msgtaker.TakeMessage
        member this.EnterLobby() = MsgClientToServer.EnterLobby |> msgtaker.TakeMessage
        member this.GetCalendarData() = MsgClientToServer.GetCalendarData |> msgtaker.TakeMessage
        member this.GetCalendarTagData tag = MsgClientToServer.GetCalendarTagData tag |> msgtaker.TakeMessage
        member this.SetCalendarData data = MsgClientToServer.SetCalendarData data |> msgtaker.TakeMessage
        member this.JoinGame() = MsgClientToServer.JoinGame |> msgtaker.TakeMessage
        member this.JoinPrivateGame name psw = MsgClientToServer.JoinPrivateGame {|name = name; psw =psw|} |> msgtaker.TakeMessage
        member this.CancelNewGame() = MsgClientToServer.CancelNewGame |> msgtaker.TakeMessage
        member this.GameStopped() = MsgClientToServer.GameStopped |> msgtaker.TakeMessage

type private MPClientToServerAsync (msgtaker : IMsgTakerAsync<MsgClientToServer>) =
    interface IClientToServerAsync with
        member this.Connect(greeting) = MsgClientToServer.Connect greeting |> msgtaker.TakeMessage
        member this.Disconnect() = MsgClientToServer.Disconnect |> msgtaker.TakeMessage
        member this.GetRegCode name psw email = MsgClientToServer.GetRegCode {|name = name; psw = psw; email = email|} |> msgtaker.TakeMessage
        member this.Register name psw regcode = MsgClientToServer.Register {|name = name; psw = psw; regcode = regcode|} |> msgtaker.TakeMessage
        member this.LogIn name psw = MsgClientToServer.LogIn {|name = name; psw =psw|} |> msgtaker.TakeMessage
        member this.LogInAsGuest name = MsgClientToServer.LogInAsGuest name |> msgtaker.TakeMessage
        member this.EnterLobby() = MsgClientToServer.EnterLobby |> msgtaker.TakeMessage
        member this.GetCalendarData() = MsgClientToServer.GetCalendarData |> msgtaker.TakeMessage
        member this.GetCalendarTagData tag = MsgClientToServer.GetCalendarTagData tag |> msgtaker.TakeMessage
        member this.SetCalendarData data = MsgClientToServer.SetCalendarData data |> msgtaker.TakeMessage
        member this.JoinGame() = MsgClientToServer.JoinGame |> msgtaker.TakeMessage
        member this.JoinPrivateGame name psw = MsgClientToServer.JoinPrivateGame {|name = name; psw =psw|} |> msgtaker.TakeMessage
        member this.CancelNewGame() = MsgClientToServer.CancelNewGame |> msgtaker.TakeMessage
        member this.GameStopped() = MsgClientToServer.GameStopped |> msgtaker.TakeMessage
