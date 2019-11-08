using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ZoleX
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PagePoints : ContentPage
    {
        public PagePoints()
        {
            InitializeComponent();
            if (DesignMode.IsDesignModeEnabled)
                BindingContext = new PointsPageVM();
        }

        public void ScrollToEnd()
        {
            if(!(listView1.BindingContext is PointsPageVM pp)) return;
            var item = pp.LastItem;
            if (item == null) return;
            listView1.SelectedItem = item;
            listView1.ScrollTo(item, ScrollToPosition.End, false);
        }
    }
}