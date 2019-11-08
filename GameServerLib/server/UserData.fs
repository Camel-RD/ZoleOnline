namespace GameServerLib
open System
open System.Text
open System.Collections.Immutable
open System.Collections.Generic
open GameLib
open SQLite
open System.IO

type UserLocation = |Offline = 0 |Raw = 1 |InLobby = 2 |InGameOrganizer = 3 |InGame = 4 |OnWay = 5
type UserRegStatus = |NotSet = 0 |RegCodeSent = 1 |Registered = 2 |ReRegPending = 3
type OnlineUserType = |Guest |Registered

type UserData() =
    [<PrimaryKey>] [<AutoIncrement>]
    member val Id = -1 with get,set
    [<Indexed>]
    member val Name = "" with get,set
    member val Psw = "" with get,set
    [<Indexed>]
    member val Email = "" with get,set
    member val RegCode = "" with get,set

    member val Points = 0 with get,set
    member val GamesPlayed = 0 with get,set

    member val RegistrationsDate : Nullable<DateTime> = Nullable() with get,set
    member val GetRegCodeRequestCount = 0 with get,set
    member val RegisterRequestCount = 0 with get,set
    member val FailedLoginCount = 0 with get,set
    member val RegStatus = UserRegStatus.NotSet with get,set

    member x.AddPoints (points, gamesplayed) =
        x.Points <- x.Points + points
        x.GamesPlayed <- x.GamesPlayed + gamesplayed

    member x.Copy() =
        let newud = UserData()
        newud.Id <- x.Id
        newud.Name <- x.Name
        newud.Psw <- x.Psw
        newud.Email <- x.Email
        newud.RegCode <- x.RegCode
        newud.Points <- x.Points
        newud.GamesPlayed <- x.GamesPlayed
        newud.RegistrationsDate <- x.RegistrationsDate
        newud.FailedLoginCount <- x.FailedLoginCount
        newud.GetRegCodeRequestCount <- x.GetRegCodeRequestCount
        newud.RegisterRequestCount <- x.RegisterRequestCount
        newud.RegStatus <- x.RegStatus
        newud
    
    member x.Equals(newud : UserData) =
        newud.Id = x.Id &&
        newud.Name = x.Name &&
        newud.Psw = x.Psw &&
        newud.Email = x.Email &&
        newud.RegCode = x.RegCode &&
        newud.Points = x.Points &&
        newud.GamesPlayed = x.GamesPlayed &&
        newud.RegistrationsDate = x.RegistrationsDate &&
        newud.FailedLoginCount = x.FailedLoginCount &&
        newud.GetRegCodeRequestCount = x.GetRegCodeRequestCount &&
        newud.RegisterRequestCount = x.RegisterRequestCount &&
        newud.RegStatus = x.RegStatus
        

type OnlineUserData(user) =
    let _User : IUser = user
    member val User = _User
    member x.Id = user.Id
    member val UserData : UserData option = None with get,set
    member val Location = UserLocation.Offline with get,set
    member val OnlineUserType = OnlineUserType.Guest with get,set

type UserDataRepository() =
    let idgenerator = IdGenerator(1)
    let mutable DbConn : SQLiteAsyncConnection option = None

    let LoadedUserData : Dictionary<int, UserData> = Dictionary<int, UserData>()

    member x.Open(dbpath : string) = async{
        let db = new SQLiteAsyncConnection(dbpath, true);
        let task_createtable = db.CreateTableAsync<UserData>()
        let! ret = Async.AwaitTask(task_createtable)
        DbConn <- Some db
    }
        
    member x.Close() = async{
        LoadedUserData.Clear()
        if DbConn.IsSome then
            let task_close = DbConn.Value.CloseAsync()
            try do! Async.AwaitTask(task_close)
            finally DbConn <- None
    }

    member x.AddNew (name, psw, email) = async{
        if DbConn.IsNone then
            return None
        else
        let db = DbConn.Value
        let ud = UserData()
        ud.Name <- name
        ud.Psw <- psw
        ud.Email <- email
        let task_insert = db.InsertAsync(ud) |> Async.AwaitTask
        let! ret = Async.Catch (task_insert)
        match ret with
        |Choice1Of2 ret when ret = 1 -> return Some ud
        |Choice1Of2 _ -> return None
        |Choice2Of2 (exc : Exception) -> return None 
    }

    member x.GetUserFromDB (userid : int) = async{
        if DbConn.IsNone then
            return Result.Error "DB not open"
        else
        let db = DbConn.Value
        let task_get = db.GetAsync<UserData>(userid) |> Async.AwaitTask
        let! ret = Async.Catch (task_get)
        match ret with
        |Choice1Of2 ret -> return Result.Ok (Some ret)
        |Choice2Of2 (exc : Exception) -> return Result.Error (exc.ToString()) 
    }
    
    member x.GetUserByNameFromDB (username : string) = async{
        if DbConn.IsNone then
            return Result.Error "DB not open"
        else
        let db = DbConn.Value
        let task_get = 
            db.Table<UserData>()
                .Where(fun ud -> ud.Name = username)
                .ToListAsync()
            |> Async.AwaitTask
        let! ret = Async.Catch (task_get)
        match ret with
        |Choice1Of2 ret when ret.Count > 0 -> return Result.Ok (Some ret.[0])
        |Choice1Of2 _ -> return Result.Ok None
        |Choice2Of2 (exc : Exception) -> return Result.Error (exc.ToString()) 
    }
    
    member x.IsEmailUsed (email : string) (userid : int) = async{
        if DbConn.IsNone then
            return Result.Error "DB not open"
        else
        let db = DbConn.Value
        let task_get = 
            db.Table<UserData>()
                .Where(fun ud -> ud.Email = email && ud.Id <> userid)
                .ToListAsync()
            |> Async.AwaitTask
        let! ret = Async.Catch (task_get)
        match ret with
        |Choice1Of2 ret when ret.Count > 0 -> return Result.Ok true
        |Choice1Of2 _ -> return Result.Ok false
        |Choice2Of2 (exc : Exception) -> return Result.Error (exc.ToString())
    }

    member x.SetUserDataOriginal(ud : UserData) =
        if ud.Id > -1  && not (LoadedUserData.ContainsKey ud.Id) then
            LoadedUserData.[ud.Id] = ud.Copy() |> ignore
        
    member x.SetUserDataUpdated (ud : UserData) = async{
        if DbConn.IsNone then
            return false
        else
        let db = DbConn.Value
        let bok, oud = LoadedUserData.TryGetValue(ud.Id)
        if bok then
            LoadedUserData.Remove ud.Id |> ignore
        if bok && oud.Equals(ud) then
            return true
        else
        let task_get = db.InsertOrReplaceAsync(ud) |> Async.AwaitTask
        let! ret = Async.Catch (task_get)
        match ret with
        |Choice1Of2 ret when ret = 1 -> return true
        |Choice1Of2 _ -> return false
        |Choice2Of2 (exc : Exception) -> return false
    }

    
type OnlineUserDataRepository() =
    let idgenerator = IdGenerator(1)
    let data : Dictionary<int, UserData> = Dictionary<int, UserData>()
    member x.AddNew () =
        let ud = UserData()
        ud.Id <- idgenerator.GetNext()
        data.[ud.Id] <- ud
        ud
