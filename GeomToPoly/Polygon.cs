using System.Windows;

namespace GeomToPoly;
internal struct Glyph
{
    internal Rect Bounds;
    internal Polygon[] Shapes;
}
internal struct Polygon
{
    internal double[] x;
    internal double[] y;
    internal Rect Bounds;

    internal void Shift(double xshift, double yshift)
    {
        for (int i = 0; i < x.Length; i++)
        {
            x[i] += xshift;
            y[i] += yshift;
        }
    }
    internal static Polygon New(IEnumerable<double> poly)
    {
        var ret = new Polygon();
        var xs = new List<double>();
        var ys = new List<double>();
        double minx=double.MaxValue, miny=double.MaxValue, maxx=double.MinValue, maxy=double.MinValue, offsetX = 0, offsetY = 0;
        foreach (var d in poly.Chunk(2))
        {
            var x = d[0];
            var y = d[1];
            minx = x < minx ? x : minx;
            miny = y < miny ? y : miny;
            maxx = x > maxx ? x : maxx;
            maxy = y > maxy ? y : maxy;
        }

        offsetX = minx < 0 ? -minx : 0;
        offsetY = miny < 0 ? -miny : 0;
        
        foreach (var d in poly.Chunk(2))
        {
            var x = d[0];
            var y = d[1];
            xs.Add(x+offsetX);
            ys.Add(y+offsetY);
        }
        
        ret.x = xs.ToArray();
        ret.y = ys.ToArray();
        ret.Bounds = new Rect(new Point(minx, miny), new Point(maxx, maxy));
        ret.Bounds.Offset(offsetX,offsetY);
        
        return ret;
    }
}