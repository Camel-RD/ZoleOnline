using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zole3.Models
{
    public partial class CalendarItemVM : ViewModel
    {
        private CalendarPageVM Owner = null;
        private int _Points = 0;
        private int _TotalPoints = 0;
        private string _Tag = "";
        public CalendarItemVM(CalendarPageVM owner)
        {
            Owner = owner;
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

        public int TotalPoints
        {
            get { return _TotalPoints; }
            set
            {
                if (_TotalPoints == value) return;
                _TotalPoints = value;
                OnPropertyChanged("TotalPoints");
            }
        }

        public string Tag
        {
            get { return _Tag; }
            set
            {
                if (_Tag == value) return;
                _Tag = value;
                OnPropertyChanged("Tag");
            }
        }

        [RelayCommand]
        public void OnBtAddClicked()
        {
            if (Owner.MaxPoints == Owner.UsedPoints) return;
            Points = Points + 1;
            Owner.UsedPoints++;
        }

        [RelayCommand]
        public void OnBtRemoveClicked()
        {
            if (Points == 0) return;
            Points = Points - 1;
            Owner.UsedPoints--;
        }

        [RelayCommand] public void OnGetUserListClick() => Owner.OnGetUserListClicked(Tag);

    }

}
