using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AllRename.Models;

namespace AllRename.Converters;

public sealed class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is MatchStatus status
            ? status switch
            {
                MatchStatus.Matched  => new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                MatchStatus.Partial  => new SolidColorBrush(Color.FromRgb(230, 81, 0)),
                MatchStatus.NotFound => new SolidColorBrush(Color.FromRgb(183, 28, 28)),
                MatchStatus.Error    => new SolidColorBrush(Color.FromRgb(130, 0, 0)),
                _                    => new SolidColorBrush(Color.FromRgb(100, 100, 100))
            }
            : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
