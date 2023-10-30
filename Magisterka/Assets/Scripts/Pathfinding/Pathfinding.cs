using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEngine.RuleTile.TilingRuleOutput;

public static class Pathfinding
{
    public static (int x, int y) DefaultMovementCosts = (10, 14);

    private static List<TileData> RetracePath(TileData startNode, TileData endNode)
    {
        List<TileData> path = new List<TileData>();
        TileData currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Add(startNode);

        path.Reverse();
        return path;
    }
    public static int GetDistance(TileData nodeA, TileData nodeB)
    {
        int distanceX = Mathf.Abs(nodeA.x - nodeB.x);
        int distanceY = Mathf.Abs(nodeA.y - nodeB.y);
        if (distanceX < distanceY)
        {
            return DefaultMovementCosts.x * distanceY + (DefaultMovementCosts.y - DefaultMovementCosts.x) * distanceX;
        }
        else
        {
            return DefaultMovementCosts.x * distanceX + (DefaultMovementCosts.y - DefaultMovementCosts.x) * distanceY;
        }
    }

    #region A*
    public static List<TileData> FindPathList(TileData startNode, TileData endNode, ref List<TileData> visitedNodes, ref long executionTime)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        List<TileData> result = new List<TileData>();
        List<TileData> openSet = new List<TileData>();
        HashSet<TileData> closedSet = new HashSet<TileData>();
        openSet.Add(startNode);
        startNode.gCost = 0;

        while (openSet.Count > 0)
        {
            if (openSet.Count > 1)
            { 
                openSet.Sort((TileData n1, TileData n2) => n1.CompareTo(n2));
            }
            TileData currentNode = openSet[0];
            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == endNode)
            {
                result = RetracePath(startNode, endNode);
                stopwatch.Stop();
                executionTime += stopwatch.ElapsedMilliseconds;
                visitedNodes = new List<TileData>(closedSet);
                return result;
            }

