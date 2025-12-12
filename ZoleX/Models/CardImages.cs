using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zole3.Models
{
    public class CardImages
    {
        public Dictionary<string, ImageSource> Images = new Dictionary<string, ImageSource>();
        public ImageSource ImageEmpty { get; private set; } = null;
        public CardImages()
        {
            var suits = new[] { "c", "d", "h", "s" };
            var values = new[] { "1", "9", "10", "k", "j", "q" };
            var sv = new[] { "d7", "d8", "empty" };
            for (int i = 0; i < suits.Length; i++)
            {
                for (int j = 0; j < values.Length; j++)
                {
                    var nm = $"{suits[i]}{values[j]}";
                    //var b = DeviceInfo.Platform == DevicePlatform.Android;
                    var s = $"{nm}.png";
                    Images[nm] = ImageSource.FromFile(s);
                }
            }
            for (int i = 0; i < sv.Length; i++)
            {
                var nm = sv[i];
                var s = $"{nm}.png";
                Images[nm] = ImageSource.FromFile(s);
            }
            ImageEmpty = Images["empty"];
        }
    }

}
