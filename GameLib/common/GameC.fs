namespace GameLib
open System
open System.Collections.Immutable
open System.Diagnostics

type GameStateC = {
    gameType : GameType
    cardsOnDesk : CardSet
    playerCards : CardSet
    buriedCards : CardSet
    cycleNr : int
    cycleMoveNr : int
    firstPlayer : int
    firstPlayerX : int
    gameCount : int
    roundWinsForBig : int
    playerPoints : ImmutableArray<int>
    activePlayerNr : int
    bigPlayerNr : int
    littlePlayerNr1 : int
    littlePlayerNr2 : int
    excmsg : string 
} with
    static member InitState =
        {GameStateC.gameType = GameType.NotSet;
        cardsOnDesk = CardSet.Empty;
        playerCards = CardSet.Empty;
        cycleNr = 0;
        cycleMoveNr = 0;
        firstPlayer = 0;
        firstPlayerX = 0;
        gameCount = 0;
        roundWinsForBig = 0;
        playerPoints = ImmutableArray.Create(0, 0, 0);
        activePlayerNr = -1;
        bigPlayerNr = -1;
        littlePlayerNr1 = -1;
        littlePlayerNr2 = -1;
        buriedCards = CardSet.Empty;
        excmsg = "" }
    member x.Fail (msg : string) = {x with gameType = GameType.Aborted; excmsg = msg}
    member x.FailBadMsg (plnr : int) (expected : string) (got : MsgToClientGame) = 
        let s = sprintf "exp: %A got: [%A]" expected (got.ToString())
        Debug.WriteLine("GameC:[{0}] bad msg {1}", plnr, s)
        {x with gameType = GameType.Aborted; excmsg = s}
    member x.Abort () = {x with gameType = GameType.Aborted}
    member x.AddCards (cards : Card seq) =
        {x with playerCards = x.playerCards.Add(cards)}
    member x.AddToCardsOnDesk (card : Card) = 
        {x with cardsOnDesk = x.cardsOnDesk.Add(card, false)}
    member x.RemoveCards (cards : Card seq) =
        {x with playerCards = x.playerCards.Remove(cards)}
    member x.AddPointsForPlayer(playernr : int, points : int) =
        let pts = 
            [for i in 0..2 do 
                yield x.playerPoints.[i] + if playernr = i then points else 0]
        {x with playerPoints = pts.ToImmutableArray()}
    member x.AddPointsForWinner(winner : int, points : int) =
        let pts = 
            [for i in 0..2 do
                let p = 
                    if winner = i || x.gameType <> GameType.Table && x.bigPlayerNr <> winner
                    then points
                    else 0
                yield x.playerPoints.[i] + p]
        let roundwinsforbig = 
            if x.bigPlayerNr = winner 
            then x.roundWinsForBig + 1 
            else x.roundWinsForBig
        {x with playerPoints = pts.ToImmutableArray(); roundWinsForBig = roundwinsforbig}
        

type GameStateVarC = {
    Reader : GameStateC -> Async<MsgToClientGame>
    State : GameStateC
    Worker : GameStateVarC -> Async<GameStateVarC>
    Flag : StateVarFlag} with
    member x.ReadMsg() = async{return! x.Reader x.State}
    member x.ShouldExit() = x.Flag = StateVarFlag.Failed "" || x.Flag = Return || x.State.gameType = GameType.Aborted
    member x.WithSt state = {x with State = state}
    member x.WithStW (state, worker) = {x with State = state; Worker = worker}

