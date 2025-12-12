using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Zole3.Models
{
    public partial class SettingsPageVM : ViewModel
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


        [RelayCommand] public void OnOkClick() => BtOkClicked?.Invoke(this, new EventArgs());
        public event EventHandler BtOkClicked;


    }

}
