namespace GameLib

open System
open System.Collections.Immutable
open System.Runtime.CompilerServices

type CardSuit = 
    | Club = 0 | Spade = 1 | Diamond = 2 | Heart = 3

type CardValue = 
    | V7 = 0 | V8 = 1 | V9 = 2 | V10 = 3 | Ace = 4 | Jack = 5 | Queen = 6 | King = 7 | None = 8

[<Extension>]
type CardSuitExt =
    [<Extension>]
    static member ToInt(cardsuit : CardSuit) =
        match cardsuit with
        | CardSuit.Club -> 0 
        | CardSuit.Spade -> 1 
        | CardSuit.Diamond -> 2 
        | CardSuit.Heart -> 3
        | _ -> invalidArg "cardsuit" "Value not defined"

    [<Extension>]
    static member FromInt(value : int) : Option<CardSuit> = 
        match value with
        | 0 -> Some CardSuit.Club
        | 1 -> Some CardSuit.Spade
        | 2 -> Some CardSuit.Diamond
        | 3 -> Some CardSuit.Heart
        | _ -> None

    static member SeqNoTrump = seq{yield CardSuit.Club; yield CardSuit.Spade; yield CardSuit.Heart}
    static member SeqAll = seq{yield CardSuit.Club; yield CardSuit.Spade; yield CardSuit.Heart; yield CardSuit.Diamond}

[<Extension>]
type CardValueExt() =
    static let cardValue_arr = 
        ImmutableArray.Create(
            CardValue.V7, CardValue.V8, CardValue.V9, CardValue.V10, CardValue.Ace, 
            CardValue.Jack, CardValue.Queen, CardValue.King, CardValue.None)
    static let cardValue_maptoint = Map[(CardValue.V7,0); (CardValue.V8,1); (CardValue.V9,2); 
        (CardValue.V10,3); (CardValue.Ace,4); (CardValue.Jack,5); (CardValue.Queen,6); 
        (CardValue.King,7); (CardValue.None,8)]

    [<Extension>]
    static member ToInt(x : CardValue) = cardValue_maptoint.[x]

    [<Extension>]
    static member FromInt (value : int) : Option<CardValue> = 
        if value < 0 || value > 8 then Option.None 
        else Some(cardValue_arr.[value])

    [<Extension>]
    static member FromInt2 (value : int) : CardValue = 
        if value < 0 || value > 8 then invalidArg "value" "Value out of range"
        else cardValue_arr.[value]


