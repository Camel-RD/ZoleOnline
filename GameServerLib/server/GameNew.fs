namespace GameServerLib
open System
open System.Collections.Immutable
open System.Collections.Generic
open GameLib

type GameNew(id) =
    member x.id : int = id
    member val Created : DateTime = DateTime.Now
    member val Users = new List<IUser>()
    member val TimeLastUserAdded : DateTime option = None with get,set
    member val IsPrivate = false with get,set
    member val GameName = "" with get,set
    member val GamePsw = "" with get,set

    override x.Equals(obj) =
        match obj with
        | :? GameNew as y -> x.id = y.id
        | _-> false
    override x.GetHashCode() = x.id.GetHashCode()
    interface IComparable with
        member x.CompareTo(obj: obj): int = 
            match obj with
            | :? GameNew as y -> compare y.id x.id
            | _ -> invalidArg "obj" "cannot compare values of different types"        
