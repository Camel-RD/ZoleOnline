// Learn more about F# at http://fsharp.org

open System
open GameServerLib
open System.Net.Mail

let PrintMenu() =
    printfn "1. Start server"
    printfn "2. Test mail"
    printfn "3. Add user"
    printfn "x. Stop server"

let SendMail (addr:string) from =
    let msg = new MailMessage()
    msg.From <- MailAddress(from, "Mana Zole")
    msg.To.Add(addr)
    msg.Subject <- "Test"
    msg.Body <- "Rinda 1\r\nrinda2"
    let SmtpServer = new SmtpClient("127.0.0.1")
    SmtpServer.Port <- 25
    SmtpServer.Send(msg)
    printfn "Mail sent"

[<EntryPoint>]
let main argv =
    let argv = 
        if argv.Length <> 7 then 
            printfn "Missing arguments, using default"
            [|"7777";"0"; "0"; ""; ""; ""; ""|]
        else argv
    let port = int argv.[0]
    let datafolder = argv.[1]
    let datafolder = if datafolder = "0" then "" else datafolder
    let addhours = int argv.[2]
    let emailserveraddr = argv.[3]
    let emailserverport = if argv.[4] = "" then 25 else int argv.[4]
    let emailfrom = argv.[5]
    let emailfromname = argv.[6]


    printfn "Server port:%A" port
    use server = 
        new AppServer(port, datafolder, addhours, emailserveraddr, 
            emailserverport, emailfrom, emailfromname)

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
            SendMail addr from
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
        |_->
            PrintMenu()
            loop()
    loop() |> ignore
    printfn "Server stopped"
    0 // return an integer exit code
