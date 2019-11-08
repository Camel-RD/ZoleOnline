using ZoleX;
using ZoleX.WPF;
using Xamarin.Forms;
using Xamarin.Forms.Platform.WPF;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;

[assembly: ExportRenderer(typeof(MyEntry), typeof(MyEntryRenderer))]
namespace ZoleX.WPF
{
    public class MyEntryRenderer : EntryRenderer
    {
        protected override void OnElementChanged(ElementChangedEventArgs<Entry> e)
        {
            base.OnElementChanged(e);
            var tb = Control as TextBox;
            var el = Element as MyEntry;
            if (tb != null && el != null)
            {
                var textcolor = el.TextColor.ToMediaColor();
                var bordercolor = el.BorderColor.ToMediaColor();
                tb.BorderThickness = new System.Windows.Thickness(1);
                tb.BorderBrush = new SolidColorBrush(bordercolor);
                tb.Foreground = new SolidColorBrush(textcolor);
                tb.FontSize = el.FontSize;
            }
        }
    }
}
