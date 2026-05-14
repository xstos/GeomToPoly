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

        var Text = "P";
        var FontSize = 400;
        var Font = new FontFamily("Sans MS");
        var cultureInfo = CultureInfo.GetCultureInfo("en-us");
        var typeface = new Typeface(Font, fontStyle, fontWeight, FontStretches.Normal);
        FormattedText formattedText;
        
        var window = new Window();
        var grid = new Grid();
        var canvas = new Canvas();
        grid.Children.Add(canvas);
        window.Content = grid;
        window.TextInput += (sender, args) =>
        {
            PaintLetter(args.Text);
        };
        window.Loaded += (sender, args) => { PaintLetter("?"); };
        System.Windows.Application.Current.Run(window);

        void PaintLetter(string Text)
        {
            canvas.Children.Clear();
            formattedText = new FormattedText(Text, cultureInfo, FlowDirection.LeftToRight, typeface, FontSize,
                System.Windows.Media.Brushes.Black, // This brush does not matter since we use the geometry of the text.
                1.0
            );
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
            var height = (int)canvas.ActualHeight;
            
            var hLineInfo = new HLineInfo(height);

            var cp2 = geom.ToPolygons();
            foreach (var poly in cp2)
            {
                var xs = new List<double>();
                var ys = new List<double>();
                foreach (var d in poly.Chunk(2))
                {
                    xs.Add(d[0]);
                    ys.Add(d[1]);
                }
                
                var hl = PolygonFiller.FillPolygon((int)canvas.ActualWidth, height, xs.ToArray(), ys.ToArray(), hLineInfo).ToArray();
                
            }
            void line2(int x1, int y1, int x2, int y2)
            {
                var l = new Line();
                l.X1 = x1;
                l.X2 = x2;
                l.Y1 = y1;
                l.Y2 = y2;
                l.Stroke = Brushes.Magenta;
                canvas.Children.Add(l);
            }
            for (int i = 0; i < hLineInfo.UsedRowIndexes.Count; i++)
            {
                var y = hLineInfo.UsedRowIndexes[i];
                var verts = hLineInfo.Rows[y];
                verts.Sort();
                foreach (var c in verts.Chunk(2))
                {
                    var (x1, x2) = (c[0], c[1]);
                    line2(x1,y,x2,y);
                }
                verts.Clear();
            }
        }
    }
}