type GameClient(gameform, playernames, firstplayer, localPlayerNr, playerType : PlayerType,
        testing, messageGateWay, gametoowner) as this =
    let mailBox = new AutoCancelAgent<MsgToClientGame>(this.DoInbox)
    let testing : bool = testing
    let gameForm : IPlayerToUI = gameform
    let playerNames : ImmutableArray<string> = playernames
    let gwToGM : IMsgTaker<MsgDataFromRemote> option = messageGateWay
    let GameToOwner : IGameToOwner option = gametoowner

    let ToUI = gameForm

    let _FromGM =
        {new IMsgTaker<MsgGameMasterToRemote> with
            member x.TakeMessage msg = this.TakeMessageSafe msg}

    let _FromPlayer = MPPlayerToGameA(this.TakeMessage) :> IPlayerToGame
    //member val FromUser = UserToPlayerWrapper(fun _-> localPlayer.FromX) :> IUserToX
    let _FromUser = MPGameUIToClient(this.TakeMessage, this.IsValidMove) :> IUserToX
    let _FromGameUI =
        {new IMsgTaker<MsgUIToX> with
            member x.TakeMessage msg = this.TakeMessage msg}

    let NextPlayerNr (pnr : int) = if pnr = 2 then 0 else pnr + 1
    let PrevPlayerNr (pnr : int) = if pnr = 0 then 2 else pnr - 1

    let localPlayer = 
        if playerType = PlayerType.Local 
        then new PlayerLocal(_FromPlayer, ToUI, localPlayerNr, playerNames.[localPlayerNr], Guid.Empty, playerNames) :> IPlayer
        else new PlayerPC(_FromPlayer, localPlayerNr, playerNames.[localPlayerNr], 1200, GameSolver.GetIGS, Guid.Empty) :> IPlayer

    let initState = GameStateC.InitState

    let isValidMove (cards : CardSet) (cardsondesk : CardSet) (card : Card) =
        if cards.Cards.Count = 0 || cardsondesk.Cards.Count >= 3 then false 
        elif cardsondesk.Cards.Count = 0 || cards.Cards.Count = 1 then true
        else
            let first_card = cardsondesk.Cards.[0]
            if cards.HasSuitX(first_card.SuitX)
            then card.SuitX = first_card.SuitX
            else true
    
    let DebugBadMsg (expected : string) (got : MsgToClientGame) = 
        let s = sprintf "exp: %A got: %A" expected (got.ToString())
        Logger.WriteLine("GameC:[{0}] bad msg {1}", localPlayerNr, s)
      
    member x.IsValidMove (card : Card) = localPlayer.FromX.IsValidMove(card)

    member x.InitGame() =
        mailBox.Start()
        let msg = MsgToClientGame.InitGame
        x.TakeMessage msg

    member val FromGM = _FromGM
    member val FromPlayer = _FromPlayer
    member val FromUser = _FromUser
    member val FromGameUI = _FromGameUI

    member private x.WhoWins (cardsondesk : CardSet) (firstplayernr : int) : int =
        let card1 = cardsondesk.Cards.[0]
        let card2 = cardsondesk.Cards.[1]
        let card3 = cardsondesk.Cards.[2]
        let (cardmax1, nr) = if card2.Beats card1 then (card2, 1) else (card1, 0)
        let nr = if card3.Beats cardmax1 then 2 else nr
        let nr = nr + firstplayernr
        let nr = if nr > 2 then nr - 3 else nr
        nr

    member private x.wait_for_tick_all (statevar : GameStateVarC) (simpletick : bool) = async{
        let! msg = statevar.ReadMsg()
        let bret = 
            match msg with
            |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskTick -> not simpletick
            |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskSimpleTick -> simpletick
            |_ -> false
        if(not bret) then return false else
        if simpletick 
        then localPlayer.FromGame.AskSimpleTick()
        else localPlayer.FromGame.AskTick()
        let rec loop (arr : bool array) = async{
            if not (arr |> Array.contains false) then return true else
                let! msg = statevar.ReadMsg()
                if localPlayerNr = 0 then
                    let k =1 in ()
                match msg with
                |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.RepliedTick plnr) when not arr.[plnr] ->
                    arr.[plnr] <- true
                    return! loop arr
                |MsgToClientGame.FromPleyer (MsgPlayerToGame.ReplyTick plnr) when not arr.[plnr] -> 
                    x.SendMessageToGameMaster (MsgPlayerToGame.ReplyTick plnr)
                    arr.[plnr] <- true
                    return! loop arr
                |_ -> return false}
        
        let arr = [|false; false; false|]
        return! loop arr
    }

    member private x.wait_for_tick_from (statevar : GameStateVarC) (playernrs : int list) (simpletick : bool) = async{
        let mutable bret = true
        if playernrs |> List.contains localPlayerNr then
            let! msg = statevar.ReadMsg()
            bret <- 
                match msg with
                |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskTick -> not simpletick
                |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskSimpleTick -> simpletick
                |_ -> DebugBadMsg "MsgAskTick" msg; false
            if bret then 
                if simpletick 
                then localPlayer.FromGame.AskSimpleTick()
                else localPlayer.FromGame.AskTick()
        if(not bret) then return false else
        let rec loop (arr : bool array) = async{
            if not (arr |> Array.contains false) then return true else
                let! msg = statevar.ReadMsg()
                match msg with
                |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.RepliedTick plnr) when not arr.[plnr] ->
                    arr.[plnr] <- true
                    return! loop arr
                |MsgToClientGame.FromPleyer (MsgPlayerToGame.ReplyTick plnr)  when not arr.[plnr]-> 
                    x.SendMessageToGameMaster (MsgPlayerToGame.ReplyTick plnr)
                    arr.[plnr] <- true
                    return! loop arr
                |_ -> 
                    DebugBadMsg "MsgReplyTick" msg
                    return false}
        let arr = [|true; true; true|]
        for k in playernrs do arr.[k] <- false
        return! loop arr    
    }

    member private x.wait_for_localtick (statevar : GameStateVarC) (simpletick : bool) = async{
        let! msg = statevar.ReadMsg()
        let bret = 
            match msg with
            |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskTick -> not simpletick
            |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskSimpleTick -> simpletick
            |_ -> false
        if(not bret) then return false else
        if simpletick 
        then localPlayer.FromGame.AskSimpleTick()
        else localPlayer.FromGame.AskTick()
        let! msg = statevar.ReadMsg()
        match msg with
            |MsgToClientGame.FromPleyer (MsgPlayerToGame.ReplyTick plnr) when plnr = localPlayerNr -> return true;
            |_ -> return false
    }

    //gaidam tikai no lokālā spēlētāja
    member private x.wait_for_started (statevar : GameStateVarC) = async{
        let! msg = statevar.ReadMsg()
        let bret = 
            match msg with
            |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskStartNewGame -> true
            |_ -> false
        if(not bret) then return false else
        localPlayer.FromGame.AskStartNewGame()
        let rec loop (arr : bool array) = async{
            if not (arr |> Array.contains false) then return true else
                let! msg = statevar.ReadMsg()
                match msg with
                |MsgToClientGame.FromPleyer ((MsgPlayerToGame.ReplyStartNewGame m) as msgin) 
                        when not arr.[m.playerNrWho] -> 
                    arr.[m.playerNrWho] <- true
                    if m.playerNrWho = localPlayerNr then
                        x.SendMessageToGameMaster msgin
                    if not m.yesorno then return false
                    else return! loop arr
                |_ -> return false}
        let arr = [|true; true; true|]
        arr.[localPlayerNr] <- false
        return! loop arr
    }

    member private x.who_will_be_big (statevar : GameStateVarC) = async{
        let cur_state = statevar.State
        let! msg = statevar.ReadMsg()
        let bret, bdoask = 
            match msg, cur_state.activePlayerNr = localPlayerNr with
            |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskBeBig, true -> true, true
            |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.AskedBeBig plnr), false 
                    when plnr = cur_state.activePlayerNr -> true, false
            |_ -> false, false
        if not bret then return statevar.WithSt (cur_state.Fail("Bad msg")) else

        if bdoask then
            localPlayer.FromGame.AskBeBig()
        else
            localPlayer.FromGame.AskedBeBig(cur_state.activePlayerNr)
        let! msg = statevar.Reader cur_state

        let msg = 
            match msg, bdoask with
            |MsgToClientGame.FromPleyer ((MsgPlayerToGame.ReplyBeBig m) as msgin), true 
                    when m.playerNrWho = cur_state.activePlayerNr -> 
                x.SendMessageToGameMaster msgin
                Some m
            |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.RepliedBeBig m), false 
                    when m.playerNrWho = cur_state.activePlayerNr -> Some m
            |_ -> None
        if msg.IsNone then return statevar.WithSt (cur_state.Fail("Bad msg")) else
        let msg = msg.Value

        let (cur_state, isdone, pnr) =
            match msg.beBig, msg.zole, cur_state.cycleNr with
            |false, false, 2 -> 
                ({cur_state with gameType = GameType.Table}, true, msg.playerNrWho)
            |false, false, _ -> 
                let cur_state = 
                    {cur_state with 
                        activePlayerNr = NextPlayerNr cur_state.activePlayerNr; 
                        cycleNr = cur_state.cycleNr + 1}
                (cur_state, false, msg.playerNrWho)
            |_, true, _ ->
                let cur_state = 
                    {cur_state with 
                        gameType = GameType.Zole;
                        bigPlayerNr = cur_state.activePlayerNr;
                        littlePlayerNr1 = NextPlayerNr cur_state.activePlayerNr;
                        littlePlayerNr2 = PrevPlayerNr cur_state.activePlayerNr}
                (cur_state, true, msg.playerNrWho)
            |true, _, _ ->
                let cur_state = 
                    {cur_state with 
                        gameType = GameType.Normal;
                        bigPlayerNr = cur_state.activePlayerNr;
                        littlePlayerNr1 = NextPlayerNr cur_state.activePlayerNr;
                        littlePlayerNr2 = PrevPlayerNr cur_state.activePlayerNr;
                        buriedCards = CardSet.Empty}
                (cur_state, true, msg.playerNrWho)

        let ret = statevar.WithSt cur_state
        if cur_state.gameType = GameType.Aborted then return ret else
        
        let mutable bret = true
        if not bdoask then
            localPlayer.FromGame.RepliedBeBig msg.playerNrWho msg.beBig msg.zole
            let! bwait = x.wait_for_localtick ret true
            bret <- bwait 
            if bwait then 
                x.SendMessageToGameMaster (MsgPlayerToGame.ReplyTick localPlayerNr)
        if not bret then return statevar.WithSt (cur_state.Fail("waittick failed")) else
        
        if isdone then 
            bret <- true
            let bigplnr = 
                if cur_state.gameType = GameType.Table
                then cur_state.firstPlayer
                else cur_state.bigPlayerNr
            localPlayer.FromGame.DoTakeGameData cur_state.gameType bigplnr cur_state.firstPlayer
            if cur_state.gameType = GameType.Table then
                let! bwait = x.wait_for_tick_all ret true
                bret <- bwait
            if bret 
            then return {ret with Flag = Return}
            else return {statevar with State = cur_state.Fail("waittick failed"); Flag = StateVarFlag.Failed "waittick failed"}
        else return ret 
    }

    member private x.Bury_cards (statevar : GameStateVarC) = async{
        let mutable cur_state = statevar.State
        if cur_state.gameType = GameType.Table || 
            cur_state.gameType = GameType.Zole
        then
            return statevar.WithSt cur_state
        else

        if cur_state.bigPlayerNr = localPlayerNr then
            let! msg = statevar.Reader cur_state
            let cards = 
                match msg with
                |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.AddCards m) -> Some m
                |_-> None
            if cards.IsNone then 
                cur_state <- cur_state.Fail "Bad msg" 
            else
                let cards = new CardSet(cards.Value.cardIds)
                localPlayer.FromGame.DoAddCards cards
                cur_state <- cur_state.AddCards cards.Cards
                let! msg = statevar.Reader cur_state
                let bgotmsg = 
                    match msg with 
                    |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskBury -> true 
                    |_-> false
                if not bgotmsg then 
                    cur_state <- cur_state.Fail "Bad msg" 
                else
                    localPlayer.FromGame.AskBuryCards()
                    let! msg = statevar.Reader cur_state
                    match msg with
                    |MsgToClientGame.FromPleyer ((MsgPlayerToGame.ReplyBury m) as msgin)
                            when m.playerNrWho = localPlayerNr ->
                        x.SendMessageToGameMaster msgin
                        let card1 = FullCardDeck.Cards.[m.card1]
                        let card2 = FullCardDeck.Cards.[m.card2]
                        let points = card1.Points + card2.Points
                        cur_state <- {cur_state with buriedCards = CardSet.Empty.Add [card1; card2]}
                        cur_state <- cur_state.RemoveCards [card1; card2]
                        cur_state <- cur_state.AddPointsForPlayer (cur_state.bigPlayerNr, points)
                    |_ -> cur_state <- cur_state.Fail("Bad msg")
        else 
            let! msg = statevar.Reader cur_state
            let bgotmsg = 
                match msg with
                |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.AskedBury plnr) 
                        when plnr = cur_state.bigPlayerNr -> true 
                |_-> false
            if not bgotmsg then 
                cur_state <- cur_state.Fail "Bad msg" 
            else
                localPlayer.FromGame.AskedBuryCards cur_state.bigPlayerNr
                let! msg = statevar.Reader cur_state
                let bgotmsg = 
                    match msg with
                    |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.Dug plnr) 
                            when plnr = cur_state.bigPlayerNr -> true 
                    |_-> false
                if not bgotmsg then 
                    cur_state <- cur_state.Fail "Bad msg" 
                else
                    localPlayer.FromGame.RepliedBury cur_state.bigPlayerNr

        if cur_state.gameType = GameType.Aborted 
        then return {statevar with State = cur_state; Flag = StateVarFlag.Failed "game aborted"}
        else return {statevar with State = cur_state; Flag = StateVarFlag.OK}
    }

    member private x.one_move (statevar : GameStateVarC) = async{
        let cur_state = statevar.State
        if cur_state.activePlayerNr = localPlayerNr then
            let! msg = statevar.Reader cur_state
            let bgotmsg = 
                match msg with
                |MsgToClientGame.FromGMToPlayer MsgGameToPlayer.AskMove -> true 
                |_-> false
            if not bgotmsg then 
                DebugBadMsg "MsgAskMove" msg
                return {statevar with State = cur_state.Fail("Bad msg"); Flag = StateVarFlag.Failed "Bad msg"} else
            
            localPlayer.FromGame.AskMakeMove()

            let! msg1 = statevar.Reader cur_state
            let msg = 
                match msg1 with
                |MsgToClientGame.FromPleyer ((MsgPlayerToGame.ReplyMove m) as msgin)
                        when m.playerNrWho = localPlayerNr -> 
                    x.SendMessageToGameMaster msgin
                    Some m
                |_-> None
            if msg.IsNone then 
                DebugBadMsg "MsgReplyMove" msg1
                return {statevar with State = cur_state.Fail("Bad msg"); Flag = StateVarFlag.Failed "Bad msg"} else
            let msg = msg.Value
            let card = FullCardDeck.Cards.[msg.card]
            let bisvalid = isValidMove cur_state.playerCards cur_state.cardsOnDesk card
            if (not bisvalid) then 
                Debug.WriteLine("ivalid move")
                return {statevar with State = cur_state.Fail("ivalid move"); Flag = StateVarFlag.Failed "ivalid move"} else
            let cur_state = cur_state.RemoveCards [card]
            let cur_state = cur_state.AddToCardsOnDesk card
            if cur_state.cycleMoveNr = 2 then
                return {statevar with State = cur_state; Flag = StateVarFlag.Return} else 
            let cur_state = 
                {cur_state with 
                    cycleMoveNr = cur_state.cycleMoveNr + 1;
                    activePlayerNr = NextPlayerNr cur_state.activePlayerNr}
            return {statevar with State = cur_state; Flag = StateVarFlag.OK}
        else
            let! msg = statevar.Reader cur_state
            let bgotmsg = 
                match msg with
                |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.MillMove plnr)
                    when plnr = cur_state.activePlayerNr -> true
                |_ -> false
            if not bgotmsg then 
                DebugBadMsg "MsgWillMove" msg
                return {statevar with State = cur_state.Fail("Bad msg"); Flag = StateVarFlag.Failed "Bad msg"} else
            localPlayer.FromGame.WillMove cur_state.activePlayerNr

            let! msg1 = statevar.Reader cur_state
            let msg = 
                match msg1 with
                |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.GotMove m)
                    when m.playerNrWho = cur_state.activePlayerNr -> Some m
                |_ -> None
            if msg.IsNone then 
                DebugBadMsg "MsgGotMove" msg1
                return {statevar with State = cur_state.Fail("Bad msg"); Flag = StateVarFlag.Failed "Bad msg"} else
            let msg = msg.Value
            let card = FullCardDeck.Cards.[msg.card]
            localPlayer.FromGame.GotMove msg.playerNrWho card
            let cur_state = cur_state.AddToCardsOnDesk card
            if cur_state.cycleMoveNr = 2 then
                return {statevar with State = cur_state; Flag = Return} else 
            let cur_state = 
                {cur_state with 
                    cycleMoveNr = cur_state.cycleMoveNr + 1;
                    activePlayerNr = NextPlayerNr cur_state.activePlayerNr}
            return {statevar with State = cur_state; Flag = StateVarFlag.OK}
    }
    
    member private x.three_moves (statevar : GameStateVarC) = async{
        let cur_state = statevar.State
        let! cur_state = (cur_state, x.one_move) |> statevar.WithStW |> x.DoInLoop
        if cur_state.gameType = GameType.Aborted then 
            return {statevar with State = cur_state; Flag = StateVarFlag.Failed "aborted"}
        else
            let winnernr = x.WhoWins cur_state.cardsOnDesk cur_state.firstPlayer
            let points = cur_state.cardsOnDesk.Cards |> Seq.sumBy (fun c -> c.Points)
            let cur_state = cur_state.AddPointsForWinner (winnernr, points)

            localPlayer.FromGame.DoAfter3 
                cur_state.cardsOnDesk.Cards.[0] 
                cur_state.cardsOnDesk.Cards.[1] 
                cur_state.cardsOnDesk.Cards.[2]
                winnernr
                points

            let! bwait = x.wait_for_tick_all {statevar with State = cur_state} false
            if not bwait then 
                return {statevar with State = cur_state.Fail("waittick failed"); Flag = StateVarFlag.Failed "waittick failed"}
            else
            
            if cur_state.cycleNr = 7 then
                return {statevar with State = cur_state; Flag = StateVarFlag.Return}
            else 
                let cur_state = 
                    {cur_state with 
                        cycleNr = cur_state.cycleNr + 1;
                        cycleMoveNr = 0;
                        firstPlayer = winnernr;
                        activePlayerNr = winnernr
                        cardsOnDesk = CardSet.Empty}
                return {statevar with State = cur_state; Flag = StateVarFlag.OK}
    }

    member private x.eight_cycles (statevar : GameStateVarC) = async{
        let cur_state = statevar.State
        let cur_state = 
            {cur_state with 
                activePlayerNr = cur_state.firstPlayer;
                cycleMoveNr = 0;
                cycleNr = 0}
        let! cur_state = (cur_state, x.three_moves) |> statevar.WithStW |> x.DoInLoop
        return statevar.WithSt cur_state
    }

    member private x.GetGamePoint (state : GameStateC) =
        let ret = [|0;0;0|]
        if state.gameType = GameType.Table then
            let xpt = state.playerPoints |> Seq.max
            let xct = state.playerPoints |> Seq.sumBy (fun pt -> if pt = xpt then 1 else 0)
            let xplnr = state.playerPoints |> Seq.findIndex (fun pt -> pt = xpt)
            if xct = 1 then
                ret.[xplnr] <- -4
                ret.[NextPlayerNr xplnr] <- 2
                ret.[PrevPlayerNr xplnr] <- 2
            ret
        else
            let bigplnr = state.bigPlayerNr
            let littplnr1 = state.littlePlayerNr1
            let littplnr2 = state.littlePlayerNr2
            let bigplpts = state.playerPoints.[bigplnr]
            let littplpts = state.playerPoints.[littplnr1]
            let roundwindforbig = state.roundWinsForBig
            let (ptgig, ptlitt) = 
                if state.gameType = GameType.Zole then
                    if roundwindforbig = 8 then         ( 14, -7)
                    elif littplpts < 31 then            ( 12, -6)
                    elif littplpts < 60 then            ( 10, -5)
                    elif roundwindforbig = 0 then       (-16,  8)
                    elif bigplpts < 31 then             (-14,  7)
                    else                                (-12,  6) //bigplpts < 61
                else
                    if roundwindforbig = 8 then         (  6, -3)
                    elif littplpts < 31 then            (  4, -2)
                    elif littplpts < 60 then            (  2, -1)
                    elif roundwindforbig = 0 then       ( -8,  4)
                    elif bigplpts < 31 then             ( -6,  3)
                    else                                ( -4,  2) //bigplpts < 61            ret.[bigplnr] <- ptgig
            ret.[littplnr1] <- ptlitt
            ret.[littplnr2] <- ptlitt
            ret

    member private x.DoMsgGame (statevar : GameStateVarC) = async{
        let cur_state = statevar.State
        let fpx = cur_state.firstPlayerX
        let cur_state = 
            {initState with
                firstPlayer = fpx;
                firstPlayerX = fpx;
                gameCount = cur_state.gameCount}
        
        let! bstartnewgame = 
            if cur_state.gameCount > 0 then
                statevar.WithSt cur_state |> x.wait_for_started
            else async{return true}
        if not bstartnewgame 
        then return {statevar with State = cur_state.Abort()} else

        localPlayer.FromGame.StartedNewGame cur_state.firstPlayer

        let! msg = statevar.Reader cur_state
        let msg = 
            match msg with
            |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.AddCards m) -> Some m
            |_ -> None
        if msg.IsNone then return {statevar with State = cur_state.Fail("Bad msg"); Flag = StateVarFlag.Failed "Bad msg"} else
        let msg = msg.Value
        let cards =  new CardSet(msg.cardIds)
        localPlayer.FromGame.DoAddCards cards

        let cur_state = 
            {cur_state with
                playerCards = cards;
                buriedCards = CardSet.Empty
                activePlayerNr = cur_state.firstPlayer}
        
        let! cur_state = (cur_state, x.who_will_be_big) |> statevar.WithStW |> x.DoInLoop 
        if cur_state.gameType = GameType.Aborted 
        then return statevar.WithSt cur_state else
        
        let! cur_state = (cur_state, x.Bury_cards) |> statevar.WithStW |> x.DoOne
        if cur_state.gameType = GameType.Aborted 
        then return statevar.WithSt cur_state else
            
        let! cur_state = (cur_state, x.eight_cycles) |> statevar.WithStW |> x.DoOne
        if cur_state.gameType = GameType.Aborted 
        then return statevar.WithSt cur_state else

        let! msg, bdoafterzgame = async{
            if cur_state.gameType = GameType.Zole then
                let! msg = statevar.Reader cur_state
                match msg with
                |MsgToClientGame.FromGMToPlayer (MsgGameToPlayer.AfterZGame pts) -> return Some pts, true
                |_ -> return None, true
            else return None, false
        }
        if bdoafterzgame && msg.IsNone then
            return {statevar with State = cur_state.Fail("Bad msg"); Flag = StateVarFlag.Failed "Bad msg"}
        else
        let cur_state = 
            if bdoafterzgame then
                let points = msg.Value
                let new_points = 
                    cur_state.playerPoints
                    |> Seq.indexed
                    |> Seq.map (fun (i, pts) -> pts + if i = cur_state.bigPlayerNr then 0 else points)
                    |> ImmutableArray.CreateRange
                localPlayer.FromGame.DoAfterZGame points
                {cur_state with playerPoints = new_points}
            else cur_state
        
        let mutable bret = true
        if bdoafterzgame then
            let! bwait = x.wait_for_tick_all {statevar with State = cur_state} false
            bret <- bwait
        if not bret then 
            let cur_state = cur_state.Fail("waittick failed")
            return {statevar with State = cur_state; Flag = StateVarFlag.Failed "waittick failed"}
        else

        let game_points = x.GetGamePoint cur_state
        localPlayer.FromGame.SetPoints game_points.[0] game_points.[1] game_points.[2]
        
        (*
        let! bwait = x.wait_for_tick_all {statevar with State = cur_state} false
        if not bwait then 
            let cur_state = cur_state.Fail("waittick failed")
            return {statevar with State = cur_state; Flag = StateVarFlag.Failed "waittick failed"}
        else
        *)

        let fpx = if cur_state.firstPlayerX = 2 then 0 else cur_state.firstPlayerX + 1
        let cur_state = 
            {cur_state with
                firstPlayer = fpx;
                firstPlayerX = fpx;
                gameCount = cur_state.gameCount + 1}

        return statevar.WithStW (cur_state, x.DoMsgGame)
    }

    member private x.DoMsgInit (statevar : GameStateVarC) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader statevar.State
        let cur_state =
            match msg with
            |MsgToClientGame.InitGame -> 
                {cur_state with
                    firstPlayer = firstplayer;
                    firstPlayerX = firstplayer}
            |_ -> cur_state.Fail "Bad state"

        if cur_state.gameType = GameType.Aborted then 
            return statevar.WithSt cur_state 
        else 

        localPlayer.Start()
        let! cur_state = statevar.WithStW (cur_state, x.DoMsgGame) |> x.DoInLoop
        return statevar.WithSt cur_state
    }

    member private x.SendMessageToGameMaster (msg : MsgPlayerToGame) = 
        match gwToGM with
        |Some gw ->
            try
                MsgDataFromRemote(localPlayerNr, msg)
                |> gw.TakeMessage 
            with 
            | exc -> 
                Logger.WriteLine("GameS: SendMessageToRemote failed ex:{0}", exc.Message)
                raise exc
        |None -> 
            Logger.WriteLine("GameS: SendMessageToRemote - No messageGateWay")

    member private x.ReceiveMessage (msg : MsgGameToPlayer) : bool =
        match msg with
        |MsgGameToPlayer.KillPill 
        |MsgGameToPlayer.GameStop ->
            MsgToPlayer.FromGame msg
            |> localPlayer.TakeMessage
            x.OnCloseGame()
            true
        |_ -> 
            MsgToClientGame.FromGMToPlayer msg
            |> x.TakeMessage 
            true


    member val private _IsGameClosed = false with get,set
    member x.IsGameClosed = x._IsGameClosed

    member private x.OnLocalPlayerStopped() = 
        if not x._IsGameClosed then
            try
                MsgPlayerToGame.PlayerStopped localPlayerNr
                |> x.SendMessageToGameMaster 
                |> ignore
            with _->()
            x.OnCloseGame()

    member private x.OnCloseGame() =
        if not x._IsGameClosed then
            try
                if GameToOwner.IsSome then
                    GameToOwner.Value.GameStopped("")
            with _->()
            x.Dispose()
            localPlayer.Dispose()
            x._IsGameClosed <- true    


    member val private MessageReaderTimeout = -1 with get,set

    member private x.MsgReader (inbox : MailboxProcessor<MsgToClientGame>) (state : GameStateC) =
        let rec loop() = async{
            let! ret = Async.Catch (inbox.Receive(x.MessageReaderTimeout))
            let msg = 
                match ret with
                |Choice1Of2 msg -> msg
                |Choice2Of2 _ -> 
                    Logger.WriteLine("GameC[{0}]: MsgReader timeout", localPlayerNr)
                    MsgToClientGame.KillPill
            
            match msg with
            |MsgToClientGame.FromPleyer (MsgPlayerToGame.SendToRemote m) ->
                Logger.WriteLine("GameC:[{0}] {1}", m.playerNr, Logger.MsgToStr2(msg, m.msg))
            |MsgToClientGame.FromGM m ->
                Debug.WriteLine("GameC:[{0}] {1}", localPlayerNr, msg.ToString())
            |_ -> Logger.WriteLine("GameC:[{0}] {1}", localPlayerNr, msg.ToString())


            try
                match msg with
                |MsgToClientGame.KillPill ->
                    return msg
                
                |MsgToClientGame.GetState channel -> 
                    channel.Reply state
                    return! loop()
                
                |MsgToClientGame.GameStop
                |MsgToClientGame.FromGM MsgGameMasterToRemote.Stop ->
                    x.OnCloseGame()
                    return KillPill
                
                |MsgToClientGame.FromPleyer (MsgPlayerToGame.Failed m) -> 
                    x.OnLocalPlayerStopped()
                    return KillPill
                
                |MsgToClientGame.FromPleyer (MsgPlayerToGame.PlayerStopped plnr) -> 
                    if plnr = localPlayerNr then 
                        x.OnLocalPlayerStopped()
                    else
                        x.OnCloseGame()
                    return KillPill
                
                |MsgToClientGame.FromGMToPlayer ((MsgGameToPlayer.PlayerStopped plnr) as msgin) -> 
                    localPlayer.TakeMessage (MsgToPlayer.FromGame msgin)
                    do! Async.Sleep(2000)
                    x.OnCloseGame()
                    return KillPill

                |MsgToClientGame.FromGameUI m ->
                    MsgToPlayer.FromUser m
                    |> localPlayer.TakeMessage
                    return! loop()
                
                |MsgToClientGame.FromGM (MsgGameMasterToRemote.ToPlayer m) ->
                    if not (x.ReceiveMessage m) then return KillPill
                    else return! loop()
                
                | _ -> return msg
    
            with |_ -> return KillPill
        }
        loop()
       
    member private x.DoInbox(inbox : MailboxProcessor<MsgToClientGame>) = 
        let rec loop (statevar : GameStateVarC) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 new_state -> 
                if new_state.ShouldExit() || new_state.Flag = StateVarFlag.Return
                then 
                    x.OnLocalPlayerStopped()
                    return () 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                x.OnLocalPlayerStopped()
                return () }
        let init_state = initState
        let init_statevar = 
            {Reader = x.MsgReader(inbox); 
            State = init_state; 
            Worker = x.DoMsgInit;
            Flag = StateVarFlag.OK}
        loop(init_statevar)

    member private x.DoInLoop(statevar : GameStateVarC) = 
        let rec loop (statevar : GameStateVarC) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 new_state -> 
                if new_state.ShouldExit() || new_state.Flag = StateVarFlag.Return
                then return new_state.State 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                return statevar.State.Fail(exc.Message)  }
        loop({statevar with Flag = StateVarFlag.OK})

    member private x.DoOne(statevar : GameStateVarC) = 
        let doit statevar = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 new_state -> return new_state.State 
            |Choice2Of2 (exc : Exception) -> 
                return statevar.State.Fail(exc.Message)  }
        doit({statevar with Flag = StateVarFlag.OK})
        
    
    member x.TakeMessage(msg : MsgToClientGame) = x.TakeMessageSafe msg

    member x.TakeMessageSafe(msg : MsgToClientGame) = 
        if not (x.IsGameClosed || x.IsDisposed) then
            try
                mailBox.Post msg
            finally ()
        else Logger.WriteLine("GameC[{0}] closed: <- {1}", localPlayerNr, msg)

    member private x.TakeMessage(msg : MsgGameMasterToRemote) = 
        let msg = MsgToClientGame.FromGM msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgGameMasterToRemote) = 
        let msg = MsgToClientGame.FromGM msg
        x.TakeMessageSafe msg

    member private x.TakeMessage(msg : MsgPlayerToGame) = 
        let msg = MsgToClientGame.FromPleyer msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgPlayerToGame) = 
        let msg = MsgToClientGame.FromPleyer msg
        x.TakeMessageSafe msg

    member private x.TakeMessage(msg : MsgUIToX) = 
        let msg = MsgToClientGame.FromGameUI msg
        x.TakeMessage msg

    member private x.TakeMessageSafe(msg : MsgUIToX) = 
        let msg = MsgToClientGame.FromGameUI msg
        x.TakeMessage msg


    member x.GetState() = 
            let msg channel = GetState channel
            let state = mailBox.PostAndReply(msg)
            (state :?> GameStateC)



    member val private _IsDisposed = false with get, set
    member x.IsDisposed = x._IsDisposed

    member x.Dispose() =
        if not x.IsDisposed then
            try
                (mailBox :> IDisposable).Dispose()
                localPlayer.Dispose()
            finally
                x._IsDisposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()

