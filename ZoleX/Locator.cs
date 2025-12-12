using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zole3.Models;

namespace Zole3
{
    public static class Locator
    {
        public static double CardWidth { get; set; } = 60;
        public static double CardHeight { get; set; } = 82;
        public static double CardColumnWidth => CardWidth + 5;
        public static double CardRowHeight => CardHeight + 23;
        public static StartUpPageVM StartUpPageVM => StartUpPageVM.ST;
        public static PointsPageVM PointsPageVM => PointsPageVM.DTST;
        public static GamePageVM GamePageVM => GamePageVM.ST;
        public static LogInPageVM LogInPageVM => LogInPageVM.ST;
        public static LobbyPageVM LobbyPageVM => LobbyPageVM.ST;
        public static NewGamePageVM NewGamePageVM => NewGamePageVM.ST;
        public static RegisterPageVM RegisterPageVM => RegisterPageVM.ST;
        public static NewPrivateGamePageVM NewPrivateGamePageVM => NewPrivateGamePageVM.ST;
        public static SettingsPageVM SettingsPageVM => SettingsPageVM.ST;
        public static CalendarPageVM CalendarPageVM => CalendarPageVM.ST;
        public static WaititngPageVM WaititngPageVM => WaititngPageVM.ST;

    }

}
