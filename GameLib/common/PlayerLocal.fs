namespace GameLib
open System
open System.Collections.Immutable

type private PlayerLocal (game, gameUI, playernr, playername, uuid, playernames) as this =
    let MailBox = new AutoCancelAgent<MsgToPlayer>(this.DoInbox)
    let Game : IPlayerToGame = game
    let GameUI : IPlayerToUI = gameUI
    let PlayerNames : ImmutableArray<string> = playernames
    let PlayerType = PlayerType.Local
    let Name = playername
    let PlayerNr = playernr
    let UUID = uuid
    let InitState = PlayerWithCardsState.InitState
    

    let _isValidMove (cards : CardSet) (cardsondesk : CardSet) (card : Card) =
        if cards.Cards.Count = 0 || cardsondesk.Cards.Count >= 3  || not (cards.Contains card) then false 
        elif cardsondesk.Cards.Count = 0 || cards.Cards.Count = 1 then true
        else
            let first_card = cardsondesk.Cards.[0]
            if cards.HasSuitX(first_card.SuitX)
            then card.SuitX = first_card.SuitX
            else true

    let MPUIToGameTarget =
        {new IMPUIToGameTarget with
             member x.PlayerNr = playernr
             member x.TakeMessage(msg) = this.TakeMessage msg 
             member x.IsValidMove(card) = this.IsValidMove card}

    let FromGame = MPGameToPlayer(this.TakeMessage) :> IGameToPlayer
    let FromX = MPPlayerToGame(MPUIToGameTarget) :> IXToPlayer
    

    
    member x.Start() =MailBox.Start()

    member private x.DoMsg (statevar : PlayerWithCardsStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader(cur_state)
        let cur_state = x.DoMsgA cur_state msg
        return {statevar with State = cur_state}
    }


    member private x.DoMsgA (state : PlayerWithCardsState) (msg : MsgToPlayer) = 
        let cur_state = state
        let cur_state = 
            match msg with
            |MsgToPlayer.KillPill 
            |MsgToPlayer.FromGame MsgGameToPlayer.GameStop -> 
                {InitState with playerType = PlayerGameType.Aborted; gameType = GameType.Aborted}
            |MsgToPlayer.FromUser MsgUIToX.StopGame -> 
                game.PlayerStopped PlayerNr
                {InitState with playerType = PlayerGameType.Aborted; gameType = GameType.Aborted}
            |MsgToPlayer.FromGame (MsgGameToPlayer.PlayerStopped plnr) -> 
                let playername = PlayerNames.[plnr]
                GameUI.ShowText <| sprintf "%s īzstājās no spēles" playername
                {InitState with playerType = PlayerGameType.Aborted; gameType = GameType.Aborted}
            |MsgToPlayer.FromGame AskTick -> 
                GameUI.AskTick()
                cur_state
            |MsgToPlayer.FromGame AskSimpleTick ->
                GameUI.AskSimpleTick()
                cur_state
            |MsgToPlayer.FromUser MsgUIToX.ReplyTick  -> 
                //GameUI.ShowText ""
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk cur_state.firstPlayerNr PlayerNr
                Game.ReplyTick PlayerNr
                cur_state
            |MsgToPlayer.FromGame MsgGameToPlayer.Clear -> InitState
            |MsgToPlayer.FromGame AskStartNewGame ->
                GameUI.AskStartGame()
                cur_state
            |MsgToPlayer.FromUser (MsgUIToX.ReplyStartNewGame yesorno) ->
                Game.ReplyStartNewGame PlayerNr yesorno
                cur_state
            |MsgToPlayer.FromGame (StartedNewGame fplnr) ->
                GameUI.DoStartGame()
                GameUI.ShowNames PlayerNames.[0] PlayerNames.[1] PlayerNames.[2] -1 PlayerNr
                {cur_state with firstPlayerNr = fplnr}
            |MsgToPlayer.FromGame (MsgGameToPlayer.AddCards m) -> 
                let cur_state = cur_state.AddCards m.cardIds
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk 0 PlayerNr
                cur_state
            |MsgToPlayer.FromGame MsgGameToPlayer.AskBeBig -> 
                GameUI.AskBeBig()
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.AskedBeBig plnr) -> 
                let playername = PlayerNames.[plnr]
                let msg = sprintf "Vai %s būs lielais" playername
                GameUI.ShowText msg
                cur_state
            |MsgToPlayer.FromUser (MsgUIToX.ReplyBeBig m) -> 
                if cur_state.cards.Cards.Count <> 8 then failwith "Got no cards"
                let new_playertype = 
                    if m.beBig || m.zole 
                    then PlayerGameType.Big 
                    else PlayerGameType.NotSet
                let msg = 
                    if m.zole then "Tu spēlēsi lielo zoli"
                    elif m.beBig then "Tu būsi lielais"
                    else "Tu nebūsi lielais"
                //GameUI.ShowText msg
                Game.ReplyBeBig PlayerNr m.beBig m.zole
                {cur_state with playerType = new_playertype}
            |MsgToPlayer.FromGame (MsgGameToPlayer.RepliedBeBig m) ->
                let playername = PlayerNames.[m.playerNrWho]
                let msg = 
                    if m.zole then sprintf "%s spēlēs zoli" playername
                    elif m.beBig then sprintf "%s būs lielais" playername
                    else sprintf "%s nebūs lielais" playername
                GameUI.ShowText msg
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.GameData m) -> 
                if m.gameType = GameType.Table then
                    GameUI.ShowText "Neviens negrib būt lielais, spēlējam mazo zoli"
                let cur_state = cur_state.SetGameData m.gameType m.bigPlayerNr m.firstPlayerNr
                let k = if cur_state.gameType = GameType.Table then -1 else cur_state.bigPlayerNr
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk m.firstPlayerNr PlayerNr
                GameUI.ShowNames PlayerNames.[0] PlayerNames.[1] PlayerNames.[2] k PlayerNr
                cur_state
            |MsgToPlayer.FromGame MsgGameToPlayer.AskBury -> 
                GameUI.AskBuryCards()
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.AskedBury plnr) -> 
                let playername = PlayerNames.[plnr]
                let msg = sprintf "%s noraks kārtis" playername
                GameUI.ShowText msg
                cur_state
            |MsgToPlayer.FromUser (MsgUIToX.ReplyBury m) -> 
                if cur_state.cards.Cards.Count <> 10 then failwith "Got no cards"
                let card1 = FullCardDeck.Cards.[m.card1]
                let card2 = FullCardDeck.Cards.[m.card2]
                let points = card1.Points + card2.Points
                if not (cur_state.cards.Contains(card1) && cur_state.cards.Contains(card2)) 
                then failwith "Norok neesošu kārti"
                Game.ReplyBuryCards PlayerNr card1 card2
                let cur_state = 
                    {cur_state with gamePoints = points}
                     .AddGoneCards([card1.IndexInFullDeck; card2.IndexInFullDeck])
                     .RemoveCards([card1.IndexInFullDeck; card2.IndexInFullDeck])
                GameUI.ShowText ""
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk cur_state.firstPlayerNr PlayerNr
                GameUI.ShowPoints points
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.Dug plnr) ->
                let playername = PlayerNames.[plnr]
                let msg = sprintf "%s noraka kārtis" playername
                GameUI.ShowText msg
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk cur_state.firstPlayerNr PlayerNr
                cur_state                
            |MsgToPlayer.FromGame (MsgGameToPlayer.MillMove plnr) ->
                let playername = PlayerNames.[plnr]
                let msg = sprintf "%s liks kārti" playername
                GameUI.ShowText msg
                if cur_state.cardsOnDesk.Cards.Count = 0 then
                    GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk cur_state.firstPlayerNr PlayerNr
                cur_state                
            |MsgToPlayer.FromGame (MsgGameToPlayer.GotMove m) ->
                let playername = PlayerNames.[m.playerNrWho]
                let msg = sprintf "%s uzlika kārti" playername
                //GameUI.ShowText msg
                let cur_state = cur_state.AddCardsOndesk([m.card])
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk cur_state.firstPlayerNr PlayerNr
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.After3 m) ->
                let playername = PlayerNames.[m.winnernr]
                let (btakepoints, txtmsg) = 
                    match m.winnernr = PlayerNr, 
                          cur_state.playerType, 
                          cur_state.bigPlayerNr = m.winnernr with
                    |true, PlayerGameType.Big, _
                    |true, PlayerGameType.Table, _ -> (true, sprintf "%d punkti tev" m.points)
                    |false, PlayerGameType.Big, _ -> (false, sprintf "%d punkti mazajiem" m.points)
                    |true, PlayerGameType.Little, _ 
                    |false, PlayerGameType.Little, false -> (true, sprintf "%d punkti mazajiem" m.points)
                    | _ -> (false, sprintf "%d punktus paņem %s" m.points playername)

                let newpoints = cur_state.gamePoints + if btakepoints then m.points else 0
                GameUI.ShowText txtmsg
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk cur_state.firstPlayerNr PlayerNr
                GameUI.ShowPoints newpoints
                let cur_state = cur_state.AddGoneCards([m.card1; m.card2; m.card3])
                let cur_state = cur_state.ClearCardsOnDesk()
                let cur_state = 
                    {cur_state with 
                        gamePoints = newpoints;
                        firstPlayerNr = m.winnernr}
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.AfterZGame points) ->
                let newpoints = 
                    cur_state.gamePoints + 
                    if cur_state.playerType = PlayerGameType.Little 
                        then points 
                        else 0
                let txtmsg = sprintf "%d punkti mazajiem no galda" points
                GameUI.ShowText txtmsg
                GameUI.ShowPoints newpoints
                {cur_state with gamePoints = newpoints}
            |MsgToPlayer.FromGame MsgGameToPlayer.AskMove ->
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk cur_state.firstPlayerNr PlayerNr
                GameUI.AskMakeMove()
                cur_state
            |MsgToPlayer.FromUser (MsgUIToX.ReplyMove card) ->
                if cur_state.cards.Cards.Count = 0 then failwith "Got no cards"
                let card = FullCardDeck.Cards.[card]
                let isvalidmove = _isValidMove cur_state.cards cur_state.cardsOnDesk card
                if not isvalidmove then failwith "Uzlikta nepareiza kārts"
                Game.ReplyMakeMove PlayerNr card
                let cur_state = cur_state.RemoveCards([card.IndexInFullDeck])
                let cur_state = cur_state.AddCardsOndesk([card.IndexInFullDeck])
                GameUI.ShowText ""
                GameUI.ShowCards cur_state.cards cur_state.cardsOnDesk cur_state.firstPlayerNr PlayerNr
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.SetPoints m) ->
                GameUI.AddRowToStats m.pts1 m.pts2 m.pts3 PlayerNr
                GameUI.ShowStats true
                InitState
            |_ -> cur_state
        cur_state

    member val private _IsPlayerClosed = false with get,set
    member x.IsPlayerClosed = x._IsPlayerClosed

    member private x.OnLocalPlayerStopped() = 
        if not x._IsPlayerClosed then
            try
                game.PlayerStopped PlayerNr
                x.Dispose()
            finally x._IsPlayerClosed <- true    

    member private x.ClosePlayer() = 
        if not x._IsPlayerClosed then
            try x.Dispose()
            finally x._IsPlayerClosed <- true    


    member private x.MsgReader (inbox : MailboxProcessor<MsgToPlayer>) (state : PlayerWithCardsState) =
        let rec loop() = async{
            if state.playerType = PlayerGameType.Aborted ||
                state.gameType = GameType.Aborted then
                x.ClosePlayer(); 
                //x.OnLocalPlayerStopped();
                return MsgToPlayer.KillPill
            else

            let! msg = inbox.Receive()
            match msg with
            |MsgToPlayer.KillPill -> return msg
            |MsgToPlayer.GetState channel -> 
                channel.Reply state
                return! loop()
            | _ -> return msg}
        loop()

    member private x.DoInbox(inbox : MailboxProcessor<MsgToPlayer>) = 
        let rec loop (st : PlayerWithCardsStateVar) = async{
            let! ret = Async.Catch (x.DoMsg(st))
            match ret with
            |Choice1Of2 new_state -> 
                if new_state.ShouldExit()
                then 
                    x.ClosePlayer(); 
                    return () 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                Game.PlayerFailed PlayerNr exc.Message
                x.ClosePlayer()
                return () }
        let init_state = InitState
        let init_statevar = 
            {Reader = x.MsgReader(inbox); 
            State = init_state; 
            Worker = x.DoMsg}
        loop(init_statevar)

    member x.GetState() = 
            let msg channel = MsgToPlayer.GetState channel
            let state = MailBox.PostAndReply(msg)
            state
    
    member x.GetStateEx() = (x.GetState() :?> PlayerWithCardsState)
    
    member x.IsValidMove (card : Card) =
        let state = x.GetStateEx()
        if state.gameType = GameType.NotSet || state.gameType = GameType.Aborted 
        then false
        else _isValidMove state.cards state.cardsOnDesk card


    member private x.TakeMessage(msg : MsgToPlayer) = 
        if not (x.IsPlayerClosed || x.IsDisposed) then
            MailBox.Post msg
        else Logger.WriteLine("Player[{0}] disposed: {1}", PlayerNr, msg)
    
    member private x.TakeMessageSafe(msg : MsgToPlayer) = 
        if not (x.IsPlayerClosed || x.IsDisposed) then
            try
                MailBox.Post msg
            finally ()
        else Logger.WriteLine("Player[{0}] disposed: {1}", PlayerNr, msg)

    member private x.TakeMessage(msg : MsgGameToPlayer) = 
        let msg = MsgToPlayer.FromGame msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgGameToPlayer) = 
        let msg = MsgToPlayer.FromGame msg
        x.TakeMessageSafe msg

    member private x.TakeMessage(msg : MsgPlayerToGame) = 
        let msg = MsgToPlayer.FromRemote msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgPlayerToGame) = 
        let msg = MsgToPlayer.FromRemote msg
        x.TakeMessageSafe msg

    member private x.TakeMessage(msg : MsgUIToX) = 
        let msg = MsgToPlayer.FromUser msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgUIToX) = 
        let msg = MsgToPlayer.FromUser msg
        x.TakeMessageSafe msg

        
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


    interface IPlayer with
        override val PlayerType = PlayerType
        override val Name = Name
        override val PlayerNr = PlayerNr
        override val UUID = UUID
        override x.Start() = x.Start()
        override x.GetState() = x.GetState()
        override x.FromGame = FromGame
        override x.FromX = FromX
        override x.TakeMessage msg = x.TakeMessage msg



        

