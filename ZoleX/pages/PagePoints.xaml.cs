using Zole3.Models;

namespace Zole3.Pages;

public partial class PagePoints : ContentPage
{
	public PagePoints()
	{
		InitializeComponent();
        if (DesignMode.IsDesignModeEnabled)
            BindingContext = PointsPageVM.DTST;
    }

    public void ScrollToEnd()
    {
        if (!(listView1.BindingContext is PointsPageVM pp)) return;
        var item = pp.LastItem;
        if (item == null) return;
        listView1.SelectedItem = item;
        listView1.ScrollTo(item, ScrollToPosition.End, false);
    }

}