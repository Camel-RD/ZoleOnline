using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zole3.Models
{
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

}
