using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Zole3.Models
{
    public partial class NewPrivateGamePageVM : ViewModel
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

        [RelayCommand] public void OnJoinClick() => BtJoinClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnCancelClick() => BtCancelClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtJoinClicked;
        public event EventHandler BtCancelClicked;

    }

}
