using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace ZoleX
{
    public class CardImages
    {
        public Dictionary<string, ImageSource> Images = new Dictionary<string, ImageSource>();
        public ImageSource ImageEmpty { get; private set; } = null;
        public CardImages()
        {
            var suits = new[] { "c", "d", "h", "s" };
            var values = new[] { "1", "9", "10", "k", "j", "q" };
            var sv = new[] {"d7","d8", "empty"};
            for (int i = 0; i < suits.Length; i++)
            {
                for (int j = 0; j < values.Length; j++)
                {
                    var nm = $"{suits[i]}{values[j]}";
                    var s = Device.RuntimePlatform == Device.WPF ? $"images/{nm}.png" : $"{nm}.png";
                    Images[nm] = ImageSource.FromFile(s);
                }
            }
            for (int i = 0; i < sv.Length; i++)
            {
                var nm = sv[i];
                var s = Device.RuntimePlatform == Device.WPF ? $"images/{nm}.png" : $"{nm}.png";
                Images[nm] = ImageSource.FromFile(s);
            }
            ImageEmpty = Images["empty"];
        }
    }

    public abstract class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class CardVM : ViewModel
    {
        private CardImages CardImages = null;
        public int Index { get; private set; } = 0;
        private string _ImgName = "";
        private bool _IsSelected = false;

        public CardVM(CardImages cardImages, int index)
        {
            CardImages = cardImages;
            Index = index;
        }

        public ImageSource ImgSource
        {
            get
            {
                if (!CardImages.Images.TryGetValue(ImgName, out var img))
                    img = CardImages.ImageEmpty;
                return img;
            }
        }

        public string ImgName
        {
            get { return _ImgName; }
            set
            {
                if (_ImgName == value) return;
                _ImgName = value;
                OnPropertyChanged("ImgName");
                OnPropertyChanged("ImgSource");
            }
        }

        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                _IsSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public event EventHandler<IntEventArgs> CardClicked;

        public void OnCardClicked(object sender, EventArgs ev)
        {
            if(CardClicked != null)
                CardClicked.Invoke(this, new IntEventArgs(Index));
        }
    }

    public class IntEventArgs : EventArgs
    {
        public int EventData { get; private set; }
        public IntEventArgs(int EventData)
        {
            this.EventData = EventData;
        }
    }

    public class StringEventArgs : EventArgs
    {
        public string EventData { get; private set; }
        public StringEventArgs(string EventData)
        {
            this.EventData = EventData;
        }
    }

    public class StartUpPageVM : ViewModel
    {
        private string _PlayerName = "Es";
        private bool _ShowOnlineGame = true;

        public string PlayerName
        {
            get { return _PlayerName; }
            set
            {
                if (_PlayerName == value) return;
                _PlayerName = value;
                OnPropertyChanged("PlayerName");
            }
        }

        public bool ShowOnlineGame
        {
            get { return _ShowOnlineGame; }
            set
            {
                if (_ShowOnlineGame == value) return;
                _ShowOnlineGame = value;
                OnPropertyChanged("ShowOnlineGame");
            }
        }

        private static StartUpPageVM _ST = null;
        public static StartUpPageVM ST
        {
            get
            {
                if (_ST == null)
                {
                    _ST = new StartUpPageVM();
                }
                return _ST;
            }
        }

        public StartUpPageVM() { }

        public void OnStarted() => Started?.Invoke(this, new StringEventArgs(PlayerName));
        public void OnBtPlayOnlineClick() => BtPlayOnlineClicked?.Invoke(this, new EventArgs());
        public void OnBtSettingsClicked() => BtSettingsClicked?.Invoke(this, new EventArgs());

        public event EventHandler<StringEventArgs> Started;
        public event EventHandler BtPlayOnlineClicked;
        public event EventHandler BtSettingsClicked;
    }

    public class PointsRow
    {
        public int Points1 { get; set; } = 0;
        public int Points2 { get; set; } = 0;
        public int Points3 { get; set; } = 0;

        public PointsRow() { }
        public PointsRow(int pt1, int pt2, int pt3)
        {
            Points1 = pt1;
            Points2 = pt1;
            Points3 = pt1;
        }

        public void AddPoints(int pt1, int pt2, int pt3)
        {
            Points1 += pt1;
            Points2 += pt2;
            Points3 += pt3;
        }

        public PointsRow Copy()
        {
            var new_row = new PointsRow()
            {
                Points1 = Points1,
                Points2 = Points2,
                Points3 = Points3
            };
            return new_row;
        }

    }

    public class PointsPageVM : ViewModel
    {
        public ObservableCollection<PointsRow> PointsRows { get; } = new ObservableCollection<PointsRow>();
        public PointsRow CurrentPoint { get; } = new PointsRow();
        private string _PlayerName1 = "", _PlayerName2 = "", _PlayerName3 = "";
        private PointsRow _LastItem = null;
        private bool _ShowArrow = true;

        public void AddPoints(int pt1, int pt2, int pt3)
        {
            CurrentPoint.AddPoints(pt1, pt2, pt3);
            var new_row = CurrentPoint.Copy();
            PointsRows.Add(new_row);
            LastItem = PointsRows.LastOrDefault();
            OnPropertyChanged("PointsRows");
        }


        public void Clear()
        {
            PointsRows.Clear();
            CurrentPoint.Points1 = 0;
            CurrentPoint.Points2 = 0;
            CurrentPoint.Points3 = 0;
            ShowArrow = true;
        }

        public void SetNames(string nm1, string nm2, string nm3)
        {
            PlayerName1 = nm1;
            PlayerName2 = nm2;
            PlayerName3 = nm3;
        }

        private void MockData()
        {
            PlayerName1 = "Askolds";
            PlayerName2 = "Aivars";
            PlayerName3 = "Haralds";
            var pr = new PointsRow(4, -2, -2);
            PointsRows.Add(pr);
            PointsRows.Add(pr);
        }

        private static PointsPageVM _DTST = null;
        public static PointsPageVM DTST 
        {
            get
            {
                if (_DTST == null)
                {
                    _DTST = new PointsPageVM();
                    if (DesignMode.IsDesignModeEnabled)
                    {
                        _DTST.MockData();
                    }
                }
                return _DTST;
            }
        }

        public string PlayerName1
        {
            get { return _PlayerName1; }
            set
            {
                if (_PlayerName1 == value) return;
                _PlayerName1 = value;
                OnPropertyChanged("PlayerName1");
            }
        }

        public string PlayerName2
        {
            get { return _PlayerName2; }
            set
            {
                if (_PlayerName2 == value) return;
                _PlayerName2 = value;
                OnPropertyChanged("PlayerName2");
            }
        }

        public string PlayerName3
        {
            get { return _PlayerName3; }
            set
            {
                if (_PlayerName3 == value) return;
                _PlayerName3 = value;
                OnPropertyChanged("PlayerName3");
            }
        }

        public bool ShowArrow
        {
            get { return _ShowArrow; }
            set
            {
                if (_ShowArrow == value) return;
                _ShowArrow = value;
                OnPropertyChanged("ShowArrow");
            }
        }

        public PointsRow LastItem 
        {
            get { return _LastItem; }
            set
            {
                if (_LastItem == value) return;
                _LastItem = value;
                OnPropertyChanged("LastItem");
            }
        }

        public void OnBtGoClicked() => BtGoClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtGoClicked;
    }

    public class GamePageVM : ViewModel
    {
        public ObservableCollection<CardVM> Cards { get; private set; } = null;
        public CardVM CardsOnDesk1 { get; private set; } = null;
        public CardVM CardsOnDesk2 { get; private set; } = null;
        public CardVM CardsOnDesk3 { get; private set; } = null;
        public CardVM[] CardsOnDesk { get; private set; } = null;

        CardImages CardImages = null;

        private string _PlayerName1 = "", _PlayerName2 = "", _PlayerName3 = "";
        private string _Message = "";
        private int _Points = 0;
        private bool _IsNameHighlighted1 = false, _IsNameHighlighted2 = false, _IsNameHighlighted3 = false;
        private bool _IsButtonGoVisible = false;
        private bool _IsYesNoPanelVisible = false;
        private bool _IsButtonZoleVisible = false;
        private bool _IsPointsVisible = true;
        private bool _IsNamePlatesVisible = true;
        private bool _IsInDegugMode = false;


        private static GamePageVM _ST = null;
        public static GamePageVM ST
        {
            get
            {
                if (_ST == null)
                    _ST = new GamePageVM();
                return _ST;
            }
        }

        public GamePageVM()
        {
            CardImages = new CardImages();
            Cards = new ObservableCollection<CardVM>();
            for (int i = 0; i < 10; i++)
            {
                var card = new CardVM(CardImages, i);
                card.CardClicked += Card_CardClicked;
                Cards.Add(card);
            }
            CardsOnDesk1 = new CardVM(CardImages, -1);
            CardsOnDesk2 = new CardVM(CardImages, -2);
            CardsOnDesk3 = new CardVM(CardImages, -3);
            CardsOnDesk = new[] { CardsOnDesk1, CardsOnDesk2, CardsOnDesk3 };

            if (DesignMode.IsDesignModeEnabled) MockData();
        }

        private void MockData()
        {
            for (int i = 0; i < 10; i++)
            {
                Cards[i].ImgName = "c10";
            }
            CardsOnDesk1.ImgName = "c10";
            CardsOnDesk2.ImgName = "dq";
            CardsOnDesk3.ImgName = "hk";
            PlayerName1 = "Askolds";
            PlayerName2 = "Aivars";
            PlayerName3 = "Haralds";
            Message = "Neviens negrib būt lielais, spēlēsim galda zoli";
            Points = 50;
            IsNamePlatesVisible = true;
            IsPointsVisible = true;
            IsButtonGoVisible = true;
            IsNameHighlighted1 = true;
            IsYesNoPanelVisible = true;
            IsButtonZoleVisible = true;
        }

        public void Clear()
        {
            foreach (var card in Cards)
                card.CardClicked -= Card_CardClicked;
            //Cards.Clear()
        }


        public CardVM Card1 => Cards[0];
        public CardVM Card2 => Cards[1];
        public CardVM Card3 => Cards[2];
        public CardVM Card4 => Cards[3];
        public CardVM Card5 => Cards[4];
        public CardVM Card6 => Cards[5];
        public CardVM Card7 => Cards[6];
        public CardVM Card8 => Cards[7];
        public CardVM Card9 => Cards[8];
        public CardVM Card10 => Cards[9];


        public string PlayerName1
        {
            get { return _PlayerName1; }
            set
            {
                if (_PlayerName1 == value) return;
                _PlayerName1 = value;
                OnPropertyChanged("PlayerName1");
            }
        }

        public string PlayerName2
        {
            get { return _PlayerName2; }
            set
            {
                if (_PlayerName2 == value) return;
                _PlayerName2 = value;
                OnPropertyChanged("PlayerName2");
            }
        }

        public string PlayerName3
        {
            get { return _PlayerName3; }
            set
            {
                if (_PlayerName3 == value) return;
                _PlayerName3 = value;
                OnPropertyChanged("PlayerName3");
            }
        }

        public bool IsNameHighlighted1
        {
            get { return _IsNameHighlighted1; }
            set
            {
                if (_IsNameHighlighted1 == value) return;
                _IsNameHighlighted1 = value;
                OnPropertyChanged("IsNameHighlighted1");
            }
        }

        public bool IsNameHighlighted2
        {
            get { return _IsNameHighlighted2; }
            set
            {
                if (_IsNameHighlighted2 == value) return;
                _IsNameHighlighted2 = value;
                OnPropertyChanged("IsNameHighlighted2");
            }
        }

        public bool IsNameHighlighted3
        {
            get { return _IsNameHighlighted3; }
            set
            {
                if (_IsNameHighlighted3 == value) return;
                _IsNameHighlighted3 = value;
                OnPropertyChanged("IsNameHighlighted3");
            }
        }

        public string Message
        {
            get { return _Message; }
            set
            {
                if (_Message == value) return;
                _Message = value;
                OnPropertyChanged("Message");
            }
        }

        public int Points
        {
            get { return _Points; }
            set
            {
                if (_Points == value) return;
                _Points = value;
                OnPropertyChanged("Points");
            }
        }

        public bool IsButtonGoVisible
        {
            get { return _IsButtonGoVisible; }
            set
            {
                if (_IsButtonGoVisible == value) return;
                _IsButtonGoVisible = value;
                OnPropertyChanged("IsButtonGoVisible");
            }
        }

        public bool IsYesNoPanelVisible
        {
            get { return _IsYesNoPanelVisible; }
            set
            {
                if (_IsYesNoPanelVisible == value) return;
                _IsYesNoPanelVisible = value;
                OnPropertyChanged("IsYesNoPanelVisible");
            }
        }

        public bool IsButtonZoleVisible
        {
            get { return _IsButtonZoleVisible; }
            set
            {
                if (_IsButtonZoleVisible == value) return;
                _IsButtonZoleVisible = value;
                OnPropertyChanged("IsButtonZoleVisible");
            }
        }
        public bool IsPointsVisible
        {
            get { return _IsPointsVisible; }
            set
            {
                if (_IsPointsVisible == value) return;
                _IsPointsVisible = value;
                OnPropertyChanged("IsPointsVisible");
            }
        }
        public bool IsNamePlatesVisible
        {
            get { return _IsNamePlatesVisible; }
            set
            {
                if (_IsNamePlatesVisible == value) return;
                _IsNamePlatesVisible = value;
                OnPropertyChanged("IsNamePlatesVisible");
            }
        }

        public bool IsInDegugMode 
        {
            get { return _IsInDegugMode; }
            set
            {
                if (_IsInDegugMode == value) return;
                _IsInDegugMode = value;
                OnPropertyChanged("IsInDegugMode");
                OnDebugModeChanged();
            }
        }


        private void Card_CardClicked(object sender, IntEventArgs e)
        {
            CardClicked?.Invoke(this, new IntEventArgs(e.EventData));
        }

        public void OnBtGoClicked() => BtGoClicked?.Invoke(this, new EventArgs());
        public void OnYesClick() => YesNoZoleClick?.Invoke(this, new StringEventArgs("Jā"));
        public void OnNoClick() => YesNoZoleClick?.Invoke(this, new StringEventArgs("Nē"));
        public void OnZoleClick() => YesNoZoleClick?.Invoke(this, new StringEventArgs("Zole"));
        public void OnDebugModeChanged() => DebugModeChanged?.Invoke(this, new EventArgs());

        public event EventHandler<IntEventArgs> CardClicked;
        public event EventHandler<StringEventArgs> YesNoZoleClick;
        public event EventHandler BtGoClicked;
        public event EventHandler DebugModeChanged;

    }


    public class LogInPageVM : ViewModel
    {
        private string _Name = "";
        private string _Psw = "";

        public string Name
        {
            get { return _Name; }
            set
            {
                if (_Name == value) return;
                _Name = value;
                OnPropertyChanged("Name");
            }
        }

        public string Psw
        {
            get { return _Psw; }
            set
            {
                if (_Psw == value) return;
                _Psw = value;
                OnPropertyChanged("Psw");
            }
        }

        static LogInPageVM _ST = null;
        static public LogInPageVM ST
        {
            get
            {
                if (_ST == null) _ST = new LogInPageVM();
                return _ST;
            }
        }

        public void OnLogInClick() => BtLogInClicked?.Invoke(this, new EventArgs());
        public void OnLogInAsGuestClick() => BtLogInAsGuestClicked?.Invoke(this, new EventArgs());
        public void OnRegisterClick() => BtRegisterClicked?.Invoke(this, new EventArgs());
        public void OnCancelClick() => BtCancelClicked?.Invoke(this, new EventArgs());
        public void OnHelpClick() => BtHelpClicked?.Invoke(this, new EventArgs());
        public void OnCloseHelpClick() => BtCloeseHelpClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtLogInClicked;
        public event EventHandler BtLogInAsGuestClicked;
        public event EventHandler BtRegisterClicked;
        public event EventHandler BtCancelClicked;
        public event EventHandler BtHelpClicked;
        public event EventHandler BtCloeseHelpClicked;

    }

    public class LobbyPlayerVM : ViewModel
    {
        private string _Name = "";
        private string _ExtraInfo = "";

        public string Name
        {
            get { return _Name; }
            set
            {
                if (_Name == value) return;
                _Name = value;
                OnPropertyChanged("Name");
            }
        }

        public string ExtraInfo
        {
            get { return _ExtraInfo; }
            set
            {
                if (_ExtraInfo == value) return;
                _ExtraInfo = value;
                OnPropertyChanged("ExtraInfo");
            }
        }

    }

    public class LobbyPageVM : ViewModel
    {
        private int _PlayerOnlineCount = 0;

        public ObservableCollection<LobbyPlayerVM> PlayersOnline { get; private set; } = null;

        public LobbyPageVM()
        {
            PlayersOnline = new ObservableCollection<LobbyPlayerVM>();
        }

        public void MockData()
        {
            PlayersOnline.Add(new LobbyPlayerVM() { Name = "Aivars", ExtraInfo = "123 (200)" });
            PlayersOnline.Add(new LobbyPlayerVM() { Name = "Askolds", ExtraInfo = "-2123 (3200)" });
            PlayersOnline.Add(new LobbyPlayerVM() { Name = "Haralds Osis", ExtraInfo = "123 (200)" });
        }

        public int PlayerOnlineCount
        {
            get { return _PlayerOnlineCount; }
            set
            {
                if (_PlayerOnlineCount == value) return;
                _PlayerOnlineCount = value;
                OnPropertyChanged("PlayerOnlineCount");
            }
        }

        static LobbyPageVM _ST = null;
        static public LobbyPageVM ST
        {
            get
            {
                if (_ST == null)
                {
                    _ST = new LobbyPageVM();
                    _ST.MockData();
                }
                return _ST;
            }
        }

        public void OnBtJoinGameClick() => BtJoinGameClicked?.Invoke(this, new EventArgs());
        public void OnBtJoinPrivateGameClicked() => BtJoinPrivateGameClicked?.Invoke(this, new EventArgs());
        public void OnBtCalendarClicked() => BtCalendarClicked?.Invoke(this, new EventArgs());
        public void OnBtListPlayersClicked() => BtListPlayersClicked?.Invoke(this, new EventArgs());
        public void OnBtBackFromListClicked() => BtBackFromListClicked?.Invoke(this, new EventArgs());
        public void OnBtExitClick() => BtExitClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtJoinGameClicked;
        public event EventHandler BtJoinPrivateGameClicked;
        public event EventHandler BtCalendarClicked;
        public event EventHandler BtListPlayersClicked;
        public event EventHandler BtBackFromListClicked;
        public event EventHandler BtExitClicked;

    }

    public class NewGamePlayerPageVM : ViewModel
    {
        private string _Name = "";
        private string _ExtraInfo = "";

        public string Name
        {
            get { return _Name; }
            set
            {
                if (_Name == value) return;
                _Name = value;
                OnPropertyChanged("Name");
            }
        }

        public string ExtraInfo
        {
            get { return _ExtraInfo; }
            set
            {
                if (_ExtraInfo == value) return;
                _ExtraInfo = value;
                OnPropertyChanged("ExtraInfo");
            }
        }
    }

    public class NewGamePageVM : ViewModel
    {
        public ObservableCollection<NewGamePlayerPageVM> Players { get; private set; } = null;
        public int _Counter = 0;

        public int Counter
        {
            get { return _Counter; }
            set
            {
                if (_Counter == value) return;
                _Counter = value;
                OnPropertyChanged("Counter");
            }
        }


        static NewGamePageVM _ST = null;
        static public NewGamePageVM ST
        {
            get
            {
                if (_ST == null)
                {
                    _ST = new NewGamePageVM();
                    _ST.MockData();
                }
                return _ST;
            }
        }

        private void MockData()
        {
            Players.Add(new NewGamePlayerPageVM() { 
                Name = "Askolds",
                ExtraInfo = "-1056 (1563)"
            });
            Players.Add(new NewGamePlayerPageVM()
            {
                Name = "Haralds",
                ExtraInfo = "-2056 (3563)"
            });
        }

        public NewGamePageVM()
        {
            Players = new ObservableCollection<NewGamePlayerPageVM>();
        }

        public void OnBtCancelClick() => BtCancelClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtCancelClicked;

    }

    public class RegisterPageVM : ViewModel
    {
        private string _Name = "";
        private string _Psw = "";
        private string _RegCode = "";
        private string _Email = "";

        public string Name
        {
            get { return _Name; }
            set
            {
                if (_Name == value) return;
                _Name = value;
                OnPropertyChanged("Name");
            }
        }

        public string Psw
        {
            get { return _Psw; }
            set
            {
                if (_Psw == value) return;
                _Psw = value;
                OnPropertyChanged("Psw");
            }
        }

        public string RegCode
        {
            get { return _RegCode; }
            set
            {
                if (_RegCode == value) return;
                _RegCode = value;
                OnPropertyChanged("RegCode");
            }
        }

        public string Email
        {
            get { return _Email; }
            set
            {
                if (_Email == value) return;
                _Email = value;
                OnPropertyChanged("Email");
            }
        }


        static RegisterPageVM _ST = null;
        static public RegisterPageVM ST
        {
            get
            {
                if (_ST == null) _ST = new RegisterPageVM();
                return _ST;
            }
        }

        public void OnGetCodeClick() => BtGetCodeClicked?.Invoke(this, new EventArgs());
        public void OnRegisterClick() => BtRegisterClicked?.Invoke(this, new EventArgs());
        public void OnCancelClick() => BtCancelClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtGetCodeClicked;
        public event EventHandler BtRegisterClicked;
        public event EventHandler BtCancelClicked;

    }

    public class NewPrivateGamePageVM : ViewModel
    {
        private string _Name = "";
        private string _Psw = "";

        public string Name
        {
            get { return _Name; }
            set
            {
                if (_Name == value) return;
                _Name = value;
                OnPropertyChanged("Name");
            }
        }

        public string Psw
        {
            get { return _Psw; }
            set
            {
                if (_Psw == value) return;
                _Psw = value;
                OnPropertyChanged("Psw");
            }
        }

        static NewPrivateGamePageVM _ST = null;
        static public NewPrivateGamePageVM ST
        {
            get
            {
                if (_ST == null) _ST = new NewPrivateGamePageVM();
                return _ST;
            }
        }

        public void OnJoinClick() => BtJoinClicked?.Invoke(this, new EventArgs());
        public void OnCancelClick() => BtCancelClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtJoinClicked;
        public event EventHandler BtCancelClicked;

    }

    public class SettingsPageVM : ViewModel
    {
        private bool _ShowArrow = true;
        private bool _RememberPsw = true;
        private bool _HideOnlineGameButton = false;
        private string _ServerIp = "";
        private string _ServerPort = "7777";

        public bool ShowArrow
        {
            get { return _ShowArrow; }
            set
            {
                if (_ShowArrow == value) return;
                _ShowArrow = value;
                OnPropertyChanged("ShowArrow");
            }
        }

        public bool RememberPsw
        {
            get { return _RememberPsw; }
            set
            {
                if (_RememberPsw == value) return;
                _RememberPsw = value;
                OnPropertyChanged("RememberPsw");
            }
        }

        public bool HideOnlineGameButton
        {
            get { return _HideOnlineGameButton; }
            set
            {
                if (_HideOnlineGameButton == value) return;
                _HideOnlineGameButton = value;
                OnPropertyChanged("HideOnlineGameButton");
            }
        }

        public string ServerIp
        {
            get { return _ServerIp; }
            set
            {
                if (_ServerIp == value) return;
                _ServerIp = value;
                OnPropertyChanged("ServerIp");
            }
        }

        public string ServerPort
        {
            get { return _ServerPort; }
            set
            {
                if (_ServerPort == value) return;
                _ServerPort = value;
                OnPropertyChanged("ServerPort");
            }
        }

        static SettingsPageVM _ST = null;
        static public SettingsPageVM ST
        {
            get
            {
                if (_ST == null) _ST = new SettingsPageVM();
                return _ST;
            }
        }


        public void OnOkClick() => BtOkClicked?.Invoke(this, new EventArgs());
        public event EventHandler BtOkClicked;


    }

    public class CalendarItemVM : ViewModel
    {
        private CalendarPageVM Owner = null;
        private int _Points = 0;
        private int _TotalPoints = 0;
        private string _Tag = "";
        public CalendarItemVM(CalendarPageVM owner)
        {
            Owner = owner;
        }

        public int Points
        {
            get { return _Points; }
            set
            {
                if (_Points == value) return;
                _Points = value;
                OnPropertyChanged("Points");
            }
        }

        public int TotalPoints
        {
            get { return _TotalPoints; }
            set
            {
                if (_TotalPoints == value) return;
                _TotalPoints = value;
                OnPropertyChanged("TotalPoints");
            }
        }

        public string Tag
        {
            get { return _Tag; }
            set
            {
                if (_Tag == value) return;
                _Tag = value;
                OnPropertyChanged("Tag");
            }
        }

        public void OnBtAddClicked()
        {
            if (Owner.MaxPoints == Owner.UsedPoints) return;
            Points = Points + 1;
            Owner.UsedPoints++;
        }

        public void OnBtRemoveClicked()
        {
            if (Points == 0) return;
            Points = Points - 1;
            Owner.UsedPoints--;
        }

        public void OnGetUserListClick() => Owner.OnGetUserListClicked(Tag);

    }

    public class CalendarPageVM : ViewModel
    {
        public ObservableCollection<CalendarItemVM> Items { get; private set; } = null;
        private int _MaxPoints = 5;
        private DateTime _ForDay = DateTime.Today;

        public int UsedPoints { get; set; } = 0;

        private static CalendarPageVM _ST = null;
        public static CalendarPageVM ST
        {
            get
            {
                if (_ST == null)
                {
                    _ST = new CalendarPageVM();
                    _ST.MockData();
                }
                return _ST;
            }
        }

        public CalendarPageVM()
        {
            Items = new ObservableCollection<CalendarItemVM>();
        }

        public void MockData()
        {
            for (int i = 1; i <= 24; i++)
            {
                var it = new CalendarItemVM(this)
                {
                    Tag = i.ToString("00") + ":00",
                    TotalPoints = 521,
                    Points = 0
                };
                Items.Add(it);
            }
        }

        public string CurrenData = null;

        public bool SetData(string data)
        {
            if (string.IsNullOrEmpty(data)) return false;
            var ret = new List<CalendarItemVM>();
            Items.Clear();
            var segments = data.Split("|".ToCharArray());
            if (segments.Length != 2) return false;
            var tags = segments[0].Split("!".ToCharArray());
            var points = segments[1].Split("!".ToCharArray());
            if (tags.Length == 0 || tags.Length != points.Length) return false;

            for (int i = 0; i < tags.Length; i++)
            {
                var stag = tags[i];
                var spoints = points[i];
                if (string.IsNullOrEmpty(stag)) return false;
                if (string.IsNullOrEmpty(spoints)) return false;
                var tagparts = stag.Split(";".ToCharArray());
                if (tagparts.Length != 2) return false;
                var pointsparts = spoints.Split(";".ToCharArray());
                if (pointsparts.Length != 2) return false;
                var tag = tagparts[0];
                var tpoints = tagparts[1];
                var ppoints = pointsparts[1];
                if (string.IsNullOrEmpty(tag)) return false;
                if (string.IsNullOrEmpty(tpoints)) return false;
                if (!int.TryParse(tpoints, out int tp)) return false;
                if (!int.TryParse(ppoints, out int p)) return false;
                var it = new CalendarItemVM(this)
                {
                    Tag = tag,
                    TotalPoints = tp,
                    Points = p
                };
                ret.Add(it);
            }
            MaxPoints = 5;
            UsedPoints = ret.Sum(d => d.Points);
            foreach (var it in ret)
                Items.Add(it);
            CurrenData = data;
            return true;
        }

        public string GetData()
        {
            var ret = new StringBuilder();
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (i > 0) ret.Append("!");
                ret.Append($"{it.Tag};{it.Points}");
            }
            return ret.ToString();
        }

        public int MaxPoints
        {
            get { return _MaxPoints; }
            set
            {
                if (_MaxPoints == value) return;
                _MaxPoints = value;
                OnPropertyChanged("MaxPoints");
            }
        }

        public string SForDay => ForDay.ToString("dd.MM.yyyy");

        public DateTime ForDay
        {
            get { return _ForDay; }
            set
            {
                if (_ForDay == value) return;
                _ForDay = value;
                OnPropertyChanged("ForDay");
                OnPropertyChanged("SForDay");
            }
        }

        public void OnGetUserListClicked(string tag) =>
            GetUserListClicked?.Invoke(this, new StringEventArgs(tag));

        public event EventHandler<StringEventArgs> GetUserListClicked;

        public void OnBtSendDataClick()
        {
            string newdata = GetData();
            if (newdata == CurrenData)
            {
                OnBtBackClick();
                return;
            }
            BtSendDataClicked?.Invoke(this, new StringEventArgs(newdata));
        }
        public event EventHandler<StringEventArgs> BtSendDataClicked;

        public void OnBtBackClick() => BtBackClick?.Invoke(this, new EventArgs());
        public event EventHandler BtBackClick;

    }



    public class WaititngPageVM : ViewModel
    {
        private string _Message = "Uzgaidi...";
        public string Message
        {
            get { return _Message; }
            set
            {
                if (_Message == value) return;
                _Message = value;
                OnPropertyChanged("Message");
            }
        }

        static WaititngPageVM _ST = null;
        static public WaititngPageVM ST
        {
            get
            {
                if (_ST == null)
                    _ST = new WaititngPageVM();
                return _ST;
            }
        }


    }



}
