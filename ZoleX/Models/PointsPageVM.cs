using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Zole3.Models
{
    public partial class PointsRow
    {
        public int Points1 { get; set; } = 0;
        public int Points2 { get; set; } = 0;
        public int Points3 { get; set; } = 0;

        public PointsRow() { }
        public PointsRow(int pt1, int pt2, int pt3)
        {
            Points1 = pt1;
            Points2 = pt1;
            Points3 = pt1;
        }

        public void AddPoints(int pt1, int pt2, int pt3)
        {
            Points1 += pt1;
            Points2 += pt2;
            Points3 += pt3;
        }

        public PointsRow Copy()
        {
            var new_row = new PointsRow()
            {
                Points1 = Points1,
                Points2 = Points2,
                Points3 = Points3
            };
            return new_row;
        }

    }

    public partial class PointsPageVM : ViewModel
    {
        public ObservableCollection<PointsRow> PointsRows { get; } = new ObservableCollection<PointsRow>();
        public PointsRow CurrentPoint { get; } = new PointsRow();
        private string _PlayerName1 = "", _PlayerName2 = "", _PlayerName3 = "";
        private PointsRow _LastItem = null;
        private bool _ShowArrow = true;
        private bool _ShowYesNo = true;

        public void AddPoints(int pt1, int pt2, int pt3)
        {
            CurrentPoint.AddPoints(pt1, pt2, pt3);
            var new_row = CurrentPoint.Copy();
            PointsRows.Add(new_row);
            LastItem = PointsRows.LastOrDefault();
            OnPropertyChanged("PointsRows");
        }


        public void Clear()
        {
            PointsRows.Clear();
            CurrentPoint.Points1 = 0;
            CurrentPoint.Points2 = 0;
            CurrentPoint.Points3 = 0;
            ShowArrow = true;
        }

        public void SetNames(string nm1, string nm2, string nm3)
        {
            PlayerName1 = nm1;
            PlayerName2 = nm2;
            PlayerName3 = nm3;
        }

        private void MockData()
        {
            PlayerName1 = "Askolds";
            PlayerName2 = "Aivars";
            PlayerName3 = "Haralds";
            var pr = new PointsRow(4, -2, -2);
            PointsRows.Add(pr);
            PointsRows.Add(pr);
        }

        private static PointsPageVM _DTST = null;
        public static PointsPageVM DTST
        {
            get
            {
                if (_DTST == null)
                {
                    _DTST = new PointsPageVM();
                    if (DesignMode.IsDesignModeEnabled)
                    {
                        _DTST.MockData();
                    }
                }
                return _DTST;
            }
        }

        public string PlayerName1
        {
            get { return _PlayerName1; }
            set
            {
                if (_PlayerName1 == value) return;
                _PlayerName1 = value;
                OnPropertyChanged("PlayerName1");
            }
        }

        public string PlayerName2
        {
            get { return _PlayerName2; }
            set
            {
                if (_PlayerName2 == value) return;
                _PlayerName2 = value;
                OnPropertyChanged("PlayerName2");
            }
        }

        public string PlayerName3
        {
            get { return _PlayerName3; }
            set
            {
                if (_PlayerName3 == value) return;
                _PlayerName3 = value;
                OnPropertyChanged("PlayerName3");
            }
        }

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

        public bool ShowYesNo
        {
            get { return _ShowYesNo; }
            set
            {
                if (_ShowYesNo == value) return;
                _ShowYesNo = value;
                OnPropertyChanged("ShowYesNo");
            }
        }

        public PointsRow LastItem
        {
            get { return _LastItem; }
            set
            {
                if (_LastItem == value) return;
                _LastItem = value;
                OnPropertyChanged("LastItem");
            }
        }

        [RelayCommand] public void OnBtGoClicked() => BtGoClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtYesClicked() => BtYesClicked?.Invoke(this, new EventArgs());
        [RelayCommand] public void OnBtNoClicked() => BtNoClicked?.Invoke(this, new EventArgs());

        public event EventHandler BtGoClicked;
        public event EventHandler BtYesClicked;
        public event EventHandler BtNoClicked;
    }
}
