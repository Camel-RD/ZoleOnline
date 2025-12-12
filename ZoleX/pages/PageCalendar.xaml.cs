using Zole3.Models;

namespace Zole3.Pages;

public partial class PageCalendar : ContentPage
{
	public PageCalendar()
	{
		InitializeComponent();
        if (DesignMode.IsDesignModeEnabled)
            BindingContext = CalendarPageVM.ST;
    }
}