using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zole3.Models
{
    public class CardVM : ViewModel
    {
        private CardImages CardImages = null;
        public int Index { get; private set; } = 0;
        private string _ImgName = "";
        private bool _IsSelected = false;

        public CardVM(CardImages cardImages, int index)
        {
            CardImages = cardImages;
            Index = index;
        }

        public ImageSource ImgSource
        {
            get
            {
                if (!CardImages.Images.TryGetValue(ImgName, out var img))
                    img = CardImages.ImageEmpty;
                return img;
            }
        }

        public double W => Locator.CardWidth;
        public double H => Locator.CardHeight;

        public string ImgName
        {
            get { return _ImgName; }
            set
            {
                if (_ImgName == value) return;
                _ImgName = value;
                OnPropertyChanged("ImgName");
                OnPropertyChanged("ImgSource");
            }
        }

        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                _IsSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public event EventHandler<IntEventArgs> CardClicked;

        public void OnCardClicked(object sender, EventArgs ev)
        {
            if (CardClicked != null)
                CardClicked.Invoke(this, new IntEventArgs(Index));
        }
    }

    public class IntEventArgs : EventArgs
    {
        public int EventData { get; private set; }
        public IntEventArgs(int EventData)
        {
            this.EventData = EventData;
        }
    }

    public class StringEventArgs : EventArgs
    {
        public string EventData { get; private set; }
        public StringEventArgs(string EventData)
        {
            this.EventData = EventData;
        }
    }

}
