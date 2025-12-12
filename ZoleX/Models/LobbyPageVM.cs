using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Zole3.Models
{
    public partial class LobbyPageVM : ViewModel
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

        [RelayCommand] public void OnBtJoinGameClick() => BtJoinGameClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtJoinPrivateGameClicked() => BtJoinPrivateGameClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtCalendarClicked() => BtCalendarClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtListPlayersClicked() => BtListPlayersClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtBackFromListClicked() => BtBackFromListClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtExitClick() => BtExitClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtJoinGameClicked;
        public event EventHandler BtJoinPrivateGameClicked;
        public event EventHandler BtCalendarClicked;
        public event EventHandler BtListPlayersClicked;
        public event EventHandler BtBackFromListClicked;
        public event EventHandler BtExitClicked;

    }

}