type Card private (suit, value, power, points, indexInFullDeck) = 
    static let fullDeck : ImmutableList<Card> =
        let seqAllCards() = 
            let sq = seq{
                yield (CardSuit.Heart, CardValue.V9, 1, 0)
                yield (CardSuit.Heart, CardValue.King, 2, 4)
                yield (CardSuit.Heart, CardValue.V10, 3, 10)
                yield (CardSuit.Heart, CardValue.Ace, 4, 11)

                yield (CardSuit.Spade, CardValue.V9, 1, 0)
                yield (CardSuit.Spade, CardValue.King, 2, 4)
                yield (CardSuit.Spade, CardValue.V10, 3, 10)
                yield (CardSuit.Spade, CardValue.Ace, 4, 11)

                yield (CardSuit.Club, CardValue.V9, 1, 0)
                yield (CardSuit.Club, CardValue.King, 2, 4)
                yield (CardSuit.Club, CardValue.V10, 3, 10)
                yield (CardSuit.Club, CardValue.Ace, 4, 11)
    
                yield (CardSuit.Diamond, CardValue.V7, 5, 0)
                yield (CardSuit.Diamond, CardValue.V8, 6, 0)
                yield (CardSuit.Diamond, CardValue.V9, 7, 0)
                yield (CardSuit.Diamond, CardValue.King, 8, 4)
                yield (CardSuit.Diamond, CardValue.V10, 9, 10)
                yield (CardSuit.Diamond, CardValue.Ace, 10, 11)

                yield (CardSuit.Diamond, CardValue.Jack, 11, 2)
                yield (CardSuit.Heart, CardValue.Jack, 12, 2)
                yield (CardSuit.Spade, CardValue.Jack, 13, 2)
                yield (CardSuit.Club, CardValue.Jack, 14, 2)

                yield (CardSuit.Diamond, CardValue.Queen, 15, 3)
                yield (CardSuit.Heart, CardValue.Queen, 16, 3)
                yield (CardSuit.Spade, CardValue.Queen, 17, 3)
                yield (CardSuit.Club, CardValue.Queen, 18, 3)
            }
            sq 
            |> Seq.indexed 
            |> Seq.map (fun (i, (suit, value, power, points)) ->
                Card(suit, value, power, points, i))
        ImmutableList.CreateRange(seqAllCards())

    member val Suit : CardSuit = suit
    member val Value : CardValue = value
    member val Power : int = power
    member val Points : int = points
    member val IndexInFullDeck : int = indexInFullDeck
    member val IsTrump : bool =
        suit = CardSuit.Diamond || value = CardValue.Jack || value = CardValue.Queen
    member val SuitX : CardSuit =
        if suit = CardSuit.Diamond || value = CardValue.Jack || value = CardValue.Queen 
        then CardSuit.Diamond else suit
    
    static member FullDeck = fullDeck

    static member Get suit value = 
        let fr = fullDeck |> Seq.tryFind (fun c -> c.Value = value && c.Suit = suit)
        match fr with
        | Some card -> card
        | None -> invalidArg "suit, value" "Card not found"
    
    member x.Beats (card2 : Card) =
        if x.IsTrump 
        then x.Power > card2.Power
        elif card2.IsTrump then false
        elif x.Suit <> card2.Suit then false
        else x.Power > card2.Power

    override x.ToString() = 
        sprintf "Card:(%A - %A ind:%i)" x.Suit x.Value x.IndexInFullDeck

    member x.ToString2() = 
        let v = 
            match x.Value with
            |CardValue.Queen -> "Q"
            |CardValue.Jack -> "J"
            |CardValue.Ace -> "A"
            |CardValue.King -> "K"
            |CardValue.V10 -> "10"
            |CardValue.V9 -> "9"
            |CardValue.V8 -> "8"
            |CardValue.V7 -> "7"
            |_ -> ""
        let s = 
            match x.Suit with
            |CardSuit.Club -> "C"
            |CardSuit.Spade -> "S"
            |CardSuit.Heart -> "H"
            |CardSuit.Diamond -> "D"
            |_-> ""
        s + v

    override x.Equals(yobj) =
        match yobj with
        | :? Card as y -> x.IndexInFullDeck = y.IndexInFullDeck
        | _-> false

    override x.GetHashCode() = x.IndexInFullDeck
    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? Card as y -> compare y.IndexInFullDeck x.IndexInFullDeck
            | _ -> invalidArg "yobj" "cannot compare values of different types"        

type FullCardDeck() = 
    static let fullCardDeck = Card.FullDeck
    static member Cards = fullCardDeck
    static member GetByIds (cardids : seq<int>) =
        cardids |> Seq.map (fun cid -> fullCardDeck.[cid])
    static member cardlist_tostring (cardlist : ImmutableList<Card>) =
        let mutable ret = ""
        for i = 0 to cardlist.Count - 1 do
            ret <- ret + cardlist.[i].ToString()
            if i < cardlist.Count - 1 then
                ret <- ret + "\n"
        ret
    static member cardlist_tostring2 (cardlist : ImmutableList<Card>) =
        let mutable ret = ""
        for i = 0 to cardlist.Count - 1 do
            ret <- ret + cardlist.[i].ToString2()
            if i < cardlist.Count - 1 then
                ret <- ret + " "
        ret

