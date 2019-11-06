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

type UserCalendarItem() =
    member val Tag = "" with get, set
    member val Points = 0 with get, set
    member x.ToStr() = sprintf "%s;%i" x.Tag x.Points

type UserCalendar(userid, name) =
    member val UserId : int = userid
    member val UserName : string = name
    member val Items = List<UserCalendarItem>()
    member val DataStr = "" with get,set

    member x.ToStr() =
        if x.Items.Count = 0 then ""
        else
            let s1 = x.Items.[0].ToStr()
            x.Items
            |> Seq.skip 1
            |> Seq.fold (fun ss it -> ss + "!" + it.ToStr()) s1
    
    member x.FromStr(ss : string, expectedcount : int) =
        if x.DataStr = ss then true else
        let ret = List<UserCalendarItem>()
        if ss = "" then false else
        let lines = ss.Split '!'
        let rec loop i = 
            if i = lines.Length then true else
            let line = lines.[i]
            if line = "" then false else
            let parts = line.Split ';'
            if parts.Length <> 2 then false else
            let tag = parts.[0]
            let sp = parts.[1]
            if tag = "" || sp = "" then false else
            let bok, p = System.Int32.TryParse sp
            if not bok then false else
            let new_item = UserCalendarItem()
            new_item.Tag <- tag
            new_item.Points <- p
            ret.Add new_item
            loop (i + 1)
        let bok = loop 0
        if bok && expectedcount = ret.Count then
            x.Items.Clear()
            x.Items.AddRange ret
            x.DataStr <- ss
            true
        else false
    

type ServerCalendar(taglist : string []) =
    let TagList : string [] = taglist
    let UserList : Dictionary<int, UserCalendar> = Dictionary<int, UserCalendar>()
    let mutable ForDate : DateTime = DateTime.MinValue
    let ExpectedCount = taglist.Length
    let PointsByTag : int[] = Array.zeroCreate ExpectedCount
    let UsersByTag : string[] = Array.create ExpectedCount ""
    let mutable PointsStr : string = ""
    let mutable EmptyCalendarStr : string = ""

    member x.Reset() =
        for i = 0 to PointsByTag.Length-1 do 
            PointsByTag.[i] <- 0
            UsersByTag.[i] <- ""
        UserList.Clear()
        PointsStr <- ""
        EmptyCalendarStr <- ""

    member x.GetUserNamesFotTag(date : DateTime, tag : string) =
        let date = date.Date
        if date > ForDate || PointsByTag.Length = 0 then
            UserList.Clear()
            ForDate <- date
            x.UpdateX ()
            ""
        else
            let k = Array.IndexOf(TagList, tag)
            if k = -1 then ""
            else UsersByTag.[k]
    
    member x.GetUserCalendar (date : DateTime, userid : int) =
        let date = date.Date
        if date > ForDate || PointsByTag.Length = 0 then
            UserList.Clear()
            ForDate <- date
            x.UpdateX ()
            PointsStr + "|" + EmptyCalendarStr
        else
            let bok, usercal = UserList.TryGetValue userid
            if bok then
                PointsStr + "|" + usercal.DataStr
            else
                PointsStr + "|" + EmptyCalendarStr

    member x.UpdateUserCalendar (date : DateTime, userid : int, username : string, data : string) =
        let date = date.Date
        if date > ForDate || PointsByTag.Length = 0 then
            UserList.Clear()
            ForDate <- date
            x.UpdateX ()

        let bok, usercal = UserList.TryGetValue userid
        let bchanged =
            if bok then
                if data = usercal.DataStr then false
                else
                let bok = usercal.FromStr (data, ExpectedCount)
                if not bok then 
                    UserList.Remove userid |> ignore
                true
            else 
                let new_cal = UserCalendar(userid, username)
                UserList.[userid] <- new_cal
                let bok = new_cal.FromStr (data, ExpectedCount)
                if not bok then 
                    UserList.Remove userid |> ignore
                    false
                else true
        if bchanged then x.UpdateX ()
    
    member x.UpdateX () =
        for i = 0 to PointsByTag.Length-1 do 
            PointsByTag.[i] <- 0
            UsersByTag.[i] <- ""
        for uc in UserList.Values do
            for i = 0 to uc.Items.Count-1 do 
            PointsByTag.[i] <- PointsByTag.[i] + uc.Items.[i].Points
            if uc.Items.[i].Points > 0 then
                UsersByTag.[i] <- UsersByTag.[i] + ", " + uc.UserName
        let sb = StringBuilder()
        EmptyCalendarStr <- ""
        for i = 0 to PointsByTag.Length-1 do 
            let s1 = sprintf "%s;%i" TagList.[i] PointsByTag.[i]
            if i > 0 then 
                sb.Append "!" |> ignore
                EmptyCalendarStr <- EmptyCalendarStr + "!"
            sb.Append s1 |> ignore
            EmptyCalendarStr <- EmptyCalendarStr + TagList.[i] + ";0"
        PointsStr <- sb.ToString()
        
        
        

        
