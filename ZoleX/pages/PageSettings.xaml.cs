using Zole3.Models;

namespace Zole3.Pages;

public partial class PageSettings : ContentPage
{
	public PageSettings()
	{
		InitializeComponent();
        BindingContext = SettingsPageVM.ST;
    }
}