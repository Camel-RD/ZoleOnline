namespace GameLib
open System
open System.Collections.Immutable
open System.Diagnostics

type GameState = {
    gameType : GameType
    cardsOnDesk : CardSet
    playerCards : ImmutableArray<CardSet>
    buriedCards : CardSet
    cycleNr : int
    cycleMoveNr : int
    firstPlayer : int
    firstPlayerX : int
    playerPoints : ImmutableArray<int>
    playerPointsX : ImmutableArray<int>
    activePlayer : IPlayer
    bigPlayer : IPlayer
    littlePlayer1 : IPlayer
    littlePlayer2 : IPlayer
    excmsg : string } with
    static member InitState =
        {gameType = GameType.NotSet;
        cardsOnDesk = CardSet.Empty;
        playerCards = ImmutableArray<CardSet>.Empty;
        cycleNr = 0;
        cycleMoveNr = 0;
        firstPlayer = 0;
        firstPlayerX = 0;
        playerPoints = ImmutableArray.Create(0, 0, 0);
        playerPointsX = ImmutableArray.Create(0, 0, 0);
        activePlayer = PlayerEmpty.Empty;
        bigPlayer = PlayerEmpty.Empty;
        littlePlayer1 = PlayerEmpty.Empty;
        littlePlayer2 = PlayerEmpty.Empty;
        buriedCards = CardSet.Empty;
        excmsg = "" }

    member x.Fail (msg : string) = {x with gameType = GameType.Aborted; excmsg = msg}
    member x.Abort () = {x with gameType = GameType.Aborted}
    member x.AddCards (player : IPlayer) (cards : Card seq) =
        let playerCards = [
            for i in 0..2 do
                let cs = x.playerCards.[i]
                yield if i = player.PlayerNr then cs.Add cards else cs ]
        {x with playerCards = playerCards.ToImmutableArray()}
    member x.AddToCardsOnDesk (card : Card) = 
        {x with cardsOnDesk = x.cardsOnDesk.Add(card, false)}
    member x.RemoveCards (player : IPlayer) (cards : Card seq) =
        let playerCards = [
            for i in 0..2 do
                let cs = x.playerCards.[i]
                yield if i = player.PlayerNr then cs.Remove cards else cs ]
        {x with playerCards = playerCards.ToImmutableArray()}
    member x.AddPointsForPlayer(playernr : int, points : int) =
        let pts = 
            [for i in 0..2 do 
                yield x.playerPoints.[i] + if playernr = i then points else 0]
        {x with playerPoints = pts.ToImmutableArray()}
    member x.AddPointsForWinner(winner : int, points : int) =
        let pts = 
            [for i in 0..2 do
                let p = 
                    if winner = i || x.gameType <> GameType.Table && x.bigPlayer.PlayerNr <> winner
                    then points
                    else 0
                yield x.playerPoints.[i] + p]
        {x with playerPoints = pts.ToImmutableArray()}
        

type GameStateVar = {
    Reader : GameState -> int -> Async<MsgToGame>
    State : GameState
    Worker : GameStateVar -> Async<GameStateVar>
    Flag : StateVarFlag} with
    member x.ReadMsg() = async{return! x.Reader x.State -1}
    member x.ReadMsg(timeout) = async{return! x.Reader x.State timeout}
    member x.ShouldExit() = x.Flag = StateVarFlag.Failed "" || x.Flag = Return || x.State.gameType = GameType.Aborted
    member x.WithSt state = {x with State = state}
    member x.WithStW (state, worker) = {x with State = state; Worker = worker}

