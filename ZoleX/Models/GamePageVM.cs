using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zole3.Models
{
    public partial class GamePageVM : ViewModel
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

            //if (DesignMode.IsDesignModeEnabled) 
            MockData();
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


        [RelayCommand] public void OnBtGoClicked() => BtGoClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnYesClick() => YesNoZoleClick?.Invoke(this, new StringEventArgs("Jā"));
        [RelayCommand] public void OnNoClick() => YesNoZoleClick?.Invoke(this, new StringEventArgs("Nē"));
        [RelayCommand] public void OnZoleClick() => YesNoZoleClick?.Invoke(this, new StringEventArgs("Zole"));
        [RelayCommand] public void OnDebugModeChanged() => DebugModeChanged?.Invoke(this, new EventArgs());

        public event EventHandler<IntEventArgs> CardClicked;
        public event EventHandler<StringEventArgs> YesNoZoleClick;
        public event EventHandler BtGoClicked;
        public event EventHandler DebugModeChanged;

    }


}
