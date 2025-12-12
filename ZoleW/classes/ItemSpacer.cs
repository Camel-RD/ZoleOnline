using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ZoleW
{
    public class MarginSetter
    {
        private static Thickness GetLastItemMargin(Panel obj)
        {
            return (Thickness)obj.GetValue(LastItemMarginProperty);
        }

        public static Thickness GetMargin(DependencyObject obj)
        {
            return (Thickness)obj.GetValue(MarginProperty);
        }

        private static void MarginChangedCallback(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Make sure this is put on a panel
            var panel = sender as Panel;
            if (panel == null) return;

            // Avoid duplicate registrations
            panel.Loaded -= OnPanelLoaded;
            panel.Loaded += OnPanelLoaded;

            if (panel.IsLoaded)
            {
                OnPanelLoaded(panel, null);
            }
        }

        private static void OnPanelLoaded(object sender, RoutedEventArgs e)
        {
            var panel = (Panel)sender;

            // Go over the children and set margin for them:
            for (var i = 0; i < panel.Children.Count; i++)
            {
                UIElement child = panel.Children[i];
                var fe = child as FrameworkElement;
                if (fe == null) continue;

                bool isLastItem = i == panel.Children.Count - 1;
                fe.Margin = isLastItem ? GetLastItemMargin(panel) : GetMargin(panel);
            }
        }

        public static void SetLastItemMargin(DependencyObject obj, Thickness value)
        {
            obj.SetValue(LastItemMarginProperty, value);
        }

        public static void SetMargin(DependencyObject obj, Thickness value)
        {
            obj.SetValue(MarginProperty, value);
        }

        // Using a DependencyProperty as the backing store for Margin.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MarginProperty =
            DependencyProperty.RegisterAttached("Margin", typeof(Thickness), typeof(MarginSetter),
                new UIPropertyMetadata(new Thickness(), MarginChangedCallback));

        public static readonly DependencyProperty LastItemMarginProperty =
            DependencyProperty.RegisterAttached("LastItemMargin", typeof(Thickness), typeof(MarginSetter),
                new UIPropertyMetadata(new Thickness(), MarginChangedCallback));
    }

    public class Spacing
    {
        public static double GetHorizontal(DependencyObject obj)
        {
            return (double)obj.GetValue(HorizontalProperty);
        }

        public static double GetVertical(DependencyObject obj)
        {
            return (double)obj.GetValue(VerticalProperty);
        }

        private static void HorizontalChangedCallback(object sender, DependencyPropertyChangedEventArgs e)
        {
            var space = (double)e.NewValue;
            var obj = (DependencyObject)sender;

            MarginSetter.SetMargin(obj, new Thickness(0, 0, space, 0));
            MarginSetter.SetLastItemMargin(obj, new Thickness(0));
        }

        public static void SetHorizontal(DependencyObject obj, double space)
        {
            obj.SetValue(HorizontalProperty, space);
        }

        public static void SetVertical(DependencyObject obj, double value)
        {
            obj.SetValue(VerticalProperty, value);
        }

        private static void VerticalChangedCallback(object sender, DependencyPropertyChangedEventArgs e)
        {
            var space = (double)e.NewValue;
            var obj = (DependencyObject)sender;
            MarginSetter.SetMargin(obj, new Thickness(0, 0, 0, space));
            MarginSetter.SetLastItemMargin(obj, new Thickness(0));
        }

        public static readonly DependencyProperty VerticalProperty =
            DependencyProperty.RegisterAttached("Vertical", typeof(double), typeof(Spacing),
                new UIPropertyMetadata(0d, VerticalChangedCallback));

        public static readonly DependencyProperty HorizontalProperty =
            DependencyProperty.RegisterAttached("Horizontal", typeof(double), typeof(Spacing),
                new UIPropertyMetadata(0d, HorizontalChangedCallback));
    }
}
