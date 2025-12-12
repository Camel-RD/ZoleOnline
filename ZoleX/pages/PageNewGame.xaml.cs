using Zole3.Models;

namespace Zole3.Pages;

public partial class PageNewGame : ContentPage
{
	public PageNewGame()
	{
		InitializeComponent();
        if (DesignMode.IsDesignModeEnabled)
            BindingContext = NewGamePageVM.ST;
    }
}