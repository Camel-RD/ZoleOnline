using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Threading.Tasks;
using GameLib;
using System.Collections.Immutable;
using System.Windows;
using Microsoft.Win32;

namespace ZoleW
{
    public class GameController : IGameForm
    {
        private MainWindow MainWindow = null;
        private PageStartUp PageStartUp = null;
        private PageSettings PageSettings = null;
        private PageLogIn PageLogIn = null;
        private PageRegHelp PageRegHelp = null;
        private PageLobby PageLobby = null;
        private PageCalendar PageCalendar = null;
        private PageRegister PageRegister = null;
        private PageRegister2 PageRegister2 = null;
        private PageNewGame PageNewGame = null;
        private PageNewPrivateGame PageNewPrivateGame = null;
        private PageGame GamePage = null;
        private PagePoints PagePoints = null;
        private PageWait PageWait = null;

        private CardImages CardImages = null;
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


        private double GamePageNormalHeight = 370;
        private double GamePageFullHeight = 590;

        private double MainWindowNormalHeight = 390d * Locator.Scale;
        private double MainWindowFullHeight = 610d * Locator.Scale;

        public enum EState
        {
            none, startGame, beBig, waitingOthers,
            makeMove, Bury, waitForTick, waitForTickSimple
        }

        public EState state = EState.none;

        private int selectedCard1 = -1;
        private int selectedCard2 = -1;

        public GameController(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            CardImages = new CardImages();

            GamePage = new PageGame();
            PagePoints = new PagePoints();
            PageStartUp = new PageStartUp();
            PageSettings = new PageSettings();
            PageLogIn = new PageLogIn();
            PageCalendar = new PageCalendar();
            PageRegHelp = new PageRegHelp();
            PageRegister = new PageRegister();
            PageRegister2 = new PageRegister2();
            PageLobby = new PageLobby();
            PageNewGame = new PageNewGame();
            PageNewPrivateGame = new PageNewPrivateGame();
            PageWait = new PageWait();

            StartUpPageVM = new StartUpPageVM();
            SettingsPageVM = new SettingsPageVM();
            LogInPageVM = new LogInPageVM();
            RegisterPageVM = new RegisterPageVM();
            LobbyPageVM = new LobbyPageVM();
            CalendarPageVM = new CalendarPageVM();
            NewGamePageVM = new NewGamePageVM();
            NewPrivateGamePageVM = new NewPrivateGamePageVM();
            GamePageVM = new GamePageVM(CardImages);
            PointsPageVM = new PointsPageVM();
            WaititngPageVM = new WaititngPageVM();

            PageStartUp.DataContext = StartUpPageVM;
            PageSettings.DataContext = SettingsPageVM;
            PageLogIn.DataContext = LogInPageVM;
            PageRegHelp.DataContext = LogInPageVM;
            PageRegister.DataContext = RegisterPageVM;
            PageRegister2.DataContext = RegisterPageVM;
            PageLobby.DataContext = LobbyPageVM;
            PageCalendar.DataContext = CalendarPageVM;
            PageNewGame.DataContext = NewGamePageVM;
            PageNewPrivateGame.DataContext = NewPrivateGamePageVM;
            GamePage.DataContext = GamePageVM;
            PagePoints.DataContext = PointsPageVM;
            PageWait.DataContext = WaititngPageVM;


            StartUpPageVM.Started += StartUpPageVM_Started;
            StartUpPageVM.BtPlayOnlineClicked += StartUpPageVM_BtPlayOnlineClicked;
            StartUpPageVM.BtSettingsClicked += StartUpPageVM_BtSettingsClicked;

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

            LobbyPageVM.BtBtExitClicked += LobbyPageVM_BtBtExitClicked;
            LobbyPageVM.BtJoinGameClicked += LobbyPageVM_BtJoinGameClicked;
            LobbyPageVM.BtCalendarClicked += LobbyPageVM_BtCalendarClicked;
            LobbyPageVM.BtJoinPrivateGameClicked += LobbyPageVM_BtJoinPrivateGameClicked;

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
            GamePageVM.IsDebugPromptVisible = Properties.Settings.Default.ShowDebugPrompt;

            GamePage.Height = GamePageNormalHeight;
            MainWindow.Height = MainWindowNormalHeight;
            MainWindow.Width = 820d * Locator.Scale;

            Init();
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
            if (string.IsNullOrEmpty(ServerIp)) ServerIp = "zole.klons.id.lv";
            if (string.IsNullOrEmpty(ServerPort)) ServerPort = "7777";
            if (ServerIp?.ToLower() == "klons.id.lv")
                ServerIp = "zole.klons.id.lv";

            var gameformwrapped = GameFormWrapper.GetGUIWrapper(this);
            AppClient = new AppClient(gameformwrapped);

            ToGame = (AppClient as IClient).FromGameUI;
            ToClient = (AppClient as IClient).FromClientUI;

            StartGame();

            MainWindow.Content = PageStartUp;
        }

