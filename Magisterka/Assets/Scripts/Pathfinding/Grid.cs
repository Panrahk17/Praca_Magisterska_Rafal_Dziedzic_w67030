using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

public class Grid : MonoBehaviour
{
    private int xSize;
    private int ySize;
    public TileData[,] gridData;

    public int XSize { get => xSize; }
    public int YSize { get => ySize; }

    //FRACTAL NOISE
    [Header("Settings for Fractal noise")]
    [SerializeField] bool previewNoise = false;
    [Range(1, 256)][SerializeField] int terrainLayers = 6;
    [SerializeField] int xShift = 0;
    [SerializeField] int yShift = 0;
    [SerializeField] float Frequency = 0.05f;
    [SerializeField] int Octaves = 1;
    [SerializeField] float Lacunarity = 2f;
    [SerializeField] float Persistence = 0.5f;
    [Range(0, 1)][SerializeField] float minTreshold = 0f;
    [Range(0, 1)][SerializeField] float maxTreshold = 1f;
    [Range(0, 1)][SerializeField] float Amplitude = 0.6f;
    [Header("Optional settings for Fractal noise")]
    [SerializeField] bool normalizeValuesRange = true;
    [SerializeField] bool floodValuesBelowMinTreshold = false;

    public static Grid Instance = null;
    bool highWaterLevel = false;
    public bool HighWaterLevel { get => highWaterLevel; }

    //display
    Display display;
    public Color[] terrainColors = new Color[6];
    List<TileData> pathfindingClosedNodes = new List<TileData>();
    Coroutine closedNodesCoroutine = null;

    //pathfinding related
    List<TileData> path = new List<TileData>();
    Vector2Int startPoint = new Vector2Int(-1, -1);
    Vector2Int endPoint = new Vector2Int(-1, -1);
    private Stopwatch stopwatch;

    //chunk division related
    List<MapChunk> mapChunks = new List<MapChunk>();
    private bool chunksDirty = false;
    public static int MaxChunkSize;

    //tests
    int currentMethod = 1;
    int iterations = 649;
    
    long aStarTime;
    int aStarCost;

    long inflatedAStarTime;
    int inflatedAStarCost;
    ExtremeResults inflatedWorstCost = new ExtremeResults();
    ExtremeResults inflatedBestCost = new ExtremeResults();
    ExtremeResults inflatedWorstTime = new ExtremeResults();
    ExtremeResults inflatedBestTime = new ExtremeResults();

    long chunkTime;
    int chunkCost;
    ExtremeResults chunkWorstCost = new ExtremeResults();
    ExtremeResults chunkBestCost = new ExtremeResults();
    ExtremeResults chunkWorstTime = new ExtremeResults();
    ExtremeResults chunkBestTime = new ExtremeResults();

    long heatTime;
    int heatCost;
    ExtremeResults heatWorstCost = new ExtremeResults();
    ExtremeResults heatBestCost = new ExtremeResults();
    ExtremeResults heatWorstTime = new ExtremeResults();
    ExtremeResults heatBestTime = new ExtremeResults();

    long protectedAStarTime;
    int protectedAStarCost;
    ExtremeResults protectedWorstCost = new ExtremeResults();
    ExtremeResults protectedBestCost = new ExtremeResults();
    ExtremeResults protectedWorstTime = new ExtremeResults();
    ExtremeResults protectedBestTime = new ExtremeResults();

    int mapChunkingAttempts;
    long mapChunkingTime;

