using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

namespace ZoleX
{
    public partial class App : Application
    {
        GameController GameController = null;
        public App()
        {
            InitializeComponent();
        }

        public void DoQuit()
        {
            if (Device.RuntimePlatform == Device.Android)
                DependencyService.Get<IAndroidMethods>().CloseApp();
        }

        private void InitCardSizes()
        {
            if (Device.RuntimePlatform != Device.Android) return;
            var mdi = DeviceDisplay.MainDisplayInfo;
            var w = Math.Max(mdi.Height, mdi.Width);
            int w2 = (int)(w / mdi.Density * 0.9d);
            int cw = w2 / 10 - 2;
            int ch = (int)(96d / 71d * (double)cw);
            Locator.CardWidth = cw;
            Locator.CardHeight = ch;
        }

        protected override void OnStart()
        {
            InitCardSizes();

            GameController = new GameController(this);
        }

        protected override void OnSleep()
        {
            GameController?.DoOnGameClosing();
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }

    public interface IAndroidMethods
    {
        void CloseApp();
    }

    public static class Locator
    {
        public static int CardWidth { get; set; } = 60;
        public static int CardHeight { get; set; } = 82;
        public static int CardColumnWidth => CardWidth + 5;
        public static int CardRowHeight => CardHeight + 23;
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
