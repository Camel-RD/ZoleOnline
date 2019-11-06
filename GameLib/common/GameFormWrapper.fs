namespace GameLib

type GameFormBareWrapper(gameform : IGameForm) =
    interface IGameForm with
        member x.ShowMessage(msg) =         gameform.ShowMessage(msg)
        member x.AskStartGame() =           gameform.AskStartGame()
        member x.DoStartGame() =            gameform.DoStartGame()
        member x.AskBeBig() =               gameform.AskBeBig()
        member x.AskBuryCards() =           gameform.AskBuryCards()
        member x.AskMakeMove() =            gameform.AskMakeMove()
        member x.AskTick() =                gameform.AskTick()
        member x.AskSimpleTick() =          gameform.AskSimpleTick()
        member x.ShowText s =               gameform.ShowText(s)
        member x.ShowPoints points =        gameform.ShowPoints points
        member x.ShowNames s1 s2 s3 highlight localplayernr = gameform.ShowNames s1 s2 s3 highlight localplayernr
        member x.HideThings () =            gameform.HideThings()
        member x.AddRowToStats v1 v2 v3 localplayernr =   gameform.AddRowToStats v1 v2 v3 localplayernr
        member x.ShowStats b =              gameform.ShowStats b
    
        member this.DoStartUp() =           gameform.DoStartUp()
        member this.CancelNewGame(msg) =    gameform.CancelNewGame(msg)
        member this.ConnectionFailed(msg) = gameform.ConnectionFailed(msg)
        member this.GoToLobby() =           gameform.GoToLobby()
        member this.GoToLoginPage() =       gameform.GoToLoginPage()
        member this.GoToRegisterPage() =    gameform.GoToRegisterPage()
        member this.GoToNewGame() =         gameform.GoToNewGame()
        member this.GotPlayerForNewGame name info = gameform.GotPlayerForNewGame name info
        member this.IsClosing() =           gameform.IsClosing()
        member this.ShowMessage2(msg) =      gameform.ShowMessage2(msg)
        member this.LostPlayerForNewGame(name) = gameform.LostPlayerForNewGame(name)
        member this.SetLobbyData(data) =    gameform.SetLobbyData(data)
        member this.AddLobbyData  data =    gameform.AddLobbyData(data)
        member this.RemoveLobbyData name =  gameform.RemoveLobbyData(name)
        member this.UpdateLobbyData data =  gameform.UpdateLobbyData(data)
        member this.CalendarData data =     gameform.CalendarData(data)
        member this.CalendarTagData data =  gameform.CalendarTagData(data)

        member this.SetMyPlayerNr(nr) =     gameform.SetMyPlayerNr(nr)
        member this.SetNames plnm1 plnm2 plnm3 localplayernr = 
                                            gameform.SetNames plnm1 plnm2 plnm3 localplayernr
        member x.ShowCards cards cardsondesk firstplayernr localplayernr = 
                                            gameform.ShowCards cards cardsondesk firstplayernr localplayernr
        member this.ShowCards2 cards cardsondesk firstplayernr localplayernr = 
                                            gameform.ShowCards2 cards cardsondesk firstplayernr localplayernr
        member this.Wait(msg) =             gameform.Wait(msg)


type GameFormWrapper(gameform : IGameForm, fw : (unit -> unit) -> unit) =

    static member GetGUIWrapper(gameform) =
        let context = System.ComponentModel.AsyncOperationManager.SynchronizationContext
        let runInGuiContext f =
                context.Post(new System.Threading.SendOrPostCallback(fun _ -> f()), null)
        GameFormWrapper(gameform, runInGuiContext)

    interface IGameForm with
        member x.AskStartGame() =       fw <| fun _ -> gameform.AskStartGame()
        member x.DoStartGame() =        fw <| fun _ -> gameform.DoStartGame()
        member x.AskBeBig() =           fw <| fun _ -> gameform.AskBeBig()
        member x.AskBuryCards() =       fw <| fun _ -> gameform.AskBuryCards()
        member x.AskMakeMove() =        fw <| fun _ -> gameform.AskMakeMove()
        member x.AskTick() =            fw <| fun _ -> gameform.AskTick()
        member x.AskSimpleTick() =      fw <| fun _ -> gameform.AskSimpleTick()
        member x.ShowText s =           fw <| fun _ -> gameform.ShowText(s)
        member x.ShowPoints points =    fw <| fun _ -> gameform.ShowPoints points 
        member x.ShowNames s1 s2 s3 highlight localplayernr = 
                                        fw <| fun _ -> gameform.ShowNames s1 s2 s3 highlight localplayernr
        member x.HideThings () =        fw <| fun _ -> gameform.HideThings()
        member x.AddRowToStats v1 v2 v3 localplayernr = fw <| fun _ -> gameform.AddRowToStats v1 v2 v3 localplayernr
        member x.ShowStats b =          fw <| fun _ -> gameform.ShowStats b
    
        member this.DoStartUp() =           fw <| fun _ -> gameform.DoStartUp()
        member this.CancelNewGame(msg) =    fw <| fun _ -> gameform.CancelNewGame(msg)
        member this.ConnectionFailed(msg) = fw <| fun _ -> gameform.ConnectionFailed(msg)
        member this.GoToLobby() =           fw <| fun _ -> gameform.GoToLobby()
        member this.GoToLoginPage() =       fw <| fun _ -> gameform.GoToLoginPage()
        member this.GoToRegisterPage() =    fw <| fun _ -> gameform.GoToRegisterPage()
        member this.GoToNewGame() =         fw <| fun _ -> gameform.GoToNewGame()
        member this.GotPlayerForNewGame name info = fw <| fun _ -> gameform.GotPlayerForNewGame name info
        member this.IsClosing() =           false
        member this.ShowMessage2(msg) =      fw <| fun _ -> gameform.ShowMessage2(msg)
        member this.LostPlayerForNewGame(name) =fw <| fun _ -> gameform.LostPlayerForNewGame(name)
        member this.SetLobbyData(data) =    fw <| fun _ -> gameform.SetLobbyData(data)
        member this.AddLobbyData data =     fw <| fun _ -> gameform.AddLobbyData(data)
        member this.RemoveLobbyData name =  fw <| fun _ -> gameform.RemoveLobbyData(name)
        member this.UpdateLobbyData data =  fw <| fun _ -> gameform.UpdateLobbyData(data)
        member this.CalendarData data =  fw <| fun _ -> gameform.CalendarData(data)
        member this.CalendarTagData data =  fw <| fun _ -> gameform.CalendarTagData(data)
        member this.SetMyPlayerNr(nr) =     fw <| fun _ -> gameform.SetMyPlayerNr(nr)
        member this.SetNames plnm1 plnm2 plnm3 localplayernr =    
                                            fw <| fun _ -> gameform.SetNames plnm1 plnm2 plnm3 localplayernr
        member x.ShowCards cards cardsondesk firstplayernr localplayernr = 
                                            fw <| fun _ -> gameform.ShowCards cards cardsondesk firstplayernr localplayernr
        member this.ShowCards2 cards cardsondesk firstplayernr localplayernr = 
                                            fw <| fun _ -> gameform.ShowCards2 cards cardsondesk firstplayernr localplayernr
        member this.Wait(msg) =             fw <| fun _ -> gameform.Wait(msg)
        member x.ShowMessage(msg) =     fw <| fun _ -> gameform.ShowMessage(msg)