        void PlayOfflineGame()
        {
            UserName = StartUpPageVM.PlayerName;
            WriteToRegistry();
            LocalPlayerNr = 0;
            PlayerNames = new[] { UserName, "Askolds", "Haralds" };

            GamePageVM.PlayerName1 = PlayerNames[NextPlayerNr];
            GamePageVM.PlayerName2 = PlayerNames[LocalPlayerNr];
            GamePageVM.PlayerName3 = PlayerNames[PriorPlayerNr];
            PagePoints.SetNames(PlayerNames[0], PlayerNames[1], PlayerNames[2]);

            MainWindow.Content = GamePage;
            IsOnlineGame = false;
            ToClient.PlayOffline(UserName);
        }

        public void DoOnGameClosing()
        {
            WriteToRegistry();
            ToClient?.AppClosing();
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
            if(string.IsNullOrEmpty(ServerIp) || string.IsNullOrEmpty(ServerPort))
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

            MainWindow.Content = PageSettings;
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
            MainWindow.Content = PageStartUp;
        }

        private void LogInPageVM_BtCancelClicked(object sender, EventArgs e)
        {
            ToClient.Disconnect();
            MainWindow.Content = PageStartUp;
        }

        private void LogInPageVM_BtCloeseHelpClicked(object sender, EventArgs e)
        {
            MainWindow.Content = PageLogIn;
        }

        private void LogInPageVM_BtHelpClicked(object sender, EventArgs e)
        {
            MainWindow.Content = PageRegHelp;
        }

        private void LogInPageVM_BtRegisterClicked(object sender, EventArgs e)
        {
            string name = LogInPageVM.Name;
            RegisterPageVM.Name = name;
            if (AppClient.UseEmailValidation)
                MainWindow.Content = PageRegister;
            else
                MainWindow.Content = PageRegister2;
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
            ToClient.LogIn(name, psw);
        }
        private void LogInPageVM_BtLogInAsGuestClicked(object sender, EventArgs e)
        {
            string name = LogInPageVM.Name;
            if (string.IsNullOrEmpty(name) )
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
            MainWindow.Content = PageStartUp;
        }

        private void LobbyPageVM_BtBtExitClicked(object sender, EventArgs e)
        {
            ToClient.Disconnect();
            MainWindow.Content = PageStartUp;
        }

        private void LobbyPageVM_BtJoinGameClicked(object sender, EventArgs e)
        {
            ToClient.JoinGame();
        }

        private void LobbyPageVM_BtJoinPrivateGameClicked(object sender, EventArgs e)
        {
            MainWindow.Content = PageNewPrivateGame;
        }

        private void CalendarPageVM_GetUserListClicked(object sender, StringEventArgs e)
        {
            if (string.IsNullOrEmpty(e.EventData)) return;
            ToClient.GetCalendarTagData(e.EventData);
        }

        private void CalendarPageVM_BtBackClick(object sender, EventArgs e)
        {
            MainWindow.Content = PageLobby;
        }

        private void CalendarPageVM_BtSendDataClicked(object sender, StringEventArgs e)
        {
            if (string.IsNullOrEmpty(e.EventData)) return;
            ToClient.SetCalendarData(e.EventData);
            MainWindow.Content = PageLobby;
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
            MainWindow.Content = PageLobby;
        }

