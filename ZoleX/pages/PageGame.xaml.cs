using Zole3.Models;

namespace Zole3.Pages;

public partial class PageGame : ContentPage
{
	public PageGame()
	{
		InitializeComponent();
        BindingContext = GamePageVM.ST;
    }

    private void OnCardTap(object sender, EventArgs e)
    {
        if (!(sender is Image im)) return;
        if (!(im.Parent is FlexLayout fl)) return;
        if (!(fl.BindingContext is GamePageVM gp)) return;
        int k = fl.Children.IndexOf(im);
        if (k == -1) return;
        gp.Cards[k].OnCardClicked(sender, e);
    }

    public void UpdateImage(CardVM card)
    {
        if (card == null || card.Index < 0) return;
        var im = FLCarDeck.Children[card.Index] as Image;
        if (im == null) return;
        im.Source = card.ImgSource;
    }

}