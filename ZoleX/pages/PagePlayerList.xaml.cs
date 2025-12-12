using Zole3.Models;

namespace Zole3.Pages;

public partial class PagePlayerList : ContentPage
{
	public PagePlayerList()
	{
		InitializeComponent();
        if (DesignMode.IsDesignModeEnabled)
            BindingContext = LobbyPageVM.ST;
    }
}