using System;
using System.Linq;
using System.Threading.Tasks;
using GameLib;
using System.Collections.Immutable;
using Zole3.Pages;
using Zole3.Models;

namespace Zole3
{
    public class GameController : IGameForm
    {
        private App App;
        private PageStartUp PageStartUp = null;
        private PageSettings PageSettings = null;
        private PageLogIn PageLogIn = null;
        private PageRegHelp PageRegHelp = null;
        private PageLobby PageLobby = null;
        private PagePlayerList PagePlayerList = null;
        private PageCalendar PageCalendar = null;
        private PageRegister PageRegister = null;
        private PageRegister2 PageRegister2 = null;
        private PageNewGame PageNewGame = null;
        private PageNewPrivateGame PageNewPrivateGame = null;
        private PageGame GamePage = null;
        private PagePoints PagePoints = null;
        private PageWait PageWait = null;

        private StartUpPageVM StartUpPageVM = null;
        private SettingsPageVM SettingsPageVM = null;
        private LogInPageVM LogInPageVM = null;
        private RegisterPageVM RegisterPageVM = null;
        private LobbyPageVM LobbyPageVM = null;
        private CalendarPageVM CalendarPageVM = null;
        private NewGamePageVM NewGamePageVM = null;
        private NewPrivateGamePageVM NewPrivateGamePageVM = null;
        private GamePageVM GamePageVM = null;
        private PointsPageVM PointsPageVM = null;
        private WaititngPageVM WaititngPageVM = null;

        public AppClient AppClient = null;
        public IUserToX ToGame = null;
        public IUIToClient ToClient = null;

        private string[] PlayerNames = new string[3];
        private int LocalPlayerNr = 0;
        private int NextPlayerNr => LocalPlayerNr < 2 ? LocalPlayerNr + 1 : 0;
        private int PriorPlayerNr => LocalPlayerNr > 0 ? LocalPlayerNr - 1 : 2;
        public CardSet UserCards = null;

        public int myPlayerNr = -1;
        private bool isClosing = false;
        
        private bool IsOnlineGame { get; set; } = false;

        private string UserName = "";
        private string UserPsw = "";
        private bool ShowArrow = true;
        private bool RememberPsw = true;
        private bool HideOnlineGameButton = false;
        private string ServerIp = "";
        private string ServerPort = "";

        public enum EState
        {
            none, startGame, beBig, waitingOthers,
            makeMove, Bury, waitForTick, waitForTickSimple
        }

        public EState state = EState.none;

        private int selectedCard1 = -1;
        private int selectedCard2 = -1;

        static GameController ST;

        public static GameController Create(App app)
        {
            if (ST == null)
            {
                ST = new GameController(app);
            }
            return ST;
        }

