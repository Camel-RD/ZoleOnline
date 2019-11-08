using Xamarin.Forms;

namespace ZoleX
{
    public class MyEntry : Entry
    {
        public Color BorderColor { get; set; }

        public MyEntry() : base()
        {
            BorderColor = TextColor;
        }
    }
}
