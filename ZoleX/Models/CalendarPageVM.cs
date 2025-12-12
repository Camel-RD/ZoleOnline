using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zole3.Models
{
    public partial class CalendarPageVM : ViewModel
    {
        public ObservableCollection<CalendarItemVM> Items { get; private set; } = null;
        private int _MaxPoints = 5;
        private DateTime _ForDay = DateTime.Today;

        public int UsedPoints { get; set; } = 0;

        private static CalendarPageVM _ST = null;
        public static CalendarPageVM ST
        {
            get
            {
                if (_ST == null)
                {
                    _ST = new CalendarPageVM();
                    _ST.MockData();
                }
                return _ST;
            }
        }

        public CalendarPageVM()
        {
            Items = new ObservableCollection<CalendarItemVM>();
        }

        public void MockData()
        {
            for (int i = 1; i <= 24; i++)
            {
                var it = new CalendarItemVM(this)
                {
                    Tag = i.ToString("00") + ":00",
                    TotalPoints = 521,
                    Points = 0
                };
                Items.Add(it);
            }
        }

        public string CurrenData = null;

        public bool SetData(string data)
        {
            if (string.IsNullOrEmpty(data)) return false;
            var ret = new List<CalendarItemVM>();
            Items.Clear();
            var segments = data.Split("|".ToCharArray());
            if (segments.Length != 2) return false;
            var tags = segments[0].Split("!".ToCharArray());
            var points = segments[1].Split("!".ToCharArray());
            if (tags.Length == 0 || tags.Length != points.Length) return false;

            for (int i = 0; i < tags.Length; i++)
            {
                var stag = tags[i];
                var spoints = points[i];
                if (string.IsNullOrEmpty(stag)) return false;
                if (string.IsNullOrEmpty(spoints)) return false;
                var tagparts = stag.Split(";".ToCharArray());
                if (tagparts.Length != 2) return false;
                var pointsparts = spoints.Split(";".ToCharArray());
                if (pointsparts.Length != 2) return false;
                var tag = tagparts[0];
                var tpoints = tagparts[1];
                var ppoints = pointsparts[1];
                if (string.IsNullOrEmpty(tag)) return false;
                if (string.IsNullOrEmpty(tpoints)) return false;
                if (!int.TryParse(tpoints, out int tp)) return false;
                if (!int.TryParse(ppoints, out int p)) return false;
                var it = new CalendarItemVM(this)
                {
                    Tag = tag,
                    TotalPoints = tp,
                    Points = p
                };
                ret.Add(it);
            }
            MaxPoints = 5;
            UsedPoints = ret.Sum(d => d.Points);
            foreach (var it in ret)
                Items.Add(it);
            CurrenData = data;
            return true;
        }

        public string GetData()
        {
            var ret = new StringBuilder();
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (i > 0) ret.Append("!");
                ret.Append($"{it.Tag};{it.Points}");
            }
            return ret.ToString();
        }

        public int MaxPoints
        {
            get { return _MaxPoints; }
            set
            {
                if (_MaxPoints == value) return;
                _MaxPoints = value;
                OnPropertyChanged("MaxPoints");
            }
        }

        public string SForDay => ForDay.ToString("dd.MM.yyyy");

        public DateTime ForDay
        {
            get { return _ForDay; }
            set
            {
                if (_ForDay == value) return;
                _ForDay = value;
                OnPropertyChanged("ForDay");
                OnPropertyChanged("SForDay");
            }
        }

        public void OnGetUserListClicked(string tag) =>
            GetUserListClicked?.Invoke(this, new StringEventArgs(tag));
        
        public event EventHandler<StringEventArgs> GetUserListClicked;

        [RelayCommand]
        public void OnBtSendDataClick()
        {
            string newdata = GetData();
            if (newdata == CurrenData)
            {
                OnBtBackClick();
                return;
            }
            BtSendDataClicked?.Invoke(this, new StringEventArgs(newdata));
        }
        public event EventHandler<StringEventArgs> BtSendDataClicked;

        [RelayCommand] public void OnBtBackClick() => BtBackClick?.Invoke(this, new EventArgs());
        public event EventHandler BtBackClick;

    }

}