type CardSet(cards : ImmutableList<Card>) =
    let cardmaptonr = cards |> Seq.indexed |> Seq.map (fun (i,c) -> (c.IndexInFullDeck, i)) |> Map
    static member val Empty = CardSet(ImmutableList<Card>.Empty)
    member x.Cards = cards
    member x.Contains(card : Card) =
        cardmaptonr.ContainsKey(card.IndexInFullDeck)

    member x.IndexOf(card : Card) =
        if x.Contains(card)
        then Some cardmaptonr.[card.IndexInFullDeck]
        else None

    member x.Add(cards : seq<Card>, ?sort : bool) : CardSet =
        if cards |> Seq.exists (fun c -> x.Contains(c))
        then invalidArg "Card" "Card allready in cardset"
        let new_cards = x.Cards.AddRange(cards)
        let new_cards = if defaultArg sort true then new_cards.Sort() else new_cards
        CardSet(new_cards)

    member x.Add(card : Card, ?sort : bool) : CardSet =
        if x.Contains(card) 
        then invalidArg "Card" "Card allready in cardset"
        let new_cards = x.Cards.Add(card)
        let new_cards = if defaultArg sort true then new_cards.Sort() else new_cards
        CardSet(new_cards)

    member x.Add(cards : CardSet) : CardSet =
        x.Add cards.Cards        
    
    new(cardids : list<int>) = 
        let cards = List.map (fun k -> FullCardDeck.Cards.[k]) cardids
        CardSet(cards.ToImmutableList().Sort())
    
    member x.Remove(card : Card) : CardSet =
        if not (x.Contains(card))
        then invalidArg "Card" "Card is not in cardset"
        let ind = cardmaptonr.[card.IndexInFullDeck]
        let new_cards = x.Cards.RemoveAt(ind)
        CardSet(new_cards)
    
    member x.Remove(cards : seq<Card>) : CardSet =
        if cards |> Seq.exists (fun c -> not (x.Contains(c)))
        then invalidArg "Card" "Card is not in cardset"
        let new_cards = x.Cards.RemoveRange(cards)
        CardSet(new_cards)

    member x.RemoveAt(index : int) : CardSet =
        if index < 0 || index >= x.Cards.Count
        then invalidArg "index" "index out of bounds"
        let new_cards = x.Cards.RemoveAt(index)
        CardSet(new_cards)

    member x.HasSuit suit = 
        x.Cards.Exists(fun card -> card.Suit = suit)

    member x.HasSuitX suitx = 
        x.Cards.Exists(fun card -> card.SuitX = suitx)

    member x.HasTrump() = 
        x.Cards.Exists(fun card -> card.IsTrump)

    member x.GetTrumps() =
        let found_cards = x.Cards.FindAll(fun card -> card.IsTrump)
        CardSet(found_cards)

    member x.GetBySuit suit =
        let found_cards = x.Cards.FindAll(fun card -> card.Suit = suit)
        CardSet(found_cards)

    member x.GetBySuitX suit =
        let found_cards = x.Cards.FindAll(fun card -> card.SuitX = suit)
        CardSet(found_cards)

    override x.ToString() = 
        "CardSet: " + FullCardDeck.cardlist_tostring2 x.Cards

type FullCardDeckShuffled() =
    static let rnd = Random()
    let _Cards = 
        let ind : int array = Array.init 26 (fun i -> i)
        for i = 25 downto 1 do
            let j = rnd.Next(i + 1)
            let v = ind.[j]
            ind.[j] <- ind.[i]
            ind.[i] <- v
        ind |> Seq.map (fun k -> FullCardDeck.Cards.[k])
        |> ImmutableList.CreateRange

    member x.Cards = _Cards

    member x.GetRange (ind : int) (count : int) : CardSet =
        if ind < 0 || ind + count - 1 >= x.Cards.Count
        then invalidArg "ind, count" "Out of range"
        let new_cards = x.Cards.GetRange(ind, count).Sort()
        CardSet(new_cards)

    override x.ToString() = 
        "FullCardDeckShuffled:\n" + FullCardDeck.cardlist_tostring x.Cards