        private GameController(App app)
        {
            App = app;

            GamePage = new PageGame();
            PagePoints = new PagePoints();
            PageStartUp = new PageStartUp();
            PageSettings = new PageSettings();
            PageLogIn = new PageLogIn();
            PagePlayerList = new PagePlayerList();
            PageCalendar = new PageCalendar();
            PageRegHelp = new PageRegHelp();
            PageRegister = new PageRegister();
            PageRegister2 = new PageRegister2();
            PageLobby = new PageLobby();
            PageNewGame = new PageNewGame();
            PageNewPrivateGame = new PageNewPrivateGame();
            PageWait = new PageWait();

            StartUpPageVM = StartUpPageVM.ST;
            SettingsPageVM = SettingsPageVM.ST;
            LogInPageVM = LogInPageVM.ST;
            RegisterPageVM = RegisterPageVM.ST;
            LobbyPageVM = LobbyPageVM.ST;
            CalendarPageVM = CalendarPageVM.ST;
            NewGamePageVM = NewGamePageVM.ST;
            NewPrivateGamePageVM = NewPrivateGamePageVM.ST;
            GamePageVM = GamePageVM.ST;
            PointsPageVM = PointsPageVM.DTST;
            WaititngPageVM = WaititngPageVM.ST;


            PageStartUp.BindingContext = StartUpPageVM;
            PageSettings.BindingContext = SettingsPageVM;
            PageLogIn.BindingContext = LogInPageVM;
            PageRegHelp.BindingContext = LogInPageVM;
            PageRegister.BindingContext = RegisterPageVM;
            PageRegister2.BindingContext = RegisterPageVM;
            PageLobby.BindingContext = LobbyPageVM;
            PagePlayerList.BindingContext = LobbyPageVM;
            PageCalendar.BindingContext = CalendarPageVM;
            PageNewGame.BindingContext = NewGamePageVM;
            PageNewPrivateGame.BindingContext = NewPrivateGamePageVM;
            GamePage.BindingContext = GamePageVM;
            PagePoints.BindingContext = PointsPageVM;
            PageWait.BindingContext = WaititngPageVM;

            StartUpPageVM.Started += StartUpPageVM_Started;
            StartUpPageVM.BtPlayOnlineClicked += StartUpPageVM_BtPlayOnlineClicked;
            StartUpPageVM.BtSettingsClicked += StartUpPageVM_BtSettingsClicked;
            StartUpPageVM.BtExitClicked += StartUpPageVM_BtExitClicked;

            SettingsPageVM.BtOkClicked += SettingsPageVM_BtOkClicked;

            LogInPageVM.BtLogInClicked += LogInPageVM_BtLogInClicked;
            LogInPageVM.BtRegisterClicked += LogInPageVM_BtRegisterClicked;
            LogInPageVM.BtCancelClicked += LogInPageVM_BtCancelClicked;
            LogInPageVM.BtHelpClicked += LogInPageVM_BtHelpClicked;
            LogInPageVM.BtCloeseHelpClicked += LogInPageVM_BtCloeseHelpClicked;
            LogInPageVM.BtLogInAsGuestClicked += LogInPageVM_BtLogInAsGuestClicked;

            RegisterPageVM.BtCancelClicked += RegisterPageVM_BtCancelClicked;
            RegisterPageVM.BtGetCodeClicked += RegisterPageVM_BtGetCodeClicked;
            RegisterPageVM.BtRegisterClicked += RegisterPageVM_BtRegisterClicked;

            LobbyPageVM.BtExitClicked += LobbyPageVM_BtExitClicked;
            LobbyPageVM.BtJoinGameClicked += LobbyPageVM_BtJoinGameClicked;
            LobbyPageVM.BtCalendarClicked += LobbyPageVM_BtCalendarClicked;
            LobbyPageVM.BtJoinPrivateGameClicked += LobbyPageVM_BtJoinPrivateGameClicked;
            LobbyPageVM.BtListPlayersClicked += LobbyPageVM_BtListPlayersClicked;
            LobbyPageVM.BtBackFromListClicked += LobbyPageVM_BtBackFromListClicked;

            CalendarPageVM.BtSendDataClicked += CalendarPageVM_BtSendDataClicked;
            CalendarPageVM.BtBackClick += CalendarPageVM_BtBackClick;
            CalendarPageVM.GetUserListClicked += CalendarPageVM_GetUserListClicked;

            NewGamePageVM.BtCancelClicked += NewGamePageVM_BtCancelClicked;
            NewPrivateGamePageVM.BtJoinClicked += NewPrivateGamePageVM_BtJoinClicked;
            NewPrivateGamePageVM.BtCancelClicked += NewPrivateGamePageVM_BtCancelClicked;

            GamePageVM.BtGoClicked += Game_BtGoClicked;
            GamePageVM.YesNoZoleClick += Game_YesNoZoleClick;
            GamePageVM.CardClicked += Game_CardClicked;
            GamePageVM.DebugModeChanged += Game_DebugModeChanged;

            PointsPageVM.BtGoClicked += Game_BtGoClicked;
            PointsPageVM.BtYesClicked += PointsPageVM_BtYesClicked;
            PointsPageVM.BtNoClicked += PointsPageVM_BtNoClicked;

            GamePageVM.IsInDegugMode = false;

            Init();
        }

        public Page MainPage
        {
            get => App.Windows[0].Page;
            set => App.Windows[0].Page = value;
        }

        void Init()
        {
            ShowNames("", "", "", -1, 0);
            ShowPoints(-1);

            GamePageVM.IsYesNoPanelVisible = false;
            GamePageVM.IsButtonGoVisible = false;
            ShowText("");
            ShowPoints(-1);
            ShowNameLabels(false);

            ReadFromRegistry();
            if (string.IsNullOrEmpty(UserName)) UserName = "Es";
            StartUpPageVM.PlayerName = UserName;
            StartUpPageVM.ShowOnlineGame = !HideOnlineGameButton;
            if (string.IsNullOrEmpty(ServerIp)) ServerIp = "localhost";
            if (string.IsNullOrEmpty(ServerPort)) ServerPort = "7777";

            var gameformwrapped = GameFormWrapper.GetGUIWrapper(this);
            AppClient = new AppClient(gameformwrapped);

            ToGame = (AppClient as IClient).FromGameUI;
            ToClient = (AppClient as IClient).FromClientUI;

            StartGame();

            MainPage = PageStartUp;
        }

        void PlayOfflineGame()
        {
            UserName = StartUpPageVM.PlayerName;
            WriteToRegistry();
            LocalPlayerNr = 0;
            PlayerNames = [ UserName, "Askolds", "Haralds" ];

            GamePageVM.PlayerName1 = PlayerNames[NextPlayerNr];
            GamePageVM.PlayerName2 = PlayerNames[LocalPlayerNr];
            GamePageVM.PlayerName3 = PlayerNames[PriorPlayerNr];
            PointsPageVM.PlayerName1 = PlayerNames[0];
            PointsPageVM.PlayerName2 = PlayerNames[1];
            PointsPageVM.PlayerName3 = PlayerNames[2];

            MainPage = GamePage;
            IsOnlineGame = false;
            ToClient.PlayOffline(UserName);
        }

        public void DoOnGameClosing()
        {
            WriteToRegistry();
            //ToClient?.AppClosing();
        }

