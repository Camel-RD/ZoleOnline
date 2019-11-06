namespace GameLib
open System
open System.Collections.Immutable

type private PlayerRemoteServer (game, playernr, playername, delay, uuid) as this =
    let MailBox = new AutoCancelAgent<MsgToPlayer>(this.DoInbox)
    let Game : IPlayerToGame = game
    let PlayerType = PlayerType.RemoteServer
    let Name = playername
    let PlayerNr = playernr
    let UUID = uuid

    let MPUIToGameTarget =
        {new IMPUIToGameTarget with
             member x.PlayerNr = playernr
             member x.TakeMessage(msg) = this.TakeMessage msg 
             member x.IsValidMove(card) = this.IsValidMove card}

    let FromGame = MPGameToPlayer(this.TakeMessage) :> IGameToPlayer
    let FromX = MPPlayerToGame(MPUIToGameTarget) :> IXToPlayer

    let InitState = PlayerWithCardsState.InitState
    
    let SendToServer msg = 
        match msg with
        |MsgToPlayer.FromGame msgin -> game.SendMessage PlayerNr msgin
        |_ -> failwith "Bad call"

    member x.Start() = MailBox.Start()
    
    member x.IsValidMove(card : Card) = false

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
            |MsgToPlayer.FromRemote StopGameFromUser -> 
                game.PlayerStopped PlayerNr
                {InitState with playerType = PlayerGameType.Aborted; gameType = GameType.Aborted}
            |MsgToPlayer.FromGame (MsgGameToPlayer.PlayerStopped plnr) -> 
                {InitState with playerType = PlayerGameType.Aborted; gameType = GameType.Aborted}
            |MsgToPlayer.FromGame AskTick -> 
                SendToServer msg
                cur_state
            |MsgToPlayer.FromGame AskSimpleTick ->
                SendToServer msg
                cur_state
            |MsgToPlayer.FromRemote (MsgPlayerToGame.ReplyTick _) -> 
                Game.ReplyTick PlayerNr
                cur_state
            |MsgToPlayer.FromGame MsgGameToPlayer.Clear -> InitState
            |MsgToPlayer.FromGame AskStartNewGame ->
                SendToServer msg
                cur_state
            |MsgToPlayer.FromRemote (MsgPlayerToGame.ReplyStartNewGame m) ->
                Game.ReplyStartNewGame PlayerNr m.yesorno
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.AddCards m) -> 
                SendToServer msg
                cur_state.AddCards m.cardIds
            |MsgToPlayer.FromGame MsgGameToPlayer.AskBeBig -> 
                SendToServer msg
                if cur_state.cards.Cards.Count <> 8 then failwith "Got no cards"
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.AskedBeBig plnr) -> 
                SendToServer msg
                cur_state
            |MsgToPlayer.FromRemote (MsgPlayerToGame.ReplyBeBig m) -> 
                if cur_state.cards.Cards.Count <> 8 then failwith "Got no cards"
                let new_playertype = 
                    if m.beBig || m.zole 
                    then PlayerGameType.Big 
                    else PlayerGameType.NotSet
                Game.ReplyBeBig PlayerNr m.beBig m.zole
                {cur_state with playerType = new_playertype}
            |MsgToPlayer.FromGame (MsgGameToPlayer.RepliedBeBig m) ->
                SendToServer msg
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.GameData m) -> 
                //SendToServer msg
                let firstplayernr = 
                    if m.gameType = GameType.Table
                    then cur_state.firstPlayerNr
                    else m.bigPlayerNr
                cur_state.SetGameData m.gameType m.bigPlayerNr firstplayernr
            |MsgToPlayer.FromGame MsgGameToPlayer.AskBury -> 
                SendToServer msg
                if cur_state.cards.Cards.Count <> 10 then failwith "Got no cards"
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.AskedBury plnr) -> 
                SendToServer msg
                cur_state
            |MsgToPlayer.FromRemote (MsgPlayerToGame.ReplyBury m) -> 
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
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.Dug plnr) ->
                SendToServer msg
                cur_state                
            |MsgToPlayer.FromGame (MsgGameToPlayer.MillMove plnr) ->
                SendToServer msg
                cur_state                
            |MsgToPlayer.FromGame (MsgGameToPlayer.GotMove m) ->
                SendToServer msg
                cur_state.AddCardsOndesk([m.card])
            |MsgToPlayer.FromGame (MsgGameToPlayer.After3 m) ->
                //SendToServer msg
                let st1 = cur_state.AddGoneCards([m.card1; m.card2; m.card3])
                let st1 = st1.ClearCardsOnDesk()
                let btakepoints = 
                    m.winnernr = PlayerNr ||
                    cur_state.playerType <> PlayerGameType.Table &&
                    cur_state.bigPlayerNr <> m.winnernr
                let newpoints = st1.gamePoints + if btakepoints then m.points else 0
                {st1 with gamePoints = newpoints}
            |MsgToPlayer.FromGame (MsgGameToPlayer.AfterZGame points) ->
                SendToServer msg
                let newpoints = 
                    cur_state.gamePoints + 
                    if cur_state.playerType = PlayerGameType.Little 
                        then points 
                        else 0
                {cur_state with gamePoints = newpoints}
            |MsgToPlayer.FromGame MsgGameToPlayer.AskMove ->
                SendToServer msg
                if cur_state.cards.Cards.Count = 0 then failwith "Got no cards"
                cur_state
            |MsgToPlayer.FromRemote (MsgPlayerToGame.ReplyMove m) ->
                if cur_state.cards.Cards.Count = 0 then failwith "Got no cards"
                let card = FullCardDeck.Cards.[m.card]
                Game.ReplyMakeMove PlayerNr card
                let cur_state = cur_state.RemoveCards([card.IndexInFullDeck])
                let cur_state = cur_state.AddCardsOndesk([card.IndexInFullDeck])
                cur_state
            |MsgToPlayer.FromGame (MsgGameToPlayer.SetPoints m) ->
                //SendToServer msg
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
        override val FromGame = FromGame
        override val FromX = FromX
        override x.TakeMessage msg = x.TakeMessage msg


