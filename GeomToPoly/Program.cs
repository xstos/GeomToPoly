using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace GeomToPoly;

public class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        System.Windows.FontStyle fontStyle = FontStyles.Normal;
        FontWeight fontWeight = FontWeights.Medium;

        var Text = "?";
        var FontSize = 800;
        var Font = new FontFamily("Sans MS");
        var cultureInfo = CultureInfo.GetCultureInfo("en-us");
        var typeface = new Typeface(Font, fontStyle, fontWeight, FontStretches.Normal);
        var formattedText = new FormattedText(Text, cultureInfo, FlowDirection.LeftToRight, typeface, FontSize,
            System.Windows.Media.Brushes.Black, // This brush does not matter since we use the geometry of the text.
            1.0
        );
        var window = new Window();
        var grid = new Grid();
        var canvas = new Canvas();
        grid.Children.Add(canvas);
        window.Content = grid;

        window.Loaded += (sender, args) =>
        {
            canvas.Background=Brushes.DarkGreen;
            var geom = formattedText.BuildGeometry(new System.Windows.Point(0, 0));

            var smoothPoly = geom.ToPolygons().ToLineSegments().Select(s=>s.ToLine());
            foreach (var line in smoothPoly)
            {
                line.Stroke = Brushes.White;
                line.StrokeThickness = 2;
                canvas.Children.Add(line);
            }
            geom.Transform = new TranslateTransform(400,0);
            geom = geom.GetFlattenedPathGeometry(0.01, ToleranceType.Relative);
            
            
            var coarsePoly = geom.ToPolygons().ToLineSegments().Select(s=>s.ToLine());
            foreach (var line in coarsePoly)
            {
                line.Stroke = Brushes.Yellow;
                line.StrokeThickness = 2;
                canvas.Children.Add(line);
            }
            
        };
        System.Windows.Application.Current.Run(window);
    }
}