type GameServer(gameform, playernames, firstplayer, playertypes, testing, messageGateWay, gametoowner) as this =
    let mailBox = new AutoCancelAgent<MsgToGame>(this.DoInbox)
    let testing : bool = testing
    let gameForm : IGameForm = gameform
    let playerNames : ImmutableArray<string> = playernames
    let playertypes : ImmutableArray<PlayerType> = playertypes
    let gwToRemote : IMsgTaker<MsgDataToRemote> option = messageGateWay
    let GameToOwner : IGameToOwner option = gametoowner
    let localPlayerNr = playertypes.IndexOf PlayerType.Local
    let HaveLocalPlayer = localPlayerNr > 0

    let override_ShowCards (cards : CardSet) (cardsondesk : CardSet) (firstplayernr : int) = 
        let cur_state = this.GetState()
        let cardsets = [
            for i in 0..2 do
                yield if i = localPlayerNr then cards else cur_state.playerCards.[i]]
        let cardsets = cardsets.ToImmutableArray()
        gameForm.ShowCards2 cardsets cardsondesk firstplayernr

    let ToUI = GameFormWrapperForGameServer(gameForm, override_ShowCards) :> IGameForm

    let _FromRemote =
        {new IMsgTaker<MsgDataFromRemote> with
            member x.TakeMessage msg = this.TakeMessageSafe msg.msg}

    let _FromPlayer = MPPlayerToGameA(this.TakeMessage) :> IPlayerToGame
    //member val FromUser = UserToPlayerWrapper(LocalplayerAsTarget) :> IUserToX
    let _FromUser = MPGameUIToClient(this.TakeMessage, this.IsValidMove) :> IUserToX


    let NextPlayerNr (pnr : int) = if pnr = 2 then 0 else pnr + 1
    let PrevPlayerNr (pnr : int) = if pnr = 0 then 2 else pnr - 1

    let MakePlayer plnr plnm pltp =
        let delay = if localPlayerNr = -1 then 0 else 1200
        match pltp with
        |PlayerType.Local -> new PlayerLocal(_FromPlayer, ToUI, plnr, plnm, Guid.Empty, playerNames) :> IPlayer
        |PlayerType.RemoteServer -> new PlayerRemoteServer(_FromPlayer, plnr, plnm, delay, Guid.Empty) :> IPlayer
        |PlayerType.PcAI -> new PlayerPC(_FromPlayer, plnr, plnm, delay, GameSolver.GetIGS, Guid.Empty) :> IPlayer
        |_ -> failwith "Bad call"

    let Players = 
        playertypes 
        |> Seq.zip playerNames
        |> Seq.indexed
        |> Seq.map (fun (k, (plnm, plt)) -> MakePlayer k plnm plt)
        |> ImmutableList.CreateRange

    let localPlayer = Players |> Seq.tryFind (fun pl -> pl.PlayerType = PlayerType.Local)
    let localPlayerX = if localPlayer.IsSome then localPlayer.Value else PlayerEmpty.Empty  :> IPlayer

    let LocalplayerAsTarget() = localPlayerX.FromX
    let MsgConverter = MsgConverter(localPlayerNr)

    let NextPlayer (player : IPlayer) = Players.[NextPlayerNr player.PlayerNr]
    let PrevPlayer (player : IPlayer) = Players.[PrevPlayerNr player.PlayerNr]

    let initState = GameState.InitState

    let isValidMove (cards : CardSet) (cardsondesk : CardSet) (card : Card) =
        if cards.Cards.Count = 0 || cardsondesk.Cards.Count >= 3 then false 
        elif cardsondesk.Cards.Count = 0 || cards.Cards.Count = 1 then true
        else
            let first_card = cardsondesk.Cards.[0]
            if cards.HasSuitX(first_card.SuitX)
            then card.SuitX = first_card.SuitX
            else true

    member x.IsValidMove (card : Card) = localPlayerX.FromX.IsValidMove(card)
    
    member val FromRemote = _FromRemote
    member val FromPlayer = MPPlayerToGameA(this.TakeMessage) :> IPlayerToGame
    //member val FromUser = UserToPlayerWrapper(LocalplayerAsTarget) :> IUserToX
    member val FromUser = MPGameUIToClient(this.TakeMessage, this.IsValidMove) :> IUserToX

    member x.InitGame() =
        mailBox.Start()
        let msg = MsgToGame.InitGame
        x.TakeMessage msg
    
    member private x.WhoWins (cardsondesk : CardSet) (firstplayernr : int) : int =
        let card1 = cardsondesk.Cards.[0]
        let card2 = cardsondesk.Cards.[1]
        let card3 = cardsondesk.Cards.[2]
        let (cardmax1, nr) = if card2.Beats card1 then (card2, 1) else (card1, 0)
        let nr = if card3.Beats cardmax1 then 2 else nr
        let nr = nr + firstplayernr
        let nr = if nr > 2 then nr - 3 else nr
        nr

    member private x.wait_for_tick_all (statevar : GameStateVar) (simpletick : bool) = 
        for player in Players do
            if simpletick 
            then player.FromGame.AskSimpleTick()
            else player.FromGame.AskTick()
        let rec loop (arr : bool array) = async{
            if not (arr |> Array.contains false) then return true else
                let! msg = statevar.ReadMsg(x.MessageReaderTimeout)
                match msg with
                |MsgToGame.FromPleyer (MsgPlayerToGame.ReplyTick plnr) when not arr.[plnr] -> 
                    let msg2 = MsgGameToPlayer.RepliedTick plnr
                    x.MessageToBeSentToRemote (NextPlayerNr plnr) msg2
                    x.MessageToBeSentToRemote (PrevPlayerNr plnr) msg2
                    arr.[plnr] <- true
                    return! loop arr
                |_ -> return false}
        loop [|false; false; false|]

    member private x.wait_for_tick_from (statevar : GameStateVar) (playernrs : int list) (simpletick : bool)= 
        for plnr in playernrs do
            if simpletick 
            then Players.[plnr].FromGame.AskSimpleTick()
            else Players.[plnr].FromGame.AskTick()
        let rec loop (arr : bool array) = async{
            if not (arr |> Array.contains false) then return true else
                let! msg = statevar.ReadMsg(x.MessageReaderTimeout)
                match msg with
                |MsgToGame.FromPleyer (MsgPlayerToGame.ReplyTick plnr) when not arr.[plnr] -> 
                    if not simpletick then
                        let msg2 = MsgGameToPlayer.RepliedTick plnr
                        x.MessageToBeSentToRemote (NextPlayerNr plnr) msg2
                        x.MessageToBeSentToRemote (PrevPlayerNr plnr) msg2
                    arr.[plnr] <- true
                    return! loop arr
                |_ -> return false}
        let arr = [|true; true; true|]
        for plnr in playernrs do arr.[plnr] <- false
        loop arr

    member private x.wait_for_started_all (statevar : GameStateVar) : Async<StartAllResult> = 
        for player in Players do
            player.FromGame.AskStartNewGame()
        let rec loop (arr : bool array) = async{
            if not (arr |> Array.contains false) then return StartAllResult.OK else
                let! msg = statevar.ReadMsg(x.MessageReaderTimeout)
                match msg with
                |MsgToGame.FromPleyer (MsgPlayerToGame.ReplyStartNewGame m) when not arr.[m.playerNrWho] -> 
                    if m.yesorno then 
                        arr.[m.playerNrWho] <- true
                        return! loop arr
                    else 
                        return StartAllResult.PlayerCanceled m.playerNrWho
                |_ -> return StartAllResult.Failed}
        loop [|false; false; false|]


    member private x.who_will_be_big (statevar : GameStateVar) = async{
        let cur_state = statevar.State
        cur_state.activePlayer.FromGame.AskBeBig()
        NextPlayer(cur_state.activePlayer).FromGame.AskedBeBig cur_state.activePlayer.PlayerNr
        PrevPlayer(cur_state.activePlayer).FromGame.AskedBeBig cur_state.activePlayer.PlayerNr
        let! msg = statevar.Reader cur_state x.MessageReaderTimeout
        let (cur_state, isdone, pnr) =
            match msg with
            |MsgToGame.FromPleyer (MsgPlayerToGame.ReplyBeBig m) when m.playerNrWho = cur_state.activePlayer.PlayerNr ->
                let player = Players.[m.playerNrWho]
                (NextPlayer player).FromGame.RepliedBeBig m.playerNrWho m.beBig m.zole
                (PrevPlayer player).FromGame.RepliedBeBig m.playerNrWho m.beBig m.zole
                match m.beBig, m.zole, cur_state.cycleNr with
                |false, false, 2 -> 
                    ({cur_state with gameType = GameType.Table}, true, m.playerNrWho)
                |false, false, _ -> 
                    let cur_state = 
                        {cur_state with 
                            activePlayer = NextPlayer cur_state.activePlayer; 
                            cycleNr = cur_state.cycleNr + 1}
                    (cur_state, false, m.playerNrWho)
                |_, true, _ ->
                    let cur_state = 
                        {cur_state with 
                            gameType = GameType.Zole;
                            bigPlayer = cur_state.activePlayer;
                            littlePlayer1 = NextPlayer cur_state.activePlayer;
                            littlePlayer2 = PrevPlayer cur_state.activePlayer}
                    (cur_state, true, m.playerNrWho)
                |true, _, _ ->
                    let cur_state = 
                        {cur_state with 
                            gameType = GameType.Normal;
                            bigPlayer = cur_state.activePlayer;
                            littlePlayer1 = NextPlayer cur_state.activePlayer;
                            littlePlayer2 = PrevPlayer cur_state.activePlayer}
                    (cur_state, true, m.playerNrWho)
            |_ -> (cur_state.Fail("Bad msg"), true, -1)
        let ret = statevar.WithSt cur_state
        if cur_state.gameType = GameType.Aborted then return ret else
        
        let playernrs = [NextPlayerNr pnr; PrevPlayerNr pnr]
        let! bwait = x.wait_for_tick_from ret playernrs true
        if not bwait then return statevar.WithSt (cur_state.Fail("Bad msg")) else
        
        if isdone then 
            let bigplnr = 
                if cur_state.gameType = GameType.Table
                then cur_state.firstPlayer
                else cur_state.bigPlayer.PlayerNr
            for player in Players do
                player.FromGame.DoTakeGameData cur_state.gameType bigplnr cur_state.firstPlayer
            if cur_state.gameType = GameType.Table then
                let! bwait = x.wait_for_tick_all ret true
                if not bwait then 
                    let cur_state = cur_state.Fail("waittick failed")
                    return {statevar with State = cur_state; Flag = StateVarFlag.Failed "waittick failed"}
                else return {ret with Flag = Return}
            else return {ret with Flag = Return}
        else return ret  }

    member private x.Bury_cards (statevar : GameStateVar) = async{
        let cur_state = statevar.State
        if cur_state.gameType = GameType.Table || 
            cur_state.gameType = GameType.Zole
        then
            return statevar.WithSt cur_state
        else
        cur_state.bigPlayer.FromGame.DoAddCards cur_state.buriedCards
        let cur_state = cur_state.AddCards cur_state.bigPlayer cur_state.buriedCards.Cards
        cur_state.bigPlayer.FromGame.AskBuryCards()
        NextPlayer(cur_state.bigPlayer).FromGame.AskedBuryCards cur_state.bigPlayer.PlayerNr
        PrevPlayer(cur_state.bigPlayer).FromGame.AskedBuryCards cur_state.bigPlayer.PlayerNr
        let! msg = statevar.Reader cur_state x.MessageReaderTimeout
        match msg with
        |MsgToGame.FromPleyer (MsgPlayerToGame.ReplyBury m) when m.playerNrWho = cur_state.bigPlayer.PlayerNr ->
            let card1 = FullCardDeck.Cards.[m.card1]
            let card2 = FullCardDeck.Cards.[m.card2]
            let points = card1.Points + card2.Points
            let cur_state = {cur_state with buriedCards = CardSet.Empty.Add [card1; card2]}
            let cur_state = cur_state.RemoveCards cur_state.bigPlayer [card1; card2]
            let cur_state = cur_state.AddPointsForPlayer (cur_state.bigPlayer.PlayerNr, points)
            (NextPlayer (cur_state.bigPlayer)).FromGame.RepliedBury cur_state.bigPlayer.PlayerNr
            (PrevPlayer (cur_state.bigPlayer)).FromGame.RepliedBury cur_state.bigPlayer.PlayerNr
            return statevar.WithSt cur_state
        |_ -> return statevar.WithSt (cur_state.Fail("Bad msg"))
    }

    member private x.one_move (statevar : GameStateVar) = async{
        let cur_state = statevar.State
        let player = cur_state.activePlayer
        NextPlayer(player).FromGame.WillMove player.PlayerNr
        PrevPlayer(player).FromGame.WillMove player.PlayerNr

        //**********  TO BE REMOVED
        (*
        if localPlayer.IsSome then
            localPlayer.Value.GameToPlayer.AskSimpleTick()
            let! bret = x.wait_for_tick_from statevar [localPlayer.Value.PlayerNr] true
            ()
        *)

        player.FromGame.AskMakeMove()
        let! msg = statevar.Reader cur_state x.MessageReaderTimeout
        match msg with
        |MsgToGame.FromPleyer (MsgPlayerToGame.ReplyMove m) when m.playerNrWho = player.PlayerNr ->
            let card = FullCardDeck.Cards.[m.card]
            let bisvalid = isValidMove cur_state.playerCards.[player.PlayerNr] cur_state.cardsOnDesk card
            if (not bisvalid) then 
                let cur_state = cur_state.Fail("Bad move")
                return {statevar with State = cur_state; Flag = StateVarFlag.Failed "Bad move"}
            else
                let cur_state = cur_state.RemoveCards player [card]
                let cur_state = cur_state.AddToCardsOnDesk card
                (NextPlayer player).FromGame.GotMove player.PlayerNr card
                (PrevPlayer player).FromGame.GotMove player.PlayerNr card
                if cur_state.cycleMoveNr = 2 then
                    return {statevar with State = cur_state; Flag = Return}
                else 
                    let cur_state = 
                        {cur_state with 
                            cycleMoveNr = cur_state.cycleMoveNr + 1;
                            activePlayer = NextPlayer player}
                    return {statevar with State = cur_state; Flag = StateVarFlag.OK}
        |_ -> 
            let cur_state = cur_state.Fail("Bad msg")
            return {statevar with State = cur_state; Flag = StateVarFlag.Failed "Bad msg"}
    }
    
    member private x.three_moves (statevar : GameStateVar) = async{
        let cur_state = statevar.State
        let! cur_state = (cur_state, x.one_move) |> statevar.WithStW |> x.DoInLoop
        if cur_state.gameType = GameType.Aborted then 
            return {statevar with State = cur_state; Flag = StateVarFlag.Failed ""}
        else
            let winnernr = x.WhoWins cur_state.cardsOnDesk cur_state.firstPlayer
            let points = cur_state.cardsOnDesk.Cards |> Seq.sumBy (fun c -> c.Points)
            let cur_state = cur_state.AddPointsForWinner (winnernr, points)

            for player in Players do
                player.FromGame.DoAfter3 
                    cur_state.cardsOnDesk.Cards.[0] 
                    cur_state.cardsOnDesk.Cards.[1] 
                    cur_state.cardsOnDesk.Cards.[2]
                    winnernr
                    points

            let! bwait = x.wait_for_tick_all {statevar with State = cur_state} false
            if not bwait then 
                let cur_state = cur_state.Fail("waittick failed")
                return {statevar with State = cur_state; Flag = StateVarFlag.Failed "waittick failed"}

            elif cur_state.cycleNr = 7 then
                return {statevar with State = cur_state; Flag = StateVarFlag.Return}
            else 
                let cur_state = 
                    {cur_state with 
                        cycleNr = cur_state.cycleNr + 1;
                        cycleMoveNr = 0;
                        firstPlayer = winnernr;
                        activePlayer = Players.[winnernr]
                        cardsOnDesk = CardSet.Empty}
                return {statevar with State = cur_state; Flag = StateVarFlag.OK}
    }

    member private x.eight_cycles (statevar : GameStateVar) = async{
        let cur_state = statevar.State
        let cur_state = 
            {cur_state with 
                activePlayer = Players.[cur_state.firstPlayer];
                cycleMoveNr = 0;
                cycleNr = 0}
        let! cur_state = (cur_state, x.three_moves) |> statevar.WithStW |> x.DoInLoop
        return statevar.WithSt cur_state
    }

    member private x.GetGamePoint (state : GameState) =
        let ret = [|0;0;0|]
        if state.gameType = GameType.Table then
            let xpl = Players |> Seq.maxBy (fun pl -> state.playerPoints.[pl.PlayerNr])
            let xpt = state.playerPoints.[xpl.PlayerNr]
            let xct = Players |> Seq.sumBy (fun pl -> 
                        if state.playerPoints.[pl.PlayerNr] = xpt then 1 else 0)
            if xct = 1 then
                ret.[xpl.PlayerNr] <- -4
                ret.[NextPlayerNr xpl.PlayerNr] <- 2
                ret.[PrevPlayerNr xpl.PlayerNr] <- 2
            ret
        else
            let bigplnr = state.bigPlayer.PlayerNr
            let littplnr1 = state.littlePlayer1.PlayerNr
            let littplnr2 = state.littlePlayer2.PlayerNr
            let bigplpts = state.playerPoints.[bigplnr]
            let littplpts = state.playerPoints.[littplnr1]
            let (ptgig, ptlitt) = 
                if state.gameType = GameType.Zole then
                    if littplpts = 0 then               ( 12, -6)
                    elif littplpts < 31 then            ( 10, -5)
                    elif littplpts < 60 then            (  8, -4)
                    elif bigplpts = 0 then              (-14,  7)
                    elif bigplpts < 31 then             (-12,  6)
                    else                                (-10,  5) //bigplpts < 61
                else
                    if littplpts = 0 then               (  6, -3)
                    elif littplpts < 31 then            (  4, -2)
                    elif littplpts < 60 then            (  2, -1)
                    elif bigplpts = 0 then              ( -8,  4)
                    elif bigplpts < 31 then             ( -6,  3)
                    else                                ( -4,  2) //bigplpts < 61
            ret.[bigplnr] <- ptgig
            ret.[littplnr1] <- ptlitt
            ret.[littplnr2] <- ptlitt
            ret

    member private x.DoMsgGame (statevar : GameStateVar) = async{
        let cur_state = statevar.State
        let fpx = cur_state.firstPlayerX
        let cur_state = 
            {initState with
                firstPlayer = fpx;
                firstPlayerX = fpx}
        
        let! ret_startnewgame = statevar.WithSt cur_state |> x.wait_for_started_all
        let babort = 
            match ret_startnewgame with
            |StartAllResult.OK -> false
            |StartAllResult.Failed -> true
            |StartAllResult.PlayerCanceled plnr ->
                x.OnPlayerStopped plnr
                true
        if babort
        then return {statevar with State = cur_state.Abort()} else

        for player in Players do player.FromGame.StartedNewGame cur_state.firstPlayer

        let shcards = FullCardDeckShuffled()
        let playercards = [for i in 0..2 do yield shcards.GetRange (i*8) 8].ToImmutableArray()
        let burycards = shcards.GetRange 24 2
        for i in 0..2 do Players.[i].FromGame.DoAddCards (playercards.[i])

        let cur_state = 
            {cur_state with
                playerCards = playercards;
                buriedCards = burycards
                activePlayer = Players.[cur_state.firstPlayer]}
        
        let! cur_state = (cur_state, x.who_will_be_big) |> statevar.WithStW |> x.DoInLoop 
        if cur_state.gameType = GameType.Aborted 
        then return statevar.WithSt cur_state else
        
        let! cur_state = (cur_state, x.Bury_cards) |> statevar.WithStW |> x.DoOne
        if cur_state.gameType = GameType.Aborted 
        then return statevar.WithSt cur_state else
            
        let! cur_state = (cur_state, x.eight_cycles) |> statevar.WithStW |> x.DoOne
        if cur_state.gameType = GameType.Aborted 
        then return statevar.WithSt cur_state else

        let (cur_state, bdowait) = 
            if cur_state.gameType = GameType.Zole then
                let points = cur_state.buriedCards.Cards |> Seq.sumBy (fun c -> c.Points)
                let new_points = 
                    [for i in 0..2 do
                        let pts = 
                            if i <> cur_state.bigPlayer.PlayerNr
                            then points
                            else 0
                        yield cur_state.playerPoints.[i] + pts].ToImmutableArray()
                let cur_state = {cur_state with playerPoints = new_points}

                for player in Players do
                    player.FromGame.DoAfterZGame points
                
                (cur_state, true)
            else (cur_state, false)
        
        let! bwait = async{
            if not bdowait then return true
            else return! x.wait_for_tick_all {statevar with State = cur_state} false}
        if not bwait then 
            let cur_state = cur_state.Fail("waittick failed")
            return {statevar with State = cur_state; Flag = StateVarFlag.Failed "waittick failed"}
        else

        let game_points = x.GetGamePoint cur_state
        for i in 0..2 do 
            Players.[i].FromGame.SetPoints game_points.[0] game_points.[1] game_points.[2]
        
        match GameToOwner with
        |Some gow -> 
            {GamePoints.UserIds = [|0;1;2|]; Points = game_points; GamesPlayed = [|1;1;1|]}
            |> gow.AddPoints
        |None -> ()

        if testing then
            ToUI.AddRowToStats game_points.[0] game_points.[1] game_points.[2] localPlayerNr
        
        let! bwait = x.wait_for_tick_all {statevar with State = cur_state} false
        if not bwait then 
            let cur_state = cur_state.Fail("waittick failed")
            return {statevar with State = cur_state; Flag = StateVarFlag.Failed "waittick failed"}
        else

        let fpx = if cur_state.firstPlayerX = 2 then 0 else cur_state.firstPlayerX + 1
        let cur_state = 
            {cur_state with
                firstPlayer = fpx;
                firstPlayerX = fpx}

        return statevar.WithStW (cur_state, x.DoMsgGame)
    }

    member private x.DoMsgInit (statevar : GameStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader statevar.State -1
        let cur_state =
            match msg with
            |MsgToGame.InitGame -> 
                {cur_state with
                    firstPlayer = firstplayer;
                    firstPlayerX = firstplayer}
            |_ -> cur_state.Fail "Bad state"

        if cur_state.gameType = GameType.Aborted then 
            return statevar.WithSt cur_state 
        else 

        for player in Players do player.Start()
        let! cur_state = statevar.WithStW (cur_state, x.DoMsgGame) |> x.DoInLoop
        return statevar.WithSt cur_state
    }

    member private x.MessageToBeSentToRemote (playernr : int) (msg : MsgGameToPlayer) = 
        {|playerNr = playernr; msg = msg|}
        |> MsgPlayerToGame.SendToRemote 
        |> MsgToGame.FromPleyer
        |> x.TakeMessage

    member private x.SendMessageToRemote (playernr : int, msg : MsgGameToPlayer) = 
        match gwToRemote with
        |Some gw ->
            try
                let msg2 = MsgGameMasterToRemote.ToPlayer msg
                MsgDataToRemote(playernr, msg2)
                |> gw.TakeMessage 
                true
            with 
            | exc -> 
                Logger.WriteLine("GameS: SendMessageToRemote failed ex:{0}", exc.Message)
                false
        |None -> 
            Logger.WriteLine("GameS: SendMessageToRemote - No messageGateWay")
            true

    member private x.SendMessageToRemote (msg : MsgDataToRemote) = 
        match gwToRemote with
        |Some gw ->
            try
                gw.TakeMessage msg
                true
            with 
            | exc -> 
                Logger.WriteLine("GameS: SendMessageToRemote failed ex:{0}", exc.Message)
                false
        |None -> 
            Logger.WriteLine("GameS: SendMessageToRemote - No messageGateWay")
            true
    
    member private x.ReceiveMessage (msgdata : MsgDataFromRemote) =
        let msg = MsgToPlayer.FromRemote msgdata.msg
        Players.[msgdata.playerNr].TakeMessage msg
        true
    
    member val private IsGameClosed = false with get,set

    member private x.OnPlayerStopped (playernr : int) = 
        if not x.IsGameClosed then
            Players.[NextPlayerNr playernr].FromGame.PlayerStopped playernr
            Players.[PrevPlayerNr playernr].FromGame.PlayerStopped playernr
            let msg = MsgGameToPlayer.PlayerStopped playernr
            x.SendMessageToRemote(NextPlayerNr playernr, msg) |> ignore
            x.SendMessageToRemote(PrevPlayerNr playernr, msg) |> ignore
            if GameToOwner.IsSome then
                try GameToOwner.Value.GameStopped("")
                finally ()
            x.OnCloseGame()

    member private x.OnCloseGame() =
        if not x.IsGameClosed then
            if GameToOwner.IsSome then
                try GameToOwner.Value.GameClosed()
                finally ()
            try
                let msg = MsgGameToPlayer.GameStop
                for player in Players do 
                    player.FromGame.GameStop()
                    x.SendMessageToRemote(player.PlayerNr, msg) |> ignore
                x.Dispose()
            finally
                x.IsGameClosed <- true    

    member val private MessageReaderDefaultTimeout = -1 with get,set
    member val private MessageReaderTimeout = -1 with get,set
        
    member private x.MsgReader (inbox : MailboxProcessor<MsgToGame>) (state : GameState) (timeout : int) =
        let rec loop() = async{
            if state.gameType = GameType.Aborted then
                x.OnCloseGame();
                return MsgToGame.KillPill
            else
                
            let! ret = Async.Catch (inbox.Receive(timeout))
            let msg = 
                match ret with
                |Choice1Of2 msg -> msg
                |Choice2Of2 _ -> MsgToGame.KillPill

            match msg with
            |MsgToGame.FromPleyer (MsgPlayerToGame.SendToRemote m) ->
                Logger.WriteLine("GameS: FromPleyer[{0}] SendToRemote {1}", m.playerNr, Logger.MsgToStr2(msg, m.msg))
            |MsgToGame.FromRemote m ->
                Logger.WriteLine("GameS: FromRemote[{0}] {1}", m.playerNr, Logger.MsgToStr2(msg, m.msg))
            |_ -> Logger.WriteLine("GameS: {0}", msg.ToString())
            
            try
                match msg with
                |MsgToGame.KillPill -> 
                    x.OnCloseGame();
                    return msg

                |MsgToGame.GameStop -> 
                    x.OnCloseGame()
                    return MsgToGame.KillPill

                |MsgToGame.FromPleyer (PlayerFailed m) -> 
                    x.OnPlayerStopped m.playerNr
                    return MsgToGame.KillPill

                |MsgToGame.FromPleyer (PlayerStopped plnr) -> 
                    x.OnPlayerStopped plnr
                    return MsgToGame.KillPill

                |MsgToGame.GetState channel -> 
                    channel.Reply state
                    return! loop()

                |MsgToGame.FromGameUI m ->
                    MsgToPlayer.FromUser m
                    |> localPlayerX.TakeMessage
                    return! loop()

                |MsgToGame.FromPleyer (MsgPlayerToGame.SendToRemote m) ->
                    if not (x.SendMessageToRemote (m.playerNr, m.msg)) then return MsgToGame.KillPill
                    else return! loop()

                |MsgToGame.FromRemote m ->
                    if not (x.ReceiveMessage m) then return MsgToGame.KillPill
                    else return! loop()

                |_ -> return msg

            with |_ -> return MsgToGame.KillPill
        }
        loop()
       
    member private x.DoInbox(inbox : MailboxProcessor<MsgToGame>) = 
        let rec loop (statevar : GameStateVar) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 new_state -> 
                if new_state.ShouldExit() || new_state.Flag = Return
                then 
                    x.OnCloseGame()
                    return () 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                Logger.WriteLine("GamaS: exc: {0}", exc.Message)
                x.OnCloseGame()
                return () }
        let init_state = initState
        let init_statevar = 
            {Reader = x.MsgReader inbox; 
            State = init_state; 
            Worker = x.DoMsgInit;
            Flag = StateVarFlag.OK}
        loop(init_statevar)

    member private x.DoInLoop(statevar : GameStateVar) = 
        let rec loop (statevar : GameStateVar) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 new_state -> 
                if new_state.ShouldExit() || new_state.Flag = StateVarFlag.Return
                then return new_state.State 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                return statevar.State.Fail(exc.Message)  }
        loop({statevar with Flag = StateVarFlag.OK})

    member private x.DoOne(statevar : GameStateVar) = 
        let doit statevar = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 new_state -> return new_state.State 
            |Choice2Of2 (exc : Exception) -> 
                return statevar.State.Fail(exc.Message)  }
        doit({statevar with Flag = StateVarFlag.OK})
        

    member x.TakeMessage(msg : MsgToGame) = mailBox.Post msg

    member x.TakeMessageSafe(msg : MsgToGame) = 
        if not (x.IsGameClosed && x.IsDisposed) then
            try
                mailBox.Post msg
            finally ()

    member private x.TakeMessage(msg : MsgPlayerToGame) = 
        let msg = MsgToGame.FromPleyer msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgPlayerToGame) = 
        let msg = MsgToGame.FromPleyer msg
        x.TakeMessageSafe msg

    member private x.TakeMessage(msg : MsgUIToX) = 
        let msg = MsgToGame.FromGameUI msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgUIToX) = 
        let msg = MsgToGame.FromGameUI msg
        x.TakeMessageSafe msg


    member val private _IsDisposed = false with get, set
    member x.IsDisposed = x._IsDisposed

    member x.Dispose() =
        if not x.IsDisposed then
            try
                Logger.WriteLine("GameS: Disposing")
                (mailBox :> IDisposable).Dispose()
                for player in Players do player.Dispose()
            finally
                x._IsDisposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()


    member x.GetState() = 
            let msg channel = MsgToGame.GetState channel
            let state = mailBox.PostAndReply(msg)
            (state :?> GameState)
    

    
and private GameFormWrapperForGameServer(gameForm, foverride_showcards) =
    inherit GameFormBareWrapper(gameForm)
    interface IGameForm with
        member x.ShowCards cards cardsondesk firstplayernr localplayernr = 
            foverride_showcards cards cardsondesk firstplayernr localplayernr

