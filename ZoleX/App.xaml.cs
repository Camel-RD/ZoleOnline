using Zole3.Pages;

namespace Zole3
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Dark;
            InitCardSizes();
        }

        GameController GameController = null;

        protected override Window CreateWindow(IActivationState activationState)
        {
            //Window window = base.CreateWindow(activationState);
            Window window = new Window(new InitPage());
            window.Activated += (s, e) =>
            {
                GameController = GameController.Create(this);
                if (LastPage != null && Windows[0].Page != LastPage)
                    Windows[0].Page = LastPage;
            };
            window.Resumed += (s, e) =>
            {
                if (LastPage != null && Windows[0].Page != LastPage)
                    Windows[0].Page = LastPage;
            };
            return window; 
        }

        public string Called = "";

        private void InitCardSizes()
        {
            if (DeviceInfo.Platform != DevicePlatform.Android) return;
            var mdi = DeviceDisplay.MainDisplayInfo;
            double sz_big = Math.Max(mdi.Width, mdi.Height);
            double sz_small = Math.Min(mdi.Width, mdi.Height);
            int w1 = (int)(sz_big / mdi.Density * 0.9d / 10d) - 2;
            int w2 = (int)(sz_small / mdi.Density * 0.9d / 5d) - 2;
            var cw = Math.Min(w1, w2);
            int ch = (int)(96d / 71d * (double)cw);
            Locator.CardWidth = cw;
            Locator.CardHeight = ch;
            Called += cw;
        }

        protected override void OnStart()
        {
        }

        Page LastPage;
        
        protected override void OnSleep()
        {
            base.OnSleep();
            GameController?.DoOnGameClosing();
            LastPage = Windows[0].Page;
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (LastPage != null && Windows[0].Page != LastPage)
                Windows[0].Page = LastPage;
        }

    }
}