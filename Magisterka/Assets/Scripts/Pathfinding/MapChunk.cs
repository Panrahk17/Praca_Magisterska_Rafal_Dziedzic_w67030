using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class MapChunk : IHeapItem<MapChunk>
{
    //IHeapItem
    public int HeapIndex { get; set; }

    //STATE
    public int x = 0;
    public int y = 0;
    public HashSet<MapChunk> neighbours = new HashSet<MapChunk>();
    public List<TileData> tilesInChunk = new List<TileData>();
    Vector2Int centroid = new Vector2Int(-1, -1);
    public Vector2Int Centroid
    {
        get
        {
            if (centroid.x == -1)
            {
                CalculateCentroid();
            }
            return centroid;
        }
    }

    //A*
    public MapChunk parent = null;
    public int GCost;
    public int HCost;
    public int FCost { get { return GCost + HCost; } }


    void CalculateCentroid()
    {
        (int, int) result = (0, 0);
        for (int i = 0; i < tilesInChunk.Count; i++)
        {
            result.Item1 += tilesInChunk[i].x;
            result.Item2 += tilesInChunk[i].y;
        }
        result.Item1 /= tilesInChunk.Count;
        result.Item2 /= tilesInChunk.Count;

        centroid = new Vector2Int(result.Item1, result.Item2);
    }
    public static TileData GetTileClosestToCentroid(MapChunk chunk)
    {
        Grid grid = Grid.Instance;
        var gridData = grid.gridData;
        int xSize = gridData.GetLength(0);
        int ySize = gridData.GetLength(1);

        int size = Grid.MaxChunkSize / Pathfinding.DefaultMovementCosts.x;// Spiral size (steps count)
        int x = chunk.Centroid.x;    // Start x value (middle)
        int y = chunk.Centroid.y;    // Start y value (middle)

        for (int step = 1; step <= size; step++)
        {
            // shift right
            for (int i = 0; i < step; i++)
            {
                if (x >= 0 && x < xSize && y >= 0 && y < ySize)
                {
                    if (gridData[x, y].associatedChunk == chunk)
                    {
                        return gridData[x, y];
                    }
                }
                x++;
            }

            // Shift down
            for (int i = 0; i < step; i++)
            {
                if (x >= 0 && x < xSize && y >= 0 && y < ySize)
                {
                    if (gridData[x, y].associatedChunk == chunk)
                    {
                        return gridData[x, y];
                    }
                }
                y--;
            }

            // Shift left
            for (int i = 0; i < step + 1; i++)
            {
                if (x >= 0 && x < xSize && y >= 0 && y < ySize)
                {
                    if (gridData[x, y].associatedChunk == chunk)
                    {
                        return gridData[x, y];
                    }
                }
                x--;
            }

            // Shift up
            for (int i = 0; i < step + 1; i++)
            {
                if (x >= 0 && x < xSize && y >= 0 && y < ySize)
                {
                    if (gridData[x, y].associatedChunk == chunk)
                    {
                        return gridData[x, y];
                    }
                }
                y++;
            }
        }
        return null;
    }
    public int CompareTo(MapChunk other)
    {
        int fCostComparison = FCost.CompareTo(other.FCost);
        if (fCostComparison != 0)
        {
            return fCostComparison;
        }
        else
        {
            int hCostComparison = HCost.CompareTo(other.HCost);
            return hCostComparison;
        }
    }

    public static bool MapChunkAstar(MapChunk startChunk, MapChunk endChunk)
    {
        endChunk.parent = null;
        if (startChunk == null)
        {
            return false;
        }
        int maxChunkSize = Grid.MaxChunkSize;
        Heap<MapChunk> openSet = new Heap<MapChunk>(256 * 256 / maxChunkSize);
        HashSet<MapChunk> closedSet = new HashSet<MapChunk>();

        openSet.Add(startChunk);
        startChunk.GCost = 0;
        startChunk.parent = null;

        while (openSet.Count > 0)
        {
            MapChunk currentChunk = openSet.RemoveFirst();
            closedSet.Add(currentChunk);

            var neighbours = currentChunk.neighbours;
            foreach (MapChunk neighbour in neighbours)
            {
                if (closedSet.Contains(neighbour))
                {
                    continue;
                }                 
                else if (!openSet.Contains(neighbour))
                {
                    neighbour.GCost = int.MaxValue;
                    neighbour.HCost = GetDistance(neighbour, endChunk);
                    neighbour.parent = currentChunk;
                }

                int newGCost = currentChunk.GCost + maxChunkSize;
                if (newGCost < neighbour.GCost)
                {
                    neighbour.GCost = newGCost;
                    neighbour.parent = currentChunk;

                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                    }
                    else
                    {
                        openSet.UpdateItem(neighbour);
                    }
                }
            }
        }
        return endChunk.parent != null;
    }
    public static bool PathExists(MapChunk startChunk, MapChunk endChunk)
    {
        if (startChunk == null || endChunk == null)
        {
            return false;
        }
        else if (startChunk == endChunk)
        {
            return true;
        }

        Heap<MapChunk> openSet = new Heap<MapChunk>(256 * 256 / Grid.MaxChunkSize);
        HashSet<MapChunk> closedSet = new HashSet<MapChunk>();

        openSet.Add(startChunk);

        while (openSet.Count > 0)
        {
            MapChunk currentChunk = openSet.RemoveFirst();
            closedSet.Add(currentChunk);

            if (currentChunk == endChunk)
            {
                return true;
            }

            var neighbours = currentChunk.neighbours;
            foreach (MapChunk neighbour in neighbours)
            {
                if (closedSet.Contains(neighbour))
                {
                    continue;
                }
                else if (!openSet.Contains(neighbour))
                {
                    neighbour.GCost = 0;
                    neighbour.HCost = GetDistance(neighbour, endChunk);
                    openSet.Add(neighbour);
                }
            }
        }
        return false;
    }
    private static int GetDistance(MapChunk nodeA, MapChunk nodeB)
    {
        (int x, int y) movementCosts = Pathfinding.DefaultMovementCosts;
        int distanceX = Mathf.Abs(nodeA.Centroid.x - nodeB.Centroid.x);
        int distanceY = Mathf.Abs(nodeA.Centroid.y - nodeB.Centroid.y);
        if (distanceX < distanceY)
        {
            return movementCosts.x * distanceY + (movementCosts.y - movementCosts.x) * distanceX;
        }
        else
        {
            return movementCosts.x * distanceX + (movementCosts.y - movementCosts.x) * distanceY;
        }
    }    
}
