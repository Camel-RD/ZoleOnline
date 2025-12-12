namespace GameServerLib
open System
open System.Diagnostics
open System.Collections.Generic
open System.Collections.Immutable
open GameLib
open System.IO
open System.Reflection
open System.Net.Mail
open System.Text.RegularExpressions

type ServerStatus = |Offline |Started |Closed

type ServerState = {
    Status : ServerStatus
    GamesPlaying : Map<int,GamePlaying>
} with
    static member Init = {
        ServerState.Status = ServerStatus.Offline
        GamesPlaying = Map.empty}

type private ServerStateVar = {
    Reader : ServerState -> Async<MsgToServer>
    State : ServerState
    Worker : ServerStateVar -> Async<ServerStateVar>
    Flag : StateVarFlag} with
    member x.ShouldExit() = x.Flag <> StateVarFlag.OK

type ServerOptions = {
    Port : int
    DataFolder : string
    AddHours : int
    EmailServerAddr : string
    EmailServerPort : int
    EmailFrom : string
    EmailFromName : string
    EmailServerPsw : string
    UseEmailValidation : bool} with
    static member Empty : ServerOptions = {
        Port = 7777
        DataFolder = ""
        AddHours = 0
        EmailServerAddr = ""
        EmailServerPort = 0
        EmailFrom = ""
        EmailFromName = ""
        EmailServerPsw = ""
        UseEmailValidation = false
    }

