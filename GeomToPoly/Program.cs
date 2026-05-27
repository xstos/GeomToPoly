using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

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

    static Func<int> MakeGetNextColor()
    {
        var enu = ColorWheel().GetEnumerator();
        int Next()
        {
            enu.MoveNext();
            return enu.Current;
        }

        return Next;
    }
    
    [STAThread]
    public static void Main()
    {
        var app = new Application();

        var Text = "P";
        var FontSize = 20;
        var fontName = "Jetbrains Mono";
        var typeface = new Typeface(new FontFamily(fontName), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var nextColor = MakeGetNextColor();
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

        var txt = Res("GeomToPoly.twist.txt");
        //var txt = new WebClient().DownloadString("https://www.gutenberg.org/cache/epub/730/pg730.txt");
        var chars = txt.Distinct();
        var max = chars.Max(c => (int)c)+1;
        var lines = txt.ReplaceLineEndings("█").Split('█');
        var widest = lines.Max(l => l.Length);
        var brushes = new MyBrush[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
        {
            //lines[i] = lines[i].PadRight(widest,' ');
            brushes[i] = Enumerable.Range(0, widest).Select(_ => (MyBrush)nextColor()).ToArray();
        }

        var glyphs = new Glyph[max];
        using (timer("make glyphs"))
        {
            foreach (var c in chars)
            {
                glyphs[c] = MakeGlyph(c);
            }
        }

        var popts = new ParallelOptions() { MaxDegreeOfParallelism = 1 };
        var box = MakeGlyph('█');
        HLineInfo hi = new HLineInfo(1200);
        HLineInfo whole = new HLineInfo(1200);
        
        var window = new Window();
        window.Left = 0;
        window.Top = 0;
        window.Width = 1920;
        window.Height = 1200;
        
        window.Background = Brushes.Black;
        var test = new PixelBuffer();
        window.Content = test;
        int lineOffset = 0;
        window.PreviewMouseWheel += (sender, args) =>
        {
            var offs = -args.Delta/10;
            lineOffset += offs;
            if (lineOffset < 0) lineOffset = 0;
            Refresh();
        };
        
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
            Refresh();
        };
        window.TextInput += (sender, args) =>
        {
        };
        
        window.SizeChanged += (sender, args) =>
        {
            var height = (int)Math.Floor(args.NewSize.Height);
            hi = new HLineInfo(height);
            whole = new HLineInfo(height);
        };
        
        test.Render = Render;
        
        void Render()
        {
            
            hi.UsedRowIndexes.Clear();
            whole.UsedRowIndexes.Clear();
            var arr = test.Pixels;
            Array.Fill(arr, 0);
            var w = (int)test.ActualWidth;
            var h = (int)test.ActualHeight;
            var (canvasWidth, canvasHeight) = (w, h);
            var glyphWidth = (int)Math.Round(box.Bounds.Width, MidpointRounding.AwayFromZero);
            var glyphHeight = (int)Math.Round(box.Bounds.Height, MidpointRounding.AwayFromZero);
            var numCols = canvasWidth / glyphWidth;
            var numRows = canvasHeight / glyphHeight;
            
            for (int i = 0; i < numRows - 1; i++)
            {
                var yoffs = i * glyphHeight;
                for (int j = 0; j < numCols; j++)
                {
                    var xoffs = j * glyphWidth;
                    if (j > lines[i+lineOffset].Length - 1) break;
                    char c = lines[i+lineOffset][j];
                    MyBrush brush = brushes[i + lineOffset][j];
                    var ix = (int)c;
                    var glyph = glyphs[ix];
                    foreach (var shape in glyph.Shapes)
                    {
                        foreach (var tuple in PolygonFiller.FillPolygon(shape, xoffs, yoffs))
                        {
                            hi.Push(tuple.y,tuple.x1,tuple.x2);
                        }
                    }

                    for (int ui = 0; ui < hi.UsedRowIndexes.Count; ui++)
                    {
                        var y = hi.UsedRowIndexes[ui];
                        var verts = hi.Rows[y];
                        verts.Sort();
                        whole.Rows[y].AddRange(verts);
                        foreach (var t in verts)
                        {
                            whole.Brushes[y][t] = brush;
                        }
                        verts.Clear();
                    }

                    hi.UsedRowIndexes.Clear();
                }
            }
            
            Parallel.For(0, whole.Rows.Length,popts, i =>
            {
                var verts = whole.Rows[i];
                var brushes = whole.Brushes[i];
                foreach (var c in verts.Chunk(2))
                {
                    var (x1, x2) = (c[0], c[1]);
                    var startIndex = i * w + x1;
                    var count = x2 - x1+1;
                    
                    Array.Fill(arr, brushes[x1].Color, startIndex, count);
                }

                verts.Clear();
            });
            
        }
        void Refresh()
        {
            using (timer("redraw"))
            {
                Render();
            }

            test.Paint();
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
        
        Application.Current.Run(window);
        
    }
    static IEnumerable<int> ColorWheel()
    {
        var colors = Enum.GetValues(typeof(KnownColor)).Cast<int>().ToArray();
        while (true)
        {
            foreach (var t in colors)
            {
                yield return t;
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