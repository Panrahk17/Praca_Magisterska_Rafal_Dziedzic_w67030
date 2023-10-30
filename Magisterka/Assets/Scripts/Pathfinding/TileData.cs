using System;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class TileData : IHeapItem<TileData>
{
    //IHeapItem
    public int HeapIndex { get; set; }
    
    //CACHED SINGLETONS
    Grid grid;

    //COORDINATES
    public int x;
    public int y;
    
    //STATE
    List<TileData> neighbours = new List<TileData>();
    List<TileData> filteredNeighbours = new List<TileData>();
    int tileTypeID = 0;
    public int TileType { get => tileTypeID; set => tileTypeID = value; }

    
    //PATHFINDING A*
    public TileData parent;
    public int gCost;
    public int hCost;
    public int fCost { get => gCost + hCost; }

    //HPA*
    public bool Chunked = false;
    public MapChunk associatedChunk = null;

    //PATHFINDING HEAT
    public int heat = 0;
    public int inheritedCostReduction;

    public TileData(int x, int y)
    {
        this.x = x;
        this.y = y;
        grid = Grid.Instance;
    }
    public int CompareTo(TileData other)
    {
        int fCostComparison = fCost.CompareTo(other.fCost);
        if (fCostComparison != 0)
        {
            return fCostComparison;
        }
        else
        {
            int hCostComparison = hCost.CompareTo(other.hCost);
            return hCostComparison;
        }
    }

    public void FindNeighbours()
    {
        neighbours.Clear();
        filteredNeighbours.Clear();
        //TOP row, from LEFT to RIGHT
        for (int i = -1; i < 2; i++)
        {
            if (x + i >= 0 && x + i < grid.XSize && y + 1 < grid.YSize)
            {
                neighbours.Add(grid.gridData[x + i, y + 1]);
                filteredNeighbours.Add(grid.gridData[x + i, y + 1]);
            }
            else neighbours.Add(null);
        }
        //MIDDLE RIGHT
        if (x + 1 > 0 && x + 1 < grid.XSize)
        {
            neighbours.Add(grid.gridData[x + 1, y]);
            filteredNeighbours.Add(grid.gridData[x + 1, y]);
        }
        else neighbours.Add(null);
        //BOT row, from RIGHT to LEFT
        for (int i = 1; i >-2; i--)
        {
            if (x + i >= 0 && x + i < grid.XSize && y - 1 >= 0)
            {
                neighbours.Add(grid.gridData[x + i, y - 1]);
                filteredNeighbours.Add(grid.gridData[x + i, y - 1]);
            }
            else neighbours.Add(null);
        }
        //MIDDLE LEFT
        if (x - 1 >= 0 && x - 1 < grid.XSize)
        {
            neighbours.Add(grid.gridData[x - 1, y]);
            filteredNeighbours.Add(grid.gridData[x - 1, y]);
        }
        else neighbours.Add(null);
    }
    public List<TileData> GetNeighbours()
    {
        return new List<TileData>(filteredNeighbours);    
    }
    public TileData GetNeighbour(int id)//clockwise 0-7, beginning in top-left corner, may return null
    {
        return neighbours[id];
    }
    public Vector2Int GetMoveCost()
    {
        Vector2Int result = new Vector2Int(Pathfinding.DefaultMovementCosts.x, Pathfinding.DefaultMovementCosts.y);
        if (TileType == 0)//water
        {
            result = new Vector2Int(-1, -1);
        }
        else if (TileType == 1)//wetland
        {
            if (grid.HighWaterLevel)//wetland flooded with water
            {
                result = new Vector2Int(-1, -1);
            }
            else result *= 2;
        }
        else if (TileType == 3)//forest
        {
            result *= 2;
        }
        else if (TileType == 4)//rocky terrain
        {
            result *= 3;
        }
        else if (TileType == 5)//mountain peek
        {
            result *= 4;
        }
        return result;
    }
    public bool IsWalkable()
    {
        return tileTypeID != 0 && !(tileTypeID == 1 && grid.HighWaterLevel);
    }
    public void ResetData()
    {
        tileTypeID = 0;
        heat = 0;
        Chunked = false;
        associatedChunk = null;
    }
}