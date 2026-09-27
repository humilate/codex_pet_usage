using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PetUsageOverlay;

internal sealed class QuotaBadge
{
    public const double Width = 174;
    public const double Height = 62;

    public Border Root { get; }
    private readonly TextBlock percent;
    private readonly TextBlock reset;

    public QuotaBadge(string label, string color)
    {
        var accent = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(color)!;
        Root = new Border
        {
            Width = Width,
            Height = Height,
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = ColorBrush("#E9EBEF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(31),
            Padding = new Thickness(13, 7, 11, 7),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 3,
                Opacity = .18,
                Color = System.Windows.Media.Color.FromRgb(40, 51, 76)
            }
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) });
        layout.RowDefinitions.Add(new RowDefinition());

        var accentDash = new Border
        {
            Width = 10,
            Height = 3,
            Background = accent,
            CornerRadius = new CornerRadius(1.5),
            VerticalAlignment = VerticalAlignment.Center
        };
        layout.Children.Add(accentDash);

        var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(new TextBlock
        {
            Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = ColorBrush("#515B6B"), VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 1, 6, 0)
        });
        percent = new TextBlock
        {
            Text = "—", FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = ColorBrush("#1E2736"), VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(percent);
        header.Children.Add(new TextBlock
        {
            Text = " 剩余", FontSize = 10, Foreground = ColorBrush("#778394"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(header, 1);
        layout.Children.Add(header);

        reset = new TextBlock
        {
            Text = "等待额度数据", FontSize = 10.5,
            Foreground = ColorBrush("#8995A5"), VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        Grid.SetColumn(reset, 1);
        Grid.SetRow(reset, 1);
        layout.Children.Add(reset);
        Root.Child = layout;
    }

    public void Set(double? remainingPercent, string resetText)
    {
        percent.Text = remainingPercent is null ? "—" : $"{remainingPercent.Value:0}%";
        reset.Text = resetText;
    }

    private static System.Windows.Media.Brush ColorBrush(string hex) =>
        (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(hex)!;
}
