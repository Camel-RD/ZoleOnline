namespace GameServerLib
open System
open System.Text
open System.Collections.Generic

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
                if uc.Items.[i].Points > 0 then
                    PointsByTag.[i] <- PointsByTag.[i] + uc.Items.[i].Points
                    UsersByTag.[i] <- 
                        if UsersByTag.[i] = "" 
                        then uc.UserName
                        else UsersByTag.[i] + ", " + uc.UserName
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
        
        
        

        
open GameLib