        public void ShowMessage(string msg)
        {
            MainPage?.DisplayAlert("", msg, "OK");
        }

        private void StartUpPageVM_Started(object sender, StringEventArgs e)
        {
            var playername = e.EventData;
            
            if (string.IsNullOrEmpty(playername))
            {
                ShowMessage("Jānorāda vārds!");
                return;
            }
            if (playername.Length > 15)
            {
                ShowMessage("Vārds ir par garu");
                return;
            }
            PlayOfflineGame();
        }

        private void StartUpPageVM_BtPlayOnlineClicked(object sender, EventArgs e)
        {
            UserName = StartUpPageVM.PlayerName;
            if (string.IsNullOrEmpty(ServerIp) || string.IsNullOrEmpty(ServerPort))
            {
                ShowMessage("Nav norādīta servera IP adrese un ports");
                return;
            }
            if (!int.TryParse(ServerPort, out int port))
            {
                ShowMessage("Norādīts nekorekts servera ports");
                return;
            }
            ToClient.Connect(ServerIp, port);
        }

        private void StartUpPageVM_BtSettingsClicked(object sender, EventArgs e)
        {
            SettingsPageVM.ShowArrow = ShowArrow;
            SettingsPageVM.RememberPsw = RememberPsw;
            SettingsPageVM.HideOnlineGameButton = HideOnlineGameButton;
            SettingsPageVM.ServerIp = ServerIp;
            SettingsPageVM.ServerPort = ServerPort;

            MainPage = PageSettings;
        }

        private void StartUpPageVM_BtExitClicked(object sender, EventArgs e)
        {
            WriteToRegistry();
            App.Quit();
        }

        private void SettingsPageVM_BtOkClicked(object sender, EventArgs e)
        {
            bool changed =
                    ShowArrow != SettingsPageVM.ShowArrow ||
                    RememberPsw != SettingsPageVM.RememberPsw ||
                    HideOnlineGameButton != SettingsPageVM.HideOnlineGameButton ||
                    ServerIp != SettingsPageVM.ServerIp ||
                    ServerPort != SettingsPageVM.ServerPort;
            if (changed)
            {
                ShowArrow = SettingsPageVM.ShowArrow;
                RememberPsw = SettingsPageVM.RememberPsw;
                HideOnlineGameButton = SettingsPageVM.HideOnlineGameButton;
                ServerIp = SettingsPageVM.ServerIp;
                ServerPort = SettingsPageVM.ServerPort;

                WriteToRegistry();
                StartUpPageVM.ShowOnlineGame = !HideOnlineGameButton;
            }
            MainPage = PageStartUp;
        }

        private void LogInPageVM_BtCancelClicked(object sender, EventArgs e)
        {
            ToClient.Disconnect();
            MainPage = PageStartUp;
        }

        private void LogInPageVM_BtCloeseHelpClicked(object sender, EventArgs e)
        {
            MainPage = PageLogIn;
        }

        private void LogInPageVM_BtHelpClicked(object sender, EventArgs e)
        {
            MainPage = PageRegHelp;
        }

        private void LogInPageVM_BtRegisterClicked(object sender, EventArgs e)
        {
            string name = LogInPageVM.Name;
            RegisterPageVM.Name = name;
            if (AppClient.UseEmailValidation)
                MainPage = PageRegister;
            else
                MainPage = PageRegister2;
        }

        private void LogInPageVM_BtLogInClicked(object sender, EventArgs e)
        {
            string name = LogInPageVM.Name;
            string psw = LogInPageVM.Psw;
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(psw))
            {
                ShowMessage("Jānorāda lietāja vārds un parole");
                return;
            }
            if (name.Length > 15 || psw.Length > 15)
            {
                ShowMessage("Ievadīts pārāk garšs teksts");
                return;
            }
            UserName = name;
            UserPsw = psw;
            WriteToRegistry();
            ToClient.LogIn(name, psw);
        }
        private void LogInPageVM_BtLogInAsGuestClicked(object sender, EventArgs e)
        {
            string name = LogInPageVM.Name;
            if (string.IsNullOrEmpty(name))
            {
                ShowMessage("Jānorāda lietāja vārds");
                return;
            }
            if (name.Length > 15)
            {
                ShowMessage("Ievadīts pārāk garšs teksts");
                return;
            }
            UserName = name;
            WriteToRegistry();
            ToClient.LogInAsGuest(name);
        }

        private void RegisterPageVM_BtRegisterClicked(object sender, EventArgs e)
        {
            string name = RegisterPageVM.Name;
            string psw = RegisterPageVM.Psw;
            string regcode = RegisterPageVM.RegCode;
            if (string.IsNullOrEmpty(name))
            {
                ShowMessage("Jānorāda lietotāja vārds");
                return;
            }
            if (string.IsNullOrEmpty(psw))
            {
                ShowMessage("Jānorāda parole");
                return;
            }
            if (AppClient.UseEmailValidation)
            {
                if (string.IsNullOrEmpty(regcode))
                {
                    ShowMessage("Jānorāda reģistrācijas kods");
                    return;
                }
                if (name.Length > 15 || psw.Length > 15 || regcode.Length > 15)
                {
                    ShowMessage("Ievadīts pārāk garšs teksts");
                    return;
                }
            }
            UserName = name;
            UserPsw = psw;
            WriteToRegistry();
            ToClient.Register(name, psw, regcode);
        }

