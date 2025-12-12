using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Zole3.Models
{
    public partial class NewGamePageVM : ViewModel
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
            Players.Add(new NewGamePlayerPageVM()
            {
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

        [RelayCommand] public void OnBtCancelClick() => BtCancelClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtCancelClicked;

    }

}
