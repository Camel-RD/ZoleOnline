namespace GameLib
open System
open System.Collections.Generic
open System.Diagnostics

type AppData() =
    static member val Greeting = "ZoleV03"
    static member val RetGreetingNoEmailValidation = "NoEmailValidation"

type StateVarFlag = |OK |Return |Failed of msg : string

type MyRet = 
    |OK |Error of msg : string
    with member x.isok = x |> function |OK -> true |_ -> false

type GameType = |NotSet = 0 | Normal = 1 | Zole = 2 | Table = 3 | Aborted = 4

type IMsgTaker<'Msg> =
    abstract member TakeMessage : msg : 'Msg -> unit

type IMsgTakerAsync<'Msg> =
    abstract member TakeMessage : msg : 'Msg -> Async<bool>

type MsgTakeEmptyr<'Msg>() =
    static member val Empty = MsgTakeEmptyr<'Msg>()
    interface IMsgTaker<'Msg> with
        member x.TakeMessage (msg : 'Msg) = ()

type IMsgTakerX<'Msg> =
    abstract member TakeMessage : msg : 'Msg -> unit
    abstract member TakeMessageGetReply<'Reply> : builder : (AsyncReplyChannel<'Reply> ->  'Msg) * ?timeout : int -> Async<'Reply option>
    abstract member TakeMessageGetReplyX<'Reply> : msg : 'Msg * ?timeout : int -> Async<'Reply option>

type StartAllResult =
    |OK |PlayerCanceled of plnr : int |Failed


type LobbyPlayerInfo ={
    name : string
    info : string
}

type LobbyData = {
    playerCount : int
    players : List<LobbyPlayerInfo>
}

type LobbyUpdateData = 
    |NewPlayer of data : LobbyPlayerInfo
    |LostPlayer of name : string
    |UpdatePlayer of data : LobbyPlayerInfo

type GamePoints = {UserIds : int[]; Points : int[]; GamesPlayed : int[]}

module MyConverter =
    let IntToByte (v : int) =
        let ret : byte[] = [|0uy;0uy;0uy;0uy|]
        ret.[0] <- byte v 
        ret.[1] <- byte (v >>> 8)
        ret.[2] <- byte (v >>> 16)
        ret.[3] <- byte (v >>> 24)
        ret

    let ByteToInt (v : byte[]) = 
        (int v.[0]) ||| 
        (int v.[1] <<< 8) ||| 
        (int v.[2] <<< 16) ||| 
        (int v.[3] <<< 24)

type Logger() =
    static member WriteLine(msg :string) = ()
    static member WriteLine(format : string, [<ParamArray>] args : obj []) = ()
    static member GatCaseLabel (obj : 'a) = ""
    static member MsgToStr2 (msg1 : 'a, msg2 : 'b) = ""
    static member BadMsg ((expected : string), (got : obj)) = ()
    static member BadMsg ((tag : string), (expected : string), (got : obj)) = ()

type LoggerA() =
    static member WriteLine(msg :string) = Debug.WriteLine(msg);
    static member WriteLine(format : string, [<ParamArray>] args : obj []) = Debug.WriteLine(format, args);
    
    static member GatCaseLabel (obj : 'a) = 
        match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(obj, typeof<'a>) with
        | case, _ -> case.Name
    
    static member MsgToStr2 (msg1 : 'a, msg2 : 'b) = 
        LoggerA.GatCaseLabel(msg1) + "->" + LoggerA.GatCaseLabel(msg2)

    static member BadMsg ((expected : string), (got : obj)) = 
        let method = (new StackFrame(2, false)).GetMethod();
        let declaringType = method.DeclaringType;
        let methodname = method.Name
        let typename = if isNull(declaringType) then "???" else declaringType.Name
        let s = sprintf "(%A.%A) exp: %A got: %A" typename methodname expected (got.ToString())
        Logger.WriteLine(s)

    static member BadMsg ((tag : string), (expected : string), (got : obj)) = 
        let method = (new StackFrame(2, false)).GetMethod();
        let declaringType = method.DeclaringType;
        let methodname = method.Name
        let typename = if isNull(declaringType) then "???" else declaringType.Name
        let s = sprintf "(%A: %A.%A) exp: %A got: %A" tag typename methodname expected (got.ToString())
        Logger.WriteLine(s)

[<AutoOpen>]
module MyExtentions =
    type Result<'T,'Terror> with
        member x.IsError = x |> function |Result.Error _-> true |Result.Ok _ ->false
