using System.Globalization;
using System.Windows.Data;

namespace MetroHub.Presentation.Converters;

public class TileSpanToPixelsConverter : IValueConverter
{
    // Base unit = 64px step (56px card + 8px gap)
    public int GridStep { get; set; } = 64;
    public int Gap { get; set; } = 8;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int span)
        {
            return Math.Max(56, (span * GridStep) - Gap);
        }
        return 120.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