            List<TileData> neighbours = currentNode.GetNeighbours();
            for (int i = 0; i < neighbours.Count; i++)
            {
                TileData neighbour = neighbours[i];
                if (!neighbour.IsWalkable() || closedSet.Contains(neighbour))
                {
                    continue;
                }
                if (!closedSet.Contains(neighbour) && !openSet.Contains(neighbour))
                {
                    neighbour.gCost = int.MaxValue;
                    neighbour.hCost = GetDistance(neighbour, endNode);
                    neighbour.parent = currentNode;
                }

                int newGCost = currentNode.gCost;
                newGCost +=  (currentNode.x != neighbour.x && currentNode.y != neighbour.y) ? neighbour.GetMoveCost().y : neighbour.GetMoveCost().x;
                if (newGCost < neighbour.gCost)
                {
                    neighbour.gCost = newGCost;
                    neighbour.parent = currentNode;

                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                    }
                }
            }
        }

        stopwatch.Stop();
        executionTime += stopwatch.ElapsedMilliseconds;
        visitedNodes = new List<TileData>(closedSet);
        return result;
    }
    public static List<TileData> FindPathHeap(TileData startNode, TileData endNode, ref List<TileData> visitedNodes, ref long executionTime)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        List<TileData> result = new List<TileData>();
        Heap<TileData> openSet = new Heap<TileData>(256 * 256);
        HashSet<TileData> closedSet = new HashSet<TileData>();

        openSet.Add(startNode);
        startNode.gCost = 0;

        while (openSet.Count > 0)
        {
            TileData currentNode = openSet.RemoveFirst();
            closedSet.Add(currentNode);

            if (currentNode == endNode)
            {
                result = RetracePath(startNode, endNode);
                stopwatch.Stop();
                executionTime += stopwatch.ElapsedMilliseconds;
                visitedNodes = new List<TileData>(closedSet);
                return result;
            }

            List<TileData> neighbours = currentNode.GetNeighbours();
            for (int i = 0; i < neighbours.Count; i++)
            {
                TileData neighbour = neighbours[i];
                if (!neighbour.IsWalkable() || closedSet.Contains(neighbour))
                {
                    continue;
                }
                if (!closedSet.Contains(neighbour) && !openSet.Contains(neighbour))
                {
                    neighbour.gCost = int.MaxValue;
                    neighbour.hCost = GetDistance(neighbour, endNode);
                    neighbour.parent = currentNode;
                }
                int newGCost = currentNode.gCost;
                newGCost += (currentNode.x != neighbour.x && currentNode.y != neighbour.y) ? neighbour.GetMoveCost().y : neighbour.GetMoveCost().x;
                if (newGCost < neighbour.gCost)
                {
                    neighbour.gCost = newGCost;
                    neighbour.parent = currentNode;

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

        stopwatch.Stop();
        executionTime += stopwatch.ElapsedMilliseconds;
        visitedNodes = new List<TileData>(closedSet);
        return result;
    }
    public static List<TileData> FindPathHeapInflatedHeuristic(TileData startNode, TileData endNode, ref List<TileData> visitedNodes, ref long executionTime)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        List<TileData> result = new List<TileData>();
        Heap<TileData> openSet = new Heap<TileData>(256 * 256);
        HashSet<TileData> closedSet = new HashSet<TileData>();

        openSet.Add(startNode);
        startNode.gCost = 0;

        while (openSet.Count > 0)
        {
            TileData currentNode = openSet.RemoveFirst();
            closedSet.Add(currentNode);

            if (currentNode == endNode)
            {
                result = RetracePath(startNode, endNode);
                stopwatch.Stop();
                executionTime += stopwatch.ElapsedMilliseconds;
                visitedNodes = new List<TileData>(closedSet);
                return result;
            }

            List<TileData> neighbours = currentNode.GetNeighbours();
            for (int i = 0; i < neighbours.Count; i++)
            {
                TileData neighbour = neighbours[i];
                if (!neighbour.IsWalkable() || closedSet.Contains(neighbour))
                {
                    continue;
                }
                if (!closedSet.Contains(neighbour) && !openSet.Contains(neighbour))
                {
                    neighbour.gCost = int.MaxValue;
                    neighbour.hCost = GetDistance(neighbour, endNode) * 3/2;
                    neighbour.parent = currentNode;
                }
                int newGCost = currentNode.gCost;
                newGCost += (currentNode.x != neighbour.x && currentNode.y != neighbour.y) ? neighbour.GetMoveCost().y : neighbour.GetMoveCost().x;
                if (newGCost < neighbour.gCost)
                {
                    neighbour.gCost = newGCost;
                    neighbour.parent = currentNode;

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

        stopwatch.Stop();
        executionTime += stopwatch.ElapsedMilliseconds;
        visitedNodes = new List<TileData>(closedSet);
        return result;
    }
    #endregion

    #region HPA*
    public static List<TileData> FindPathWithChunks(TileData startNode, TileData endNode, ref List<TileData> visitedNodes, ref long executionTime)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        List<TileData> result = new List<TileData>();
        Heap<TileData> openSet = new Heap<TileData>(256 * 256);
        HashSet<TileData> closedSet = new HashSet<TileData>();
        openSet.Add(startNode);
        startNode.gCost = 0;


        while (openSet.Count > 0)
        {
            TileData currentNode = openSet.RemoveFirst();
            closedSet.Add(currentNode);

            if (currentNode == endNode)
            {
                result = RetracePath(startNode, endNode);
                stopwatch.Stop();
                executionTime += stopwatch.ElapsedMilliseconds;
                visitedNodes = new List<TileData>(closedSet);
                return result;
            }
            List<TileData> neighbours = currentNode.GetNeighbours();
            for (int i = 0; i < neighbours.Count; i++)
            {
                TileData neighbour = neighbours[i];
                if (!neighbour.IsWalkable() || closedSet.Contains(neighbour))
                {
                    continue;
                }
                if (!closedSet.Contains(neighbour) && !openSet.Contains(neighbour))
                {
                    neighbour.gCost = int.MaxValue;
                    neighbour.hCost = GetDistance(neighbour, endNode) + neighbour.associatedChunk.GCost;
                    neighbour.parent = currentNode;
                }

                int newGCost = currentNode.gCost;
                newGCost += (currentNode.x != neighbour.x && currentNode.y != neighbour.y) ? neighbour.GetMoveCost().y : neighbour.GetMoveCost().x;
                if (newGCost < neighbour.gCost)
                {
                    neighbour.gCost = newGCost;
                    neighbour.parent = currentNode;

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

        stopwatch.Stop();
        executionTime += stopwatch.ElapsedMilliseconds;
        visitedNodes = new List<TileData>(closedSet);
        return result;
    }    
    #endregion

    #region HEATMAP
    public static List<TileData> FindPathWithHeat(TileData startNode, TileData endNode, ref List<TileData> visitedNodes, ref long executionTime)
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        List<TileData> result = new List<TileData>();
        Heap<TileData> openSet = new Heap<TileData>(256 * 256);
        HashSet<TileData> closedSet = new HashSet<TileData>();

        openSet.Add(startNode);
        startNode.gCost = 0;

        while (openSet.Count > 0)
        {
            TileData currentNode = openSet.RemoveFirst();
            closedSet.Add(currentNode);

            if (currentNode == endNode)
            {
                result = RetracePath(startNode, endNode);
                IncreaseHeat(result);
                stopwatch.Stop();
                executionTime += stopwatch.ElapsedMilliseconds;
                visitedNodes = new List<TileData>(closedSet);
                return result;
            }

            List<TileData> neighbours = currentNode.GetNeighbours();
            for (int i = 0; i < neighbours.Count; i++)
            {
                TileData neighbour = neighbours[i];
                if (!neighbour.IsWalkable() || closedSet.Contains(neighbour))
                {
                    continue;
                }
                if (!closedSet.Contains(neighbour) && !openSet.Contains(neighbour))
                {
                    neighbour.gCost = int.MaxValue;
                    neighbour.hCost = GetDistance(neighbour, endNode);
                    neighbour.parent = currentNode;
                    neighbour.inheritedCostReduction = 0;
                }

                int newGCost = currentNode.gCost;
                int entryCost = (currentNode.x != neighbour.x && currentNode.y != neighbour.y) ? neighbour.GetMoveCost().y : neighbour.GetMoveCost().x;
                newGCost += entryCost;

                int entryCostReduction = GetHeatCostReduction(entryCost, neighbour.heat);
                int newInheritedCostReduction = entryCostReduction + currentNode.inheritedCostReduction;

                if (newGCost - newInheritedCostReduction < neighbour.gCost - neighbour.inheritedCostReduction)
                {
                    neighbour.gCost = newGCost;
                    neighbour.parent = currentNode;
                    neighbour.hCost += neighbour.inheritedCostReduction - (newInheritedCostReduction);
                    neighbour.inheritedCostReduction = newInheritedCostReduction;

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

        stopwatch.Stop();
        executionTime += stopwatch.ElapsedMilliseconds;
        visitedNodes = new List<TileData>(closedSet);
        return result;
    }
    public static void IncreaseHeat(List<TileData> tilesToIncreaseHeat)
    {
        if (tilesToIncreaseHeat != null && tilesToIncreaseHeat.Count > 0)
        { 
            for (int i = 0; i < tilesToIncreaseHeat.Count; i++)
            {
                TileData tile = tilesToIncreaseHeat[i];
                tile.heat++;
            }
        }
    }
    public static void DecreaseHeat(List<TileData> tilesToDecreaseHeat)
    {
        if (tilesToDecreaseHeat != null && tilesToDecreaseHeat.Count > 0)
        {
            for (int i = 0; i < tilesToDecreaseHeat.Count; i++)
            {
                TileData tile = tilesToDecreaseHeat[i];
                if (tile.heat > 0)
                { 
                    tile.heat--;            
                }
            }
        }
    }
    static int GetHeatCostReduction(int entryCost, int heat)
    {
        int reductionPerHeatPoint = entryCost % DefaultMovementCosts.x == 0 ? DefaultMovementCosts.x : DefaultMovementCosts.y;
        int maxHeatValue = entryCost / reductionPerHeatPoint - 1;
        int result = reductionPerHeatPoint * Mathf.Clamp(heat, 0, maxHeatValue);
        return result;
    }
    #endregion
}