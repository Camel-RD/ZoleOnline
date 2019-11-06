using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ZoleW
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        
        public StarterWindow StarterWindow = null;

    }


    public static class Locator
    {
        public static double Scale => 1.0d;
        public static PointsPageVM PointsPageVM => PointsPageVM.DTST;
        public static GamePageVM GamePageVM => DTGamePageVM.GamePageVM;
        public static LobbyPageVM LobbyPageVM => LobbyPageVM.ST;
        public static CalendarPageVM CalendarPageVM => CalendarPageVM.St;
    }

}
