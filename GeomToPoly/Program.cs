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

        var FontSize = 20;
        var fontName = "Jetbrains Mono";
        var typeface = new Typeface(new FontFamily(fontName), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var nextColor = MakeGetNextColor();
        int glyphWidth, glyphHeight;
        
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

        var txt = Res("GeomToPoly.twist.txt"); /*.Replace("W","W█"); */
        //var txt = new WebClient().DownloadString("https://www.gutenberg.org/cache/epub/730/pg730.txt");
        var chars = txt.Distinct();
        var max = chars.Max(c => (int)c)+1;
        Console.WriteLine("last char index "+max);
        var glyphs = new Glyph[max];
        var lines = txt.ReplaceLineEndings("\n").Split('\n');
        var widest = lines.Max(l => l.Length);
        var brushes = new MyBrush[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
        {
            //lines[i] = lines[i].PadRight(widest,' ');
            brushes[i] = Enumerable.Range(0, widest).Select(_ => (MyBrush)(nextColor(),nextColor())).ToArray();
        }

        void MakeGlyphs()
        {
            using var _ = timer("make glyphs");
            glyphWidth = int.MinValue;
            glyphHeight = int.MinValue;
            foreach (var c in chars)
            {
                var g = MakeGlyph(c);
                glyphs[c] = g;
                var (w, h) = g.Size();
                if (w > glyphWidth) glyphWidth = w;
                if (h > glyphHeight) glyphHeight = h;
            }
        }
        MakeGlyphs();

        var popts = new ParallelOptions() { MaxDegreeOfParallelism = 1 };
        HLineInfo singleGlyphLineInfo = new HLineInfo(1200);
        HLineInfo wholeLineInfo = new HLineInfo(1200);
        
        var window = new Window();
        window.Left = 0;
        window.Top = 0;
        window.Width = 1920;
        window.Height = 1200;
        
        window.Background = Brushes.Black;
        var pixelBuffer = new PixelBuffer();
        window.Content = pixelBuffer;
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

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (args.Key == Key.OemPlus)
                {
                    FontSize += 1;
                    MakeGlyphs();
                }

                if (args.Key == Key.OemMinus)
                {
                    FontSize -= 1;
                    if (FontSize < 1) FontSize = 1;
                    MakeGlyphs();
                }

                
            }
            Refresh();
        };
        window.TextInput += (sender, args) =>
        {
        };
        
        window.SizeChanged += (sender, args) =>
        {
            var height = (int)Math.Floor(args.NewSize.Height);
            if (singleGlyphLineInfo.Height >= height) return;
            singleGlyphLineInfo = new HLineInfo(height);
            wholeLineInfo = new HLineInfo(height);
        };
        
        pixelBuffer.Render = Render;
        
        void Render()
        {
            singleGlyphLineInfo.UsedRowIndexes.Clear();
            wholeLineInfo.UsedRowIndexes.Clear();
            var wholeRows = wholeLineInfo.Rows;
            var singleGlyphUsedRowIndexes = singleGlyphLineInfo.UsedRowIndexes;
            var pixels = pixelBuffer.Pixels;
            Array.Fill(pixels, 0);
            var pixelBufferWidth = (int)pixelBuffer.ActualWidth;
            var pixelBufferHeight = (int)pixelBuffer.ActualHeight;
            var numCols = pixelBufferWidth / glyphWidth;
            var numRows = pixelBufferHeight / glyphHeight;
            
            for (int i = 0; i < numRows - 1; i++)
            {
                var lineIndex = i+lineOffset;
                var line = lines[lineIndex];
                var lineLength = line.Length - 1;
                var yoffs = i * glyphHeight;
                for (int j = 0; j < Math.Min(numCols,lineLength); j++)
                {
                    var xoffs = j * glyphWidth;
                    char c = line[j];
                    MyBrush brush = brushes[lineIndex][j];
                    
                    var ix = (int)c;
                    var glyph = glyphs[ix];
                    foreach (var shape in glyph.Shapes)
                    {
                        foreach (var tuple in PolygonFiller.FillPolygon(shape, xoffs, yoffs))
                        {
                            singleGlyphLineInfo.Push(tuple.y,tuple.x1,tuple.x2);
                        }
                    }

                    var count = singleGlyphUsedRowIndexes.Count;
                    for (int k = 0; k < count; k++)
                    {
                        var y = singleGlyphUsedRowIndexes[k];
                        var verts = singleGlyphLineInfo.Rows[y];
                        verts.Sort();
                        //wholeRows[y].Add(brush.ForegroundColor);
                        wholeRows[y].AddRange(verts);
                        foreach (var t in verts)
                        {
                            wholeLineInfo.Brushes[y][t] = brush;
                        }
                        verts.Clear();
                    }

                    singleGlyphUsedRowIndexes.Clear();
                }
            }
            
            Parallel.For(0, wholeRows.Length,popts, i =>
            {
                var verts = wholeRows[i];
                var brushes = wholeLineInfo.Brushes[i];
                foreach (var c in verts.Chunk(2))
                {
                    var (x1, x2) = (c[0], c[1]);
                    var startIndex = i * pixelBufferWidth + x1;
                    var count = x2 - x1 + 1;
                    
                    Array.Fill(pixels, brushes[x1].ForegroundColor, startIndex, count);
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

            pixelBuffer.Paint();
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