type AppServer(options : ServerOptions) as this =
    let MailBox = new AutoCancelAgent<MsgToServer>(this.DoInbox)
    let RawUserId_Generator = IdGenerator(0)
    let GameId_Generator = IdGenerator(0)
    let rnd = Random()
    let InitState = ServerState.Init
    static let mutable _AddHours = 0
    do _AddHours <- options.AddHours
    let RealDate() = DateTime.Now.AddHours(float options.AddHours).Date

    let AppUserData = UserDataRepository()
    let AppOnlineUserData = Dictionary<int,OnlineUserData>()
    let TagList = [|"17:00";"18:00";"19:00";"20:00";"21:00";"22:00";"23:00";"24:00"|]
    let ServerCalendar = ServerCalendar(TagList)

    let ftakemsgfromlistener (msg : MsgListenerToServer) =
        MsgToServer.FromListener msg |> this.TakeMessage
    
    let Listener = ServerListener(options.Port, ftakemsgfromlistener)


    let mutable _CountNewConnections = 0
    let mutable _CountRegCodesSent = 0
    let mutable _CountRegistrations = 0
    let mutable _CountLoggins = 0
    let mutable _CountNewGames = 0


    let OUDById id = 
        let bfaund, v = AppOnlineUserData.TryGetValue(id) 
        if bfaund then Some v else None

    let OUDByName name skipuserid : OnlineUserData option = 
        let en = AppOnlineUserData.Values :> IEnumerable<OnlineUserData>
        en |> Seq.tryFind (fun ou -> 
            ou.UserData.IsSome && 
            ou.UserData.Value.Name = name && 
            ou.Id <> skipuserid)
        

    let MessageTaker = 
        {new IMsgTakerX<MsgToServer> with
            member x.TakeMessage msg = this.TakeMessageSafe msg
            member x.TakeMessageGetReply<'Reply> (builder, ?timeout) = this.TakeMessageGetReply<'Reply> (builder, timeout)
            member x.TakeMessageGetReplyX (msg, ?timeout) = this.TakeMessageGetReplyX(msg, timeout) }

    let _To = MPToServer(MessageTaker)
    let _FromUser = _To :> IUserToServer
    let _FromGamePlaying = _To :> IGamePlayingToServer
    let GameOrganizer = new GameOrganizer(_To :> IGameOrganizerToServer)
    let Lobby = new Lobby()

    let DataFolder = 
        if options.DataFolder <> "" then options.DataFolder
        else
        let mydirinfo =  
            Assembly.GetEntryAssembly().Location
            |>Path.GetDirectoryName
            |>DirectoryInfo
        let mydirinfo =
            if ["Debug"; "Release"; "netcoreapp3.0"] |> List.contains mydirinfo.Name then
                mydirinfo.Parent.Parent
            else mydirinfo
        let mydir = mydirinfo.FullName    
        Path.Combine(mydir,"Data")
    
    let rex_email = new Regex(@"([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})")
    let rex_username = new Regex("^[\w+]+([-_ ]?[\w+])*$")
    
    let IsGoodName (name : string) = 
        (not (isNull(name))) && 
        name.Length < 16 &&
        rex_username.IsMatch(name)
    
    let IsGoodEmail (email : string) = 
        (not (isNull(email))) && 
        email.Length < 50 &&
        rex_email.IsMatch(email)

    static member AddHours = _AddHours
    
    member val To = _To

    member x.Start() = 
        MailBox.Start()
        GameOrganizer.Start()
        Lobby.Start()
        let dbpath = Path.Combine(DataFolder, "mydb.db")
        let task_open = AppUserData.Open dbpath
        Async.RunSynchronously task_open
        Listener.Start()
    
    member x.Stop() = async{
        for oud in AppOnlineUserData.Values do
            if oud.UserData.IsSome then
                let! q = AppUserData.SetUserDataUpdated oud.UserData.Value true
                ()
        x.Dispose()
        do! Async.Sleep(2000)
    }

    member private x.SendMail (addr : string, code:string) =
        if addr = "" || options.EmailServerAddr = "" || options.EmailFrom = "" then ()
        else
        let task () = 
            let msg = new MailMessage()
            msg.From <- MailAddress(options.EmailFrom, options.EmailFromName)
            msg.To.Add(addr)
            msg.Subject <- "Tavs reģistrācijas kods"
            msg.Body <- "Tavs reģistrācijas kods Zolītes serverī: " + code
            let SmtpServer = new SmtpClient(options.EmailServerAddr)
            SmtpServer.Port <- options.EmailServerPort
            SmtpServer.Credentials <- new System.Net.NetworkCredential(options.EmailFrom, options.EmailServerPsw);
            SmtpServer.Send(msg)
            Logger.WriteLine("Sent code:{0} to {1}", code, addr)
        let task_async = async{
            try task()
            with _-> ()}
        Async.Start(task_async)

    member x.AddRegUser (name : string, psw : string) =
        AppUserData.AddRegUser (name, psw) |> Async.RunSynchronously

    member x.GetStats () =
        (_CountNewConnections, 
         _CountRegCodesSent, 
         _CountRegistrations, 
         _CountLoggins, 
         _CountNewGames)

    member private x.AddNewConnection (state : ServerState) (connection : ServerConnection) =
        let new_user = new User(_FromUser, connection, options.UseEmailValidation)
        let new_rawid = RawUserId_Generator.GetNext()
        let new_oud = OnlineUserData(new_user)
        new_oud.Location <- UserLocation.Raw
        new_user.SetUserData (new_rawid, "", "")
        AppOnlineUserData.Add (new_rawid, new_oud)
        _CountNewConnections <- _CountNewConnections + 1
        Logger.WriteLine("AppServer: AddNewConnection: userid {0}", new_user.Id)
        new_user.Start()
        let b = connection.Start((new_user :> IUser).FromClient)
        state


    member private x.UserClosed (state : ServerState) (userid : int) = async{
        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            Logger.WriteLine "AppServer: UserClosed - userid not foud"
            return state
        else 
        let onlineuser = onlineuser.Value
        let user = onlineuser.User
        match onlineuser.Location with
        |UserLocation.InGameOrganizer -> GameOrganizer.To.UserClosed userid
        |UserLocation.InGame -> () // TODO
        |_ -> ()
        onlineuser.Location <- UserLocation.Offline
        let! q = 
            if onlineuser.UserData.IsSome && 
                    onlineuser.OnlineUserType = OnlineUserType.Registered then
                AppUserData.SetUserDataUpdated onlineuser.UserData.Value true
            else async{return true}
        onlineuser.UserData <- None
        AppOnlineUserData.Remove userid |> ignore
        Lobby.To.LeaveServer user
        Logger.WriteLine("AppServer: UserClosed - userid: {0}", userid)
        return state
    }

    member private x.GetRegCode (state : ServerState) (userid : int) 
            (name : string) (psw : string) (email : string) 
            (replych : (AsyncReplyChannel<NewOrLoginUserReply>)) = async{
        
        if not options.UseEmailValidation then
            replych.Reply (NewOrLoginUserReply.Failed("Serverī var reģistrēties bez reģistrācijas koda"))
            Logger.WriteLine "AppServer: GetRegCode - regcode not needed"
            return state
        else 
        if not (IsGoodName name) then 
            replych.Reply (NewOrLoginUserReply.Failed("Tāds vārds nederēs"))
            Logger.WriteLine "AppServer: GetRegCode - bad username"
            return state
        else 
        if not (IsGoodEmail email) then 
            replych.Reply (NewOrLoginUserReply.Failed("Tāda e-pasta adrese nederēs"))
            Logger.WriteLine "AppServer: GetRegCode - bad email"
            return state
        else 
        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "AppServer: GetRegCode - userid not foud"
            return state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.Raw then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "AppServer: GetRegCode - user not Raw"
            return state
        else 
        let ou2 = OUDByName name userid
        if ou2.IsSome then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu jau ir pierakstijies serverī"))
            Logger.WriteLine("AppServer: GetRegCode: user name allready in server {0}", name)
            return state
        else 

        let! bok, userdata  =
            if onlineuser.UserData.IsSome then
                async{ return true, onlineuser.UserData }
            else
            async{
                let! userdata = AppUserData.GetUserByNameFromDB(name)
                match userdata with
                |Result.Ok ud -> return true, ud
                |Result.Error ers ->
                    replych.Reply (NewOrLoginUserReply.Failed("Datu bāzes kļūda"))
                    Logger.WriteLine("AppServer: GetRegCode: DB error: {0}", ers)
                    return false, None}
        if not bok then
            return state
        else

        let userdata = 
            if userdata.IsSome 
            then userdata.Value
            else 
                let ud = UserData()
                ud.Name <- name
                ud.Psw <- psw
                ud.Email <- email
                ud

        if userdata.RegStatus = UserRegStatus.Registered then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu jau ir reģistrējies"))
            Logger.WriteLine("AppServer: GetRegCode: user allready Registered {0}", name)
            return state
        else 
        if userdata.GetRegCodeRequestCount > 10 then
            replych.Reply (NewOrLoginUserReply.Failed("Jau vairāk kā 10 reizes prasīts reģistrācijas kods"))
            Logger.WriteLine("AppServer: GetRegCode: regcount > max {0}", name)
            return state
        else 
        userdata.GetRegCodeRequestCount <- userdata.GetRegCodeRequestCount + 1
        if userdata.RegStatus = UserRegStatus.ReRegPending && userdata.Psw <> psw then
            replych.Reply (NewOrLoginUserReply.Failed("Norādīta nepareiza parole"))
            Logger.WriteLine("AppServer: GetRegCode: wrong psw {0}", name)
            return state
        else 

        let! bemailused = AppUserData.IsEmailUsed email userdata.Id
        let bok, bemailused  =
            match bemailused with
            |Result.Ok b -> true, b
            |Result.Error ers ->
                replych.Reply (NewOrLoginUserReply.Failed("Datu bāzes kļūda"))
                Logger.WriteLine("AppServer: GetRegCode: DB error")
                false, false
        if not bok then
            return state
        else
        if bemailused then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu epastu ir jau reģistrējies"))
            Logger.WriteLine("AppServer: GetRegCode: used email {0}", name)
            return state
        else 

        onlineuser.UserData <- Some userdata

        userdata.RegStatus <- UserRegStatus.RegCodeSent
        userdata.RegCode <- rnd.Next(10000, 100000).ToString()
        userdata.GetRegCodeRequestCount <- 0

        let! q = 
            if userdata.Id = -1 then
                AppUserData.AddNew userdata
            else
                AppUserData.SetUserDataUpdated userdata false
        AppUserData.SetUserDataOriginal userdata
        let user = onlineuser.User
        user.SetUserData (user.Id, name, psw)
        x.SendMail (userdata.Email, userdata.RegCode)
        replych.Reply (NewOrLoginUserReply.OK user.Id)
        _CountRegCodesSent <- _CountRegCodesSent + 1
        Logger.WriteLine("AppServer: GetRegCode: userid: {0} name: {1} code: {2}", user.Id, name, userdata.RegCode)
        return state
    }

    member private x.RegisterNewUser (state : ServerState) (userid : int) 
            (name : string) (psw : string) (regcode : string) 
            (replych : (AsyncReplyChannel<NewOrLoginUserReply>)) = async{

        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "AppServer: RegisterNewUser - userid not foud"
            return state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.Raw then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "AppServer: RegisterNewUser - user not Raw"
            return state
        else 
        let ou2 = OUDByName name userid
        if ou2.IsSome then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu jau ir pierakstijies severī"))
            Logger.WriteLine("AppServer: RegisterNewUser: user name allready in server {0}", name)
            return state
        else 

        let! bok, userdata  =
            if onlineuser.UserData.IsSome then
                async{ return true, onlineuser.UserData }
            else
            async{
                let! userdata = AppUserData.GetUserByNameFromDB(name)
                match userdata with
                |Result.Ok ud -> return true, ud
                |Result.Error ers ->
                    replych.Reply (NewOrLoginUserReply.Failed("Datu bāzes kļūda"))
                    Logger.WriteLine("AppServer: RegisterNewUser: DB error: {0}", ers)
                    return false, None}
        if not bok then
            return state
        else

        if userdata.IsNone then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu nav prasijis reģistrācijas kodu"))
            Logger.WriteLine("AppServer: RegisterNewUser: user name not found {0}", name)
            return state
        else 
        
        onlineuser.UserData <- userdata
        let userdata = userdata.Value
        
        if userdata.RegStatus = UserRegStatus.Registered then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu jau ir reģistrējies"))
            Logger.WriteLine("AppServer: RegisterNewUser: user allready Registered {0}", name)
            return state
        else 
        if userdata.RegStatus <> UserRegStatus.RegCodeSent then
            replych.Reply (NewOrLoginUserReply.Failed("No sākuma jāpieprasa reģistrācijas kods"))
            Logger.WriteLine("AppServer: RegisterNewUser: RegCodeSent not sent {0}", name)
            return state
        else 
        userdata.RegisterRequestCount <- userdata.RegisterRequestCount + 1
        if userdata.RegisterRequestCount > 10 then
            replych.Reply (NewOrLoginUserReply.Failed("Jau vairāk kā 10 neveiksmīgu reģistrācijas mēģinājumu"))
            Logger.WriteLine("AppServer: RegisterNewUser: regcount > max {0}", name)
            return state
        else 
        if userdata.Psw <> psw  then
            replych.Reply (NewOrLoginUserReply.Failed("Norādīta nepareiza parole"))
            Logger.WriteLine("AppServer: RegisterNewUser: wrong psw {0}", name)
            return state
        else 
        if userdata.RegCode <> regcode then
            replych.Reply (NewOrLoginUserReply.Failed("Norādīta nepareizs reģistrācijas kods"))
            Logger.WriteLine("AppServer: RegisterNewUser: wrong regcode {0}", name)
            return state
        else 
        userdata.RegStatus <- UserRegStatus.Registered
        userdata.RegistrationsDate <- Nullable DateTime.Now
        let user = onlineuser.User
        user.SetUserData (user.Id, name, psw)
        user.SetPoints (userdata.Points, userdata.GamesPlayed)
        onlineuser.Location <- UserLocation.OnWay
        let! q = AppUserData.SetUserDataUpdated userdata false
        AppUserData.SetUserDataOriginal userdata
        replych.Reply (NewOrLoginUserReply.OK user.Id)
        Lobby.To.EnterServer user
        _CountRegistrations <- _CountRegistrations + 1
        Logger.WriteLine("AppServer: RegisterNewUser: userid: {0} name: {1}", user.Id, name)
        return state
    }

    member private x.RegisterNewUser2 (state : ServerState) (userid : int) 
            (name : string) (psw : string) 
            (replych : (AsyncReplyChannel<NewOrLoginUserReply>)) = async{

        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "AppServer: RegisterNewUser2 - userid not foud"
            return state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.Raw then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "AppServer: RegisterNewUser2 - user not Raw"
            return state
        else 
        let ou2 = OUDByName name userid
        if ou2.IsSome then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu jau ir pierakstijies severī"))
            Logger.WriteLine("AppServer: RegisterNewUser2: user name allready in server {0}", name)
            return state
        else 

        let! bok, userdata  =
            if onlineuser.UserData.IsSome then
                async{ return true, onlineuser.UserData }
            else
            async{
                let! userdata = AppUserData.GetUserByNameFromDB(name)
                match userdata with
                |Result.Ok ud -> return true, ud
                |Result.Error ers ->
                    replych.Reply (NewOrLoginUserReply.Failed("Datu bāzes kļūda"))
                    Logger.WriteLine("AppServer: RegisterNewUser2: DB error: {0}", ers)
                    return false, None}
        if not bok then
            return state
        else

        if userdata.IsSome then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu jau ir reģistrācijies"))
            Logger.WriteLine("AppServer: RegisterNewUser2: user name taken {0}", name)
            return state
        else 
        
        let userdata = UserData()
        userdata.Name <- name
        userdata.Psw <- psw
        userdata.RegStatus <- UserRegStatus.Registered
        userdata.RegistrationsDate <- Nullable DateTime.Now

        let! q = AppUserData.AddNew userdata
        if not q then
            replych.Reply (NewOrLoginUserReply.Failed("Datu bāzes kļūda"))
            Logger.WriteLine("AppServer: RegisterNewUser2: DB error")
            return state
        else 

        onlineuser.UserData <- Some userdata
        AppUserData.SetUserDataOriginal userdata
        let user = onlineuser.User
        user.SetUserData (user.Id, name, psw)
        user.SetPoints (userdata.Points, userdata.GamesPlayed)
        onlineuser.Location <- UserLocation.OnWay
        replych.Reply (NewOrLoginUserReply.OK user.Id)
        Lobby.To.EnterServer user
        _CountRegistrations <- _CountRegistrations + 1
        Logger.WriteLine("AppServer: RegisterNewUser2: userid: {0} name: {1}", user.Id, name)
        return state
    }

    member private x.LogInUser (state : ServerState) (userid : int) 
            (name : string) (psw : string) 
            (replych : (AsyncReplyChannel<NewOrLoginUserReply>)) = async{
        
        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "LogInUser: LogInUser - userid not foud"
            return state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.Raw then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "LogInUser: LogInUser - user not Raw"
            return state
        else 
        let ou2 = OUDByName name userid
        if ou2.IsSome then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu jau ir pierakstijies severī"))
            Logger.WriteLine("LogInUser: LogInUser: user name allready in server {0}", name)
            return state
        else
        
        let! bok, userdata  =
            if onlineuser.UserData.IsSome then
                async{ return true, onlineuser.UserData }
            else
            async{
                let! userdata = AppUserData.GetUserByNameFromDB(name)
                match userdata with
                |Result.Ok ud -> return true, ud
                |Result.Error ers ->
                    replych.Reply (NewOrLoginUserReply.Failed("Datu bāzes kļūda"))
                    Logger.WriteLine("AppServer: LogInUser: DB error: {0}", ers)
                    return false, None}
        if not bok then
            return state
        else        

        if userdata.IsNone then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu nav reģisrtējies"))
            Logger.WriteLine("LogInUser: LogInUser: user name not found {0}", name)
            return state
        else 
        let userdata = userdata.Value
        onlineuser.UserData <- Some userdata
        if userdata.RegStatus <> UserRegStatus.Registered then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu nav reģistrējies"))
            Logger.WriteLine("LogInUser: LogInUser: user not registered {0}", name)
            return state
        else 
        if userdata.FailedLoginCount > 20 then
            replych.Reply (NewOrLoginUserReply.Failed("Jau vairāk kā 20 neveiksmīgu pierakstīšanās mēģinājumu\nJāreģistrējas no jauna"))
            Logger.WriteLine("LogInUser: LogInUser: FailedLoginCount > max {0}", name)
            return state
        else 
        AppUserData.SetUserDataOriginal userdata
        if userdata.Psw <> psw then
            userdata.FailedLoginCount <- userdata.FailedLoginCount + 1
            replych.Reply (NewOrLoginUserReply.Failed("Norādīta nepareiza parole"))
            Logger.WriteLine("LogInUser: LogInUser: wrong psw {0}", name)
            return state
        else 
        let user = onlineuser.User
        user.SetUserData (user.Id, name, psw)
        user.SetPoints (userdata.Points, userdata.GamesPlayed)
        onlineuser.Location <- UserLocation.OnWay
        onlineuser.OnlineUserType <- OnlineUserType.Registered
        replych.Reply (NewOrLoginUserReply.OK userid)
        Lobby.To.EnterServer user
        _CountLoggins <- _CountLoggins + 1
        Logger.WriteLine("AppServer: LogInUser: userid: {0} name: {1}", user.Id, name)
        return state
    }

    member private x.LogInUserAsGuest (state : ServerState) (userid : int) 
            (name : string) (replych : (AsyncReplyChannel<NewOrLoginUserReply>)) = async{
        
        let onlineuser = OUDById userid
        if not (IsGoodName name) then 
            replych.Reply (NewOrLoginUserReply.Failed("Tāds vārds nederēs"))
            Logger.WriteLine "AppServer: LogInUserAsGuest - bad username"
            return state
        else 
        if onlineuser.IsNone then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "LogInUser: LogInUserAsGuest - userid not foud"
            return state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.Raw then 
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs nav pieslēdzies"))
            Logger.WriteLine "LogInUser: LogInUserAsGuest - user not Raw"
            return state
        else 
        let ou2 = OUDByName name userid
        if ou2.IsSome then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu jau ir pierakstijies severī"))
            Logger.WriteLine("LogInUser: LogInUserAsGuest: user name allready in server {0}", name)
            return state
        else
        
        let! r_userdata = AppUserData.GetUserByNameFromDB(name)
        let bok, r_userdata  =
            match r_userdata with
            |Result.Ok ud -> true, ud
            |Result.Error ers ->
                replych.Reply (NewOrLoginUserReply.Failed("Datu bāzes kļūda"))
                Logger.WriteLine("AppServer: LogInUserAsGuest: DB error: {0}", ers)
                false, None
        if not bok then
            return state
        else        

        if r_userdata.IsSome then
            replych.Reply (NewOrLoginUserReply.Failed("Lietotājs ar šādu vārdu ir reģisrtējies"))
            Logger.WriteLine("LogInUser: LogInUserAsGuest: user name not found {0}", name)
            return state
        else 

        let userdata = onlineuser.UserData
        let userdata = 
            if userdata.IsSome 
            then userdata.Value
            else UserData()
        
        userdata.Name <- name
        userdata.Psw <- ""
        userdata.Email <- ""
        onlineuser.UserData <- Some userdata

        let user = onlineuser.User
        user.SetUserData (user.Id, name, "")
        user.SetPoints (userdata.Points, userdata.GamesPlayed)
        onlineuser.Location <- UserLocation.OnWay
        onlineuser.OnlineUserType <- OnlineUserType.Guest
        replych.Reply (NewOrLoginUserReply.OK userid)
        Lobby.To.EnterServer user
        _CountLoggins <- _CountLoggins + 1
        Logger.WriteLine("AppServer: LogInUserAsGuest: userid: {0} name: {1}", user.Id, name)
        return state
    }

    member private x.UserEnterLobby (state : ServerState) (userid : int) =
        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            Logger.WriteLine "LogInUser: UserEnterLobby - userid not foud"
            state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location = UserLocation.Raw then 
            Logger.WriteLine "LogInUser: UserEnterLobby - user is raw"
            state
        else 
        if onlineuser.Location = UserLocation.InLobby then 
            Logger.WriteLine "LogInUser: UserEnterLobby - allready in lobby"
            state
        else 
        let user = onlineuser.User
        onlineuser.Location <- UserLocation.InLobby
        Lobby.To.EnterLobby user
        Logger.WriteLine("AppServer: UserEnterLobby: userid: {0} name: {1}", user.Id, user.Name)
        state

    member private x.GetCalendarData (state : ServerState) (userid : int) 
            (replych : (AsyncReplyChannel<string>))=

        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            replych.Reply ""
            Logger.WriteLine "LogInUser: GetCalendarData - userid not foud"
            state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.InLobby then 
            replych.Reply ""
            Logger.WriteLine "LogInUser: GetCalendarData - user not in lobby"
            state
        else 
        let user = onlineuser.User
        if onlineuser.UserData.IsSome && onlineuser.OnlineUserType = OnlineUserType.Registered then
            let userdata = onlineuser.UserData.Value
            let data = ServerCalendar.GetUserCalendar(RealDate(), userdata.Id)
            replych.Reply data
        else
            let data = ServerCalendar.GetUserCalendar(RealDate(), -1)
            replych.Reply data
        Logger.WriteLine("AppServer: GetCalendarData: userid: {0} name: {1}", user.Id, user.Name)
        state

    member private x.GetCalendarTagData (state : ServerState) (userid : int) (tag : string)
            (replych : (AsyncReplyChannel<string>))=

        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            replych.Reply ""
            Logger.WriteLine "LogInUser: GetCalendarTagData - userid not foud"
            state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.InLobby then 
            replych.Reply ""
            Logger.WriteLine "LogInUser: GetCalendarTagData - user not in lobby"
            state
        else 
        let user = onlineuser.User
        if onlineuser.UserData.IsSome then
            let data = ServerCalendar.GetUserNamesFotTag(RealDate(), tag)
            replych.Reply data
        else
            let data = ServerCalendar.GetUserNamesFotTag(RealDate(), tag)
            replych.Reply data
        Logger.WriteLine("AppServer: GetCalendarTagData: userid: {0} name: {1}", user.Id, user.Name)
        state

    member private x.SetCalendarData (state : ServerState) (userid : int) (data : string) =
        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            Logger.WriteLine "LogInUser: SetCalendarData - userid not foud"
            state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.InLobby then 
            Logger.WriteLine "LogInUser: SetCalendarData - user not in lobby"
            state
        else 
        if onlineuser.OnlineUserType <> OnlineUserType.Registered then 
            Logger.WriteLine "LogInUser: SetCalendarData - user not registered"
            state
        else
        let user = onlineuser.User
        if onlineuser.UserData.IsSome then
            let userdata = onlineuser.UserData.Value
            ServerCalendar.UpdateUserCalendar(RealDate(), userdata.Id, userdata.Name, data)
        Logger.WriteLine("AppServer: SetCalendarData: userid: {0} name: {1}", user.Id, user.Name)
        state

    member private x.UserStartWaitForNewGame (state : ServerState) (userid : int) =
        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            Logger.WriteLine "LogInUser: UserStartWaitForNewGame - userid not foud"
            state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.InLobby then 
            Logger.WriteLine "LogInUser: UserStartWaitForNewGame - user not in Lobby"
            state
        else 
        let user = onlineuser.User
        onlineuser.Location <- UserLocation.InGameOrganizer
        Lobby.To.LeaveLobby user
        GameOrganizer.To.UserStartWaitForNewGame user
        Logger.WriteLine("AppServer: UserStartWaitForNewGame: userid: {0} name: {1}", user.Id, user.Name)
        state

    member private x.UserStartWaitForPrivateGame (state : ServerState) (userid : int) 
            (gamename : string) (gamepsw : string) =

        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            Logger.WriteLine "LogInUser: UserStartWaitForPrivateGame - userid not foud"
            state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.InLobby then 
            Logger.WriteLine "LogInUser: UserStartWaitForPrivateGame - user not in Lobby"
            state
        else 
        let user = onlineuser.User
        onlineuser.Location <- UserLocation.InGameOrganizer
        Lobby.To.LeaveLobby user
        GameOrganizer.To.UserStartWaitForPrivateGame user gamename gamepsw
        Logger.WriteLine("AppServer: UserStartWaitForPrivateGame: userid: {0} name: {1}", user.Id, user.Name)
        state

    member private x.UserCancelWaitForNewGame (state : ServerState) (userid : int) =
        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            Logger.WriteLine "LogInUser: UserCancelWaitForNewGame - userid not foud"
            state
        else 
        let onlineuser = onlineuser.Value
        if onlineuser.Location <> UserLocation.InGameOrganizer then 
            Logger.WriteLine "LogInUser: UserCancelWaitForNewGame - user not in InGameOrganizer"
            state
        else 
        let user = onlineuser.User
        onlineuser.Location <- UserLocation.InLobby
        GameOrganizer.To.UserCancelWaitForNewGame user
        Logger.WriteLine("AppServer: UserCancelWaitForNewGame: userid: {0} name: {1}", user.Id, user.Name)
        state

    member private x.StartNewGame (state : ServerState) (userids : int []) =
        let ouserids_present = userids |> Array.filter (fun id -> AppOnlineUserData.ContainsKey id)
        let ousers_present = ouserids_present |> Array.map (fun id -> AppOnlineUserData.[id])
        let users_present = ousers_present |> Array.map (fun ou -> ou.User)
        if ouserids_present.Length <> 3 then
            Logger.WriteLine "AppServer: StartNewGame: userid not found"
            for ouser in ousers_present do 
                ouser.User.FromGameOrganizer.CancelNewGame()
                ouser.Location <- UserLocation.InLobby
            state
        else
        let new_game_id = GameId_Generator.GetNext()
        let new_game = new GamePlaying(new_game_id, _FromGamePlaying, users_present)
        let new_games = state.GamesPlaying.Add (new_game_id, new_game)
        new_game.Start()
        _CountNewGames <- _CountNewGames + 1
        Logger.WriteLine("AppServer.StartNewGame: startin new game{0}", new_game_id)
        {state with 
            GamesPlaying = new_games}

    member private x.GameStopped (state : ServerState) (userid : int) (gameid : int) =
        let onlineuser = OUDById userid
        if onlineuser.IsNone then 
            Logger.WriteLine "LogInUser: GameStopped - userid not foud"
            state
        else 
        let onlineuser = onlineuser.Value
        if not (state.GamesPlaying.ContainsKey(gameid)) then 
            Logger.WriteLine "AppServer.GameStopped: gameid not foud"
            state
        else 
        if onlineuser.Location <> UserLocation.InGameOrganizer then 
            Logger.WriteLine "LogInUser: GameStopped - user not in InGameOrganizer"
            state
        else 
        let user = onlineuser.User
        let game = state.GamesPlaying.[gameid]
        onlineuser.Location <- UserLocation.InLobby
        game.To.Stop()
        state

    member private x.GameClosed (state : ServerState) (gameid : int) =
        if not (state.GamesPlaying.ContainsKey(gameid)) then 
            Logger.WriteLine "AppServer.GameStopped: gameid not foud"
            state
        else 
        let game = state.GamesPlaying.[gameid]
        let new_games = state.GamesPlaying.Remove game.id
        game.Dispose()
        {state with GamesPlaying = new_games}
    
    member private x.DoMsgB (statevar : ServerStateVar) (msg : MsgToServer) =
        let cur_state = statevar.State
        let cur_state =
            match msg with
            |MsgToServer.FromListener (MsgListenerToServer.NewConnection m) ->
                match m with
                | :? ServerConnection as sc -> x.AddNewConnection cur_state sc
                |_ -> cur_state
                
            |MsgToServer.FromUser (MsgUserToServer.EnterLobby userid)->
                x.UserEnterLobby cur_state userid

            |MsgToServer.FromUser (MsgUserToServer.GetCalendarData m)->
                x.GetCalendarData cur_state m.userid m.ch

            |MsgToServer.FromUser (MsgUserToServer.GetCalendarTagData m)->
                x.GetCalendarTagData cur_state m.userid m.tag m.ch

            |MsgToServer.FromUser (MsgUserToServer.SetCalendarData m)->
                x.SetCalendarData cur_state m.userid m.data

            |MsgToServer.FromUser (MsgUserToServer.UserStartWaitForNewGame m)->
                x.UserStartWaitForNewGame cur_state m.userid

            |MsgToServer.FromUser (MsgUserToServer.UserStartWaitForPrivateGame m)->
                x.UserStartWaitForPrivateGame cur_state m.userid m.name m.psw

            |MsgToServer.FromUser (MsgUserToServer.UserCancelWaitForNewGame m)->
                x.UserCancelWaitForNewGame cur_state m.userid

            |MsgToServer.FromUser (MsgUserToServer.GameStopped m)->
                x.GameStopped cur_state m.userid m.gameid

            |MsgToServer.FromGameOrganizer (MsgGameOrganizerToServer.GotUsersForGame m)->
                let userid = [|m.userid1; m.userid2; m.userid3|]
                x.StartNewGame cur_state userid

            |MsgToServer.FromGamePlaying (MsgGamePlayingToServer.GameStopped gameid)->
                x.GameClosed cur_state gameid

            |MsgToServer.FromGamePlaying (MsgGamePlayingToServer.AddGamePoints mdata)->
                for i in 0..2 do
                    let userid = mdata.UserIds.[i]
                    if AppOnlineUserData.ContainsKey userid then
                        let ouser = AppOnlineUserData.[userid] 
                        let user = ouser.User
                        let userdata = ouser.UserData
                        //user.FromServer.AddPoints mdata.GamesPlayed.[i] mdata.Points.[i]
                        user.AddPoints (mdata.Points.[i], mdata.GamesPlayed.[i])
                        if userdata.IsSome then
                            userdata.Value.AddPoints (mdata.Points.[i], mdata.GamesPlayed.[i])
                        Lobby.To.UpdateUser user
                cur_state

            |_ -> cur_state
        cur_state

    member private x.DoMsgA (statevar : ServerStateVar) (msg : MsgToServer) = async{
        let cur_state = statevar.State
        let! cur_state =
            match msg with
            |MsgToServer.FromUser (MsgUserToServer.UserClosed m) ->
                x.UserClosed cur_state m.userid

            |MsgToServer.FromUser (MsgUserToServer.GetRegCode m) ->
                x.GetRegCode cur_state m.userid m.name m.psw m.email m.ch

            |MsgToServer.FromUser (MsgUserToServer.Register m) ->
                if options.UseEmailValidation then
                    x.RegisterNewUser cur_state m.userid m.name m.psw m.regcode m.ch
                else
                    x.RegisterNewUser2 cur_state m.userid m.name m.psw m.ch

            |MsgToServer.FromUser (MsgUserToServer.LoginUser m) ->
                x.LogInUser cur_state m.userid m.name m.psw m.ch

            |MsgToServer.FromUser (MsgUserToServer.LoginUserAsGuest m) ->
                x.LogInUserAsGuest cur_state m.userid m.name m.ch
            |_ -> 
                async{ return x.DoMsgB statevar msg}

        return {statevar with State = cur_state}
    }

    member private x.DoMsg (statevar : ServerStateVar) = async{
        let cur_state = statevar.State
        let! msg = statevar.Reader(cur_state)
        match msg with
        |MsgToServer.Control MsgControl.KillPill -> 
            x.Close()
            let cur_state = {InitState with Status = ServerStatus.Closed}
            return {statevar with State = cur_state; Flag = StateVarFlag.Return}
        |_ ->
            return! x.DoMsgA statevar msg
    }


    member private x.DoInbox(inbox : MailboxProcessor<MsgToServer>) = 
        let rec loop (statevar : ServerStateVar) = async{
            let! ret = Async.Catch (statevar.Worker statevar)
            match ret with
            |Choice1Of2 (new_state : ServerStateVar) -> 
                if new_state.ShouldExit()
                then
                    x.Close()
                    return () 
                else return! loop(new_state)
            |Choice2Of2 (exc : Exception) -> 
                printfn "AppServer exc: %A" (exc.ToString())
                x.Close()
                return () }
        let init_state = InitState
        let init_statevar = 
            {ServerStateVar.Reader = x.MsgReader(inbox); 
                State = init_state; 
                Worker = x.DoMsg;
                Flag = StateVarFlag.OK}
        loop(init_statevar)

    member private x.MsgReader (inbox : MailboxProcessor<MsgToServer>) (state : ServerState) =
        let rec loop() = async{
            let! msg = inbox.Receive()
            
            Logger.WriteLine("AppServer: <- {0}", msg.ToString())

            match msg with
            |MsgToServer.Control MsgControl.KillPill -> return msg
            |MsgToServer.Control (MsgControl.GetState channel) ->
                channel.Reply state
                return! loop()
            | _ -> return msg}
        loop()


    member x.GetState() = 
            let msg channel = MsgToServer.Control (GetReply (MsgControl.GetState, channel))
            let state = MailBox.PostAndReply(msg)
            state

    member private x.TakeMessage(msg : MsgToServer) = MailBox.Post msg

    member private x.TakeMessageSafe(msg : MsgToServer) = 
        if not (x.IsClosed || x.IsDisposed) then 
            try MailBox.Post msg finally ()

    member private x.TakeMessageGetReply<'Reply>(builder : AsyncReplyChannel<'Reply> -> MsgToServer, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout MPHelper.MyTimeOut
        if x.IsClosed || x.IsDisposed then 
            async{return Option.None}
        else
        try 
            let ret = MailBox.PostAndTryAsyncReply(builder, timeout)
            ret
        with |_-> async{return Option.None}

    member private x.TakeMessageGetReplyX<'Reply>(msg : MsgToServer, timeout : int option) : Async<'Reply option>= 
        let timeout = defaultArg timeout MPHelper.MyTimeOut
        if x.IsClosed || x.IsDisposed then 
            async{return None}
        else
        let fmsg ch = MsgToServer.Control (GetReply (msg, ch))
        async{
            let! ret = Async.Catch (MailBox.PostAndTryAsyncReply(fmsg, timeout))
            match ret with
            |Choice1Of2 ret -> 
                match ret with
                |Some (:? 'Reply as mm) -> return Some mm
                |_ -> return None 
            |Choice2Of2 (exc : Exception) -> 
                return None }


    member val private _IsClosed = false with get,set
    member x.IsClosed = x._IsClosed

    member private x.Close() = 
        if not x._IsClosed then
            x.Dispose()
            x._IsClosed <- true    


    member val private _IsDisposed = false with get, set
    member x.IsDisposed = x._IsDisposed

    member x.Dispose() =
        if not x.IsDisposed then
            try 
                (MailBox :> IDisposable).Dispose()
                Listener.Close()
                GameOrganizer.Dispose()
                Lobby.Dispose()
                for ou in AppOnlineUserData.Values do
                    (ou.User :> IDisposable).Dispose()
                AppOnlineUserData.Clear()
                Async.RunSynchronously(AppUserData.Close())
            finally x._IsDisposed <- true

    interface IDisposable with
        member x.Dispose() = x.Dispose()


