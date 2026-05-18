using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace GeomToPoly;

public class Program
{
    static Stopwatch sw = new Stopwatch();
    static Disposable timer(string name="") => new Disposable(() =>
    {
        sw.Reset();
        //Console.WriteLine("start "+ name);
        sw.Start();
    }, () =>
    {
        sw.Stop();
        Console.WriteLine(name+" took "+sw.Elapsed.TotalMilliseconds);
    } );
    [STAThread]
    public static void Main()
    {
        var app = new Application();

        var Text = "P";
        var FontSize = 20;
        var typeface = new Typeface(new FontFamily("Jetbrains Mono"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        FormattedText formattedText;
        
        Glyph MakeGlyph(char c)
        {
            var ft = new FormattedText(c+"", CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, typeface, FontSize, Brushes.White, 1.0);
            var geom = ft.BuildGeometry(new Point(0, 0)).GetFlattenedPathGeometry(0.01, ToleranceType.Relative);
            Glyph g = new Glyph();
            g.Shapes = geom.ToPolygons().Select(Polygon.New).ToArray();
            foreach (var s in g.Shapes)
            {
                g.Bounds.Union(s.Bounds);
            }
            return g;
        }
        string txt;
        
        txt = Res("GeomToPoly.twist.txt");
        //var txt = new WebClient().DownloadString("https://www.gutenberg.org/cache/epub/730/pg730.txt");
        var chars = txt.Distinct();
        var max = chars.Max(c => (int)c)+1;
        var lines = txt.ReplaceLineEndings("█").Split('█');
        var widest = lines.Max(l => l.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].PadRight(widest);
        }

        var glyphs = new Glyph[max];
        using (timer("make glyphs"))
        {
            foreach (var c in chars)
            {
                glyphs[c] = MakeGlyph(c);
            }
        }
        
        var box = MakeGlyph('█');
        
        var window = new Window();
        window.Background = Brushes.Black;
        var grid = new Grid();
        var canvas = new Canvas();
        canvas.Background=Brushes.Black;
        //grid.Children.Add(canvas);
        var fastPixels = new FastPixels();
        //grid.Children.Add(fastPixels);
        var test = new SimpleHwndHost();
        
        grid.Children.Add(test);
        window.Content = grid;
        int lineOffset = 0;
        window.PreviewKeyDown += (sender, args) =>
        {
            if (args.Key == Key.Up)
            {
                lineOffset--;
                if (lineOffset < 0) lineOffset = 0;
            }

            if (args.Key == Key.Down)
            {
                lineOffset++;
            }
            fastPixels.Clear();
            Refresh();
            fastPixels.Paint();
        };
        window.TextInput += (sender, args) =>
        {
            
            //PaintLetter(args.Text);
        };
        void line2(int x1, int y1, int x2, int y2)
        {
            var l = new Line();
            l.X1 = x1;
            l.X2 = x2;
            l.Y1 = y1;
            l.Y2 = y2;
            l.Stroke = Brushes.White;
            canvas.Children.Add(l);
        }
        
        void Refresh()
        {
            var arr = test.Pixels;
            Array.Fill(arr, 0);
            var w = (int)test.ActualWidth;
            var h = (int)test.ActualHeight;
            var (canvasWidth, canvasHeight) = (w, h);
            var glyphWidth = (int)Math.Round(box.Bounds.Width, MidpointRounding.AwayFromZero);
            var glyphHeight = (int)Math.Round(box.Bounds.Height, MidpointRounding.AwayFromZero);
            var numCols = canvasWidth / glyphWidth;
            var numRows = canvasHeight / glyphHeight;
            HLineInfo hi = new HLineInfo(1080);
            HLineInfo whole = new HLineInfo(1080);
            for (int i = 0; i < numRows - 1; i++)
            {
                var yoffs = i * glyphHeight;
                for (int j = 0; j < numCols; j++)
                {
                    var xoffs = j * glyphWidth;
                    if (j > lines[i+lineOffset].Length - 1) break;
                    char c = lines[i+lineOffset][j];
                    var ix = (int)c;
                    var glyph = glyphs[ix];

                    foreach (var shape in glyph.Shapes)
                    {
                        foreach (var _ in PolygonFiller.FillPolygon2(shape, hi, xoffs, yoffs))
                        {
                        }
                    }

                    for (int ui = 0; ui < hi.UsedRowIndexes.Count; ui++)
                    {
                        var y = hi.UsedRowIndexes[ui];
                        var verts = hi.Rows[y];
                        verts.Sort();
                        whole.Rows[y].AddRange(verts);
                        verts.Clear();
                    }

                    hi.UsedRowIndexes.Clear();
                }
            }

            var color = BitConverter.ToInt32([0, 255, 0, 0]); //bgra
            for (int i = 0; i < whole.Rows.Length; i++)
            {
                var verts = whole.Rows[i];
                foreach (var c in verts.Chunk(2))
                {
                    var (x1, x2) = (c[0], c[1]);
                    var startIndex = i * w + x1;
                    var count = x2 - x1;
                    Array.Fill(arr, color, startIndex, count);
                    //.Write(startIndex + " " + count);
                    //line2(x1,i,x2,i);
                }
            }
            test.Paint();
            //PaintLetter("?");
        }
        window.MouseMove += (sender, args) =>
        {
            //Console.WriteLine(args.GetPosition(test).Y);
        };
        window.Loaded += (sender, args) =>
        {
            
            using (timer("paint"))
            {
                Refresh();
            }
        };
        System.Windows.Application.Current.Run(window);
        
        void PaintLetter(string Text)
        {
            var teststr =
                @" █ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890~!@#$%^&*()_+`-=[];',./{}:""<>?|";
            canvas.Children.Clear();
            formattedText = new FormattedText(Text+teststr, CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, typeface, FontSize,
                System.Windows.Media.Brushes.Black, // This brush does not matter since we use the geometry of the text.
                1.0
            );
            var geom = formattedText.BuildGeometry(new System.Windows.Point(0, 0));

            var smoothPoly = geom.ToPolygons().ToLineSegments().Select(s=>s.ToLine());
            foreach (var line in smoothPoly)
            {
                line.Stroke = Brushes.White;
                line.StrokeThickness = 2;
                //canvas.Children.Add(line);
            }
            geom.Transform = new TranslateTransform(400,0);
            geom = geom.GetFlattenedPathGeometry(0.01, ToleranceType.Relative);
            
            
            var coarsePoly = geom.ToPolygons().ToLineSegments().Select(s=>s.ToLine());
            foreach (var line in coarsePoly)
            {
                line.Stroke = Brushes.Yellow;
                line.StrokeThickness = 2;
                //canvas.Children.Add(line);
            }
            var height = (int)canvas.ActualHeight;
            
            var hLineInfo = new HLineInfo(height);
 
            var geom2 = formattedText.BuildGeometry(new System.Windows.Point(0, 0)).GetFlattenedPathGeometry(0.01, ToleranceType.Relative);
            
            var cp2 = geom2.ToPolygons();
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

    static string Res(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var n = assembly.GetManifestResourceNames();
        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}