    int desirePathsAttempts;
    long desirePathsTime;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            MaxChunkSize = Pathfinding.DefaultMovementCosts.x * 30;
        }
        else Destroy(gameObject);
    }
    private void Start()
    {
        //create 256x256 TileData grid
        CreateGrid(256, 256);
        display = Display.Instance;
        display.Initialize(256,256);

        //generate data for the grid
        GenerateGridData();
        DivideMapIntoChunks();
    }
    private void Update()
    {
        if (Input.GetKey(KeyCode.X))//randomly modify map 
        {
            RandomlyChangeTiles();
            DivideMapIntoChunks();
            UpdateDesirePaths();
            UpdateHeatMapDisplayer();
        }
        if (Input.GetKey(KeyCode.C))//reset displayer 
        {
            startPoint = new Vector2Int(-1, -1);
            endPoint = new Vector2Int(-1, -1);
            ResetMainDisplay();
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            Amplitude -= 0.01f;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            Amplitude += 0.01f;
        }
        if (previewNoise)
        {
            chunksDirty = true;
            GenerateGridData();
        }
        else if(chunksDirty)
        {
            DivideMapIntoChunks();
            chunksDirty = false;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            currentMethod = 1;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            currentMethod = 2;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            currentMethod = 3;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            currentMethod = 4;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            currentMethod = 5;
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            ChangeWaterLevel();
        }

        if (Input.GetMouseButtonDown(0))
        {
            SetStartPosition();            
        }
        else if (Input.GetMouseButtonDown(1))
        {
            SetTargetPosition();
        }

        if (Input.GetKeyDown(KeyCode.Space) && Input.GetKey(KeyCode.LeftShift))
        {
            if (startPoint.x == -1)
            {
                startPoint = new Vector2Int(0, 0);
            }

            bool changeTerrain = true;//false;
            aStarTime = 0;
            aStarCost = 0; ;

            inflatedAStarTime = 0;
            inflatedAStarCost = 0;
            inflatedWorstCost = new ExtremeResults(0, 0, 0, 0, -1, -1);
            inflatedBestCost = new ExtremeResults(0, 0, 0, 0, -1, -1);
            inflatedWorstTime = new ExtremeResults(0, 0, 0, 0, -1, -1);
            inflatedBestTime = new ExtremeResults(0, 0, 0, 0, -1, -1);

            chunkTime = 0;
            chunkCost = 0;
            chunkWorstCost = new ExtremeResults(0, 0, 0, 0, -1, -1);
            chunkBestCost = new ExtremeResults(0, 0, 0, 0, -1, -1);
            chunkWorstTime = new ExtremeResults(0, 0, 0, 0, -1, -1);
            chunkBestTime = new ExtremeResults(0, 0, 0, 0, -1, -1);

            heatTime = 0;
            heatCost = 0;
            heatWorstCost = new ExtremeResults(0, 0, 0, 0, -1, -1);
            heatBestCost = new ExtremeResults(0, 0, 0, 0, -1, -1);
            heatWorstTime = new ExtremeResults(0, 0, 0, 0, -1, -1);
            heatBestTime = new ExtremeResults(0, 0, 0, 0, -1, -1);

            protectedAStarTime = 0;
            protectedAStarCost = 0;
            protectedWorstCost = new ExtremeResults(0, 0, 0, 0, -1, -1);
            protectedBestCost = new ExtremeResults(0, 0, 0, 0, -1, -1);
            protectedWorstTime = new ExtremeResults(0, 0, 0, 0, -1, -1);
            protectedBestTime = new ExtremeResults(0, 0, 0, 0, -1, -1);

            mapChunkingAttempts = 0;
            mapChunkingTime = 0;

            desirePathsAttempts = 0;
            desirePathsTime = 0;
            //

            bool randomizeEndPoint = endPoint.x < 0;
            if (endPoint.x == -1)
            {
                endPoint = new Vector2Int(XSize - 1, YSize - 1);
            }
            for (int i = 0; i < iterations; i++)
            {
                if (changeTerrain)
                { 
                    if (i % 100 == 0)//changing tile types
                    {
                        RandomlyChangeTiles();
                        DivideMapIntoChunks();
                        UpdateDesirePaths();
                        UpdateHeatMapDisplayer();
                    }
                    else if (i % 50 == 0)//changing water lvl
                    {
                        ChangeWaterLevel();
                        UpdateDesirePaths();
                        UpdateHeatMapDisplayer();
                    }
                }

                if (randomizeEndPoint)
                {
                    do
                    {
                        startPoint = new Vector2Int(Random.Range(0, XSize), Random.Range(0, YSize));
                        endPoint = new Vector2Int(Random.Range(0, XSize), Random.Range(0, YSize));
                    }
                    while (startPoint == endPoint || gridData[startPoint.x, startPoint.y].GetMoveCost().x == -1 || gridData[endPoint.x, endPoint.y].GetMoveCost().x == -1);
                }
                else
                {
                    do
                    {
                        startPoint = new Vector2Int(Random.Range(0, XSize), Random.Range(0, YSize));
                    }
                    while (startPoint == endPoint || gridData[startPoint.x, startPoint.y].GetMoveCost().x == -1);
                }

                int currentAStarGCost=0;
                long currentAStarTime=0;
                for (int j = 1; j < 6; j++)
                {
                    currentMethod = j;

                    path = new List<TileData>();
                    pathfindingClosedNodes = new List<TileData>();
                    gridData[endPoint.x, endPoint.y].gCost = 0;
                    long executionTime = 0;

                    if (currentMethod == 1)//A*
                    {
                        path = Pathfinding.FindPathHeap(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                        aStarCost += gridData[endPoint.x, endPoint.y].gCost;
                        aStarTime += executionTime;

                        currentAStarGCost = gridData[endPoint.x, endPoint.y].gCost;
                        currentAStarTime = executionTime;

                    }
                    else if (currentMethod == 2)//A* + CHUNKS
                    {
                        stopwatch = Stopwatch.StartNew();
                        bool pathExists = MapChunk.MapChunkAstar(gridData[endPoint.x, endPoint.y].associatedChunk, gridData[startPoint.x, startPoint.y].associatedChunk);
                        if (pathExists)
                        {
                            stopwatch.Stop();
                            executionTime += stopwatch.ElapsedMilliseconds;
                            path = Pathfinding.FindPathWithChunks(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                            chunkCost += gridData[endPoint.x, endPoint.y].gCost;
                            chunkTime += executionTime;
                        }
                        else
                        { 
                            stopwatch.Stop();
                            executionTime += stopwatch.ElapsedMilliseconds;
                            path = new List<TileData>();
                            chunkCost += gridData[endPoint.x, endPoint.y].gCost;
                            chunkTime += executionTime;
                        }
                        CheckForExtremeData(path, executionTime, currentAStarGCost, currentAStarTime, ref chunkWorstCost, ref chunkBestCost, ref chunkWorstTime, ref chunkBestTime);
                    }
                    else if (currentMethod == 3)//A* + HEAT
                    {
                        path = Pathfinding.FindPathWithHeat(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                        heatCost += gridData[endPoint.x, endPoint.y].gCost;
                        heatTime += executionTime;
                        CheckForExtremeData(path,executionTime,currentAStarGCost,currentAStarTime, ref heatWorstCost, ref heatBestCost, ref heatWorstTime, ref heatBestTime);
                    }
                    else if (currentMethod == 4)//INFLATED A*
                    {
                        path = Pathfinding.FindPathHeapInflatedHeuristic(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                        inflatedAStarCost += gridData[endPoint.x, endPoint.y].gCost;
                        inflatedAStarTime += executionTime;
                        CheckForExtremeData(path, executionTime, currentAStarGCost, currentAStarTime, ref inflatedWorstCost, ref inflatedBestCost, ref inflatedWorstTime, ref inflatedBestTime);
                    }
                    else if (currentMethod == 5)//PROTECTED A*
                    {
                        stopwatch = Stopwatch.StartNew();
                        bool pathExists = MapChunk.PathExists(gridData[endPoint.x, endPoint.y].associatedChunk, gridData[startPoint.x, startPoint.y].associatedChunk);
                        if (pathExists)
                        {
                            stopwatch.Stop();
                            executionTime += stopwatch.ElapsedMilliseconds;
                            path = Pathfinding.FindPathHeap(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                            protectedAStarCost += gridData[endPoint.x, endPoint.y].gCost;
                            protectedAStarTime += executionTime;
                        }
                        else
                        {
                            stopwatch.Stop();
                            executionTime += stopwatch.ElapsedMilliseconds;
                            path = new List<TileData>();
                            protectedAStarCost += gridData[endPoint.x, endPoint.y].gCost;
                            protectedAStarTime += executionTime;
                        }
                        CheckForExtremeData(path, executionTime, currentAStarGCost, currentAStarTime, ref protectedWorstCost, ref protectedBestCost, ref protectedWorstTime, ref protectedBestTime);
                    }
                }
            }

            UnityEngine.Debug.Log($"RESULTS");
            UnityEngine.Debug.Log($"A* CZAS:[{aStarTime / iterations / 1000}s {aStarTime / iterations % 1000}ms] KOSZT[{aStarCost / iterations}]");

            ExtremeResults worstC = chunkWorstCost; ExtremeResults bestC = chunkBestCost; ExtremeResults worstT = chunkWorstTime; ExtremeResults bestT = chunkBestTime;
            UnityEngine.Debug.Log($"CHUNKS CZAS:[{chunkTime / iterations / 1000}s {chunkTime / iterations % 1000}ms] KOSZT[{chunkCost / iterations}]" +
                $"\nWORST COST:({worstC.cost}, {worstC.GetTimeAsString()}) vs ({worstC.aCost}, {worstC.GetAStarTimeAsString()}) -> [C: {worstC.cRatio*100}%, T: {worstC.tRatio*100}%]" +
                $"\nBEST COST:({bestC.cost}, {bestC.GetTimeAsString()}) vs ({bestC.aCost}, {bestC.GetAStarTimeAsString()}) -> [C: {bestC.cRatio * 100}%, T: {bestC.tRatio * 100}%]" +
                $"\nWORST TIME:({worstT.cost}, {worstT.GetTimeAsString()}) vs ({worstT.aCost}, {worstT.GetAStarTimeAsString()}) -> [C: {worstT.cRatio * 100}%, T: {worstT.tRatio * 100}%]" +
                $"\nBEST TIME:({bestT.cost}, {bestT.GetTimeAsString()}) vs ({bestT.aCost}, {bestT.GetAStarTimeAsString()}) -> [C: {bestT.cRatio * 100}%, T: {bestT.tRatio * 100}%]");

            worstC = heatWorstCost; bestC = heatBestCost; worstT = heatWorstTime; bestT = heatBestTime;
            UnityEngine.Debug.Log($"HEAT CZAS:[{heatTime / iterations / 1000}s {heatTime / iterations % 1000}ms] KOSZT[{heatCost / iterations}]" +
                $"\nWORST COST:({worstC.cost}, {worstC.GetTimeAsString()}) vs ({worstC.aCost}, {worstC.GetAStarTimeAsString()}) -> [C: {worstC.cRatio * 100}%, T: {worstC.tRatio * 100}%]" +
                $"\nBEST COST:({bestC.cost}, {bestC.GetTimeAsString()}) vs ({bestC.aCost}, {bestC.GetAStarTimeAsString()}) -> [C: {bestC.cRatio * 100}%, T: {bestC.tRatio * 100}%]" +
                $"\nWORST TIME:({worstT.cost}, {worstT.GetTimeAsString()}) vs ({worstT.aCost}, {worstT.GetAStarTimeAsString()}) -> [C: {worstT.cRatio * 100}%, T: {worstT.tRatio * 100}%]" +
                $"\nBEST TIME:({bestT.cost}, {bestT.GetTimeAsString()}) vs ({bestT.aCost}, {bestT.GetAStarTimeAsString()}) -> [C: {bestT.cRatio * 100}%, T: {bestT.tRatio * 100}%]");
            
            worstC = inflatedWorstCost; bestC = inflatedBestCost; worstT = inflatedWorstTime; bestT = inflatedBestTime;
            UnityEngine.Debug.Log($"INFLATED A* CZAS:[{inflatedAStarTime / iterations / 1000}s {inflatedAStarTime / iterations % 1000}ms] KOSZT[{inflatedAStarCost / iterations}]" +
                $"\nWORST COST:({worstC.cost}, {worstC.GetTimeAsString()}) vs ({worstC.aCost}, {worstC.GetAStarTimeAsString()}) -> [C: {worstC.cRatio * 100}%, T: {worstC.tRatio * 100}%]" +
                $"\nBEST COST:({bestC.cost}, {bestC.GetTimeAsString()}) vs ({bestC.aCost}, {bestC.GetAStarTimeAsString()}) -> [C: {bestC.cRatio * 100}%, T: {bestC.tRatio * 100}%]" +
                $"\nWORST TIME:({worstT.cost}, {worstT.GetTimeAsString()}) vs ({worstT.aCost}, {worstT.GetAStarTimeAsString()}) -> [C: {worstT.cRatio * 100}%, T: {worstT.tRatio * 100}%]" +
                $"\nBEST TIME:({bestT.cost}, {bestT.GetTimeAsString()}) vs ({bestT.aCost}, {bestT.GetAStarTimeAsString()}) -> [C: {bestT.cRatio * 100}%, T: {bestT.tRatio * 100}%]");
            
            worstC = protectedWorstCost; bestC = protectedBestCost; worstT = protectedWorstTime; bestT = protectedBestTime;
            UnityEngine.Debug.Log($"PROTECTED A* CZAS:[{protectedAStarTime / iterations / 1000}s {protectedAStarTime / iterations % 1000}ms] KOSZT[{protectedAStarCost / iterations}]" +
                $"\nWORST COST:({worstC.cost}, {worstC.GetTimeAsString()}) vs ({worstC.aCost}, {worstC.GetAStarTimeAsString()}) -> [C: {worstC.cRatio * 100}%, T: {worstC.tRatio * 100}%]" +
                $"\nBEST COST:({bestC.cost}, {bestC.GetTimeAsString()}) vs ({bestC.aCost}, {bestC.GetAStarTimeAsString()}) -> [C: {bestC.cRatio * 100}%, T: {bestC.tRatio * 100}%]" +
                $"\nWORST TIME:({worstT.cost}, {worstT.GetTimeAsString()}) vs ({worstT.aCost}, {worstT.GetAStarTimeAsString()}) -> [C: {worstT.cRatio * 100}%, T: {worstT.tRatio * 100}%]" +
                $"\nBEST TIME:({bestT.cost}, {bestT.GetTimeAsString()}) vs ({bestT.aCost}, {bestT.GetAStarTimeAsString()}) -> [C: {bestT.cRatio * 100}%, T: {bestT.tRatio * 100}%]");
            if (changeTerrain){
                UnityEngine.Debug.Log($"CHUNKING TOTAL TIME:[{mapChunkingTime / 1000}s {mapChunkingTime % 1000}ms] " +
                    $"CHUNKING AVERAGE TIME:[{mapChunkingTime / mapChunkingAttempts / 1000}s {mapChunkingTime / mapChunkingAttempts % 1000}ms]");
                UnityEngine.Debug.Log($"DESIRE PATHS TOTAL TIME:[{desirePathsTime / 1000}s {desirePathsTime % 1000}ms] " +
                    $"DESIRE PATHS AVERAGE TIME:[{desirePathsTime / desirePathsAttempts / 1000}s {desirePathsTime / desirePathsAttempts % 1000}ms]");}
            //HEAT MAP VISUALIZATION
            UpdateHeatMapDisplayer();
            if (closedNodesCoroutine != null)
            {
                StopCoroutine(closedNodesCoroutine);
            }
            closedNodesCoroutine = StartCoroutine(VisualizeLookingForPath());
        }
        //SINGLE PATH
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            if (startPoint.x == -1)
            {
                startPoint = new Vector2Int(0, 0);
            }
            if (endPoint.x == -1)
            {
                endPoint = new Vector2Int(XSize - 1, YSize - 1);
            }

            long executionTime = 0;
            path = new List<TileData>();
            pathfindingClosedNodes = new List<TileData>();
            gridData[endPoint.x, endPoint.y].gCost = 0;            

            if (currentMethod == 1)//A*
            {
                path = Pathfinding.FindPathHeap(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                UnityEngine.Debug.Log($"A* CZAS:[{executionTime / 1000}s {executionTime % 1000}ms] KOSZT[{gridData[endPoint.x, endPoint.y].gCost}]");
            }
            else if (currentMethod == 2)//A* + CHUNKS
            {
                stopwatch = Stopwatch.StartNew();
                bool pathExists = MapChunk.MapChunkAstar(gridData[endPoint.x, endPoint.y].associatedChunk, gridData[startPoint.x, startPoint.y].associatedChunk);
                if (pathExists)
                {
                    stopwatch.Stop();
                    executionTime += stopwatch.ElapsedMilliseconds;
                    path = Pathfinding.FindPathWithChunks(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                }
                else
                {
                    stopwatch.Stop();
                    executionTime += stopwatch.ElapsedMilliseconds;
                    path = new List<TileData>();                  
                }
                UnityEngine.Debug.Log($"A* + CHUNKS:[{executionTime / 1000}s {executionTime % 1000}ms] KOSZT[{gridData[endPoint.x, endPoint.y].gCost}]" +
                    $"\nCHUNKING AVERAGE TIME:[{mapChunkingTime / mapChunkingAttempts / 1000}s {mapChunkingTime / mapChunkingAttempts % 1000}ms]");
            }
            else if (currentMethod == 3)//A* + HEAT
            {
                path = Pathfinding.FindPathWithHeat(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                UnityEngine.Debug.Log($"A* + HEAT:[{executionTime / 1000}s {executionTime % 1000}ms] KOSZT[{gridData[endPoint.x, endPoint.y].gCost}]");
            }
            else if (currentMethod == 4)//INFLATED A*
            {
                path = Pathfinding.FindPathHeapInflatedHeuristic(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                UnityEngine.Debug.Log($"INFLATED A*:[{executionTime / 1000}s {executionTime % 1000}ms] KOSZT[{gridData[endPoint.x, endPoint.y].gCost}]");
            }
            else if (currentMethod == 5)//PROTECTED A*
            {
                stopwatch = Stopwatch.StartNew();
                bool pathExists = MapChunk.PathExists(gridData[endPoint.x, endPoint.y].associatedChunk, gridData[startPoint.x, startPoint.y].associatedChunk);
                if (pathExists)
                {
                    stopwatch.Stop();
                    executionTime += stopwatch.ElapsedMilliseconds;
                    path = Pathfinding.FindPathHeap(gridData[startPoint.x, startPoint.y], gridData[endPoint.x, endPoint.y], ref pathfindingClosedNodes, ref executionTime);
                }
                else
                {
                    stopwatch.Stop();
                    executionTime += stopwatch.ElapsedMilliseconds;
                    path = new List<TileData>();
                }
                UnityEngine.Debug.Log($"PROTECTED A*:[{executionTime / 1000}s {executionTime % 1000}ms] KOSZT[{gridData[endPoint.x, endPoint.y].gCost}]" +
                    $"\nCHUNKING AVERAGE TIME:[{mapChunkingTime / mapChunkingAttempts / 1000}s {mapChunkingTime / mapChunkingAttempts % 1000}ms]");
            }
            
            //HEAT MAP VISUALIZATION
            UpdateHeatMapDisplayer();
            if (closedNodesCoroutine != null)
            {
                StopCoroutine(closedNodesCoroutine);
            }
            closedNodesCoroutine = StartCoroutine(VisualizeLookingForPath());
        }

    }
    void CheckForExtremeData(List<TileData> path, long executionTime, int currentAStarGCost, long currentAStarTime, ref ExtremeResults worstCostData, ref ExtremeResults bestCostData, ref ExtremeResults worstTimeData, ref ExtremeResults bestTimeData)
    {
        //COST
        int pathCost = 0;
        double currentCostRatio = 0;
        if (path.Count > 0)
        {
            pathCost = path[path.Count - 1].gCost;
            currentCostRatio = (double)pathCost / currentAStarGCost;
        }
        //TIME
        double currentTimeRatio = 0;
        if (executionTime == 0 && currentAStarTime == 0)
        {
            currentTimeRatio = 1;
        }
        else if (executionTime == 0)
        {
            currentTimeRatio = -currentAStarTime;
        }
        else if (currentAStarTime == 0)
        {
            currentTimeRatio = executionTime;
        }
        else if (executionTime != 0 && currentAStarTime != 0)
        {
            currentTimeRatio = (double)executionTime / currentAStarTime;
        }

        //worst cost
        if (currentCostRatio > 0)
        {
            if (worstCostData.cRatio < 0 || worstCostData.cRatio < currentCostRatio || (worstCostData.cRatio == currentCostRatio && currentTimeRatio > worstCostData.tRatio))
            {
                worstCostData = new ExtremeResults(pathCost, executionTime, currentAStarGCost, currentAStarTime, currentCostRatio, currentTimeRatio);
            }
        }
        //best cost
        if (currentCostRatio > 0)
        {
            if (bestCostData.cRatio < 0 || bestCostData.cRatio > currentCostRatio || (bestCostData.cRatio == currentCostRatio && currentTimeRatio < bestCostData.tRatio))
            {
                bestCostData = new ExtremeResults(pathCost, executionTime, currentAStarGCost, currentAStarTime, currentCostRatio, currentTimeRatio);
            }
        }
        //worst time
        if (worstTimeData.cRatio < 0 || worstTimeData.tRatio < currentTimeRatio || (worstTimeData.tRatio == currentTimeRatio && currentCostRatio > worstTimeData.cRatio))
        {
            worstTimeData = new ExtremeResults(pathCost, executionTime, currentAStarGCost, currentAStarTime, currentCostRatio, currentTimeRatio);
        }
        //best time
        if (bestTimeData.cRatio < 0 || bestTimeData.tRatio > currentTimeRatio || (bestTimeData.tRatio == currentTimeRatio && currentCostRatio < bestTimeData.cRatio))
        {
            bestTimeData = new ExtremeResults(pathCost, executionTime, currentAStarGCost, currentAStarTime, currentCostRatio, currentTimeRatio);
        }
    }

    void SetStartPosition()
    {
        ResetMainDisplay();
        Vector3 pointerWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 plainWorldPos = display.GetDisplayWorldPosition();
        Vector3 meshSize = display.GetDisplaySize();

        //remove previous point
        if (startPoint.x != -1)
        {
            display.SetPixel(startPoint.x, startPoint.y, terrainColors[GetTileColorID(gridData[startPoint.x, startPoint.y])], DisplayType.Main);
        }

        if (pointerWorldPos.x >= plainWorldPos.x - meshSize.x / 2 && pointerWorldPos.x <= plainWorldPos.x + meshSize.x / 2 &&
            pointerWorldPos.y >= plainWorldPos.y - meshSize.z / 2 && pointerWorldPos.y <= plainWorldPos.y + meshSize.z / 2)//check if pointer is inside grid
        {
            //calculate start position
            int texturePixelXPos = (int)(XSize * (pointerWorldPos.x + meshSize.x / 2) / meshSize.x);
            int texturePixelYPos = (int)(YSize * (pointerWorldPos.y + meshSize.z / 2) / meshSize.z);

            startPoint.x = texturePixelXPos;
            startPoint.y = texturePixelYPos;
            display.SetPixel(startPoint.x, startPoint.y, Color.red, DisplayType.Main);
        }
        else
        {
            startPoint.x = -1;
            startPoint.y = -1;
        }

        if (endPoint.x != -1)
        {
            display.SetPixel(endPoint.x, endPoint.y, Color.red, DisplayType.Main);
        }
        display.ApplyDisplayChanges(DisplayType.Main);        
    }
    void SetTargetPosition()
    {
        ResetMainDisplay();
        Vector3 pointerWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 plainWorldPos = display.GetDisplayWorldPosition();
        Vector3 meshSize = display.GetDisplaySize();

        //remove previous point
        if (endPoint.x != -1)
        {
            display.SetPixel(endPoint.x, endPoint.y, terrainColors[GetTileColorID(gridData[endPoint.x, endPoint.y])], DisplayType.Main);
        }

        if (pointerWorldPos.x >= plainWorldPos.x - meshSize.x / 2 && pointerWorldPos.x <= plainWorldPos.x + meshSize.x / 2 &&
            pointerWorldPos.y >= plainWorldPos.y - meshSize.z / 2 && pointerWorldPos.y <= plainWorldPos.y + meshSize.z / 2)//check if pointer is inside grid
        {
            //calculate end position
            int texturePixelXPos = (int)(XSize * (pointerWorldPos.x + meshSize.x / 2) / meshSize.x);
            int texturePixelYPos = (int)(YSize * (pointerWorldPos.y + meshSize.z / 2) / meshSize.z);

            endPoint.x = texturePixelXPos;
            endPoint.y = texturePixelYPos;
            display.SetPixel(endPoint.x, endPoint.y, Color.red, DisplayType.Main);
        }
        else
        {
            endPoint.x = -1;
            endPoint.y = -1;
        }

        if (startPoint.x != -1)
        {
            display.SetPixel(startPoint.x, startPoint.y, Color.red, DisplayType.Main);
        }
        display.ApplyDisplayChanges(DisplayType.Main);
    }

    #region UI
    int GetTileColorID(TileData tileData)
    {
        int typeID = tileData.TileType;
        if (typeID == 1 && HighWaterLevel)
        {
            typeID = 0;
        }
        return typeID;
    }
    IEnumerator VisualizeLookingForPath()
    {
        ResetMainDisplay();
        List<TileData> nodes = new List<TileData>(pathfindingClosedNodes);
        int i = 0;
        while (i < nodes.Count)
        {
            Color explorationColor = display.GetPixel(nodes[i].x, nodes[i].y, DisplayType.Main);
            explorationColor.r = explorationColor.r * 0.3f + Color.white.r * 0.7f;
            explorationColor.g = explorationColor.g * 0.3f + Color.white.r * 0.7f;
            explorationColor.b = explorationColor.b * 0.3f + Color.white.r * 0.7f;
            display.SetPixel(nodes[i].x, nodes[i].y, explorationColor, DisplayType.Main);
            i++;
            if (i % 50 == 0)
            {
                display.ApplyDisplayChanges(DisplayType.Main);
                yield return new WaitForEndOfFrame();
            }
        }
        display.ApplyDisplayChanges(DisplayType.Main);

        for (int j = 0; j < path.Count; j++)
        {
            TileData tile = path[j];
            display.SetPixel(tile.x, tile.y, Color.red, DisplayType.Main);
            display.ApplyDisplayChanges(DisplayType.Main);
        }
        display.ApplyDisplayChanges(DisplayType.Main);
        closedNodesCoroutine = null;
    }
    void UpdateHeatMapDisplayer()
    {
        for (int i = 0; i < xSize; i++)
        {
            for (int j = 0; j < ySize; j++)
            {
                TileData tile = gridData[i, j];
                float colorValue = tile.heat * 32f / 256;
                display.SetPixel(tile.x, tile.y, new Color(colorValue, colorValue, colorValue), DisplayType.HeatMap);
            }
        }
        display.ApplyDisplayChanges(DisplayType.HeatMap);
    }
    #endregion

    private void ChangeWaterLevel()
    {
        highWaterLevel = !highWaterLevel;
        for (int i = 0; i < XSize; i++)
        {
            for (int j = 0; j < YSize; j++)
            {
                var tile = gridData[i, j];
                tile.Chunked = false;
                tile.associatedChunk = null;
            }
        }
        ResetMainDisplay();
        DivideMapIntoChunks();
    }

    private void GenerateGridData()
    {
        for (int i = 0; i < XSize; i++)
        {
            for (int j = 0; j < YSize; j++)
            {
                //DATA RESET
                gridData[i, j].ResetData();

                float fractalNoiseValue = FractalNoise(i, j);
                gridData[i, j].TileType = GetTileTypeFromFractal(fractalNoiseValue, terrainLayers, normalizeValuesRange);

                display.SetPixel(i, j, terrainColors[GetTileColorID(gridData[i, j])], DisplayType.Main);
                display.SetPixel(i, j, new Color(0, 0, 0), DisplayType.HeatMap);
            }
        }
        display.ApplyDisplayChanges(DisplayType.Main);
        display.ApplyDisplayChanges(DisplayType.HeatMap);
    }

    public void CreateGrid(int gridXSize = 256, int gridYSize = 256)
    {
        xSize = gridXSize;
        ySize = gridYSize;
        gridData = new TileData[XSize, YSize];

        for (int i = 0; i < XSize; i++)
        {
            for (int j = 0; j < YSize; j++)
            {
                TileData node = new TileData(i, j);
                gridData[i, j] = node;
            }
        }
        for (int i = 0; i < XSize; i++)
        {
            for (int j = 0; j < YSize; j++)
            {
                gridData[i, j].FindNeighbours();
            }
        }
    }

    float FractalNoise(int x, int y)
    {
        x += xShift;
        y += yShift;
        float elevation = 0;
        float t_frequency = Frequency;
        float t_amplitude = Amplitude;
        for (int k = 0; k < Octaves; k++)
        {
            float sample_x = x * t_frequency;
            float sample_y = y * t_frequency;
            float elevationChange = Mathf.Clamp(Mathf.PerlinNoise(sample_x, sample_y), 0, 1) * t_amplitude;
            elevation += elevationChange;
            t_frequency *= Lacunarity;
            t_amplitude *= Persistence;
        }
        if (normalizeValuesRange)
        {
            elevation = Mathf.Clamp(elevation, minTreshold, maxTreshold);
        }
        else
        {
            elevation = Mathf.Clamp(elevation, 0, 1);
        }
        return elevation;
    }
    int GetTileTypeFromFractal(float noiseValue, int possibleTileTypes, bool normalizeValues = true)
    {
        int result;
        if (normalizeValues)
        {
            noiseValue = (noiseValue - minTreshold) / Mathf.Clamp((maxTreshold - minTreshold), 0.00001f, maxTreshold);
        }
        //"flooding" low values
        if (floodValuesBelowMinTreshold && noiseValue < minTreshold)
        {
            result = 0;
            return result;
        }
        else
        {
            float tileType = noiseValue * possibleTileTypes;
            tileType = tileType >= possibleTileTypes ? possibleTileTypes - 1 : tileType;//Clamping ID
            result = Mathf.FloorToInt(tileType);
            return result;
        }
    }    
    
    void DivideMapIntoChunks()
    {
        //CLEAR DISPLAY
        for (int x = 0; x < XSize; x++)
        {
            for (int y = 0; y < YSize; y++)
            {
                display.SetPixel(x, y, Color.black, DisplayType.Chunks);
                gridData[x, y].Chunked = false;
            }
        }

        //CALCULATIONS
        mapChunkingAttempts++;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch = Stopwatch.StartNew();
        mapChunks.Clear();
        for (int x = 0; x < XSize; x++)
        {
            for (int y = 0; y < YSize; y++)
            {
                if (!gridData[x, y].Chunked)
                {
                    Flood(gridData[x, y], MaxChunkSize);
                }
            }
        }
        stopwatch.Stop();
        mapChunkingTime+=stopwatch.ElapsedMilliseconds;

        //CHUNKS VISUALIZATION        
        foreach (MapChunk chunk in mapChunks)
        {
            Color chunkColor;
            do
            {
                chunkColor = new Color(Random.Range(0f, 0.4f), Random.Range(0.4f, 1f), Random.Range(0f, 0.4f));
            }
            while (chunk.neighbours.Any(n => display.GetPixel(n.Centroid.x, n.Centroid.y, DisplayType.Chunks) == chunkColor));
            foreach (TileData tile in chunk.tilesInChunk)
            {
                display.SetPixel(tile.x, tile.y, chunkColor, DisplayType.Chunks);
            }
        }
        display.ApplyDisplayChanges(DisplayType.Chunks);
    }
    
    void Flood(TileData startTile, int maxCost)
    {       
        var moveCosts = startTile.GetMoveCost();
        if (!startTile.IsWalkable() || startTile.Chunked || moveCosts.x > maxCost)
        {            
            return;
        }
        Queue<TileData> openSet = new Queue<TileData>();
        HashSet<TileData> closedSet = new HashSet<TileData>();
        openSet.Enqueue(startTile); 
        startTile.gCost = moveCosts.x;

        MapChunk chunk = new MapChunk();
        while (openSet.Count > 0)
        {
            TileData currentTile = openSet.Dequeue();
            closedSet.Add(currentTile);
            currentTile.Chunked = true;
            chunk.tilesInChunk.Add(currentTile);
            chunk.x += currentTile.x;
            chunk.y += currentTile.y;
            currentTile.associatedChunk = chunk;

            var neighbours = currentTile.GetNeighbours();
            foreach (TileData neighbour in neighbours)
            {
                if (!neighbour.IsWalkable())
                {
                    continue;
                }
                if (neighbour.Chunked)
                {
                    if (neighbour.associatedChunk != null && neighbour.associatedChunk != chunk)
                    {
                        if (!chunk.neighbours.Contains(neighbour.associatedChunk))
                        { 
                            chunk.neighbours.Add(neighbour.associatedChunk);
                            neighbour.associatedChunk.neighbours.Add(chunk);
                        }
                    }
                    continue;
                }                
                moveCosts = neighbour.GetMoveCost();
                int newGCost = currentTile.gCost;
                newGCost += neighbour.x != startTile.x && neighbour.y != startTile.y ? moveCosts.y : moveCosts.x;
                if (newGCost > maxCost)
                {
                    continue;
                }
                if (openSet.Contains(neighbour))
                {
                    if (newGCost < neighbour.gCost)
                    {
                        neighbour.gCost = newGCost;
                    }
                }
                else
                {
                    neighbour.gCost = newGCost;
                    openSet.Enqueue(neighbour);
                }
            }
        }
        chunk.x /= chunk.tilesInChunk.Count;
        chunk.y /= chunk.tilesInChunk.Count;
        mapChunks.Add(chunk);
    }

    void LazyFloodFill(int x, int y)
    {
        float chance = 100f;
        float decayFactor = 0.996f;
        TileData startTile = gridData[x, y];
        if (startTile.TileType >= 4 || startTile.TileType <= 0)
        {
            return;
        }

        List<TileData> openSet = new List<TileData>();
        HashSet<TileData> closedSet = new HashSet<TileData>();
        openSet.Add(startTile);
        while (openSet.Count > 0)
        {
            TileData tile = openSet[0];
            openSet.RemoveAt(0);
            closedSet.Add(tile);

            int moveCost = tile.GetMoveCost().x;
            tile.TileType = startTile.TileType;
            display.SetPixel(tile.x, tile.y, terrainColors[GetTileColorID(tile)], DisplayType.Main);
            if (moveCost < tile.GetMoveCost().x)
            {
                tile.heat = 0;
            }

            float randomNumber = Random.Range(0f, 100f);
            if (chance >= randomNumber)
            {
                for (int i = 0; i < 4; i++)
                {
                    TileData neighbour = tile.GetNeighbour(i * 2 + 1);
                    if (neighbour != null && neighbour.TileType < 4 && neighbour.TileType > 0)
                    {
                        if (!openSet.Contains(neighbour) && !closedSet.Contains(neighbour))
                        {
                            openSet.Add(neighbour);
                        }
                    }
                }
            }
            chance *= decayFactor;
        }
        display.ApplyDisplayChanges(DisplayType.Main);
        UpdateHeatMapDisplayer();
    }
    private void RandomlyChangeTiles()
    {
        int zones = 8;
        int xChunkSize = XSize / zones;
        int yChunkSize = YSize / zones;
        for (int i = 0; i < zones; i++)
        {
            for (int j = 0; j < zones; j++)
            {
                do
                {
                    startPoint = new Vector2Int(Random.Range(i * xChunkSize, Mathf.Clamp((i + 1), 1, zones) * xChunkSize), Random.Range(j * yChunkSize, Mathf.Clamp((j + 1), 1, zones) * yChunkSize));
                }
                while (gridData[startPoint.x, startPoint.y].TileType <= 0 || gridData[startPoint.x, startPoint.y].TileType >= 4);
                LazyFloodFill(startPoint.x, startPoint.y);
            }
        }
    }
    void ResetMainDisplay()
    {
        for (int i = 0; i < xSize; i++)
        {
            for (int j = 0; j < ySize; j++)
            {
                display.SetPixel(i, j, terrainColors[GetTileColorID(gridData[i, j])], DisplayType.Main);
            }
        }
        display.ApplyDisplayChanges(DisplayType.Main);
    }
    void UpdateDesirePaths()
    {
        desirePathsAttempts++;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch = Stopwatch.StartNew();
        int defaultXMoveCost = Pathfinding.DefaultMovementCosts.x;
        for (int i = 0; i < xSize; i++)
        {
            for (int j = 0; j < ySize; j++)
            {
                TileData tile = gridData[i, j];
                if (tile.heat > 0)
                { 
                    if (tile.GetMoveCost().x / defaultXMoveCost > tile.heat)
                    {
                        tile.heat = 0;
                    }
                }
            }
        }
        stopwatch.Stop();
        desirePathsTime += stopwatch.ElapsedMilliseconds;
    }
}
public struct ExtremeResults
{
    public int cost;
    public long time;
    public int aCost;
    public long aTime;
    public double cRatio;
    public double tRatio;

    public ExtremeResults(int c, long t, int aC, long aT, double cR, double tR)
    {
        cost = c;
        time = t;
        aCost = aC;
        aTime = aT;
        cRatio = cR;
        tRatio = tR;
    }
    public string GetTimeAsString()
    {
        return $"{time/1000}s {time%1000}ms";
    }
    public string GetAStarTimeAsString()
    {
        return $"{aTime / 1000}s {aTime % 1000}ms";
    }
}