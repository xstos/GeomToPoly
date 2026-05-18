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
        var colors = Enum.GetValues(typeof(KnownColor)).Cast<int>().ToArray();
        IEnumerable<int> ColorWheel()
        {
            while (true)
            {
                foreach (var t in colors)
                {
                    yield return t;
                }
            }
        }

        var enu = ColorWheel().GetEnumerator();

        int NextColor()
        {
            enu.MoveNext();
            return enu.Current;
        }
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
        var brushes = new MyBrush[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].PadRight(widest);
            brushes[i] = Enumerable.Range(0, widest).Select(_ => (MyBrush)NextColor()).ToArray();
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
        HLineInfo hi = new HLineInfo(1080);
        HLineInfo whole = new HLineInfo(1080);
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
            //fastPixels.Clear();
            Refresh();
            //fastPixels.Paint();
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
        
        test.Redraw = Redraw;
        
        void Redraw()
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
                        foreach (var t in verts)
                        {
                            whole.Brushes[y][t] = brush;
                        }
                        verts.Clear();
                    }

                    hi.UsedRowIndexes.Clear();
                }
            }

            for (int i = 0; i < whole.Rows.Length; i++)
            {
                var verts = whole.Rows[i];
                var brushes = whole.Brushes[i];
                //var color = BitConverter.ToInt32([bi, gi, 0, 0]); //bgra
                foreach (var c in verts.Chunk(2))
                {
                    var (x1, x2) = (c[0], c[1]);
                    var startIndex = i * w + x1;
                    var count = x2 - x1+1;
                    
                    Array.Fill(arr, brushes[x1].Color, startIndex, count);
                    //.Write(startIndex + " " + count);
                    //line2(x1,i,x2,i);
                }

                verts.Clear();
            }
            
        }
        void Refresh()
        {
            Redraw();
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
                
                //var hl = PolygonFiller.FillPolygon((int)canvas.ActualWidth, height, xs.ToArray(), ys.ToArray(), hLineInfo).ToArray();
                
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
internal enum KnownColor : uint
{
  //UnknownColor = 1,
  //Transparent = 16777215, // 0x00FFFFFF
  //Black = 4278190080, // 0xFF000000
  Navy = 4278190208, // 0xFF000080
  DarkBlue = 4278190219, // 0xFF00008B
  MediumBlue = 4278190285, // 0xFF0000CD
  Blue = 4278190335, // 0xFF0000FF
  DarkGreen = 4278215680, // 0xFF006400
  Green = 4278222848, // 0xFF008000
  Teal = 4278222976, // 0xFF008080
  DarkCyan = 4278225803, // 0xFF008B8B
  DeepSkyBlue = 4278239231, // 0xFF00BFFF
  DarkTurquoise = 4278243025, // 0xFF00CED1
  MediumSpringGreen = 4278254234, // 0xFF00FA9A
  Lime = 4278255360, // 0xFF00FF00
  SpringGreen = 4278255487, // 0xFF00FF7F
  Aqua = 4278255615, // 0xFF00FFFF
  Cyan = 4278255615, // 0xFF00FFFF
  MidnightBlue = 4279834992, // 0xFF191970
  DodgerBlue = 4280193279, // 0xFF1E90FF
  LightSeaGreen = 4280332970, // 0xFF20B2AA
  ForestGreen = 4280453922, // 0xFF228B22
  SeaGreen = 4281240407, // 0xFF2E8B57
  DarkSlateGray = 4281290575, // 0xFF2F4F4F
  LimeGreen = 4281519410, // 0xFF32CD32
  MediumSeaGreen = 4282168177, // 0xFF3CB371
  Turquoise = 4282441936, // 0xFF40E0D0
  RoyalBlue = 4282477025, // 0xFF4169E1
  SteelBlue = 4282811060, // 0xFF4682B4
  DarkSlateBlue = 4282924427, // 0xFF483D8B
  MediumTurquoise = 4282962380, // 0xFF48D1CC
  Indigo = 4283105410, // 0xFF4B0082
  DarkOliveGreen = 4283788079, // 0xFF556B2F
  CadetBlue = 4284456608, // 0xFF5F9EA0
  CornflowerBlue = 4284782061, // 0xFF6495ED
  MediumAquamarine = 4284927402, // 0xFF66CDAA
  DimGray = 4285098345, // 0xFF696969
  SlateBlue = 4285160141, // 0xFF6A5ACD
  OliveDrab = 4285238819, // 0xFF6B8E23
  SlateGray = 4285563024, // 0xFF708090
  LightSlateGray = 4286023833, // 0xFF778899
  MediumSlateBlue = 4286277870, // 0xFF7B68EE
  LawnGreen = 4286381056, // 0xFF7CFC00
  Chartreuse = 4286578432, // 0xFF7FFF00
  Aquamarine = 4286578644, // 0xFF7FFFD4
  Maroon = 4286578688, // 0xFF800000
  Purple = 4286578816, // 0xFF800080
  Olive = 4286611456, // 0xFF808000
  Gray = 4286611584, // 0xFF808080
  SkyBlue = 4287090411, // 0xFF87CEEB
  LightSkyBlue = 4287090426, // 0xFF87CEFA
  BlueViolet = 4287245282, // 0xFF8A2BE2
  DarkRed = 4287299584, // 0xFF8B0000
  DarkMagenta = 4287299723, // 0xFF8B008B
  SaddleBrown = 4287317267, // 0xFF8B4513
  DarkSeaGreen = 4287609999, // 0xFF8FBC8F
  LightGreen = 4287688336, // 0xFF90EE90
  MediumPurple = 4287852763, // 0xFF9370DB
  DarkViolet = 4287889619, // 0xFF9400D3
  PaleGreen = 4288215960, // 0xFF98FB98
  DarkOrchid = 4288230092, // 0xFF9932CC
  YellowGreen = 4288335154, // 0xFF9ACD32
  Sienna = 4288696877, // 0xFFA0522D
  Brown = 4289014314, // 0xFFA52A2A
  DarkGray = 4289309097, // 0xFFA9A9A9
  LightBlue = 4289583334, // 0xFFADD8E6
  GreenYellow = 4289593135, // 0xFFADFF2F
  PaleTurquoise = 4289720046, // 0xFFAFEEEE
  LightSteelBlue = 4289774814, // 0xFFB0C4DE
  PowderBlue = 4289781990, // 0xFFB0E0E6
  Firebrick = 4289864226, // 0xFFB22222
  DarkGoldenrod = 4290283019, // 0xFFB8860B
  MediumOrchid = 4290401747, // 0xFFBA55D3
  RosyBrown = 4290547599, // 0xFFBC8F8F
  DarkKhaki = 4290623339, // 0xFFBDB76B
  Silver = 4290822336, // 0xFFC0C0C0
  MediumVioletRed = 4291237253, // 0xFFC71585
  IndianRed = 4291648604, // 0xFFCD5C5C
  Peru = 4291659071, // 0xFFCD853F
  Chocolate = 4291979550, // 0xFFD2691E
  Tan = 4291998860, // 0xFFD2B48C
  LightGray = 4292072403, // 0xFFD3D3D3
  Thistle = 4292394968, // 0xFFD8BFD8
  Orchid = 4292505814, // 0xFFDA70D6
  Goldenrod = 4292519200, // 0xFFDAA520
  PaleVioletRed = 4292571283, // 0xFFDB7093
  Crimson = 4292613180, // 0xFFDC143C
  Gainsboro = 4292664540, // 0xFFDCDCDC
  Plum = 4292714717, // 0xFFDDA0DD
  BurlyWood = 4292786311, // 0xFFDEB887
  LightCyan = 4292935679, // 0xFFE0FFFF
  Lavender = 4293322490, // 0xFFE6E6FA
  DarkSalmon = 4293498490, // 0xFFE9967A
  Violet = 4293821166, // 0xFFEE82EE
  PaleGoldenrod = 4293847210, // 0xFFEEE8AA
  LightCoral = 4293951616, // 0xFFF08080
  Khaki = 4293977740, // 0xFFF0E68C
  AliceBlue = 4293982463, // 0xFFF0F8FF
  Honeydew = 4293984240, // 0xFFF0FFF0
  Azure = 4293984255, // 0xFFF0FFFF
  SandyBrown = 4294222944, // 0xFFF4A460
  Wheat = 4294303411, // 0xFFF5DEB3
  Beige = 4294309340, // 0xFFF5F5DC
  WhiteSmoke = 4294309365, // 0xFFF5F5F5
  MintCream = 4294311930, // 0xFFF5FFFA
  GhostWhite = 4294506751, // 0xFFF8F8FF
  Salmon = 4294606962, // 0xFFFA8072
  AntiqueWhite = 4294634455, // 0xFFFAEBD7
  Linen = 4294635750, // 0xFFFAF0E6
  LightGoldenrodYellow = 4294638290, // 0xFFFAFAD2
  OldLace = 4294833638, // 0xFFFDF5E6
  Red = 4294901760, // 0xFFFF0000
  Fuchsia = 4294902015, // 0xFFFF00FF
  Magenta = 4294902015, // 0xFFFF00FF
  DeepPink = 4294907027, // 0xFFFF1493
  OrangeRed = 4294919424, // 0xFFFF4500
  Tomato = 4294927175, // 0xFFFF6347
  HotPink = 4294928820, // 0xFFFF69B4
  Coral = 4294934352, // 0xFFFF7F50
  DarkOrange = 4294937600, // 0xFFFF8C00
  LightSalmon = 4294942842, // 0xFFFFA07A
  Orange = 4294944000, // 0xFFFFA500
  LightPink = 4294948545, // 0xFFFFB6C1
  Pink = 4294951115, // 0xFFFFC0CB
  Gold = 4294956800, // 0xFFFFD700
  PeachPuff = 4294957753, // 0xFFFFDAB9
  NavajoWhite = 4294958765, // 0xFFFFDEAD
  Moccasin = 4294960309, // 0xFFFFE4B5
  Bisque = 4294960324, // 0xFFFFE4C4
  MistyRose = 4294960353, // 0xFFFFE4E1
  BlanchedAlmond = 4294962125, // 0xFFFFEBCD
  PapayaWhip = 4294963157, // 0xFFFFEFD5
  LavenderBlush = 4294963445, // 0xFFFFF0F5
  SeaShell = 4294964718, // 0xFFFFF5EE
  Cornsilk = 4294965468, // 0xFFFFF8DC
  LemonChiffon = 4294965965, // 0xFFFFFACD
  FloralWhite = 4294966000, // 0xFFFFFAF0
  Snow = 4294966010, // 0xFFFFFAFA
  Yellow = 4294967040, // 0xFFFFFF00
  LightYellow = 4294967264, // 0xFFFFFFE0
  Ivory = 4294967280, // 0xFFFFFFF0
  White = 4294967295, // 0xFFFFFFFF
}