using System;
using System.Collections.Generic;

namespace GeomToPoly;

internal struct MyBrush
{
    internal int Color;

    public static implicit operator MyBrush(int i)
    {
        return new MyBrush() { Color = i };
    }
}
internal struct HLineInfo
{
    internal List<int>[] Rows;
    internal MyBrush[][] Brushes;
    internal List<int> UsedRowIndexes;
    public HLineInfo(int height)
    {
        Rows = new List<int>[height];
        Brushes = new MyBrush[height][];
        for (int i = 0; i < height; i++)
        {
            Rows[i] = new List<int>(16);
            Brushes[i] = new MyBrush[1920];
        }
        UsedRowIndexes = new List<int>(height);
    }

    public void Push(int y, int x1, int x2)
    {
        Rows[y].AddRange([x1,x2]);
        UsedRowIndexes.Add(y);
    }
}
internal static class PolygonFiller
{
    internal static IEnumerable<(int y, int x1, int x2)> FillPolygon(Polygon p, HLineInfo inf, int xoffs, int yoffs)
    {
        var polyX = p.x;
        var polyY = p.y;
        int polyCorners = polyX.Length;
        if (polyCorners < 3)
        {
            yield break;
        }
        int startY = (int)Math.Ceiling(p.Bounds.Top);
        int endY = (int)Math.Floor(p.Bounds.Bottom);

        var nodeX = new List<int>();

        for (int pixelY = startY; pixelY <= endY; pixelY++)
        {
            nodeX.Clear();

            int j = polyCorners - 1;
            for (int i = 0; i < polyCorners; i++)
            {
                // Check if edge crosses the current scanline
                if (polyY[i] < pixelY && polyY[j] >= pixelY || polyY[j] < pixelY && polyY[i] >= pixelY)
                {
                    // Calculate intersection X coordinate
                    double intersectX = polyX[i] + (pixelY - polyY[i]) / (polyY[j] - polyY[i]) * (polyX[j] - polyX[i]);
                    nodeX.Add((int)intersectX);
                }
                j = i;
            }
            var count = nodeX.Count;
            
            nodeX.Sort();

            // Fill between pairs of nodes
            for (int i = 0; i < count; i += 2)
            {
                if (i + 1 >= count) break;

                int xStart = nodeX[i];
                int xEnd = nodeX[i + 1];

                var y = pixelY+yoffs;
                var startx = xStart+xoffs;
                var endx = xEnd+xoffs;
                inf.Push(y,startx,endx);
                yield return (y,startx,endx);
            }
        }
    }
}