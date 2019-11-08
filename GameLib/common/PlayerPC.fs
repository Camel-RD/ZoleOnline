namespace GameLib
open System
open System.Collections.Immutable


type PlayerState = {
    playerGameType : PlayerGameType
    gamePoints : int}

type private PlayerWithCardsState = {
    playerType : PlayerGameType
    gamePoints : int
    gameType : GameType
    bigPlayerNr : int
    cards : CardSet
    gonecards : CardSet
    cardsOnDesk : CardSet
    firstPlayerNr : int
} with
    static member InitState =
        {playerType = PlayerGameType.NotSet;
        gameType = GameType.NotSet;
        bigPlayerNr = -1;
        gamePoints = 0;
        cards = CardSet.Empty;
        gonecards = CardSet.Empty;
        cardsOnDesk = CardSet.Empty;
        firstPlayerNr = 0}
    member x.AddCards (cardids : int list) =
        {x with cards = cardids |> FullCardDeck.GetByIds |> x.cards.Add}
    member x.AddGoneCards (cardids : int list) =
        {x with gonecards = cardids |> FullCardDeck.GetByIds |> x.gonecards.Add}
    member x.AddCardsOndesk (cardids : int list) =
        {x with cardsOnDesk = x.cardsOnDesk.Add(cardids |> FullCardDeck.GetByIds, false)}
    member x.RemoveCards (cardids : int list) =
        {x with cards = cardids |> FullCardDeck.GetByIds |> x.cards.Remove}
    member x.ClearCardsOnDesk ()=
        {x with cardsOnDesk = CardSet.Empty}
    member x.SetGameData (gameType : GameType) (bigPlayerNr : int) (firstPlayerNr : int) =
        {x with gameType = gameType; 
                bigPlayerNr = bigPlayerNr;
                firstPlayerNr = firstPlayerNr;
                playerType = 
                    if gameType = GameType.Table 
                    then PlayerGameType.Table
                    elif x.playerType = PlayerGameType.Big
                    then PlayerGameType.Big 
                    else PlayerGameType.Little}

type private PlayerWithCardsStateVar = {
    Reader : PlayerWithCardsState -> Async<MsgToPlayer>
    State : PlayerWithCardsState
    Worker : PlayerWithCardsStateVar -> Async<PlayerWithCardsStateVar>} with
    member x.ShouldExit() = 
        x.State.gameType = GameType.Aborted || x.State.playerType = PlayerGameType.Aborted


type private PlayerPC (game, playernr, playername, delay, gamesolver, uuid) as this =
    let MailBox = new AutoCancelAgent<MsgToPlayer>(this.DoInbox)
    let Game : IPlayerToGame = game
    let PlayerType = PlayerType.PcAI
    let Name = playername
    let PlayerNr = playernr
    let GameSolver : IGameSolver = gamesolver
    let UUID = uuid
    
    let MPUIToGameTarget =
        {new IMPUIToGameTarget with
             member x.PlayerNr = playernr
             member x.TakeMessage(msg) = this.TakeMessage msg 
             member x.IsValidMove(card) = this.IsValidMove card}

    let FromGame = MPGameToPlayer(this.TakeMessage) :> IGameToPlayer
    let FromX = XToPlayerEmpty.Empty :> IXToPlayer

    let InitState = PlayerWithCardsState.InitState

    member x.Start() = MailBox.Start()

    member x.IsValidMove(card : Card) = false

    member private x.DoMsg (statevar : PlayerWithCardsStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader(cur_state)
        match msg with
            |MsgToPlayer.FromGame AskTick 
            |MsgToPlayer.FromGame AskSimpleTick ->
                Game.ReplyTick(PlayerNr)
            |MsgToPlayer.FromGame AskBeBig 
            |MsgToPlayer.FromGame AskBury 
            |MsgToPlayer.FromGame AskMove -> 
                if delay > 0 then
                    do! Async.Sleep(delay)
            |_-> ()

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
            |MsgToPlayer.FromGame MsgGameToPlayer.Clear -> InitState
            |MsgToPlayer.FromGame AskStartNewGame ->
                Game.ReplyStartNewGame PlayerNr true
                cur_state
            |MsgToPlayer.FromGame (AddCards m) -> cur_state.AddCards m.cardIds
            |MsgToPlayer.FromGame AskBeBig -> 
                if cur_state.cards.Cards.Count <> 8 then failwith "Got no cards"
                let bebig = GameSolver.WantBeBig cur_state.cards
                let new_playertype = if bebig then PlayerGameType.Big else PlayerGameType.NotSet
                Game.ReplyBeBig PlayerNr bebig false
                {cur_state with playerType = new_playertype}
            |MsgToPlayer.FromGame AskBury -> 
                if cur_state.cards.Cards.Count <> 10 then failwith "Got no cards"
                let (card1,card2) = GameSolver.CardsToBury cur_state.cards
                Game.ReplyBuryCards PlayerNr card1 card2
                cur_state
                    .AddGoneCards([card1.IndexInFullDeck; card2.IndexInFullDeck])
                    .RemoveCards([card1.IndexInFullDeck; card2.IndexInFullDeck])
            |MsgToPlayer.FromGame (GameData m) -> 
                cur_state.SetGameData m.gameType m.bigPlayerNr m.firstPlayerNr
            |MsgToPlayer.FromGame (GotMove m) ->
                cur_state.AddCardsOndesk([m.card])
            |MsgToPlayer.FromGame (After3 m) ->
                let st1 = cur_state.AddGoneCards([m.card1; m.card2; m.card3])
                let st1 = st1.ClearCardsOnDesk()
                let btakepoints = 
                    m.winnernr = PlayerNr ||
                    cur_state.playerType <> PlayerGameType.Table &&
                    cur_state.bigPlayerNr <> m.winnernr
                let newpoints = st1.gamePoints + if btakepoints then m.points else 0
                {st1 with gamePoints = newpoints}
            |MsgToPlayer.FromGame (AfterZGame points) ->
                let newpoints = 
                    cur_state.gamePoints + 
                    if cur_state.playerType = PlayerGameType.Little 
                        then points 
                        else 0
                {cur_state with gamePoints = newpoints}
            |MsgToPlayer.FromGame AskMove ->
                if cur_state.cards.Cards.Count = 0 then failwith "Got no cards"
                let gonecards = cur_state.gonecards.Add cur_state.cardsOnDesk
                let isBig = cur_state.playerType = PlayerGameType.Big
                let isafterbig = 
                    not isBig &&
                    (PlayerNr - cur_state.bigPlayerNr = 1 ||
                     PlayerNr = 0 && cur_state.bigPlayerNr = 2)
                let isTableGaem = cur_state.gameType = GameType.Table
                let card = GameSolver.FindMove cur_state.cards cur_state.cardsOnDesk gonecards isTableGaem isBig isafterbig
                Game.ReplyMakeMove PlayerNr card
                cur_state.RemoveCards([card.IndexInFullDeck])
            |MsgToPlayer.FromGame (SetPoints m) ->
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