        private void RegisterPageVM_BtGetCodeClicked(object sender, EventArgs e)
        {
            string name = RegisterPageVM.Name;
            string psw = RegisterPageVM.Psw;
            string email = RegisterPageVM.Email;
            if (string.IsNullOrEmpty(name))
            {
                ShowMessage("Jānorāda lietotāja vārds");
                return;
            }
            if (string.IsNullOrEmpty(psw))
            {
                ShowMessage("Jānorāda parole");
                return;
            }
            if (string.IsNullOrEmpty(email))
            {
                ShowMessage("Jānorāda e-pasta adrese");
                return;
            }
            if (name.Length > 15 || psw.Length > 15 || email.Length > 50)
            {
                ShowMessage("Ievadīts pārāk garšs teksts");
                return;
            }
            UserName = name;
            UserPsw = psw;
            ToClient.GetRegCode(name, psw, email);
        }

        private void RegisterPageVM_BtCancelClicked(object sender, EventArgs e)
        {
            ToClient.Disconnect();
            App.Windows[0].Page = PageStartUp;
        }

        private void LobbyPageVM_BtExitClicked(object sender, EventArgs e)
        {
            ToClient.Disconnect();
            MainPage = PageStartUp;
        }

        private void LobbyPageVM_BtJoinGameClicked(object sender, EventArgs e)
        {
            ToClient.JoinGame();
        }

        private void LobbyPageVM_BtJoinPrivateGameClicked(object sender, EventArgs e)
        {
            MainPage = PageNewPrivateGame;
        }

        private void LobbyPageVM_BtBackFromListClicked(object sender, EventArgs e)
        {
            MainPage = PageLobby;
        }

        private void LobbyPageVM_BtListPlayersClicked(object sender, EventArgs e)
        {
            MainPage = PagePlayerList;
        }

        private void CalendarPageVM_GetUserListClicked(object sender, StringEventArgs e)
        {
            if (string.IsNullOrEmpty(e.EventData)) return;
            ToClient.GetCalendarTagData(e.EventData);
        }

        private void CalendarPageVM_BtBackClick(object sender, EventArgs e)
        {
            MainPage = PageLobby;
        }

        private void CalendarPageVM_BtSendDataClicked(object sender, StringEventArgs e)
        {
            if (string.IsNullOrEmpty(e.EventData)) return;
            ToClient.SetCalendarData(e.EventData);
            MainPage = PageLobby;
        }

        private void LobbyPageVM_BtCalendarClicked(object sender, EventArgs e)
        {
            ToClient.GetCalendarData();
        }

        private void NewGamePageVM_BtCancelClicked(object sender, EventArgs e)
        {
            ToClient.CancelNewGame();
        }

