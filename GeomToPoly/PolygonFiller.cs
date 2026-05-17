using System;
using System.Collections.Generic;

namespace GeomToPoly;
internal struct HLineInfo
{
    internal List<int>[] Rows;
    internal List<int> UsedRowIndexes;
    public HLineInfo(int height)
    {
        Rows = new List<int>[height];
        for (int i = 0; i < height; i++)
        {
            Rows[i] = new List<int>(16);
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

    
    /// <summary>
    /// Fills a polygon on the given bitmap using the efficient scanline algorithm. https://alienryderflex.com/polygon_fill/
    /// </summary>
    /// <param name="bitmap">The bitmap to draw on.</param>
    /// <param name="polyX">Array of X coordinates of polygon vertices.</param>
    /// <param name="polyY">Array of Y coordinates of polygon vertices.</param>
    internal static IEnumerable<(int y, int x1, int x2)> FillPolygon(int width,int height, double[] polyX, double[] polyY, HLineInfo inf)
    {
        int polyCorners = polyX.Length;
        if (polyCorners < 3)
        {
            yield return (-1, -1, -1);
            yield break;
        }

        int imageTop = 0;
        int imageBottom = height;
        int imageLeft = 0;
        int imageRight = width;

        // Precompute bounding box to reduce rows processed (optional optimization)
        double minY = polyY[0];
        double maxY = polyY[0];
        for (int i = 1; i < polyCorners; i++)
        {
            if (polyY[i] < minY) minY = polyY[i];
            if (polyY[i] > maxY) maxY = polyY[i];
        }

        int startY = Math.Max(imageTop, (int)Math.Ceiling(minY));
        int endY = Math.Min(imageBottom - 1, (int)Math.Floor(maxY));

        var nodeX = new List<int>();

        for (int pixelY = startY; pixelY <= endY; pixelY++)
        {
            nodeX.Clear();

            int j = polyCorners - 1;
            for (int i = 0; i < polyCorners; i++)
            {
                // Check if edge crosses the current scanline
                bool cond1 = (polyY[i] < pixelY && polyY[j] >= pixelY);
                bool cond2 = (polyY[j] < pixelY && polyY[i] >= pixelY);

                if (cond1 || cond2)
                {
                    // Calculate intersection X coordinate
                    double intersectX = polyX[i] +
                                        (pixelY - polyY[i]) /
                                        (polyY[j] - polyY[i]) *
                                        (polyX[j] - polyX[i]);

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

                if (xStart >= imageRight) break;
                if (xEnd <= imageLeft) continue;
                if (xStart < imageLeft) xStart = imageLeft;
                if (xEnd > imageRight) xEnd = imageRight;
                inf.Push(pixelY,xStart,xEnd);
                yield return (pixelY, xStart, xEnd);
            }
        }
    }
    internal static IEnumerable<(int y, int x1, int x2)> FillPolygon2(Polygon p, HLineInfo inf, int xoffs, int yoffs)
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
                
                inf.Push(pixelY+yoffs,xStart+xoffs,xEnd+xoffs);
                yield return (pixelY+yoffs,xStart+xoffs,xEnd+xoffs);
            }
        }
    }
}