        public void ShowMessage(string msg)
        {
            //MainWindow.ShowMessage(msg);
            //MessageBox.Show(msg);
            MessageBoxWindow.Show(MainWindow, msg, "Zolīte", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Game_DebugModeChanged(object sender, EventArgs e)
        {
            if (GamePageVM.IsInDegugMode)
            {
                MainWindow.Height = MainWindowFullHeight;
                GamePage.Height = GamePageFullHeight;
            }
            else
            {
                GamePage.Height = GamePageFullHeight;
                MainWindow.Height = MainWindowNormalHeight;
            }
        }

        private void Game_CardClicked(object sender, IntEventArgs e)
        {
            SelectCard(e.EventData);
        }

        private void Game_YesNoZoleClick(object sender, StringEventArgs e)
        {
            if(e.EventData == "Nē")
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
                ToGame.ReplyStartNewGame(true);
                MainWindow.Content = GamePage;
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
            string psw = RememberPsw ? UserPsw : "";
            RegistryKey regKey;
            regKey = Registry.CurrentUser.CreateSubKey("Software\\Zole");
            regKey.SetValue("Name", UserName);
            regKey.SetValue("Psw", psw);
            regKey.SetValue("ShowArrow", ShowArrow ? "Yes" : "No");
            regKey.SetValue("RememberPsw", RememberPsw ? "Yes" : "No");
            regKey.SetValue("HideOnlineBt", HideOnlineGameButton ? "Yes" : "No");
            regKey.SetValue("IP", ServerIp);
            regKey.SetValue("Port", ServerPort);
        }

        public void ReadFromRegistry()
        {
            RegistryKey regKey;
            regKey = Registry.CurrentUser.CreateSubKey("Software\\Zole");
            UserName = regKey.GetValue("Name", "") as string;
            UserPsw = regKey.GetValue("Psw", "") as string;
            ShowArrow = (regKey.GetValue("ShowArrow", "") as string) == "Yes";
            RememberPsw = (regKey.GetValue("RememberPsw", "") as string) == "Yes";
            HideOnlineGameButton = (regKey.GetValue("HideOnlineBt", "") as string) == "Yes";
            ServerIp = regKey.GetValue("IP", "") as string;
            ServerPort = regKey.GetValue("Port", "7777") as string;
            if (ServerIp == "") ServerIp = Properties.Settings.Default.ServerIp;
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
                    MainWindow.Dispatcher.Invoke(() => { DoOnGO(); });
                    //Device.BeginInvokeOnMainThread(() => { DoOnGO(); });
                });
            }
        }

        public void AskStartGame()
        {
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
            MainWindow.Content = GamePage;
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
            switch (suit)
            {
                case CardSuit.Club: s1 = "c"; break;
                case CardSuit.Diamond: s1 = "d"; break;
                case CardSuit.Heart: s1 = "h"; break;
                case CardSuit.Spade: s1 = "s"; break;
            };
            string s2 = "";
            switch (cardvalue)
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
            if (row == ECardRow.Cards2) return GamePageVM.Cards2[col];
            if (row == ECardRow.Cards3) return GamePageVM.Cards3[col];
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

            if (firstPlayerNr == localplayernr) cnrs = new int[3] { 1, 0, 2 };
            else if (firstPlayerNr == GetPriorPlayerNr(localplayernr)) cnrs = new int[3] { 2, 1, 0 };
            else if (firstPlayerNr == GetNextPlayerNr(localplayernr)) cnrs = new int[3] { 0, 2, 1 };

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
            PagePoints.SetNames(plnm1, plnm2, plnm3);
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
                MainWindow.Content = PagePoints;
            }
            else
                MainWindow.Content = GamePage;
        }

        public void Wait(string msg)
        {
            WaititngPageVM.ST.Message = msg;
            MainWindow.Content = PageWait;
        }

        public void DoStartUp()
        {
            MainWindow.Content = PageStartUp;
        }

        public void ConnectionFailed(string msg)
        {
            ShowMessage("Neizdevās pieslēgties serverim\n\n" + msg);
            MainWindow.Content = PageStartUp;
        }

        public void GoToLoginPage()
        {
            IsOnlineGame = true;
            LogInPageVM.Name = UserName;
            LogInPageVM.Psw = UserPsw;
            MainWindow.Content = PageLogIn;
        }
        
        public void GoToRegisterPage()
        {
            MainWindow.Content = PageRegister;
        }

        public void ShowMessage2(string msg)
        {
            ShowMessage(msg);
        }

        public void GoToLobby()
        {
            PointsPageVM.Clear();
            MainWindow.Content = PageLobby;
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
            MainWindow.Content = PageNewGame;
        }

        public void CancelNewGame(string msg)
        {
            ShowMessage("Spēles sagatavošana pārtraukta");
            MainWindow.Content = PageLobby;
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
            MainWindow.Content = PageCalendar;
        }

        public void CalendarTagData(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            ShowMessage(data);
        }
    }
}