        private void NewPrivateGamePageVM_BtJoinClicked(object sender, EventArgs e)
        {
            string name = NewPrivateGamePageVM.Name;
            string psw = NewPrivateGamePageVM.Psw;
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(psw))
            {
                ShowMessage("Jānorāda spēlei nosaukums un parole");
                return;
            }
            if (name.Length > 15 || psw.Length > 15)
            {
                ShowMessage("Ievadīts pārāk garšs teksts");
                return;
            }
            ToClient.JoinPrivateGame(name, psw);
        }

        private void NewPrivateGamePageVM_BtCancelClicked(object sender, EventArgs e)
        {
            MainPage = PageLobby;
        }



        private void Game_DebugModeChanged(object sender, EventArgs e)
        {

        }

        private void Game_CardClicked(object sender, IntEventArgs e)
        {
            SelectCard(e.EventData);
        }

        private void Game_YesNoZoleClick(object sender, StringEventArgs e)
        {
            if (e.EventData == "Nē")
                ReplyYesNoZole(false, false);
            else if (e.EventData == "Zole")
                ReplyYesNoZole(true, true);
            else
                ReplyYesNoZole(true, false);
        }

        private void Game_BtGoClicked(object sender, EventArgs e)
        {
            DoOnGO();
        }

        private void PointsPageVM_BtYesClicked(object sender, EventArgs e)
        {
            if (state == EState.startGame)
            {
                state = EState.none;
                PointsPageVM.ShowYesNo = false;
                MainPage = GamePage;
                ToGame.ReplyStartNewGame(true);
                return;
            }
            throw new Exception("wrong state");
        }

        private void PointsPageVM_BtNoClicked(object sender, EventArgs e)
        {
            if (state == EState.startGame)
            {
                state = EState.none;
                PointsPageVM.ShowYesNo = false;
                ToGame.ReplyStartNewGame(false);
                return;
            }
            throw new Exception("wrong state");
        }

        public void SetMyPlayerNr(int nr)
        {
            myPlayerNr = nr;
        }


        public bool IsClosing()
        {
            return isClosing;
        }

        public void StartGame()
        {
            AppClient.InitAppClient();
        }

        public void WriteToRegistry()
        {

            Preferences.Set("Name", UserName);
            Preferences.Set("Psw", UserPsw);
            Preferences.Set("ShowArrow", ShowArrow ? "Yes" : "No");
            Preferences.Set("RememberPsw", RememberPsw ? "Yes" : "No");
            Preferences.Set("HideOnlineBt", HideOnlineGameButton ? "Yes" : "No");
            Preferences.Set("IP", ServerIp);
            Preferences.Set("Port", ServerPort);
        }

        public void ReadFromRegistry()
        {
            UserName = "Es";
            UserPsw = "";
            ShowArrow = false;
            RememberPsw = true;
            HideOnlineGameButton = false;
            ServerIp = "zole.klons.id.lv";
            ServerPort = "7777";
            
            if (Preferences.ContainsKey("Name"))
                UserName = Preferences.Get("Name", null);
            if (Preferences.ContainsKey("Psw"))
                UserPsw = Preferences.Get("Psw", null);
            if (Preferences.ContainsKey("ShowArrow"))
                ShowArrow = Preferences.Get("ShowArrow", "Yes") == "Yes";
            if (Preferences.ContainsKey("RememberPsw"))
                RememberPsw = Preferences.Get("RememberPsw", "Yes") == "Yes";
            if (Preferences.ContainsKey("HideOnlineBt"))
                HideOnlineGameButton = Preferences.Get("HideOnlineBt", "No") == "Yes";
            if (Preferences.ContainsKey("IP"))
                ServerIp = Preferences.Get("IP", null);
            if (Preferences.ContainsKey("Port"))
                ServerPort = Preferences.Get("Port", null);
            if (ServerIp?.ToLower() == "klons.id.lv")
                ServerIp = "zole.klons.id.lv";
        }

        public void HideThings()
        {
            GamePageVM.IsYesNoPanelVisible = false;
            GamePageVM.IsButtonGoVisible = false;
            GamePageVM.Message = "";
        }


        public void AskBeBig()
        {
            ShowText("Būsi lielais?");
            state = EState.beBig;
            GamePageVM.IsYesNoPanelVisible = true;
            GamePageVM.IsButtonZoleVisible= true;
            GamePageVM.IsButtonGoVisible = false;
        }

        public void AskBuryCards()
        {
            ShowText("Jānorok divas kārtis");
            state = EState.Bury;
            selectedCard1 = -1;
            selectedCard2 = -1;
            GamePageVM.IsButtonGoVisible = true;
        }

        public void AskMakeMove()
        {
            ShowText("Tev jāiet");
            state = EState.makeMove;
            //btGO.Visible = true;
            selectedCard1 = -1;
            selectedCard2 = -1;
        }

        public void AskTick()
        {
            state = EState.waitForTick;
            GamePageVM.IsYesNoPanelVisible = false;
            if (ShowArrow)
                GamePageVM.IsButtonGoVisible = true;
            else
                AutoTick();
        }

        public void AskSimpleTick()
        {
            state = EState.waitForTickSimple;
            GamePageVM.IsYesNoPanelVisible = false;
            AutoTick();
        }

        private void AutoTick()
        {
            if (GamePageVM.IsInDegugMode)
            {
                GamePageVM.IsButtonGoVisible = true;
                PointsPageVM.ShowArrow = false;
            }
            else
            {
                GamePageVM.IsButtonGoVisible = false;
                PointsPageVM.ShowArrow = false;
                Task.Run(async () =>
                {
                    int t = IsOnlineGame ? 1200 : 2000;
                    await Task.Delay(t);
                    //Device.BeginInvokeOnMainThread(() => { DoOnGO(); });
                    MainPage.Dispatcher.Dispatch(() => { DoOnGO(); });
                });
            }
        }

        public void AskStartGame()
        {
            ShowText("Vai sāksim jaunu spēli");
            state = EState.startGame;
            GamePageVM.IsButtonGoVisible = false;
            PointsPageVM.ShowArrow = false;
            PointsPageVM.ShowYesNo = true;
        }

        public void DoStartGame()
        {
            ShowText("Sākam jaunu spēli");
            ShowPoints(0);
            ShowNameLabels(true);
            GamePageVM.IsButtonGoVisible = false;
            GamePageVM.IsYesNoPanelVisible = false;
            GamePageVM.IsButtonZoleVisible = false;
            PointsPageVM.ShowYesNo = false;
            ShowText("");
            MainPage = GamePage;
        }

        public void ReplyYesNoZole(bool yesno, bool zole)
        {
            GamePageVM.IsYesNoPanelVisible = false;
            if (state == EState.beBig)
            {
                state = EState.none;
                ToGame.ReplyBeBig(yesno, zole);
                return;
            }
            if (state == EState.startGame)
            {
                state = EState.none;
                if(!yesno && !IsOnlineGame)
                {
                    App.Quit();
                    return;
                }
                ToGame.ReplyStartNewGame(yesno);
                return;
            }
            throw new Exception("wrong state");
        }

        public void ReplyBuryCards(int cnr1, int cnr2)
        {
            if (state != EState.Bury)
                throw new Exception("wrong state");
            state = EState.none;
            GamePageVM.IsButtonGoVisible = false;
            GamePageVM.Cards[cnr1].IsSelected = false;
            GamePageVM.Cards[cnr2].IsSelected = false;
            var card1 = UserCards.Cards[cnr1];
            var card2 = UserCards.Cards[cnr2];
            ToGame.ReplyBuryCards(card1, card2);
        }

        public void ReplyMakeMove(int cnr)
        {
            if (state != EState.makeMove)
                throw new Exception("wrong state");

            var card = UserCards.Cards[cnr];

            if (!ToGame.IsValidMove(card))
            {
                ShowMessage("šī kārts nederēs");
                return;
            }
            state = EState.none;
            GamePageVM.IsButtonGoVisible = false;
            ToGame.ReplyMakeMove(card);
        }

        public void ReplyTick()
        {
            if (state != EState.waitForTick)
                throw new Exception("wrong state");

            state = EState.none;
            GamePageVM.IsButtonGoVisible = false;
            ToGame.ReplyTick();
        }

        public void ReplyTickSimple()
        {
            if (state != EState.waitForTickSimple)
                throw new Exception("wrong state");

            state = EState.none;
            GamePageVM.IsButtonGoVisible = false;
            ToGame.ReplyTick();
        }

        public void ReplyStopWaiting()
        {
            if (state != EState.waitingOthers)
                throw new Exception("wrong state");
            state = EState.none;
            //Game.ReplyStopWaiting();
        }

        public void ReplyStartGame()
        {
            if (state != EState.startGame)
                throw new Exception("wrong state");
            GamePageVM.IsButtonGoVisible = false;
            GamePageVM.IsYesNoPanelVisible = false;
        }

        void DoOnGO()
        {
            if (state == EState.waitForTick)
            {
                GamePageVM.IsButtonGoVisible = false;
                PointsPageVM.ShowArrow = false;
                ReplyTick();
                return;
            }
            if (state == EState.waitForTickSimple)
            {
                GamePageVM.IsButtonGoVisible = false;
                PointsPageVM.ShowArrow = false;
                ReplyTickSimple();
                return;
            }
            if (state == EState.makeMove)
            {
                if (selectedCard1 == -1) return;
                GamePageVM.IsButtonGoVisible = false;
                ReplyMakeMove(selectedCard1);
                return;
            }
            if (state == EState.Bury)
            {
                if (selectedCard1 == -1) return;
                if (selectedCard2 == -1) return;
                GamePageVM.IsButtonGoVisible = false;
                ReplyBuryCards(selectedCard1, selectedCard2);
                return;
            }
        }

        void SelectCard(int col)
        {
            //var erow = ECardRow.Cards1;
            if (state != EState.Bury && state != EState.makeMove) return;
            if (UserCards.Cards.Count <= col) return;

            if (state == EState.makeMove)
            {
                if (selectedCard1 > -1)
                {
                    //pb = GetPB(erow, selectedCard1);
                    //pb.Top = pb.Top + pbMoveDelta;
                }
                if (selectedCard1 == col)
                {
                    selectedCard1 = -1;
                    return;
                }
                selectedCard1 = col;
                ReplyMakeMove(col);

                //pb = GetPB(erow, selectedCard1);
                //pb.Top = pb.Top - pbMoveDelta;
                return;
            }

            if (selectedCard1 == col)
            {
                GamePageVM.Cards[selectedCard1].IsSelected = false;
                selectedCard1 = -1;
                return;
            }
            if (selectedCard2 == col)
            {
                GamePageVM.Cards[selectedCard2].IsSelected = false;
                selectedCard2 = -1;
                return;
            }
            if (selectedCard1 == -1)
            {
                GamePageVM.Cards[col].IsSelected = true;
                selectedCard1 = col;
                return;
            }
            if (selectedCard2 == -1)
            {
                GamePageVM.Cards[col].IsSelected = true;
                selectedCard2 = col;
                return;
            }

        }

        public enum ECardRow
        {
            CardsOnDesk = 0, Cards1 = 1, Cards2 = 2, Cards3 = 3
        }

        public string GetCardImageName(CardValue cardvalue, CardSuit suit)
        {
            string s1 = "";
            switch(suit)
            {
                case CardSuit.Club: s1 = "c"; break;
                case CardSuit.Diamond: s1 = "d"; break;
                case CardSuit.Heart: s1 = "h"; break;
                case CardSuit.Spade: s1 = "s"; break;
            };
            string s2 = "";
                 switch(cardvalue)
            {
                case CardValue.Ace: s2 = "1"; break;
                case CardValue.Jack: s2 = "j"; break;
                case CardValue.King: s2 = "k"; break;
                case CardValue.Queen: s2 = "q"; break;
                case CardValue.V7: s2 = "7"; break;
                case CardValue.V8: s2 = "8"; break;
                case CardValue.V9: s2 = "9"; break;
                case CardValue.V10: s2 = "10"; break;
            };
            string ret = "empty";
            if (s1 != "" && s2 != "")
                ret = s1 + s2;
            return ret;
        }

        private CardVM GetCard(ECardRow row, int col)
        {
            if (col < 0 || (row == ECardRow.CardsOnDesk && col > 2) || col > 9)
                throw new ArgumentOutOfRangeException("col");
            if (row == ECardRow.CardsOnDesk) return GamePageVM.CardsOnDesk[col];
            if (row == ECardRow.Cards1) return GamePageVM.Cards[col];
            if (row == ECardRow.Cards2) return null;
            if (row == ECardRow.Cards3) return null;
            return null;
        }

        public void SetCard(ECardRow row, int col, CardValue cardvalue, CardSuit suit)
        {
            var card = GetCard(row, col);
            if (card == null) return;
            card.ImgName = GetCardImageName(cardvalue, suit);
        }

        private int GetNextPlayerNr(int plnr)
        {
            return plnr < 2 ? plnr + 1 : 0;
        }

        private int GetPriorPlayerNr(int plnr)
        {
            return plnr > 0 ? plnr - 1 : 2;
        }

        public void ShowCards(CardSet playerCards, CardSet cardsOnDesk, int firstPlayerNr, int localplayernr)
        {
            var cards = ImmutableArray<CardSet>.Empty.Add(playerCards);
            ShowCards2(cards, cardsOnDesk, firstPlayerNr, localplayernr);
        }

        public void ShowCards2(ImmutableArray<CardSet> playerCards, CardSet cardsOnDesk, int firstPlayerNr, int localplayernr)
        {
            int i, j, n;
            Card card1;
            int[] cnrs = null;

            UserCards = playerCards[0];

            if (firstPlayerNr == localplayernr) cnrs = [ 1, 0, 2 ];
            else if (firstPlayerNr == GetPriorPlayerNr(localplayernr)) cnrs = [ 2, 1, 0 ];
            else if (firstPlayerNr == GetNextPlayerNr(localplayernr)) cnrs = [ 0, 2, 1 ];

            n = cardsOnDesk.Cards.Count;
            for (i = 0; i < n; i++)
            {
                card1 = cardsOnDesk.Cards[i];
                SetCard(ECardRow.CardsOnDesk, cnrs[i], card1.Value, card1.Suit);
            }
            for (i = n; i < 3; i++)
            {
                SetCard(ECardRow.CardsOnDesk, cnrs[i], CardValue.None, CardSuit.Club);
            }

            //int[] plnrs = { LocalPlayerNr, NextPlayerNr, PriorPlayerNr };
            int ct = playerCards.Length == 1 || playerCards[1].Cards.Count == 0 ? 1 : 3;
            for (i = 0; i < ct; i++)
            {
                var pcards = playerCards[i];
                n = pcards.Cards.Count;
                for (j = 0; j < n; j++)
                {
                    card1 = pcards.Cards[j];
                    SetCard((ECardRow)(i + 1), j, card1.Value, card1.Suit);
                }
                for (j = n; j < 10; j++)
                {
                    SetCard((ECardRow)(i + 1), j, CardValue.None, CardSuit.Club);
                }
            }
        }

        public void ShowNameLabels(bool b)
        {
            GamePageVM.IsNamePlatesVisible = b;
        }

        public void ShowText(string s)
        {
            GamePageVM.Message = s;
        }

        public void ShowPoints(int points)
        {
            if (points == -1)
            {
                GamePageVM.IsPointsVisible = false;
                return;
            }
            GamePageVM.IsPointsVisible = true;
            GamePageVM.Points = points;
        }

        public void ShowNames(string s1, string s2, string s3, int highlight, int localplayernr)
        {
            SetNames(s1, s2, s3, localplayernr);
            GamePageVM.IsNameHighlighted1 = highlight == NextPlayerNr;
            GamePageVM.IsNameHighlighted2 = highlight == LocalPlayerNr;
            GamePageVM.IsNameHighlighted3 = highlight == PriorPlayerNr;
        }

        public void SetNames(string plnm1, string plnm2, string plnm3, int localplayernr)
        {
            LocalPlayerNr = localplayernr;
            var names = new[] { plnm1, plnm2, plnm3 };
            PlayerNames = new[] { plnm1, plnm2, plnm3 };
            PlayerNames[1] = names[localplayernr];
            PlayerNames[0] = names[GetNextPlayerNr(localplayernr)];
            PlayerNames[2] = names[GetPriorPlayerNr(localplayernr)];

            GamePageVM.PlayerName1 = PlayerNames[0];
            GamePageVM.PlayerName2 = PlayerNames[1];
            GamePageVM.PlayerName3 = PlayerNames[2];

            PointsPageVM.PlayerName1 = plnm1;
            PointsPageVM.PlayerName2 = plnm2;
            PointsPageVM.PlayerName3 = plnm3;
        }

        public void AddRowToStats(int v1, int v2, int v3, int localplayernr)
        {
            PointsPageVM.AddPoints(v1, v2, v3);
            PagePoints.ScrollToEnd();
        }

        public void ShowStats(bool b)
        {
            if (b)
            {
                //PointsPageVM.ShowArrow = ShowArrow;
                PointsPageVM.ShowArrow = false;
                PointsPageVM.ShowYesNo = false;
                MainPage = PagePoints;
            }
            else
                MainPage = GamePage;
        }


        public void Wait(string msg)
        {
            WaititngPageVM.ST.Message = msg;
            MainPage = PageWait;
        }

        public void DoStartUp()
        {
            MainPage = PageStartUp;
        }

        public void ConnectionFailed(string msg)
        {
            ShowMessage("Neizdevās pieslēgties serverim\n\n" + msg);
            MainPage = PageStartUp;
        }

        public void GoToLoginPage()
        {
            IsOnlineGame = true;
            LogInPageVM.Name = UserName;
            LogInPageVM.Psw = UserPsw;
            MainPage = PageLogIn;
        }

        public void GoToRegisterPage()
        {
            MainPage = PageRegister;
        }

        public void ShowMessage2(string msg)
        {
            ShowMessage(msg);
        }

        public void GoToLobby()
        {
            PointsPageVM.Clear();
            MainPage = PageLobby;
        }

        public void SetLobbyData(LobbyData data)
        {
            LobbyPageVM.PlayerOnlineCount = data.playerCount;
            LobbyPageVM.PlayersOnline.Clear();
            AddLobbyData(data);
        }

        public void AddLobbyData(LobbyPlayerInfo data)
        {
            var pl = LobbyPageVM.PlayersOnline.Where(d => d.Name == data.name).FirstOrDefault();
            if (pl != null) return;
            var pli = new LobbyPlayerVM()
            {
                Name = data.name,
                ExtraInfo = data.info
            };
            LobbyPageVM.PlayersOnline.Add(pli);
            LobbyPageVM.PlayerOnlineCount = LobbyPageVM.PlayersOnline.Count;
        }

        public void RemoveLobbyData(string name)
        {
            var pl = LobbyPageVM.PlayersOnline.Where(d => d.Name == name).FirstOrDefault();
            if (pl == null) return;
            LobbyPageVM.PlayersOnline.Remove(pl);
            LobbyPageVM.PlayerOnlineCount = LobbyPageVM.PlayersOnline.Count;
        }

        public void UpdateLobbyData(LobbyPlayerInfo data)
        {
            var pl = LobbyPageVM.PlayersOnline.Where(d => d.Name == data.name).FirstOrDefault();
            if (pl == null) return;
            pl.Name = data.name;
            pl.ExtraInfo = data.info;
            LobbyPageVM.PlayerOnlineCount = LobbyPageVM.PlayersOnline.Count;
        }

        public void AddLobbyData(LobbyData data)
        {
            LobbyPageVM.PlayerOnlineCount = data.playerCount;
            foreach (var pl in data.players)
            {
                var pli = new LobbyPlayerVM()
                {
                    Name = pl.name,
                    ExtraInfo = pl.info
                };
                LobbyPageVM.PlayersOnline.Add(pli);
            }
        }


        public void RemoveLobbyData(LobbyData data)
        {
            LobbyPageVM.PlayerOnlineCount = data.playerCount;
            var pls = LobbyPageVM.PlayersOnline
            .Where(pl =>
                data.players
                .Where(pl2 => pl2.name == pl.Name)
                .FirstOrDefault() != null);
            foreach (var pl in pls)
                LobbyPageVM.PlayersOnline.Remove(pl);
        }

        public void UpdateLobbyData(LobbyData data)
        {
            LobbyPageVM.PlayerOnlineCount = data.playerCount;
            var pls = LobbyPageVM.PlayersOnline
            .Where(pl =>
                data.players
                .Where(pl2 => pl2.name == pl.Name)
                .FirstOrDefault() != null);
            foreach (var pl in data.players)
            {
                var pl2 =
                    LobbyPageVM.PlayersOnline
                    .Where(plo => plo.Name == pl.name)
                    .FirstOrDefault();
                if (pl2 == null) continue;
                pl2.Name = pl.name;
                pl2.ExtraInfo = pl.info;
            }
        }

        public void GoToNewGame()
        {
            NewGamePageVM.Players.Clear();
            MainPage = PageNewGame;
        }

        public void CancelNewGame(string msg)
        {
            ShowMessage("Spēles sagatavošana pārtraukta");
            MainPage = PageLobby;
        }

        public void GotPlayerForNewGame(string name, string info)
        {
            var newpl = new NewGamePlayerPageVM() { Name = name, ExtraInfo = info };
            NewGamePageVM.Players.Add(newpl);
        }

        public void LostPlayerForNewGame(string name)
        {
            var pl = NewGamePageVM.Players
                .Where(p => p.Name == name)
                .FirstOrDefault();
            if (pl == null) return;
            NewGamePageVM.Players.Remove(pl);
        }

        public void CalendarData(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            if (!CalendarPageVM.SetData(data)) return;
            MainPage = PageCalendar;
        }

        public void CalendarTagData(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            ShowMessage(data);
        }



    }
}
