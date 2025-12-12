using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ZoleW
{
    /// <summary>
    /// Interaction logic for PagePoints.xaml
    /// </summary>
    public partial class PagePoints : Page
    {
        public PagePoints()
        {
            InitializeComponent();
        }

        public void SetNames(string nm1, string nm2, string nm3)
        {
            Col1.Header = nm1;
            Col2.Header = nm2;
            Col3.Header = nm3;
        }

        public void ScrollToEnd()
        {
            if (DataGrid1.Items.Count == 0) return;
            DataGrid1.ScrollIntoView(DataGrid1.Items.GetItemAt(DataGrid1.Items.Count - 1));
            DataGrid1.SelectedItem = DataGrid1.Items.GetItemAt(DataGrid1.Items.Count - 1);
        }
    }
}
