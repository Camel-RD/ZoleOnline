using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

namespace ZoleX
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PageGame : ContentPage
    {
        public PageGame()
        {
            InitializeComponent();
            if (DesignMode.IsDesignModeEnabled)
                BindingContext = new GamePageVM();
        }

        void OnCardTap(object sender, EventArgs args)
        {
            if (!(sender is Image im)) return;
            if (!(im.Parent is FlexLayout fl)) return;
            if (!(fl.BindingContext is GamePageVM gp)) return;
            int k = fl.Children.IndexOf(im);
            if (k == -1) return;
            gp.Cards[k].OnCardClicked(sender, args);
        }

        public void UpdateImage(CardVM card)
        {
            if (card == null || card.Index < 0) return;
            var im = FLCarDeck.Children[card.Index] as Image;
            if (im == null) return;
            im.Source = card.ImgSource;
        }
    }
}