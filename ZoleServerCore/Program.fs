// Learn more about F# at http://fsharp.org

open System
open GameServerLib
open System.Net.Mail
open System.IO
open FsConfig
open Microsoft.Extensions.Configuration.Json
open Microsoft.Extensions.Configuration


let PrintMenu() =
    printfn "1. Start server"
    printfn "2. Test mail"
    printfn "3. Add user"
    printfn "4. Get Stats"
    printfn "x. Stop server"

let SendMail (addr:string) from (psw:string) =
    let msg = new MailMessage()
    msg.From <- MailAddress(from, "Mana Zole")
    msg.To.Add(addr)
    msg.Subject <- "Test"
    msg.Body <- "Rinda 1\r\nrinda2"
    let SmtpServer = new SmtpClient("127.0.0.1")
    SmtpServer.Port <- 25
    SmtpServer.Credentials <- new System.Net.NetworkCredential(from, psw);
    SmtpServer.Send(msg)
    printfn "Mail sent"

let read_config () =
    let configurationRoot =
        ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("config.json")
            .Build()
    let appConfig = AppConfig(configurationRoot)
    let config = appConfig.Get<ServerOptions>()
    match config with
    |ConfigParseResult.Ok value -> Result.Ok value
    |ConfigParseResult.Error e -> Result.Error (e.ToString())


let read_args (argv:string array) : Result<ServerOptions, string> =
    if argv.Length <> 9 then 
        Result.Error "Missing arguments"
    else
        let options : ServerOptions = {
            Port = int argv.[0]
            DataFolder = if argv.[1] = "0" then "" else argv.[1]
            AddHours = int argv.[2]
            EmailServerAddr = argv.[3]
            EmailServerPort = if argv.[4] = "" then 25 else int argv.[4]
            EmailFrom = argv.[5]
            EmailFromName = argv.[6]
            EmailServerPsw = argv.[7]
            UseEmailValidation = argv.[8] = "1"
        }
        Result.Ok options
        


[<EntryPoint>]
let main argv =
    let options = 
        if argv.Length > 0 then 
            let argv = 
                if argv.Length <> 9 then 
                    printfn "Missing arguments, using default"
                    [|"7777";"0"; "0"; ""; ""; ""; ""; ""; "0"|]
                else argv
            read_args(argv)
        else
            read_config()
    
    let options = 
        match options with
        |Result.Ok value -> value
        |Result.Error e ->
            printfn $"Error: {e}"
            exit -1
            ServerOptions.Empty
    
    printfn "Server port:%A" options.Port
    use server = new AppServer(options)

    let rec loop() = 
        let key = Console.ReadLine()
        match key with
        |"1" -> 
            server.Start()
            printfn "Server started"
            loop()
        |"x" ->
            Async.RunSynchronously(server.Stop())
            0
        |"2" -> 
            printf "\nto:"
            let addr = Console.ReadLine()
            printf "\nfrom:"
            let from = Console.ReadLine()
            printf "\npsw:"
            let psw = Console.ReadLine()
            SendMail addr from psw
            loop()
        |"3" -> 
            printf "\nName:"
            let name = Console.ReadLine()
            printf "\nPsw:"
            let psw = Console.ReadLine()
            if name <> "" && psw <> "" then
                let q = server.AddRegUser (name, psw)
                printf "Done:%A" q
            loop()
        |"4" ->
            let (_CountNewConnections, 
                 _CountRegCodesSent, 
                 _CountRegistrations, 
                 _CountLoggins, 
                 _CountNewGames) = server.GetStats()
            printfn "\nStats:"
            printfn "New Connections: %u" _CountNewConnections
            printfn "Reg Codes Sent: %u" _CountRegCodesSent
            printfn "Registrations: %u" _CountRegistrations
            printfn "Loggins: %u" _CountLoggins
            printfn "New Games: %u" _CountNewGames
            loop()
        |_->
            PrintMenu()
            loop()
    loop() |> ignore
    printfn "Server stopped"
    0 // return an integer exit code
