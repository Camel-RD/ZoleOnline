using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Zole3.Models
{
    public partial class StartUpPageVM : ViewModel
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

        [RelayCommand] public void OnStarted() => Started?.Invoke(this, new StringEventArgs(PlayerName));
        [RelayCommand] public void OnBtPlayOnlineClick() => BtPlayOnlineClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtSettingsClicked() => BtSettingsClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtExitClicked() => BtExitClicked?.Invoke(this, new EventArgs());

        public event EventHandler<StringEventArgs> Started;
        public event EventHandler BtPlayOnlineClicked;
        public event EventHandler BtSettingsClicked;
        public event EventHandler BtExitClicked;
    }
}
