using MetroHub.Core.Models;

namespace MetroHub.Core.Services;

public static class GridPlacementService
{
    public const double GridStep = 64.0;
    public const double Gap = 8.0;
    public const double BaseSideMargin = 32.0;
    public const double OriginY = 40.0;

    public static double OriginX { get; set; } = 48.0;
    public static int MaxCols { get; set; } = 28;

    public static void UpdateMetrics(double viewportWidth)
    {
        double available = Math.Max(320, viewportWidth - (2 * BaseSideMargin));
        MaxCols = Math.Max(4, (int)Math.Floor((available + Gap) / GridStep));
        double gridWidth = (MaxCols * GridStep) - Gap;
        OriginX = Math.Round(Math.Max(BaseSideMargin, (viewportWidth - gridWidth) / 2.0));
    }

    public static int GetMaxCols(double viewportWidth)
    {
        double available = Math.Max(320, viewportWidth - (2 * BaseSideMargin));
        return Math.Max(4, (int)Math.Floor((available + Gap) / GridStep));
    }

    public static int ColFromPixel(double x)
    {
        return Math.Max(0, (int)Math.Round((x - OriginX) / GridStep));
    }

    public static int RowFromPixel(double y)
    {
        return Math.Max(0, (int)Math.Round((y - OriginY) / GridStep));
    }

    public static double PixelXFromCol(int col)
    {
        return OriginX + (col * GridStep);
    }

    public static double PixelYFromRow(int row)
    {
        return OriginY + (row * GridStep);
    }

    public static int GetCol(TileModel tile) => ColFromPixel(tile.X);
    public static int GetRow(TileModel tile) => RowFromPixel(tile.Y);

    public static bool DoTilesOverlap(int c1, int r1, int sx1, int sy1, int c2, int r2, int sx2, int sy2)
    {
        return c1 < c2 + sx2 &&
               c1 + sx1 > c2 &&
               r1 < r2 + sy2 &&
               r1 + sy1 > r2;
    }

    public static bool IsRegionFree(int col, int row, int spanX, int spanY, IEnumerable<TileModel> tiles, TileModel? ignoreTile = null, int maxCols = int.MaxValue)
    {
        if (col < 0 || row < 0) return false;
        if (col + spanX > maxCols) return false;

        foreach (var other in tiles)
        {
            if (ReferenceEquals(other, ignoreTile)) continue;

            int otherCol = GetCol(other);
            int otherRow = GetRow(other);

            if (DoTilesOverlap(col, row, spanX, spanY, otherCol, otherRow, other.SpanX, other.SpanY))
            {
                return false;
            }
        }

        return true;
    }

    public static List<TileModel> GetOverlappingTiles(int col, int row, int spanX, int spanY, IEnumerable<TileModel> tiles, TileModel? ignoreTile = null)
    {
        var result = new List<TileModel>();
        if (col < 0 || row < 0) return result;

        foreach (var other in tiles)
        {
            if (ReferenceEquals(other, ignoreTile)) continue;

            int otherCol = GetCol(other);
            int otherRow = GetRow(other);

            if (DoTilesOverlap(col, row, spanX, spanY, otherCol, otherRow, other.SpanX, other.SpanY))
            {
                result.Add(other);
            }
        }

        return result;
    }

