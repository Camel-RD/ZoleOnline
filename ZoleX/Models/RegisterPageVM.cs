using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Zole3.Models
{
    public partial class RegisterPageVM : ViewModel
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

        [RelayCommand] public void OnGetCodeClick() => BtGetCodeClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnRegisterClick() => BtRegisterClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnCancelClick() => BtCancelClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtGetCodeClicked;
        public event EventHandler BtRegisterClicked;
        public event EventHandler BtCancelClicked;

    }

}
