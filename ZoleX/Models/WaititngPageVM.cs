using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zole3.Models
{
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
