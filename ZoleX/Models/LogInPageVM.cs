using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Zole3.Models
{
    public partial class LogInPageVM : ViewModel
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

        [RelayCommand] public void OnLogInClick() => BtLogInClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnLogInAsGuestClick() => BtLogInAsGuestClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnRegisterClick() => BtRegisterClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnCancelClick() => BtCancelClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnHelpClick() => BtHelpClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnCloseHelpClick() => BtCloeseHelpClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtLogInClicked;
        public event EventHandler BtLogInAsGuestClicked;
        public event EventHandler BtRegisterClicked;
        public event EventHandler BtCancelClicked;
        public event EventHandler BtHelpClicked;
        public event EventHandler BtCloeseHelpClicked;

    }

}
