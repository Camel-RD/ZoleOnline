namespace GameLib

open System
open System.Collections.Immutable

type BuryValue= {
    pointsNoBury : int
    pointsBuryOneCard : int
    pointsBuryTwoCards : int
    oneCardToBury : int
    firsrCardToBury : int
    secondCardToBury : int
    cardValueList : CardValue list}

type CaseValue= {
    points : int
    card1 : Card
    card2 : Card}

type GameSolver(cards : CardSet, cardsOnDesk : CardSet, goneCards : CardSet) =
    static let rnd = new Random()
    let validMoves =
        if cardsOnDesk.Cards.Count = 0 || cards.Cards.Count = 1 then cards
        elif cardsOnDesk.Cards.Count >= 3 then CardSet.Empty
        else
            let first_card = cardsOnDesk.Cards.[0]
            if cards.HasSuitX(first_card.SuitX)
            then cards.GetBySuitX(first_card.SuitX)
            else cards

    let cards_Club = cards.Cards.FindAll (fun c -> c.SuitX = CardSuit.Club) |> ImmutableList.ToImmutableList
    let cards_Spade = cards.Cards.FindAll (fun c -> c.SuitX = CardSuit.Spade) |> ImmutableList.ToImmutableList
    let cards_Heart = cards.Cards.FindAll (fun c -> c.SuitX = CardSuit.Heart) |> ImmutableList.ToImmutableList
    let cards_Trump = cards.Cards.FindAll (fun c -> c.IsTrump) |> ImmutableList.ToImmutableList
    let validMoves_Club = validMoves.Cards.FindAll (fun c -> c.SuitX = CardSuit.Club) |> ImmutableList.ToImmutableList
    let validMoves_Spade = validMoves.Cards.FindAll (fun c -> c.SuitX = CardSuit.Spade) |> ImmutableList.ToImmutableList
    let validMoves_Heart = validMoves.Cards.FindAll (fun c -> c.SuitX = CardSuit.Heart) |> ImmutableList.ToImmutableList
    let validMoves_Trump = validMoves.Cards.FindAll (fun c -> c.IsTrump) |> ImmutableList.ToImmutableList
    let goneCards_Club = goneCards.Cards.FindAll (fun c -> c.SuitX = CardSuit.Club) |> ImmutableList.ToImmutableList
    let goneCards_Spade = goneCards.Cards.FindAll (fun c -> c.SuitX = CardSuit.Spade) |> ImmutableList.ToImmutableList
    let goneCards_Heart = goneCards.Cards.FindAll (fun c -> c.SuitX = CardSuit.Heart) |> ImmutableList.ToImmutableList
    let goneCards_Trump = goneCards.Cards.FindAll (fun c -> c.IsTrump) |> ImmutableList.ToImmutableList
    let cardsBySuitArr = ImmutableArray.Create(cards_Club, cards_Spade, cards_Heart, cards_Trump)
    let validMovesBySuitArr = ImmutableArray.Create(validMoves_Club, validMoves_Spade, validMoves_Heart, validMoves_Trump)
    let goneCardsBySuitArr = ImmutableArray.Create(goneCards_Club, goneCards_Spade, goneCards_Heart, goneCards_Trump)
    let cardsBySuitMap = Seq.zip CardSuitExt.SeqAll cardsBySuitArr |> Map.ofSeq
    let validMovesBySuitMap = Seq.zip CardSuitExt.SeqAll validMovesBySuitArr |> Map.ofSeq
    let goneCardsBySuitMap = Seq.zip CardSuitExt.SeqAll goneCardsBySuitArr |> Map.ofSeq

    let NewBuryVal (pointsnoBury, pointsBuryonecard, pointsBurytwocards, onecardtoBury, 
                     firsrcardtoBury, secondcardtoBury, cardvaluelist) =
        {pointsNoBury = pointsnoBury; 
        pointsBuryOneCard = pointsBuryonecard;
        pointsBuryTwoCards = pointsBurytwocards;
        oneCardToBury = onecardtoBury;
        firsrCardToBury = firsrcardtoBury;
        secondCardToBury = secondcardtoBury;
        cardValueList = cardvaluelist}

    let Burydata = 
        [|
            NewBuryVal( 20, 15, -1,  1,0,0, [CardValue.Ace])
            NewBuryVal( -3, 16, -1,  1,0,0, [CardValue.V10])
            NewBuryVal(-10,  5, 22,  2,1,2, [CardValue.Ace; CardValue.V10])
            NewBuryVal( -8,  8, -1,  1,0,0, [CardValue.King])
            NewBuryVal( -5,  0, 20,  2,1,2, [CardValue.Ace; CardValue.King])
            NewBuryVal(-15, -5, 19,  1,1,2, [CardValue.V10; CardValue.King])
            NewBuryVal(-20, -8, 18,  1,1,2, [CardValue.Ace; CardValue.V10; CardValue.King])
            NewBuryVal(  2, 14, -1,  1,0,0, [CardValue.V9])
            NewBuryVal( -8,  3, 18,  2,1,2, [CardValue.Ace; CardValue.V9])
            NewBuryVal(-15,  2, 19,  1,1,2, [CardValue.V10; CardValue.V9])
            NewBuryVal(-20, -3, 18,  1,1,2, [CardValue.Ace; CardValue.V10; CardValue.V9])
            NewBuryVal( -5, -1, 13,  1,1,2, [CardValue.King; CardValue.V9])
            NewBuryVal( 15,  5, 10,  1,1,2, [CardValue.Ace; CardValue.King; CardValue.V9])
            NewBuryVal(-20, -8, 10,  1,1,2, [CardValue.V10; CardValue.King; CardValue.V9])
            NewBuryVal(-20, -8, 15,  1,1,2, [CardValue.Ace; CardValue.V10; CardValue.King; CardValue.V9])
        |]

    let Burydatafortrumps = 
        [|
            NewBuryVal(15, 11, -1, 1,0,0, [CardValue.Ace])
            NewBuryVal(14, 13, -1, 1,0,0, [CardValue.V10])
            NewBuryVal(18, 17, 21, 1,1,2, [CardValue.Ace; CardValue.V10])
        |]

    
    let RET (curval : Card option) (nextval : Lazy<Card option>) =
        if curval.IsSome then curval else nextval.Value

    let seq_count (f : 'a -> bool) (sq : seq<'a>) =
        sq |> Seq.sumBy (fun (a:'a) -> if f(a) then 1 else 0)

    let list_last (arr : ImmutableList<'a>) : 'a = arr.[arr.Count - 1]
    let list_lastX (arr : ImmutableList<'a>) : Option<'a> = 
        if arr.Count = 0 then None else Some(arr.[arr.Count - 1])

    static let IGS =
        {new IGameSolver with
            member x.WantBeBig cards =
                let gs = GameSolver(cards, CardSet.Empty, CardSet.Empty)
                gs.SWantBeBig()
            member x.CardsToBury cards =
                let gs = GameSolver(cards, CardSet.Empty, CardSet.Empty)
                gs.SCardsToBury()
            member x.FindMove cards cardsOnDesk goneCards isTableGaem isBig isafterbig =
                let gs = GameSolver(cards, cardsOnDesk, goneCards)
                gs.FindMove isTableGaem isBig isafterbig cardsOnDesk.Cards.Count}

    static member val GetIGS = IGS

    member x.SCardsToBury() : (Card * Card)  =
        let findBuryVal suitx =
            let cs = cardsBySuitMap.[suitx]
            let fk (c : Card) =
                match c.IsTrump, c.Value with 
                |true, CardValue.Ace -> 1 
                |true, CardValue.V10 -> 2 
                |false, CardValue.Ace -> 1 
                |false, CardValue.V10 -> 2 
                |false, CardValue.King -> 4 
                |false, CardValue.V9 -> 8 
                |_ -> 0
            let k = cs |> Seq.sumBy (fun card  -> fk card)
            if k = 0 then None 
            elif suitx = CardSuit.Diamond 
            then Some(Burydatafortrumps.[k-1])
            else Some(Burydata.[k-1]) 

        let Buryvals = [
            for cs in CardSuitExt.SeqAll do
                let dv = findBuryVal cs
                match dv with
                | Some v -> yield (cs, v)
                | None -> ()]
        
        let pointsWhenNoBury = Buryvals |> List.sumBy (fun (_, dv) -> dv.pointsNoBury)
        let twoCardDVs = Buryvals |> List.filter (fun (_,dv) -> dv.firsrCardToBury > 0)
        let cvForTwo = 
            twoCardDVs
            |> List.map (fun (cs, dv) ->
                let card1 = Card.Get cs dv.cardValueList.[dv.firsrCardToBury - 1]
                let card2 = Card.Get cs dv.cardValueList.[dv.secondCardToBury - 1]
                let points = pointsWhenNoBury - dv.pointsNoBury + dv.pointsBuryTwoCards
                {CaseValue.points = points;
                    card1 = card1;
                    card2 = card2})
        let cvOneToOne = [
            for (cs1, dv1) in Buryvals do
            for (cs2, dv2) in Buryvals do
                if dv1 = dv2 then ()
                else 
                    let card1 = Card.Get cs1 dv1.cardValueList.[dv1.oneCardToBury - 1]
                    let card2 = Card.Get cs2 dv2.cardValueList.[dv2.oneCardToBury - 1]
                    let points = pointsWhenNoBury - dv1.pointsNoBury - dv2.pointsNoBury + 
                                    dv1.pointsBuryOneCard + dv2.pointsBuryOneCard
                    yield
                        {CaseValue.points = points;
                            card1 = card1;
                            card2 = card2}]
        let cvTwos = 
            List.append cvForTwo cvOneToOne
            |> List.sortByDescending (fun cv -> cv.points)

        if not cvTwos.IsEmpty then
            let points = cvTwos.[0].points
            let ct = cvTwos |> seq_count (fun cv -> cv.points = points) 
            let ind = rnd.Next(ct)
            let cv = cvTwos.[ind]
            (cv.card1, cv.card2)
        elif Buryvals.IsEmpty then
            let ct = cards_Trump.Count
            (cards_Trump.[ct - 1], cards_Trump.[ct - 2])
        else
            let (cs, dv) = Buryvals.[0] 
            let card1 = Card.Get cs dv.cardValueList.[dv.oneCardToBury - 1]
            let ct = cards_Trump.Count
            let card2 = cards_Trump.[ct - 1]
            (card1, card2)

    
    member x.SWantBeBig() =
        let powertorank = [|9; 9; 7; 7; 7; 7; 6; 6; 4; 4; 3; 2; 2|]
        let ranktrumps = 
            cards_Trump 
            |> Seq.sumBy (fun c -> 
                if c.Power >= 6 && c.Power <= 18 then powertorank.[18 - c.Power] else 0)
        let rankaces = 
            cardsBySuitArr
            |> Seq.take 3
            |> Seq.where (fun cset -> cset.Count = 1 && cset.[0].Value = CardValue.Ace)
            |> Seq.length
            |> (*) 7
        let ret = ranktrumps + rankaces > 25    
        ret

    member x.IHaveCard (suitx : CardSuit) (value : CardValue) =
        cards.Cards
        |> Seq.exists (fun c -> c.SuitX = suitx && c.Value = value)
    
    // goneCards satur arī CardsOnDesk !!!!!!!!
    member x.IsCardGone (suitx : CardSuit) (value : CardValue) =
        goneCards.Cards
        |> Seq.exists (fun c -> c.SuitX = suitx && c.Value = value)
    
    // ja oponents ir lielais, viņš tā kārts var būt norakta,
    // bet neparādīsies iekš goneCards
    member x.TheyCouldHave (suitx : CardSuit) (value : CardValue) =
        not (x.IsCardGone suitx value || x.IHaveCard suitx value)

    // goneCards satur arī CardsOnDesk !!!!!!!!
    member x.TheyCouldHaveTrump () =
        let ct1 = cards.Cards |> seq_count (fun c -> c.IsTrump)
        let ct2 = goneCards.Cards |> seq_count (fun c -> c.IsTrump)
        ct1 + ct2 < 14
    
    member x.TheyCouldHaveTrumpAceOr10 () =
        x.TheyCouldHave CardSuit.Diamond CardValue.Ace || 
        x.TheyCouldHave CardSuit.Diamond CardValue.V10

    member x.Strat0() =
        if validMoves.Cards.Count = 1 
        then Some(validMoves.Cards.[0])
        else None

    member x.StratRnd() =
        Some(validMoves.Cards.[rnd.Next(validMoves.Cards.Count)])

    member x.StratTrumpAce() =
        let card = Card.Get CardSuit.Diamond CardValue.Ace
        if validMoves.Cards.Contains card 
        then Some(card)
        else None
   
    member x.StratTrumpAceOr10() =
        let card1 = Card.Get CardSuit.Diamond CardValue.Ace
        let card2 = Card.Get CardSuit.Diamond CardValue.V10
        if validMoves.Cards.Contains card1 
            then Some(card1)
            elif validMoves.Cards.Contains card2
                then Some(card2)
                else None
    
    member x.StratOne (value : CardValue) =
        let cards = 
            cardsBySuitArr
            |> Seq.take 3
            |> Seq.where (fun cs -> cs.Count = 1)
            |> Seq.map (fun cs -> cs.[0])
            |> Seq.where (fun c -> c.Value = value)
            |> Seq.toArray
        if cards.Length = 0
        then None
        else cards.[rnd.Next(cards.Length)] |> Some

    member x.StratOneAce() = x.StratOne CardValue.Ace
    
    member x.StratOneInKingOr9() =
        let cards = 
            cardsBySuitArr
            |> Seq.take 3
            |> Seq.where (fun cset -> 
                cset.Count = 1 && 
                (cset.[0].Value = CardValue.King ||
                 cset.[0].Value = CardValue.V9))
            |> Seq.concat
            |> Seq.toArray
        if cards.Length = 0
        then None
        else cards.[rnd.Next(cards.Length)] |> Some

    member x.StratAny (value : CardValue) =
        let cards = 
            cards.Cards
            |> Seq.where (fun card -> ((not card.IsTrump) && card.Value = value))
            |> Seq.toArray
        if cards.Length = 0
        then None
        else cards.[rnd.Next(cards.Length)] |> Some
    
    member x.StratAnyAce() = x.StratAny CardValue.Ace
    
    member x.StratAnyKingOr9() =
        let cards = 
            cards.Cards
            |> Seq.where (fun card -> 
                ((not card.IsTrump) && 
                 (card.Value = CardValue.King || card.Value = CardValue.V9)))
            |> Seq.toArray
        if cards.Length = 0
        then None
        else cards.[rnd.Next(cards.Length)] |> Some

    member x.StratAnyAceOr10() =
        let ret = x.StratAnyAce()
        if ret.IsSome 
            then ret
            else x.StratAny CardValue.V10
            
    member x.StratTrump789King() =
        if cards_Trump.IsEmpty then None else
            let c = list_last cards_Trump
            if c.Power >= 5 && c.Power <= 8
                then Some(c)
                else None

    member x.StratTrump789() =
        if cards_Trump.IsEmpty then None else
            let c = list_last cards_Trump
            if c.Power >= 5 && c.Power <= 7
                then Some(c)
                else None

    member x.StratLargerTrump (card : Card) =
        cards_Trump 
        |> Seq.tryFindBack (fun c -> c.Power > card.Power)

    member x.StratLargerTrumpAceOr10 (card : Card) =
        cards_Trump 
        |> Seq.tryFind (fun c -> 
            c.Power > card.Power &&
            (c.Value = CardValue.Ace || c.Value = CardValue.V10))

    member x.StratLargerTrumpNotAceOr10 (card : Card) =
        cards_Trump 
        |> Seq.tryFindBack (fun c -> 
            c.Power > card.Power &&
            c.Value <> CardValue.Ace && c.Value <> CardValue.V10)
    
    member x.StratLargerTrumpThenAce () =
        cards_Trump 
        |> Seq.tryFindBack (fun c -> c.Power > 10)

    member x.StratLargerTrumpAndLargerThenAce (card : Card) =
        cards_Trump 
        |> Seq.tryFindBack (fun c -> c.Power > 10 && c.Power > card.Power)
    
    member x.StratSmalestTrumpNotAceOr10 () =
        cards_Trump 
        |> Seq.tryFindBack (fun c -> 
            c.IsTrump && c.Value <> CardValue.Ace && c.Value <> CardValue.V10)
    
    member x.StratSmallerTrumpAceOr10 (card : Card) =
        cards_Trump 
        |> Seq.tryFind (fun c -> 
            c.Power < card.Power &&
            (c.Value = CardValue.Ace || c.Value = CardValue.V10))
    
    member x.StratSmallerTrump (card : Card) =
        cards_Trump 
        |> Seq.tryFind (fun c -> c.Power < card.Power)

    member x.FirstSomeOrStratRand sq =
        [x.StratRnd()] |> Seq.append sq
        |> Seq.find (fun v -> v.IsSome) 
        |> Option.get
    
    member x.SFindMoveBig2() =
        let cardondesk = cardsOnDesk.Cards.[0]
        let validmovesbysuit = validMovesBySuitMap.[cardondesk.SuitX]
        let gonecardsbysuit = goneCardsBySuitMap.[cardondesk.SuitX]
        
        let sq() = seq{
            if (not cardondesk.IsTrump) && validmovesbysuit.Count > 0 then
                if validmovesbysuit.Count = 2 
                then yield validmovesbysuit.[rnd.Next(2)] |> Some
                else yield validmovesbysuit.[validmovesbysuit.Count - 1] |> Some

            if (not cardondesk.IsTrump) && validmovesbysuit.Count = 0 then
                if gonecardsbysuit.Count = 1 then
                    yield x.StratTrumpAceOr10()
                if cardondesk.Points < 10 then 
                    yield x.StratOneInKingOr9()
                if cardondesk.Points >= 10 && validMoves_Trump.Count > 0 then 
                    yield validMoves_Trump.[0] |> Some
                yield x.StratTrump789King()
                yield x.StratOneInKingOr9()

            if cardondesk.IsTrump && cards_Trump.Count > 0 then
                if cardondesk.Points >= 10 then 
                    yield cards_Trump.[0] |> Some
                if cardondesk.Power > 11 && cards.Cards.Count >= 5 then 
                    yield x.StratTrump789()
                yield x.StratLargerTrumpAndLargerThenAce(cardondesk)
                yield list_lastX cards_Trump

            if cardondesk.IsTrump && cards_Trump.Count = 0 then 
                yield x.StratOneInKingOr9()
                yield x.StratAnyKingOr9()
        }
        sq() |> x.FirstSomeOrStratRand
                
    member x.SFindMoveBig1() =
        let sq() = seq{
            yield x.StratOneAce()
            yield x.StratAnyAce()
            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> cs.Count > 0)
                |> Seq.map (fun cs -> cs.[cs.Count - 1])
                |> Seq.where (fun c -> c.Value = CardValue.V9 || c.Value = CardValue.King)
                |> Seq.where (fun c -> 
                    not (x.TheyCouldHave c.SuitX CardValue.Ace || 
                            x.TheyCouldHave c.SuitX CardValue.V10))
                |> Seq.toArray
            if cards1.Length > 0 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some
            if cards.Cards.Count < 4 && cards_Trump.Count > 0 && rnd.Next(10) < 6 then 
                yield cards_Trump.[0] |> Some
            if x.TheyCouldHaveTrumpAceOr10() then 
                yield x.StratLargerTrumpThenAce()
            yield x.StratTrump789() 
            yield x.StratLargerTrumpThenAce()
        }
        sq() |> x.FirstSomeOrStratRand


    member x.SFindMoveBig3() =
        let cardondesk1 = cardsOnDesk.Cards.[0]
        let cardondesk2 = cardsOnDesk.Cards.[1]
        let validmovesbysuit = validMovesBySuitMap.[cardondesk1.SuitX]
        let gonecardsbysuit = goneCardsBySuitMap.[cardondesk1.SuitX]
        let cardondeskmax = 
            if (cardondesk1.SuitX = cardondesk2.SuitX || cardondesk2.IsTrump) && 
                cardondesk1.Power < cardondesk2.Power
            then cardondesk2
            else cardondesk1
        let pointsondesk = cardondesk1.Points + cardondesk2.Points
        
        let sq() = seq{
            if (not cardondesk1.IsTrump) && validmovesbysuit.Count > 0 then
                if validmovesbysuit.[0].Power > cardondeskmax.Power
                then yield validmovesbysuit.[0] |> Some
                else yield list_last validmovesbysuit |> Some

            if (not cardondesk1.IsTrump) && validmovesbysuit.Count = 0 then
                if pointsondesk < 7 then 
                    yield x.StratOneInKingOr9()
                yield x.StratLargerTrumpAceOr10(cardondeskmax)
                yield x.StratLargerTrump(cardondeskmax)
                yield x.StratAnyKingOr9()
                yield x.StratTrump789King()

            if cardondesk1.IsTrump && cards_Trump.Count > 0 then
                yield x.StratLargerTrumpAceOr10(cardondeskmax)
                if pointsondesk > 7 then
                    yield x.StratLargerTrump(cardondeskmax)
                if pointsondesk < 8 then
                    yield list_last cards_Trump |> Some
                yield x.StratLargerTrump(cardondeskmax)
                yield x.StratTrump789King()
                yield list_last cards_Trump |> Some
            
            if cardondesk1.IsTrump && cards_Trump.Count = 0 then 
                yield x.StratOneInKingOr9()
                yield x.StratAnyKingOr9()
            }
        sq() |> x.FirstSomeOrStratRand

    
    member x.SFindMoveLittle3AfterBig() = x.SFindMoveLittle3 true
    member x.SFindMoveLittle3AfterLittle() = x.SFindMoveLittle3 false

    member x.SFindMoveLittle3 (isafterbig : bool) =
        let cardondesk1 = cardsOnDesk.Cards.[0]
        let cardondesk2 = cardsOnDesk.Cards.[1]
        let validmovesbysuit = validMovesBySuitMap.[cardondesk1.SuitX]
        let gonecardsbysuit = goneCardsBySuitMap.[cardondesk1.SuitX]
        let cardondeskmax = 
            if (cardondesk1.SuitX = cardondesk2.SuitX || cardondesk2.IsTrump) && 
                cardondesk1.Power < cardondesk2.Power
            then cardondesk2
            else cardondesk1
        let littlewin = 
            if cardondeskmax = cardondesk1 
            then isafterbig
            else not isafterbig

        let pointsondesk = cardondesk1.Points + cardondesk2.Points

        let sq() = seq{
            if (not cardondesk1.IsTrump) && validmovesbysuit.Count > 0 then
                if littlewin || validmovesbysuit.[0].Power > cardondeskmax.Power
                then yield validmovesbysuit.[0] |> Some
                else yield list_last validmovesbysuit |> Some
            if (not cardondesk1.IsTrump) && validmovesbysuit.Count = 0 then
                if littlewin then
                    yield x.StratAnyAceOr10()
                    yield x.StratTrumpAceOr10()
                    yield x.StratOneInKingOr9()
                    yield x.StratAny CardValue.King
                    yield x.StratAny CardValue.V9
                    yield list_lastX cards_Trump
                else
                    yield  x.StratLargerTrumpAceOr10(cardondeskmax)
                    yield x.StratLargerTrump(cardondeskmax)
                    yield x.StratAny CardValue.V9
                    yield x.StratAny CardValue.King
                    yield list_lastX cards_Trump
            if cardondesk1.IsTrump && cards_Trump.Count > 0 then
                if littlewin then
                    yield x.StratTrumpAceOr10()
                else 
                    yield  x.StratLargerTrumpAceOr10(cardondeskmax)
                    if pointsondesk > 10 then
                        yield x.StratLargerTrump(cardondeskmax)
                if pointsondesk < 7 then
                    yield x.StratTrump789()
                if rnd.Next(10) < 4 then
                    yield x.StratLargerTrump(cardondeskmax)
                yield x.StratSmalestTrumpNotAceOr10()
                yield list_last cards_Trump |> Some
            if cardondesk1.IsTrump && cards_Trump.Count = 0 then 
                if littlewin then
                    yield x.StratAnyAceOr10()
                    yield x.StratAny CardValue.King
                else 
                    yield x.StratOneInKingOr9()
                    yield x.StratAnyKingOr9()
                yield list_last validMoves.Cards |> Some
        }
        sq() |> x.FirstSomeOrStratRand


    member x.SFindMoveLittle2AfterBig () =
        let cardondesk = cardsOnDesk.Cards.[0]
        let validmovesbysuit = validMovesBySuitMap.[cardondesk.SuitX]
        let gonecardsbysuit = goneCardsBySuitMap.[cardondesk.SuitX]
        let pointsondesk = cardondesk.Points

        let sq() = seq{
            if (not cardondesk.IsTrump) && validmovesbysuit.Count > 0 then
                if cardondesk.Power < validmovesbysuit.[0].Power then 
                    yield validmovesbysuit.[0] |> Some
                if validmovesbysuit.Count + gonecardsbysuit.Count = 4 then 
                    yield validmovesbysuit.[0] |> Some
                yield validmovesbysuit.[0] |> Some
            if (not cardondesk.IsTrump) && validmovesbysuit.Count = 0 then
                if gonecardsbysuit.Count = 0 then
                    yield x.StratTrumpAceOr10()
                if cardondesk.Value = CardValue.V9 || cardondesk.Value = CardValue.King then 
                    yield x.StratAnyAceOr10()
                if rnd.Next(10) < 5 then 
                    yield list_lastX validMoves_Trump
                    yield x.StratAnyAceOr10()
                yield list_lastX validMoves_Trump
                yield x.StratOneInKingOr9()
            if cardondesk.IsTrump && cards_Trump.Count > 0 then
                yield  x.StratLargerTrumpAceOr10(cardondesk)
                if cardondesk.Power < 14  then 
                    yield x.StratTrumpAceOr10()
                if x.TheyCouldHaveTrumpAceOr10() && rnd.Next(10) < 8 ||
                    rnd.Next(10) < 3 then 
                        yield x.StratLargerTrump(cardondesk)
                yield x.StratSmalestTrumpNotAceOr10()
                yield list_lastX cards_Trump
            if cardondesk.IsTrump && cards_Trump.Count = 0 then 
                yield x.StratAnyAceOr10()
                yield x.StratOneInKingOr9()
                yield x.StratAnyKingOr9()
        }
        sq() |> x.FirstSomeOrStratRand


    member x.SFindMoveLittle2AfterLittle () =
        let cardondesk = cardsOnDesk.Cards.[0]
        let validmovesbysuit = validMovesBySuitMap.[cardondesk.SuitX]
        let gonecardsbysuit = goneCardsBySuitMap.[cardondesk.SuitX]
        let pointsondesk = cardondesk.Points

        let sq() = seq{
            if (not cardondesk.IsTrump) && validmovesbysuit.Count > 0 then
                if validmovesbysuit.[0].Value = CardValue.Ace &&
                    gonecardsbysuit.Count = 1 &&    // kārts uz galda
                    validmovesbysuit.Count = 2 &&   // ja validmovesbysuit.Count = 1, tad tā tiek automātiski paņemta
                    rnd.Next(10) < 4
                then yield validmovesbysuit.[0] |> Some
                if cardondesk.Value = CardValue.Ace &&
                    gonecardsbysuit.Count = 1 &&    // kārts uz galda
                    validmovesbysuit.Count = 2 &&   // ja validmovesbysuit.Count = 1, tad tā tiek automātiski paņemta
                    rnd.Next(10) < 4
                then yield validmovesbysuit.[0] |> Some
                yield list_lastX validmovesbysuit
            if (not cardondesk.IsTrump) && validmovesbysuit.Count = 0 then
                if cardondesk.Value = CardValue.Ace &&
                    gonecardsbysuit.Count = 1 &&    // kārts uz galda
                    rnd.Next(10) < 4
                then yield x.StratAnyAceOr10()
                if (cardondesk.Value = CardValue.V10 || 
                    cardondesk.Value = CardValue.Ace) &&
                    cards_Trump.Count > 0
                then 
                    let strongest_trump = cards_Trump.[0]
                    if strongest_trump.Power = 18 ||   // kreica dāma
                        strongest_trump.Power > 15 && rnd.Next(10) < 7
                        then yield strongest_trump |> Some
                    if gonecardsbysuit.Count = 1 && // kārts uz galda
                        rnd.Next(10) < 4
                        then yield x.StratTrumpAceOr10()
                    if rnd.Next(10) < 4 then 
                        yield x.StratLargerTrumpThenAce()
                    if rnd.Next(10) < 4 then 
                        yield strongest_trump |> Some
                if rnd.Next(10) < 3 then
                    yield x.StratOneInKingOr9()
                    yield x.StratAnyKingOr9()
                    if not (x.TheyCouldHaveTrumpAceOr10()) then
                        yield x.StratTrump789()
                yield x.StratOneInKingOr9()
                yield x.StratAnyKingOr9()
                yield x.StratLargerTrumpThenAce()
            if cardondesk.IsTrump && cards_Trump.Count > 0 then
                if cardondesk.Value = CardValue.V10 || cardondesk.Value = CardValue.Ace then
                    yield cards_Trump.[0] |> Some
                yield x.StratLargerTrumpThenAce()
                yield list_lastX cards_Trump
            if cardondesk.IsTrump && cards_Trump.Count = 0 then 
                if cardondesk.Power > 15 then
                    yield x.StratAnyAceOr10()
                    yield x.StratAny CardValue.King
                yield x.StratOneInKingOr9()
                yield x.StratAnyKingOr9()
                yield list_lastX validMoves.Cards
        }
        sq() |> x.FirstSomeOrStratRand


    member x.SFindMoveLittle1BeforeBig() =
        let sq() = seq{
            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> cs.Count = 1)
                |> Seq.map (fun cs -> cs.[0])
                |> Seq.where (fun c -> c.Value = CardValue.Ace)
                |> Seq.where (fun c -> goneCardsBySuitMap.[c.SuitX].Count = 0)
                |> Seq.toArray
            if cards1.Length > 0 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some
            
            yield x.StratOneAce()
            yield x.StratAnyAce()

            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> cs.Count = 1)
                |> Seq.map (fun cs -> cs.[0])
                |> Seq.where (fun c -> goneCardsBySuitMap.[c.SuitX].Count = 0)
                |> Seq.toArray
            if cards1.Length > 0 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some
            
            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> cs.Count > 1)
                |> Seq.map (fun cs -> cs.[cs.Count - 1])
                |> Seq.where (fun c -> c.Value = CardValue.King || c.Value = CardValue.V9)
                |> Seq.where (fun c -> goneCardsBySuitMap.[c.SuitX].Count = 0)
                |> Seq.where (fun c -> 
                    not (x.TheyCouldHave c.SuitX CardValue.Ace || 
                         x.TheyCouldHave c.SuitX CardValue.V10))
                |> Seq.toArray
            if cards1.Length > 0 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some
            yield x.StratAnyKingOr9()
            yield x.StratAnyAceOr10()
            yield x.StratTrump789()
            yield x.StratLargerTrumpThenAce()
        }
        sq() |> x.FirstSomeOrStratRand


    member x.SFindMoveLittle1BeforeLittle() =
        let sq() = seq{
            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> cs.Count = 1 || cs.Count = 2)
                |> Seq.map (fun cs -> cs.[0])
                |> Seq.where (fun c -> c.Value = CardValue.Ace)
                |> Seq.where (fun c -> goneCardsBySuitMap.[c.SuitX].Count = 0)
                |> Seq.toArray
            if cards1.Length > 0 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some

            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> cs.Count = 1)
                |> Seq.map (fun cs -> cs.[0])
                |> Seq.where (fun c -> goneCardsBySuitMap.[c.SuitX].Count = 0)
                |> Seq.toArray
            if cards1.Length > 0 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some

            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> cs.Count > 1)
                |> Seq.map (fun cs -> cs.[cs.Count - 1])
                |> Seq.where (fun c -> c.Value = CardValue.King || c.Value = CardValue.V9)
                |> Seq.where (fun c -> goneCardsBySuitMap.[c.SuitX].Count = 0)
                |> Seq.where (fun c -> 
                    not (x.TheyCouldHave c.SuitX CardValue.Ace || 
                         x.TheyCouldHave c.SuitX CardValue.V10))
                |> Seq.toArray
            if cards1.Length > 0 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some
            yield x.StratAnyKingOr9()
            yield x.StratTrump789()
            yield x.StratAnyAceOr10()
            yield x.StratLargerTrumpThenAce()
        }
        sq() |> x.FirstSomeOrStratRand



    member x.SFindMoveG3() =
        let cardondesk1 = cardsOnDesk.Cards.[0]
        let cardondesk2 = cardsOnDesk.Cards.[1]
        let validmovesbysuit = validMovesBySuitMap.[cardondesk1.SuitX]
        let gonecardsbysuit = goneCardsBySuitMap.[cardondesk1.SuitX]
        let cardondeskmax = 
            if (cardondesk1.SuitX = cardondesk2.SuitX || cardondesk2.IsTrump) && 
                cardondesk1.Power < cardondesk2.Power
            then cardondesk2
            else cardondesk1
        let pointsondesk = cardondesk1.Points + cardondesk2.Points

        let sq() = seq{
            if (not cardondesk1.IsTrump) && validmovesbysuit.Count > 0 then
                if validmovesbysuit.[0].Power < cardondeskmax.Power
                then yield validmovesbysuit.[0] |> Some
                else yield list_last validmovesbysuit |> Some
            if (not cardondesk1.IsTrump) && validmovesbysuit.Count = 0 then
                yield x.StratOneAce()
                yield x.StratOne CardValue.V10
                yield x.StratAnyAce()
                yield x.StratAnyAceOr10()
                if pointsondesk < 7 && cards_Trump.Count > 0 &&
                    cards_Trump.[0].Value <> CardValue.Ace &&
                    cards_Trump.[0].Value <> CardValue.V10
                then yield cards_Trump.[0] |> Some
                yield x.StratAny CardValue.King
                yield x.StratAny CardValue.V9
                yield cards_Trump.[0] |> Some
            if cardondesk1.IsTrump && cards_Trump.Count > 0 then
                yield  x.StratSmallerTrumpAceOr10(cardondeskmax)
                if pointsondesk < 7 &&
                    cards_Trump.[0].Value <> CardValue.Ace &&
                    cards_Trump.[0].Value <> CardValue.V10
                then yield cards_Trump.[0] |> Some
                yield x.StratSmallerTrump(cardondeskmax)
                yield cards_Trump.[0] |> Some
            if cardondesk1.IsTrump && cards_Trump.Count = 0 then 
                yield  x.StratOneAce()
                yield x.StratOne CardValue.V10
                yield x.StratAnyAce()
                yield x.StratAnyAceOr10()
                yield x.StratAny CardValue.King
                yield x.StratAny CardValue.V9
        }
        sq() |> x.FirstSomeOrStratRand


    member x.SFindMoveG2() =
        let cardondesk = cardsOnDesk.Cards.[0]
        let validmovesbysuit = validMovesBySuitMap.[cardondesk.SuitX]
        let gonecardsbysuit = goneCardsBySuitMap.[cardondesk.SuitX]
        let pointsondesk = cardondesk.Points

        let sq() = seq{
            if (not cardondesk.IsTrump) && validmovesbysuit.Count > 0 then
                if validmovesbysuit.[0].Power < cardondesk.Power then 
                    yield validmovesbysuit.[0] |> Some
                if validmovesbysuit.[0].Power > cardondesk.Power &&
                    (gonecardsbysuit.Count + validmovesbysuit.Count = 0) &&
                    rnd.Next(10) < 6
                then yield validmovesbysuit.[0] |> Some
                else yield list_last validmovesbysuit |> Some
            if (not cardondesk.IsTrump) && validmovesbysuit.Count = 0 then
                yield x.StratOneAce()
                yield x.StratOne CardValue.V10
                yield x.StratAnyAce()
                yield x.StratAnyAceOr10()
                if pointsondesk < 7 && cards_Trump.Count > 0 &&
                    cards_Trump.[0].Value <> CardValue.Ace &&
                    cards_Trump.[0].Value <> CardValue.V10
                then yield cards_Trump.[0] |> Some
                yield x.StratAny CardValue.King
                yield x.StratAny CardValue.V9
            if cardondesk.IsTrump && cards_Trump.Count > 0 then
                yield x.StratSmallerTrumpAceOr10(cardondesk)
                yield x.StratSmallerTrump(cardondesk)
                if cards.Cards.Count > 4 &&
                    (not (x.TheyCouldHaveTrumpAceOr10())) &&
                    rnd.Next(10) < 7
                    then yield cards_Trump.[0] |> Some
                let fcard = list_last cards_Trump
                if fcard.Power - cardondesk.Power = 1 &&
                    fcard.Power < 14 && 
                    rnd.Next(10) < 6 ||
                    rnd.Next(10) < 3
                then yield fcard |> Some
                let fcards = 
                    cards_Trump
                    |> Seq.where (fun c -> 
                        c.Value <> CardValue.Ace && c.Value <> CardValue.V10)
                    |> Seq.toArray
                if fcards.Length > 4 && rnd.Next(10) < 4 then
                    yield fcards.[rnd.Next(3)+2] |> Some
                elif fcards.Length > 2 && rnd.Next(10) < 4 then
                    yield fcards.[rnd.Next(2)+1] |> Some
                yield list_lastX cards_Trump
            if cardondesk.IsTrump && cards_Trump.Count = 0 then 
                yield x.StratOneAce()
                yield x.StratOne CardValue.V10
                yield x.StratAnyAce()
                yield x.StratAnyAceOr10()
                yield x.StratAny CardValue.King
        }
        sq() |> x.FirstSomeOrStratRand


    member x.SFindMoveG1() =
        let sq() = seq{
            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> 
                    cs.Count > 0 &&
                    cs.Count + goneCardsBySuitMap.[cs.[0].SuitX].Count < 3)
                |> Seq.map (fun cs -> cs.[0])
                |> Seq.where (fun c -> c.Value = CardValue.King || c.Value = CardValue.V9)
                |> Seq.toArray
            if cards1.Length > 0 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some

            let cards1 = 
                cardsBySuitArr
                |> Seq.take 3
                |> Seq.where (fun cs -> cs.Count > 1)
                |> Seq.map (fun cs -> cs.[0])
                |> Seq.where (fun c -> c.Value = CardValue.V10)
                |> Seq.where (fun c -> x.TheyCouldHave c.SuitX CardValue.Ace)
                |> Seq.toArray
            if cards1.Length > 0 && rnd.Next() < 7 then 
                yield cards1.[rnd.Next(cards1.Length)] |> Some

            if cards.Cards.Count - cards_Trump.Count > cards_Trump.Count then
                yield x.StratAnyKingOr9()
                yield x.StratAnyAceOr10()
            else 
                yield x.StratSmalestTrumpNotAceOr10()
        }
        sq() |> x.FirstSomeOrStratRand

    member x.FindMove isTableGaem isBig isafterbig cardsondeskcount =
        let card0 = x.Strat0()
        let card = 
            if card0.IsSome 
            then card0.Value 
            else
                match isTableGaem, isBig, isafterbig, cardsondeskcount with
                |true, _, _, 0 -> x.SFindMoveG1()
                |true, _, _, 1 -> x.SFindMoveG2()
                |true, _, _, _ -> x.SFindMoveG3()
                |false, true, _, 0 -> x.SFindMoveBig1()
                |false, true, _, 1 -> x.SFindMoveBig2()
                |false, true, _, _ -> x.SFindMoveBig3()
                |false, false, false, 0 -> x.SFindMoveLittle1BeforeBig()
                |false, false, true, 0 -> x.SFindMoveLittle1BeforeLittle()
                |false, false, false, 1 -> x.SFindMoveLittle2AfterLittle()
                |false, false, true, 1 -> x.SFindMoveLittle2AfterBig()
                |false, false, false, 2 -> x.SFindMoveLittle3AfterLittle()
                |_ -> x.SFindMoveLittle3AfterBig()
        card
    



