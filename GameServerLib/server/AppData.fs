namespace GameServerLib
open System
open System.Diagnostics
open System.Collections.Immutable
open GameLib

type NewOrLoginUserReply =
    |OK of id : int
    |Failed of msg : string


type UserGameInitData = {
    GameId : int
    MessageGateWay : IMsgTaker<MsgDataFromRemote>
    PlayerNr : int
    PlayerNames : string[]
}

type IdGenerator(startid) =
    let mutable _id : int = startid
    member x.LastId = _id
    member x.GetNext() = if _id = Int32.MaxValue then 0 else _id <- _id + 1; _id