    public static (int Col, int Row) FindNearestAvailableSlot(int startCol, int startRow, int spanX, int spanY, IEnumerable<TileModel> tiles, TileModel? ignoreTile = null, int maxCols = int.MaxValue)
    {
        startCol = Math.Max(0, Math.Min(startCol, Math.Max(0, maxCols - spanX)));
        startRow = Math.Max(0, startRow);

        if (IsRegionFree(startCol, startRow, spanX, spanY, tiles, ignoreTile, maxCols))
        {
            return (startCol, startRow);
        }

        // Search in expanding concentric rings
        for (int radius = 1; radius <= 40; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;

                    int c = startCol + dx;
                    int r = startRow + dy;

                    if (c < 0 || r < 0) continue;
                    if (c + spanX > maxCols) continue;

                    if (IsRegionFree(c, r, spanX, spanY, tiles, ignoreTile, maxCols))
                    {
                        return (c, r);
                    }
                }
            }
        }

        // Fallback: search downward row by row
        for (int r = startRow + 1; r < startRow + 50; r++)
        {
            for (int c = 0; c <= maxCols - spanX; c++)
            {
                if (IsRegionFree(c, r, spanX, spanY, tiles, ignoreTile, maxCols))
                {
                    return (c, r);
                }
            }
        }

        return (0, startRow + 2);
    }

    public static List<TileModel> PlaceAndResolveCollisions(
        TileModel draggedTile, 
        int targetCol, 
        int targetRow, 
        int originalCol, 
        int originalRow, 
        int maxCols, 
        IList<TileModel> allTiles)
    {
        var modifiedTiles = new List<TileModel>();
        int maxAllowedCol = Math.Max(0, maxCols - draggedTile.SpanX);
        targetCol = Math.Max(0, Math.Min(targetCol, maxAllowedCol));
        targetRow = Math.Max(0, targetRow);

        // If dropped back where it started, simply re-snap
        if (targetCol == originalCol && targetRow == originalRow)
        {
            draggedTile.X = PixelXFromCol(targetCol);
            draggedTile.Y = PixelYFromRow(targetRow);
            modifiedTiles.Add(draggedTile);
            return modifiedTiles;
        }

        var overlapping = GetOverlappingTiles(targetCol, targetRow, draggedTile.SpanX, draggedTile.SpanY, allTiles, draggedTile);

        if (overlapping.Count == 0)
        {
            // Case 1: Slot is completely free
            draggedTile.X = PixelXFromCol(targetCol);
            draggedTile.Y = PixelYFromRow(targetRow);
            modifiedTiles.Add(draggedTile);
            return modifiedTiles;
        }

        if (overlapping.Count == 1 && overlapping[0].SpanX == draggedTile.SpanX && overlapping[0].SpanY == draggedTile.SpanY)
        {
            // Case 2: Clean 1-to-1 swap with tile of identical size!
            var targetTile = overlapping[0];
            int swapCol = GetCol(targetTile);
            int swapRow = GetRow(targetTile);

            draggedTile.X = PixelXFromCol(swapCol);
            draggedTile.Y = PixelYFromRow(swapRow);
            modifiedTiles.Add(draggedTile);

            // targetTile moves cleanly to draggedTile's original position!
            targetTile.X = PixelXFromCol(originalCol);
            targetTile.Y = PixelYFromRow(originalRow);
            modifiedTiles.Add(targetTile);

            return modifiedTiles;
        }

        // Case 3: Multiple tiles or different size
        // Dragged tile claims the slot
        draggedTile.X = PixelXFromCol(targetCol);
        draggedTile.Y = PixelYFromRow(targetRow);
        modifiedTiles.Add(draggedTile);

        // Build list of already placed tiles to avoid chain collisions
        var lockedTiles = allTiles.Except(overlapping).Except(new[] { draggedTile }).ToList();
        lockedTiles.Add(draggedTile);

        foreach (var displaced in overlapping)
        {
            int dCol = GetCol(displaced);
            int dRow = GetRow(displaced);

            var (newCol, newRow) = FindNearestAvailableSlot(dCol, dRow, displaced.SpanX, displaced.SpanY, lockedTiles, null, maxCols);
            displaced.X = PixelXFromCol(newCol);
            displaced.Y = PixelYFromRow(newRow);
            lockedTiles.Add(displaced);
            modifiedTiles.Add(displaced);
        }

        return modifiedTiles;
    }

    public static void SanitizeAndSnapAll(IList<TileModel> tiles, int maxCols)
    {
        var placedTiles = new List<TileModel>();

        foreach (var tile in tiles)
        {
            int col = Math.Min(Math.Max(0, ColFromPixel(tile.X)), Math.Max(0, maxCols - tile.SpanX));
            int row = Math.Max(0, RowFromPixel(tile.Y));

            var (freeCol, freeRow) = FindNearestAvailableSlot(col, row, tile.SpanX, tile.SpanY, placedTiles, null, maxCols);

            tile.X = PixelXFromCol(freeCol);
            tile.Y = PixelYFromRow(freeRow);

            placedTiles.Add(tile);
        }
    }
}
