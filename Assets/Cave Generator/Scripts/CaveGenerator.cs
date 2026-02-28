using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace CaveGeneration
{
    [ExecuteInEditMode]
    public class CaveGenerator : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [Range(1f, 5)]
        [Tooltip("Resolution of the cave mesh (a smaller value gives more detail)")]
        public int divisionSize;
        [Tooltip("Size of each individual terrain chunk")]
        public int chunkWidth;
        [Tooltip("Height of each individual terrain chunk")]
        public int chunkHeight;
        [Tooltip("Number of chunks to generate for each axis")]
        public Vector3Int chunksToGenerate;
        [Tooltip("Toggle generation of walls to enclose the caves")]
        public bool generateBorderWalls;
        [Tooltip("Toggle generation of the cave ceiling and floor")]
        public bool generateFloorAndCeiling;
        [Tooltip("Style of the cave terrain")]
        public CaveStyle caveStyle;

        [Header("Shading")]
        [Tooltip("If set, the terrain will use smooth shading (smooth shaded caves take longer to generate)")]
        public bool useSmoothShading;
        public Material mainMaterial;

        [Header("Main Settings")]
        [Tooltip("Random seed for generating the caves")]
        public int CaveSeed;
        [Tooltip("Overall scale of the generated caves")]
        public float globalScaleModifier;

        [Header("Play Mode")]
        [Tooltip("Enable this to have the caves update dynamically as the player moves around")]
        public bool UpdateUsingPlayerPosition;
        [Tooltip("Position of the player for cave update purposes")]
        public Transform PlayerTransform;

        [Header("Cave Noise")]
        [Tooltip("Perlin noise frequencies (large caves)")]
        public float[] LargeCavePerlinFrequencies;
        [Tooltip("Perlin noise amplitudes per frequency (large caves)")]
        public float[] LargeCavePerlinAmplitudes;
        Vector2[] LargeCavePerlinOctaveOffsets;
        [Tooltip("Scale of the perlin noise (large caves)")]
        public Vector3 perlinScale;
        [Range(0, 1)]
        [Tooltip("Minimum threshold for cave surfaces (large caves). Noise between these values defines the cave surfaces")]
        public float largeCaveSurfaceThresholdMin;
        [Range(0, 1)]
        [Tooltip("Maximum threshold for cave surfaces (large caves). Noise between these values defines the cave surfaces")]
        public float largeCaveSurfaceThresholdMax;

        [Space(10)]
        [Tooltip("Perlin noise frequencies (joining caves)")]
        public float[] JoiningCavePerlinFrequencies;
        [Tooltip("Perlin noise amplitudes per frequency (joining caves)")]
        public float[] JoiningCavePerlinAmplitudes;
        Vector2[] JoiningCavePerlinOctaveOffsets;
        [Tooltip("Scale of the perlin noise (joining caves)")]
        public Vector3 joiningCavePerlinScale;
        [Range(0, 1)]
        [Tooltip("Minimum threshold for cave surfaces (joining caves). Noise between these values defines the cave surfaces")]
        public float joiningCaveSurfaceThresholdMin;
        [Range(0, 1)]
        [Tooltip("Maximum threshold for cave surfaces (joining caves). Noise between these values defines the cave surfaces")]
        public float joiningCaveSurfaceThresholdMax;

        [Header("Walls, Floor, Ceiling Noise")]
        [Tooltip("Scale of the perlin noise for the cave walls, ceiling, and floor")]
        public float wallNoiseScale;
        [Tooltip("Maximum height of the noise for the walls, ceiling, and floor")]
        public float wallNoiseHeight;

        Vector2[] wallOffsets;

        [Space(10)]
        [Tooltip("Add your custom objects here")]
        public CavePrefabPlacement[] CustomObjects;

        private List<Vector3> verts;
        private List<int> tris;

        [Header("Layer Settings")]
        [Tooltip("Layer for raycasting against terrain")]
        public int TerrainLayer;
        [Tooltip("Layer for raycasting against props (custom objects)")]
        public int PropLayer;

        [Header("Colours")]
        [Tooltip("If true, the cave material will be set to use the colours below")]
        [SerializeField] private bool SetColours;
        public Color TerrainColour1;
        public Color TerrainColour2;
        public Color CliffColour1;
        public Color CliffColour2;

        private bool isGenerating;
        private IEnumerator generationCoroutine;

        //max bounds of the terrain if generating walls
        private float chunkBoundsMinX;
        private float chunkBoundsMaxX;
        private float chunkBoundsMinZ;
        private float chunkBoundsMaxZ;

        //actual chunk sizes are calculated to make sure they are correctly divisible by the marching cubes division size
        private int actualCaveHeight;
        private int actualChunkSize;
        private int actualChunkHeight;

        private Transform terrainRoot;

        //number of attempts at placing an object on the island before giving up. Increasing this has a negative imapct on editor performance
        private const int maxAttemptsPerAssetSpawn = 15;

        //player position in chunk space
        private Vector3Int playerChunkPosition;
        private Vector3Int initialPlayerChunkPosition;
        private Vector3Int previousPlayerChunkPosition;

        private Queue<ChunkUpdate> ChunkUpdateQueue;
        private bool IsUpdatingChunks;

        private float generationStartTime;

        private List<List<List<GameObject>>> chunks;

        //indices of triangles that are generated at the edge of chunks + 1. these are generated for smooth shading purposes, then removed from the mesh
        List<int> smoothTrianglesToRemoveIndices;

        //used to cache the current chunk we are working on and its position
        private Vector3 currentChunkPosition;
        private GameObject currentChunk;

        //don't spend anymore than this time in milliseconds working per frame during play mode
        private const float frameWaitTime = 13f;

        private float timeAtFrameStart;
        private float frameTimeLimit;
        private float timeElapsedThisFrame;

        void Start()
        {
            CheckAndStartPlayerPositionUpdating();
        }

        private void CheckAndStartPlayerPositionUpdating()
        {
            if (UpdateUsingPlayerPosition && Application.isPlaying)
            {
                //initial setup if we are updating the caves during play mode

                if (PlayerTransform != null)
                {
                    InitGenerator();

                    ChunkUpdateQueue = new Queue<ChunkUpdate>();

                    actualChunkSize = chunkWidth / divisionSize;
                    actualChunkSize *= divisionSize;

                    playerChunkPosition.x = Mathf.RoundToInt((PlayerTransform.position.x - transform.position.x) / actualChunkSize);
                    playerChunkPosition.y = Mathf.RoundToInt((PlayerTransform.position.y - transform.position.y) / actualChunkHeight);
                    playerChunkPosition.z = Mathf.RoundToInt((PlayerTransform.position.z - transform.position.z) / actualChunkSize);

                    initialPlayerChunkPosition = playerChunkPosition;
                    previousPlayerChunkPosition = playerChunkPosition;

                    int p = 0;

                    for (int i = 0; i < chunksToGenerate.x; i++)
                    {
                        for (int y = 0; y < chunksToGenerate.y; y++)
                        {
                            for (int f = 0; f < chunksToGenerate.z; f++)
                            {
                                chunks[i][y][f] = terrainRoot.GetChild(p).gameObject;
                                p++;
                            }
                        }
                    }
                }
            }
        }

        void Update()
        {
            if (EditorUtility.IsDirty(this) && !Application.isPlaying)
            {
                UpdateColours();
            }

            if (UpdateUsingPlayerPosition && Application.isPlaying)
            {
                //check for chunk updates based on the player's position

                if (PlayerTransform != null)
                {
                    playerChunkPosition.x = Mathf.RoundToInt((PlayerTransform.position.x - transform.position.x) / actualChunkSize);
                    playerChunkPosition.y = Mathf.RoundToInt((PlayerTransform.position.y - transform.position.y) / actualChunkHeight);
                    playerChunkPosition.z = Mathf.RoundToInt((PlayerTransform.position.z - transform.position.z) / actualChunkSize);

                    if (playerChunkPosition.x > previousPlayerChunkPosition.x)
                    {
                        ChunkUpdateQueue.Enqueue(new ChunkUpdate(MovementDirection.East, playerChunkPosition - initialPlayerChunkPosition));
                    }
                    else if (playerChunkPosition.x < previousPlayerChunkPosition.x)
                    {
                        ChunkUpdateQueue.Enqueue(new ChunkUpdate(MovementDirection.West, playerChunkPosition - initialPlayerChunkPosition));
                    }

                    if (!generateFloorAndCeiling)
                    {
                        if (playerChunkPosition.y > previousPlayerChunkPosition.y)
                        {
                            ChunkUpdateQueue.Enqueue(new ChunkUpdate(MovementDirection.Up, playerChunkPosition - initialPlayerChunkPosition));
                        }
                        else if (playerChunkPosition.y < previousPlayerChunkPosition.y)
                        {
                            ChunkUpdateQueue.Enqueue(new ChunkUpdate(MovementDirection.Down, playerChunkPosition - initialPlayerChunkPosition));
                        }
                    }

                    if (playerChunkPosition.z > previousPlayerChunkPosition.z)
                    {
                        ChunkUpdateQueue.Enqueue(new ChunkUpdate(MovementDirection.North, playerChunkPosition - initialPlayerChunkPosition));
                    }
                    else if (playerChunkPosition.z < previousPlayerChunkPosition.z)
                    {
                        ChunkUpdateQueue.Enqueue(new ChunkUpdate(MovementDirection.South, playerChunkPosition - initialPlayerChunkPosition));
                    }

                    //optimize the update queue, and perform the updates in the queue if necessary
                    if (ChunkUpdateQueue.Count > 0)
                    {
                        if (!IsUpdatingChunks)
                        {
                            OptimizeUpdateQueue();

                            if (ChunkUpdateQueue.Count > 0)
                            {
                                IsUpdatingChunks = true;
                                StartCoroutine(UpdateChunks(ChunkUpdateQueue.Dequeue()));
                            }
                        }
                    }

                    previousPlayerChunkPosition = playerChunkPosition;
                }
            }
        }

        //removes opposite direction updates from the queue (since they cancel each other out)
        private void OptimizeUpdateQueue()
        {
            for (int i = 0; i < ChunkUpdateQueue.Count; i++)
            {
                for (int z = i + 1; z < ChunkUpdateQueue.Count; z++)
                {
                    if (ChunkUpdateQueue.ElementAt(z).Direction == ChunkUpdateQueue.ElementAt(i).Direction.OppositeDirection())
                    {
                        RemoveElementFromTileUpdateQueue(i);
                        RemoveElementFromTileUpdateQueue(z - 1);    //-1 because collection has been modified
                        i--;    //-1 because collection has been modified
                        break;
                    }
                }
            }
        }

        private void RemoveElementFromTileUpdateQueue(int index)
        {
            ChunkUpdateQueue = new Queue<ChunkUpdate>(ChunkUpdateQueue.Where((value, theIndex) => theIndex != index));
        }

        //main chunk update function
        public IEnumerator UpdateChunks(ChunkUpdate update)
        {
            timeAtFrameStart = Time.realtimeSinceStartup;
            frameTimeLimit = frameWaitTime; //don't spend anymore than this time in milliseconds working on the biome map per frame
            frameTimeLimit /= 1000f;
            timeElapsedThisFrame = 0f;

            switch (update.Direction)
            {
                case MovementDirection.East:

                    float newChunkX = chunksToGenerate.x + update.ChunkPosition.x - 1;

                    List<List<GameObject>> newYColumns = new List<List<GameObject>>();
                    List<GameObject> newChunks;

                    for(int y = 0; y < chunksToGenerate.y; y++)
                    {
                        newChunks = new List<GameObject>();
                        for (int z = 0; z < chunksToGenerate.z; z++)
                        {
                            yield return StartCoroutine(GenerateChunk(new Vector3(newChunkX * actualChunkSize, (y + update.ChunkPosition.y) * actualChunkHeight, (z + update.ChunkPosition.z) * actualChunkSize)));
                            newChunks.Add(currentChunk);
                            yield return null;
                            yield return StartCoroutine(PlaceCustomObjects(currentChunkPosition, currentChunk));
                        }
                        newYColumns.Add(newChunks);
                    }

                    foreach(List<GameObject> yColumns in chunks[0])
                    {
                        foreach(GameObject zChunk in yColumns)
                        {
                            Destroy(zChunk);
                        }
                    }

                    chunks.RemoveAt(0);

                    chunks.Add(newYColumns);
                    break;

                case MovementDirection.West:

                    newChunkX = update.ChunkPosition.x;

                    newYColumns = new List<List<GameObject>>();

                    for (int y = 0; y < chunksToGenerate.y; y++)
                    {
                        newChunks = new List<GameObject>();
                        for (int z = 0; z < chunksToGenerate.z; z++)
                        {
                            yield return StartCoroutine(GenerateChunk(new Vector3(newChunkX * actualChunkSize, (y + update.ChunkPosition.y) * actualChunkHeight, (z + update.ChunkPosition.z) * actualChunkSize)));
                            newChunks.Add(currentChunk);
                            yield return null;
                            yield return StartCoroutine(PlaceCustomObjects(currentChunkPosition, currentChunk));
                        }
                        newYColumns.Add(newChunks);
                    }

                    foreach (List<GameObject> yColumn in chunks[chunks.Count - 1])
                    {
                        foreach (GameObject zChunk in yColumn)
                        {
                            Destroy(zChunk);
                        }
                    }

                    chunks.RemoveAt(chunks.Count - 1);
                    chunks.Insert(0, newYColumns);
                    break;

                case MovementDirection.North:

                    float newChunkZ = chunksToGenerate.z + update.ChunkPosition.z - 1;

                    for (int y = 0; y < chunksToGenerate.y; y++)
                    {
                        for (int x = 0; x < chunksToGenerate.x; x++)
                        {
                            yield return StartCoroutine(GenerateChunk(new Vector3((x + update.ChunkPosition.x) * actualChunkSize, (y + update.ChunkPosition.y) * actualChunkHeight, newChunkZ * actualChunkSize)));
                            chunks[x][y].Add(currentChunk);
                            yield return null;
                            yield return StartCoroutine(PlaceCustomObjects(currentChunkPosition, currentChunk));

                            Destroy(chunks[x][y][0]);
                            chunks[x][y].RemoveAt(0);
                        }
                    }
                    break;

                case MovementDirection.South:

                    newChunkZ = update.ChunkPosition.z;

                    for (int y = 0; y < chunksToGenerate.y; y++)
                    {
                        for (int x = 0; x < chunksToGenerate.x; x++)
                        {
                            yield return StartCoroutine(GenerateChunk(new Vector3((x + update.ChunkPosition.x) * actualChunkSize, (y + update.ChunkPosition.y) * actualChunkHeight, newChunkZ * actualChunkSize)));

                            chunks[x][y].Insert(0, currentChunk);
                            yield return null;
                            yield return StartCoroutine(PlaceCustomObjects(currentChunkPosition, currentChunk));

                            Destroy(chunks[x][y][chunks[x][y].Count - 1]);
                            chunks[x][y].RemoveAt(chunks[x][y].Count - 1);
                        }
                    }
                    break;

                case MovementDirection.Up:

                    float newChunkY = chunksToGenerate.y + update.ChunkPosition.y - 1;

                    for(int x = 0; x < chunksToGenerate.x; x++)
                    {
                        newChunks = new List<GameObject>();
                        for (int z = 0; z < chunksToGenerate.z; z++)
                        {
                            yield return StartCoroutine(GenerateChunk(new Vector3((x + update.ChunkPosition.x) * actualChunkSize, newChunkY * actualChunkHeight, (z + update.ChunkPosition.z) * actualChunkSize)));
                            newChunks.Add(currentChunk);
                            yield return null;
                            yield return StartCoroutine(PlaceCustomObjects(currentChunkPosition, currentChunk));
                        }
                        chunks[x].Add(newChunks);
                    }

                    foreach(List<List<GameObject>> xRow in chunks)
                    {
                        foreach(GameObject chunk in xRow[0])
                        {
                            Destroy(chunk);
                        }
                    }

                    foreach (List<List<GameObject>> xRow in chunks)
                    {
                        xRow.RemoveAt(0);
                    }
                    break;

                case MovementDirection.Down:

                    newChunkY = update.ChunkPosition.y;

                    for (int x = 0; x < chunksToGenerate.x; x++)
                    {
                        newChunks = new List<GameObject>();
                        for (int z = 0; z < chunksToGenerate.z; z++)
                        {
                            yield return StartCoroutine(GenerateChunk(new Vector3((x + update.ChunkPosition.x) * actualChunkSize, newChunkY * actualChunkHeight, (z + update.ChunkPosition.z) * actualChunkSize)));
                            newChunks.Add(currentChunk);
                            yield return null;
                            yield return StartCoroutine(PlaceCustomObjects(currentChunkPosition, currentChunk));
                        }
                        chunks[x].Insert(0, newChunks);
                    }

                    foreach (List<List<GameObject>> xRow in chunks)
                    {
                        foreach (GameObject chunk in xRow[xRow.Count - 1])
                        {
                            Destroy(chunk);
                        }
                    }

                    foreach (List<List<GameObject>> xRow in chunks)
                    {
                        xRow.RemoveAt(xRow.Count - 1);
                    }
                    break;
            }

            yield return null;

            IsUpdatingChunks = false;
        }

        //used to manually execute coroutines in edit mode
        private void CoroutineUpdate()
        {
            if (isGenerating)
            {
                generationCoroutine.MoveNext();
            }
            else
            {
                EditorApplication.update -= CoroutineUpdate;

                generationCoroutine = null;
            }
        }

        /// <summary>
        /// Main function for generating caves. Call this in play mode if you want to generate a new cave at runtime.
        /// </summary>
        public void Generate()
        {
            if (!isGenerating)
            {
                generationStartTime = Time.realtimeSinceStartup;

                if (!Application.isPlaying)
                {
                    EditorApplication.update += CoroutineUpdate;
                }
                
                isGenerating = true;

                InitGenerator();

                Debug.Log("Generating caves...");

                if (!Application.isPlaying)
                {
                    generationCoroutine = GenerateChunks();
                }
                else
                {
                    StartCoroutine(GenerateChunks());
                }
            }
        }

        /// <summary>
        /// Call this to reset the generator if an operation was interrupted
        /// </summary>
        public void RefreshGenerator()
        {
            isGenerating = false;
            generationCoroutine = null;
        }

        //destroy any existing chunks
        private void DestroyExistingGameobjects()
        {
            int childCount = terrainRoot.childCount;

            for (int i = childCount - 1; i > -1; i--)
            {
                DestroyImmediate(terrainRoot.GetChild(i).gameObject, false);
            }
        }

        //coroutine to spread cave generation over a period of time
        private IEnumerator GenerateChunks()
        {
            DestroyExistingGameobjects();

            yield return null;

            for (int i = 0; i < chunksToGenerate.x; i++)
            {
                for (int y = 0; y < chunksToGenerate.y; y++)
                {
                    for (int f = 0; f < chunksToGenerate.z; f++)
                    {
                        yield return StartCoroutine(GenerateChunk(new Vector3(i * actualChunkSize, y * actualChunkHeight, f * actualChunkSize)));
                        chunks[i][y][f] = currentChunk;
                        yield return null;
                        yield return StartCoroutine(PlaceCustomObjects(currentChunkPosition, currentChunk));
                    }
                }
            }

            Debug.Log("Done! (" + (Time.realtimeSinceStartup - generationStartTime).ToString("F2") + " seconds)");
            isGenerating = false;

            CheckAndStartPlayerPositionUpdating();
        }


        //set up the cave generator with initial values
        private void InitGenerator()
        {
            chunks = new List<List<List<GameObject>>>();

            for (int i = 0; i < chunksToGenerate.x; i++)
            {
                chunks.Add(new List<List<GameObject>>());

                for (int y = 0; y < chunksToGenerate.y; y++)
                {
                    chunks[i].Add(new List<GameObject>());

                    for (int f = 0; f < chunksToGenerate.z; f++)
                    {
                        chunks[i][y].Add(null);
                    }
                }
            }

            if (terrainRoot == null)
            {
                terrainRoot = transform.Find("Chunks");

                //create a new root for plaing objects if it's missing
                if (terrainRoot == null)
                {
                    GameObject newRoot = new GameObject("Chunks");
                    newRoot.transform.parent = transform;
                    terrainRoot = newRoot.transform;
                }
            }

            actualCaveHeight = (chunksToGenerate.y * chunkHeight) / divisionSize;
            actualCaveHeight *= divisionSize;

            actualChunkSize = chunkWidth / divisionSize;
            actualChunkSize *= divisionSize;

            actualChunkHeight = chunkHeight / divisionSize;
            actualChunkHeight *= divisionSize;

            Vector3 globalOffset = new Vector3((actualChunkSize * chunksToGenerate.x) / 2f, actualCaveHeight / 2f, (actualChunkSize * chunksToGenerate.z) / 2f);

            chunkBoundsMinX = -globalOffset.x;
            chunkBoundsMaxX = globalOffset.x;
            chunkBoundsMinZ = -globalOffset.z;
            chunkBoundsMaxZ = globalOffset.z;

            Random.InitState(CaveSeed);

            LargeCavePerlinOctaveOffsets = new Vector2[LargeCavePerlinFrequencies.Length];

            for (int i = 0; i < LargeCavePerlinOctaveOffsets.Length; i++)
            {
                LargeCavePerlinOctaveOffsets[i] = new Vector2(Random.Range(200, 2000), Random.Range(200, 2000));
            }

            JoiningCavePerlinOctaveOffsets = new Vector2[JoiningCavePerlinFrequencies.Length];

            for (int i = 0; i < JoiningCavePerlinOctaveOffsets.Length; i++)
            {
                JoiningCavePerlinOctaveOffsets[i] = new Vector2(Random.Range(200, 2000), Random.Range(200, 2000));
            }

            wallOffsets = new Vector2[6];

            for (int i = 0; i < 6; i++)
            {
                wallOffsets[i] = new Vector2(Random.Range(200, 2000), Random.Range(200, 2000));
            }

            verts = new List<Vector3>();
            tris = new List<int>();
        }

        //generates a single chunk at a given position (in chunk space)
        private IEnumerator GenerateChunk(Vector3 chunkPosition)
        {
            verts.Clear();
            tris.Clear();

            smoothTrianglesToRemoveIndices = new List<int>();

            GameObject newChunk = new GameObject("Chunk");
            newChunk.transform.SetParent(terrainRoot, false);

            MeshFilter chunkMeshFilter = newChunk.AddComponent<MeshFilter>();
            MeshRenderer chunkMeshRenderer = newChunk.AddComponent<MeshRenderer>();
            chunkMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 globalOffset = new Vector3((actualChunkSize * chunksToGenerate.x) / 2f, (actualChunkHeight * chunksToGenerate.y) / 2f, (actualChunkSize * chunksToGenerate.z) / 2f);

            int lastXIndex = Mathf.RoundToInt(actualChunkSize / divisionSize) + (useSmoothShading ? 1 : 0);
            int lastYIndex = Mathf.RoundToInt(actualChunkHeight / divisionSize) + (useSmoothShading ? 1 : 0);
            int lastZIndex = Mathf.RoundToInt(actualChunkSize / divisionSize) + (useSmoothShading ? 1 : 0);

            currentChunkPosition = new Vector3((useSmoothShading ? divisionSize : 0), (useSmoothShading ? divisionSize : 0), (useSmoothShading ? divisionSize : 0)) - globalOffset + chunkPosition;

            for (int x = 0; x < Mathf.RoundToInt(actualChunkSize / divisionSize) + (useSmoothShading ? 2 : 0); x++)
            {
                for (int z = 0; z < Mathf.RoundToInt(actualChunkSize / divisionSize) + (useSmoothShading ? 2 : 0); z++)
                {
                    for (int y = 0; y < Mathf.RoundToInt(actualChunkHeight / divisionSize) + (useSmoothShading ? 2 : 0); y++)
                    {
                        Vector3 position = new Vector3(x * divisionSize - (useSmoothShading ? divisionSize : 0), y * divisionSize - (useSmoothShading ? divisionSize : 0), z * divisionSize - (useSmoothShading ? divisionSize : 0)) - globalOffset + chunkPosition;

                        Vector3[] cubeCorners = new Vector3[8];

                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 cornerValue = MarchingTable.Corners[i];
                            cornerValue *= divisionSize;

                            Vector3 corner = position + cornerValue;
                            cubeCorners[i] = corner;
                        }

                        float perlinValue = Perlin3D(position, LargeCavePerlinOctaveOffsets, LargeCavePerlinFrequencies, LargeCavePerlinAmplitudes, perlinScale);
                        float joiningCavePerlinValue = Perlin3D(position, JoiningCavePerlinOctaveOffsets, JoiningCavePerlinFrequencies, JoiningCavePerlinAmplitudes, joiningCavePerlinScale);

                        MarchCube(position, cubeCorners, x == lastXIndex, y == lastYIndex, z == lastZIndex, x == 0, y == 0, z == 0);

                        if (verts.Count > 65535)
                        {
                            Debug.Log("Chunk mesh has exceed the Unity vertex count limit - please use a smaller chunk size. Generation aborted.");
                            isGenerating = false;
                            DestroyExistingGameobjects();
                            yield break;
                        }

                        timeElapsedThisFrame = Time.realtimeSinceStartup - timeAtFrameStart;

                        if (Application.isPlaying && timeElapsedThisFrame >= frameTimeLimit)
                        {
                            yield return null;
                            timeElapsedThisFrame = 0;
                            timeAtFrameStart = Time.realtimeSinceStartup;
                        }
                    }
                }
            }

            Mesh newMesh = new Mesh();

            newMesh.SetVertices(verts);
            newMesh.triangles = tris.ToArray();

            chunkMeshRenderer.material = mainMaterial;

            newMesh.RecalculateNormals();

            if(useSmoothShading)
            {
                for(int i = 0; i < smoothTrianglesToRemoveIndices.Count; i++)
                {
                    tris[smoothTrianglesToRemoveIndices[i]] = -1;
                }

                tris.RemoveAll(x => x == -1);
            }

            newMesh.triangles = tris.ToArray();

            chunkMeshFilter.mesh = newMesh;
            newChunk.AddComponent<MeshCollider>();

            currentChunk = newChunk;
        }

        //gets the marching cubes triangle configuration index for a marching cube at a given position
        private int GetConfigIndex(Vector3[] cubeCorners)
        {
            int configIndex = 0;

            //for each corner, check if it is above or below the cave surface
            for (int i = 0; i < 8; i++)
            {
                Vector3 cubePoint = cubeCorners[i];
                float perlinValue = Perlin3D(cubePoint, LargeCavePerlinOctaveOffsets, LargeCavePerlinFrequencies, LargeCavePerlinAmplitudes, perlinScale);
                float joiningCavePerlinValue = Perlin3D(cubePoint, JoiningCavePerlinOctaveOffsets, JoiningCavePerlinFrequencies, JoiningCavePerlinAmplitudes, joiningCavePerlinScale);

                //large cave surface check
                bool isSurface = !(perlinValue >= largeCaveSurfaceThresholdMin && perlinValue <= largeCaveSurfaceThresholdMax);

                //joining cave surface check
                if (perlinValue < largeCaveSurfaceThresholdMax)
                {
                    isSurface = !(joiningCavePerlinValue >= joiningCaveSurfaceThresholdMin && joiningCavePerlinValue <= joiningCaveSurfaceThresholdMax);
                }

                //wall surface checks
                if (generateBorderWalls && !UpdateUsingPlayerPosition)
                {
                    if (caveStyle == CaveStyle.Smooth)
                    {
                        if ((cubePoint.x >= (chunkBoundsMaxX) || cubePoint.x <= (chunkBoundsMinX) || cubePoint.z >= (chunkBoundsMaxZ) || cubePoint.z <= (chunkBoundsMinZ)))
                        {
                            isSurface = false;
                        }
                    }
                    else
                    {
                        if ((cubePoint.x >= (chunkBoundsMaxX - Perlin2D(new Vector3(cubePoint.z, 0, cubePoint.y), wallOffsets[0], Vector2.one * wallNoiseScale, wallNoiseHeight)) ||
                            cubePoint.x <= (chunkBoundsMinX + Perlin2D(new Vector3(cubePoint.z, 0, cubePoint.y), wallOffsets[1], Vector2.one * wallNoiseScale, wallNoiseHeight)) ||
                            cubePoint.z >= (chunkBoundsMaxZ - Perlin2D(new Vector3(cubePoint.x, 0, cubePoint.y), wallOffsets[2], Vector2.one * wallNoiseScale, wallNoiseHeight)) ||
                            cubePoint.z <= (chunkBoundsMinZ + Perlin2D(new Vector3(cubePoint.x, 0, cubePoint.y), wallOffsets[3], Vector2.one * wallNoiseScale, wallNoiseHeight))))
                        {
                            isSurface = false;
                        }
                    }
                }

                //floor and ceiling surface checks
                if (generateFloorAndCeiling)
                {
                    if (caveStyle == CaveStyle.Smooth)
                    {
                        if (cubePoint.y >= (actualChunkHeight * chunksToGenerate.y) / 2f || cubePoint.y <= -(actualChunkHeight * chunksToGenerate.y) / 2f)
                        {
                            isSurface = false;
                        }
                    }
                    else
                    {
                        if ((cubePoint.y >= (((actualChunkHeight * chunksToGenerate.y) / 2f) - Perlin2D(cubePoint, wallOffsets[4], Vector2.one * wallNoiseScale, wallNoiseHeight)) || cubePoint.y <= ((-(actualChunkHeight * chunksToGenerate.y) / 2f) + Perlin2D(cubePoint, wallOffsets[5], Vector2.one * wallNoiseScale, wallNoiseHeight))))
                        {
                            isSurface = false;
                        }
                    }
                }

                if (isSurface)
                {
                    configIndex |= 1 << i;
                }
            }

            return configIndex;
        }

        //adds triangles to the mesh for a marching cube at a given position
        private void MarchCube(Vector3 position, Vector3[] cubeCorners, bool isLastX, bool isLastY, bool isLastZ, bool isFirstX, bool isFirstY, bool isFirstZ)
        {
            int configIndex = GetConfigIndex(cubeCorners);

            if (configIndex == 0 || configIndex == 255)
            {
                return;
            }

            if (caveStyle == CaveStyle.Cubes)
            {
                AddCubeToMesh(position);
                return;
            }

            int edgeIndex = 0;
            for (int t = 0; t < 5; t++)
            {
                for (int v = 0; v < 3; v++)
                {
                    int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];

                    if (triTableValue == -1)
                    {
                        return;
                    }

                    Vector3 edgeValue = MarchingTable.Edges[triTableValue, 0];
                    edgeValue *= divisionSize;

                    Vector3 edgeStart = position + edgeValue;

                    edgeValue = MarchingTable.Edges[triTableValue, 1];
                    edgeValue *= divisionSize;

                    Vector3 edgeEnd = position + edgeValue;

                    float perlinValue1 = Perlin3D(edgeStart, LargeCavePerlinOctaveOffsets, LargeCavePerlinFrequencies, LargeCavePerlinAmplitudes, perlinScale);
                    float perlinValue2 = Perlin3D(edgeEnd, LargeCavePerlinOctaveOffsets, LargeCavePerlinFrequencies, LargeCavePerlinAmplitudes, perlinScale);

                    float perlinValue1Joining = Perlin3D(edgeStart, JoiningCavePerlinOctaveOffsets, JoiningCavePerlinFrequencies, JoiningCavePerlinAmplitudes, joiningCavePerlinScale);
                    float perlinValue2Joining = Perlin3D(edgeEnd, JoiningCavePerlinOctaveOffsets, JoiningCavePerlinFrequencies, JoiningCavePerlinAmplitudes, joiningCavePerlinScale);

                    Vector3 vertex;

                    if (caveStyle == CaveStyle.Smooth)
                    {
                        int edgeStartWall = GetWallValueForCubeEdgePoint(edgeStart);
                        int edgeEndWall = GetWallValueForCubeEdgePoint(edgeEnd);

                        int edgeStartFloorCeiling = GetFloorCeilingValueForCubeEdgePoint(edgeStart);
                        int edgeEndFloorCeiling = GetFloorCeilingValueForCubeEdgePoint(edgeEnd);

                        //if using smooth generation, interpolate the vertex positions within the marching cube
                        //offset the walls / ceiling / floor by the raw wall perlin value

                        if (generateBorderWalls && !UpdateUsingPlayerPosition && edgeStartWall != edgeEndWall)
                        {
                            int wallValue = Mathf.Max(edgeStartWall, edgeEndWall);

                            vertex = (edgeStart + edgeEnd) / 2;

                            float wallPerlin = 0;

                            switch (wallValue)
                            {
                                case 0:
                                    wallPerlin = Perlin2D(new Vector3(vertex.z, 0, vertex.y), wallOffsets[0], Vector2.one * wallNoiseScale, wallNoiseHeight);
                                    vertex += new Vector3(-wallPerlin, 0, 0);
                                    break;

                                case 1:
                                    wallPerlin = Perlin2D(new Vector3(vertex.z, 0, vertex.y), wallOffsets[1], Vector2.one * wallNoiseScale, wallNoiseHeight);
                                    vertex += new Vector3(wallPerlin, 0, 0);
                                    break;

                                case 2:
                                    wallPerlin = Perlin2D(new Vector3(vertex.x, 0, vertex.y), wallOffsets[2], Vector2.one * wallNoiseScale, wallNoiseHeight);
                                    vertex += new Vector3(0, 0, -wallPerlin);
                                    break;

                                case 3:
                                    wallPerlin = Perlin2D(new Vector3(vertex.x, 0, vertex.y), wallOffsets[3], Vector2.one * wallNoiseScale, wallNoiseHeight);
                                    vertex += new Vector3(0, 0, wallPerlin);
                                    break;
                            }
                        }
                        else if (generateFloorAndCeiling && edgeStartFloorCeiling != edgeEndFloorCeiling)
                        {
                            vertex = (edgeStart + edgeEnd) / 2;

                            int floorCeilingValue = Mathf.Max(edgeStartFloorCeiling, edgeEndFloorCeiling);


                            switch (floorCeilingValue)
                            {
                                case 0:
                                    float verticalPerlin = Perlin2D(vertex, wallOffsets[4], Vector2.one * wallNoiseScale, wallNoiseHeight);
                                    vertex += new Vector3(0, -verticalPerlin, 0);
                                    break;

                                case 1:
                                    verticalPerlin = Perlin2D(vertex, wallOffsets[5], Vector2.one * wallNoiseScale, wallNoiseHeight);
                                    vertex += new Vector3(0, verticalPerlin, 0);
                                    break;
                            }
                        }
                        else if ((perlinValue1Joining > joiningCaveSurfaceThresholdMax && perlinValue2Joining < joiningCaveSurfaceThresholdMax) || (perlinValue1Joining < joiningCaveSurfaceThresholdMax && perlinValue2Joining > joiningCaveSurfaceThresholdMax))
                        {
                            float difference = (perlinValue2Joining - perlinValue1Joining);

                            difference = (joiningCaveSurfaceThresholdMax - perlinValue1Joining) / difference;

                            vertex = edgeStart + ((edgeEnd - edgeStart) * (difference));
                        }
                        else if ((perlinValue1Joining > joiningCaveSurfaceThresholdMin && perlinValue2Joining < joiningCaveSurfaceThresholdMin) || (perlinValue1Joining < joiningCaveSurfaceThresholdMin && perlinValue2Joining > joiningCaveSurfaceThresholdMin))
                        {
                            float difference = (perlinValue2Joining - perlinValue1Joining);

                            difference = (joiningCaveSurfaceThresholdMin - perlinValue1Joining) / difference;

                            vertex = edgeStart + ((edgeEnd - edgeStart) * (difference));
                        }
                        else if ((perlinValue1 > largeCaveSurfaceThresholdMax && perlinValue2 < largeCaveSurfaceThresholdMax) || (perlinValue1 < largeCaveSurfaceThresholdMax && perlinValue2 > largeCaveSurfaceThresholdMax))
                        {
                            float difference = (perlinValue2 - perlinValue1);

                            difference = (largeCaveSurfaceThresholdMax - perlinValue1) / difference;

                            vertex = edgeStart + ((edgeEnd - edgeStart) * (difference));
                        }
                        else if ((perlinValue1 > largeCaveSurfaceThresholdMin && perlinValue2 < largeCaveSurfaceThresholdMin) || (perlinValue1 < largeCaveSurfaceThresholdMin && perlinValue2 > largeCaveSurfaceThresholdMin))
                        {
                            float difference = (perlinValue2 - perlinValue1);

                            difference = (largeCaveSurfaceThresholdMin - perlinValue1) / difference;

                            vertex = edgeStart + ((edgeEnd - edgeStart) * (difference));
                        }
                        else
                        {
                            vertex = (edgeStart + edgeEnd) / 2;
                        }
                    }
                    else
                    {
                        vertex = (edgeStart + edgeEnd) / 2;
                    }

                    //check for existing vertices at the desired position, to create shared vertices for smooth shading
                    if(useSmoothShading)
                    {
                        bool foundExistingVert = false;

                        if (isFirstX || isLastX || isFirstY || isLastY || isFirstZ || isLastZ)
                        {
                            smoothTrianglesToRemoveIndices.Add(tris.Count);
                        }

                        for (int i = 0; i < verts.Count; i++)
                        {
                            if((verts[i] - vertex).magnitude < 0.02f)
                            {
                                tris.Add(i);
                                foundExistingVert = true;
                                break;
                            }
                        }

                        if(!foundExistingVert)
                        {
                            verts.Add(vertex);
                            tris.Add(verts.Count - 1);
                        }
                    }
                    else
                    {
                        verts.Add(vertex);
                        tris.Add(verts.Count - 1);
                    }

                    edgeIndex++;
                }
            }
        }

        //returns an int to indicate which wall direction a given position is behind
        private int GetWallValueForCubeEdgePoint(Vector3 edgePoint)
        {
            if (edgePoint.x >= chunkBoundsMaxX)
            {
                return 0;
            }
            else if (edgePoint.x <= chunkBoundsMinX)
            {
                return 1;
            }
            else if (edgePoint.z >= chunkBoundsMaxZ)
            {
                return 2;
            }
            else if (edgePoint.z <= chunkBoundsMinZ)
            {
                return 3;
            }

            return -1;
        }

        //returns an int to indicate if a given position is behind the ceiling / floor
        private int GetFloorCeilingValueForCubeEdgePoint(Vector3 edgePoint)
        {
            if (edgePoint.y >= (actualChunkHeight * chunksToGenerate.y) / 2f)
            {
                return 0;
            }
            else if (edgePoint.y <= -(actualChunkHeight * chunksToGenerate.y) / 2f)
            {
                return 1;
            }

            return -1;
        }

        private void UpdateColours()
        {
            if (SetColours && mainMaterial != null)
            {
                mainMaterial.SetColor("_TerrainColour", TerrainColour1);
                mainMaterial.SetColor("_NoiseColour", TerrainColour2);
                mainMaterial.SetColor("_Cliff_Colour", CliffColour1);
                mainMaterial.SetColor("_Cliff_Noise_Colour", CliffColour2);
            }
        }

        //place all custom objects on a given chunk, using raycasts
        private IEnumerator PlaceCustomObjects(Vector3 chunkPosition, GameObject chunk)
        {
            foreach (CavePrefabPlacement placement in CustomObjects)
            {
                if (placement.Prefabs.Length > 0)
                {
                    foreach (GameObject prefab in placement.Prefabs)
                    {
                        if (prefab == null)
                        {
                            yield break;
                        }
                    }

                    int[] validLayers = new int[] { TerrainLayer };

                    for (int i = 0; i < placement.CountPerChunk; i++)
                    {
                        int attempts = 0;
                        int maxAttempts = maxAttemptsPerAssetSpawn;

                        while (attempts < maxAttempts)
                        {
                            Vector3 raycastPosition = new Vector3(Random.Range(chunkPosition.x, chunkPosition.x + actualChunkSize), Random.Range(chunkPosition.y, chunkPosition.y + actualChunkHeight), Random.Range(chunkPosition.z, chunkPosition.z + actualChunkSize)) + transform.position;

                            Vector3 raycastDirection;

                            if (placement.Surface == SurfacePlacement.Floor)
                            {
                                raycastDirection = Vector3.down;
                            }
                            else if (placement.Surface == SurfacePlacement.Ceiling)
                            {
                                raycastDirection = Vector3.up;
                            }
                            else
                            {
                                raycastDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                            }

                            Vector3 spawnPoint = CheckForAvailableSpace(raycastPosition, raycastDirection, placement.ObjectRadius, 1 << PropLayer | 1 << TerrainLayer, validLayers, placement.SpawnHeightThresholdMin, placement.SpawnHeightThresholdMax, placement.UseNormalLimits, placement.NormalMinLimit, placement.NormalMaxLimit, chunk);

                            if (spawnPoint.y < 999999)
                            {
                                GameObject spawn = Instantiate(placement.Prefabs[Random.Range(0, placement.Prefabs.Length)], spawnPoint, Quaternion.Euler(0, Random.Range(0, 360f), 0), chunk.transform);

                                Vector3 surfaceNormal = Vector3.zero;

                                RaycastHit normalHit;
                                Physics.Raycast(raycastPosition, raycastDirection, out normalHit, 100f, 1 << TerrainLayer);

                                surfaceNormal = GetSurfaceNormal(normalHit);

                                if (placement.ConstrainOrientationToWorldAxes)
                                {
                                    spawn.transform.up = ConstrainVectorToWorldAxes(surfaceNormal);
                                }
                                else
                                {
                                    spawn.transform.up = surfaceNormal;
                                }

                                float scaleX = Random.Range(placement.ScaleRangeX.x, placement.ScaleRangeX.y);
                                float scaleZ = placement.constrainXZScale ? scaleX : Random.Range(placement.ScaleRangeZ.x, placement.ScaleRangeZ.y);

                                Vector3 newScale = new Vector3(scaleX, Random.Range(placement.ScaleRangeY.x, placement.ScaleRangeY.y), scaleZ);
                                spawn.transform.localScale = newScale;

                                spawn.layer = PropLayer;

                                attempts = maxAttempts;
                            }
                            else
                            {
                                attempts++;
                            }

                            timeElapsedThisFrame = Time.realtimeSinceStartup - timeAtFrameStart;

                            if (Application.isPlaying && timeElapsedThisFrame >= frameTimeLimit)
                            {
                                yield return null;
                                timeElapsedThisFrame = 0;
                                timeAtFrameStart = Time.realtimeSinceStartup;
                            }
                        }
                    }
                }
            }
        }

        //constrains a vector to cardinal axes (used for placing custom objects)
        private Vector3 ConstrainVectorToWorldAxes(Vector3 vector)
        {
            float absX = Mathf.Abs(vector.x);
            float absY = Mathf.Abs(vector.y);
            float absZ = Mathf.Abs(vector.z);

            float largestAxis = Mathf.Max(Mathf.Max(absX, absY), absZ);

            if (Mathf.Approximately(largestAxis, absX))
            {
                return new Vector3(Mathf.RoundToInt(vector.x), 0, 0);
            }
            else if (Mathf.Approximately(largestAxis, absY))
            {
                return new Vector3(0, Mathf.RoundToInt(vector.y), 0);
            }
            else
            {
                return new Vector3(0, 0, Mathf.RoundToInt(vector.z));
            }
        }

        //checks if a position is a valid spawn point, based on placement requirements. returns [0, 10000, 0] if a good spawn point isn't found
        private Vector3 CheckForAvailableSpace(Vector3 point, Vector3 raycastDirection, float checkRadius, LayerMask raycastLayermask, int[] validLayers, float yThresholdMin, float yThresholdMax, bool includeNormalsCheck, float normalMinLimit, float normalMaxLimit, GameObject validObject)
        {
            //convert thresholds to world space
            float worldYThresholdMin = yThresholdMin + transform.position.y;
            float worldYThresholdMax = yThresholdMax + transform.position.y;

            //initial raycast, convert position to world space
            Vector3 raycastWorldOrigin = point;
            RaycastHit hit;
            Physics.Raycast(raycastWorldOrigin, raycastDirection, out hit, 100f, raycastLayermask);

            Vector3 spawnPoint = hit.point;

            //send raycasts down in a circle around the initial raycast, if our initial raycast hit is valid
            if (hit.collider != null && (hit.point.y >= worldYThresholdMin || Mathf.Approximately(hit.point.y, worldYThresholdMin)) && (hit.point.y <= worldYThresholdMax || Mathf.Approximately(hit.point.y, worldYThresholdMax)) && validLayers.Contains(hit.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit, normalMinLimit, normalMaxLimit)) && hit.collider.gameObject == validObject)
            {
                //check if raycast hits are valid (collider, layer, and flat ground if needed)
                //return early if any of the raycast hits are invalid

                RaycastHit hit1;
                Physics.Raycast(new Vector3(raycastWorldOrigin.x, raycastWorldOrigin.y, raycastWorldOrigin.z + checkRadius), raycastDirection, out hit1, 100f, raycastLayermask);
                bool hit1Valid = hit1.collider != null && validLayers.Contains(hit1.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit1, normalMinLimit, normalMaxLimit));
                if (!hit1Valid) return new Vector3(0, 1000000, 0);

                RaycastHit hit2;
                Physics.Raycast(new Vector3(raycastWorldOrigin.x, raycastWorldOrigin.y, raycastWorldOrigin.z - checkRadius), raycastDirection, out hit2, 100f, raycastLayermask);
                bool hit2Valid = hit2.collider != null && validLayers.Contains(hit2.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit2, normalMinLimit, normalMaxLimit));
                if (!hit2Valid) return new Vector3(0, 1000000, 0);

                RaycastHit hit3;
                Physics.Raycast(new Vector3(raycastWorldOrigin.x + checkRadius, raycastWorldOrigin.y, raycastWorldOrigin.z), raycastDirection, out hit3, 100f, raycastLayermask);
                bool hit3Valid = hit3.collider != null && validLayers.Contains(hit3.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit3, normalMinLimit, normalMaxLimit));
                if (!hit3Valid) return new Vector3(0, 1000000, 0);

                RaycastHit hit4;
                Physics.Raycast(new Vector3(raycastWorldOrigin.x - checkRadius, raycastWorldOrigin.y, raycastWorldOrigin.z), raycastDirection, out hit4, 100f, raycastLayermask);
                bool hit4Valid = hit4.collider != null && validLayers.Contains(hit4.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit4, normalMinLimit, normalMaxLimit));
                if (!hit4Valid) return new Vector3(0, 1000000, 0);

                RaycastHit hit5;
                Physics.Raycast(new Vector3(raycastWorldOrigin.x, raycastWorldOrigin.y, raycastWorldOrigin.z) + (new Vector3(1, 0, 1).normalized * checkRadius), raycastDirection, out hit5, 100f, raycastLayermask);
                bool hit5Valid = hit5.collider != null && validLayers.Contains(hit5.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit5, normalMinLimit, normalMaxLimit));
                if (!hit5Valid) return new Vector3(0, 100000, 0);

                RaycastHit hit6;
                Physics.Raycast(new Vector3(raycastWorldOrigin.x, raycastWorldOrigin.y, raycastWorldOrigin.z) + (new Vector3(1, 0, -1).normalized * checkRadius), raycastDirection, out hit6, 100f, raycastLayermask);
                bool hit6Valid = hit6.collider != null && validLayers.Contains(hit6.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit6, normalMinLimit, normalMaxLimit));
                if (!hit6Valid) return new Vector3(0, 1000000, 0);

                RaycastHit hit7;
                Physics.Raycast(new Vector3(raycastWorldOrigin.x, raycastWorldOrigin.y, raycastWorldOrigin.z) + (new Vector3(-1, 0, -1).normalized * checkRadius), raycastDirection, out hit7, 100f, raycastLayermask);
                bool hit7Valid = hit7.collider != null && validLayers.Contains(hit7.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit7, normalMinLimit, normalMaxLimit));
                if (!hit7Valid) return new Vector3(0, 1000000, 0);

                RaycastHit hit8;
                Physics.Raycast(new Vector3(raycastWorldOrigin.x, raycastWorldOrigin.y, raycastWorldOrigin.z) + (new Vector3(-1, 0, 1).normalized * checkRadius), raycastDirection, out hit8, 100f, raycastLayermask);
                bool hit8Valid = hit8.collider != null && validLayers.Contains(hit8.collider.gameObject.layer) && (!includeNormalsCheck || RaycastHitValidGround(hit8, normalMinLimit, normalMaxLimit));
                if (!hit8Valid) return new Vector3(0, 1000000, 0);

                return spawnPoint;
            }

            return new Vector3(0, 1000000, 0);
        }

        //returns true if a given raycast has hit flat ground
        private bool RaycastHitValidGround(RaycastHit hit, float normalAngleMinLimit = 0, float normalAngleMaxLimit = 0)
        {
            if (normalAngleMinLimit != 0 || normalAngleMaxLimit != 0)
            {
                MeshCollider collider = (MeshCollider)hit.collider;
                Mesh mesh = collider.sharedMesh;
                int[] triangles = mesh.triangles;

                Vector3 surfaceNormal = Vector3.Cross(mesh.vertices[triangles[hit.triangleIndex * 3 + 1]] - mesh.vertices[triangles[hit.triangleIndex * 3 + 0]], mesh.vertices[triangles[hit.triangleIndex * 3 + 2]] - mesh.vertices[triangles[hit.triangleIndex * 3 + 0]]).normalized;
                float surfaceAngle = Vector3.Angle(Vector3.up, surfaceNormal);
                return surfaceAngle >= normalAngleMinLimit && surfaceAngle <= normalAngleMaxLimit;
            }

            return true;
        }

        //gets the surface normal from a raycast hit on a mesh collider
        private Vector3 GetSurfaceNormal(RaycastHit hit)
        {
            MeshCollider collider = (MeshCollider)hit.collider;
            Mesh mesh = collider.sharedMesh;
            int[] triangles = mesh.triangles;

            return Vector3.Cross(mesh.vertices[triangles[hit.triangleIndex * 3 + 1]] - mesh.vertices[triangles[hit.triangleIndex * 3 + 0]], mesh.vertices[triangles[hit.triangleIndex * 3 + 2]] - mesh.vertices[triangles[hit.triangleIndex * 3 + 0]]).normalized;
        }

        //creates the verts and triangles for a single cube (cube style caves)
        private void AddCubeToMesh(Vector3 position)
        {
            float width = divisionSize / 2f;

            verts.Add(position + new Vector3(-width, -width, -width)); //0
            verts.Add(position + new Vector3(-width, -width, width)); //1
            verts.Add(position + new Vector3(width, -width, width)); //2
            verts.Add(position + new Vector3(width, -width, -width)); //3

            verts.Add(position + new Vector3(-width, width, -width)); //4
            verts.Add(position + new Vector3(-width, width, width)); //5
            verts.Add(position + new Vector3(width, width, width)); //6
            verts.Add(position + new Vector3(width, width, -width)); //7

            //bottom face
            verts.Add(position + new Vector3(width, -width, -width)); //3
            verts.Add(position + new Vector3(-width, -width, width)); //1
            verts.Add(position + new Vector3(-width, -width, -width)); //0

            AddLastTriangleVerts();

            verts.Add(position + new Vector3(width, -width, -width)); //3
            verts.Add(position + new Vector3(width, -width, width)); //2
            verts.Add(position + new Vector3(-width, -width, width)); //1

            AddLastTriangleVerts();

            //top face
            verts.Add(position + new Vector3(-width, width, -width)); //4
            verts.Add(position + new Vector3(-width, width, width)); //5
            verts.Add(position + new Vector3(width, width, -width)); //7

            AddLastTriangleVerts();

            verts.Add(position + new Vector3(-width, width, width)); //5
            verts.Add(position + new Vector3(width, width, width)); //6
            verts.Add(position + new Vector3(width, width, -width)); //7

            AddLastTriangleVerts();

            //left face
            verts.Add(position + new Vector3(-width, -width, -width)); //0
            verts.Add(position + new Vector3(-width, -width, width)); //1
            verts.Add(position + new Vector3(-width, width, -width)); //4

            AddLastTriangleVerts();

            verts.Add(position + new Vector3(-width, -width, width)); //1
            verts.Add(position + new Vector3(-width, width, width)); //5
            verts.Add(position + new Vector3(-width, width, -width)); //4

            AddLastTriangleVerts();

            //right face
            verts.Add(position + new Vector3(width, -width, width)); //2
            verts.Add(position + new Vector3(width, width, -width)); //7
            verts.Add(position + new Vector3(width, width, width)); //6

            AddLastTriangleVerts();

            verts.Add(position + new Vector3(width, -width, width)); //2
            verts.Add(position + new Vector3(width, -width, -width)); //3
            verts.Add(position + new Vector3(width, width, -width)); //7

            AddLastTriangleVerts();

            //front face
            verts.Add(position + new Vector3(width, -width, width)); //2
            verts.Add(position + new Vector3(-width, width, width)); //5
            verts.Add(position + new Vector3(-width, -width, width)); //1

            AddLastTriangleVerts();

            verts.Add(position + new Vector3(width, -width, width)); //2
            verts.Add(position + new Vector3(width, width, width)); //6
            verts.Add(position + new Vector3(-width, width, width)); //5

            AddLastTriangleVerts();

            //back face
            verts.Add(position + new Vector3(-width, -width, -width)); //0
            verts.Add(position + new Vector3(-width, width, -width)); //4
            verts.Add(position + new Vector3(width, -width, -width)); //3

            AddLastTriangleVerts();

            verts.Add(position + new Vector3(-width, width, -width)); //4
            verts.Add(position + new Vector3(width, width, -width)); //7
            verts.Add(position + new Vector3(width, -width, -width)); //3

            AddLastTriangleVerts();
        }

        private void AddLastTriangleVerts()
        {
            tris.Add(verts.Count - 3);
            tris.Add(verts.Count - 2);
            tris.Add(verts.Count - 1);
        }

        //gets a perlin noise value in 3D space
        private float Perlin3D(Vector3 point, Vector2[] offsets, float[] frequencies, float[] amplitudes, Vector3 scale)
        {
            float actualX = (point.x) / (scale.x * globalScaleModifier);
            float actualY = (point.y + 571) / (scale.y * globalScaleModifier);
            float actualZ = (point.z + 113) / (scale.z * globalScaleModifier);

            float ab = Perlin2DValueFor3D(actualX, actualY, offsets, frequencies, amplitudes);
            float bc = Perlin2DValueFor3D(actualY, actualZ, offsets, frequencies, amplitudes);
            float ac = Perlin2DValueFor3D(actualX, actualZ, offsets, frequencies, amplitudes);

            float ba = Perlin2DValueFor3D(actualY, actualX, offsets, frequencies, amplitudes);
            float cb = Perlin2DValueFor3D(actualZ, actualY, offsets, frequencies, amplitudes);
            float ca = Perlin2DValueFor3D(actualZ, actualX, offsets, frequencies, amplitudes);

            return Mathf.Clamp01((ab + bc + ac + ba + cb + ca) / 6f);
        }

        //gets a perlin noise value in 2D space, for the 3D perlin noise function
        private float Perlin2DValueFor3D(float x, float y, Vector2[] offsets, float[] frequencies, float[] amplitudes)
        {
            float value = 0;
            float normalisation = 0;

            for (int i = 0; i < frequencies.Length; i++)
            {
                value += amplitudes[i] * Mathf.PerlinNoise((x * frequencies[i]) + offsets[i].x + CaveSeed, (y * frequencies[i]) + offsets[i].y + CaveSeed);
                normalisation += amplitudes[i];
            }

            value /= normalisation;
            return Mathf.Clamp01(value);
        }

        //gets a perlin noise value in 2D space
        private float Perlin2D(Vector3 point, Vector2 offset, Vector2 scale, float maxHeight)
        {
            float x = (point.x + offset.x) / (scale.x * globalScaleModifier);
            float z = (point.z + offset.y - 337) / (scale.y * globalScaleModifier);

            return Mathf.Clamp01(Mathf.PerlinNoise(x, z)) * maxHeight;
        }

        [System.Serializable]
        public struct CavePrefabPlacement
        {
            [Tooltip("List of prefabs to select from")]
            public GameObject[] Prefabs;
            [Tooltip("Minimum height within the cave that these objects are placed")]
            public int SpawnHeightThresholdMin;
            [Tooltip("Maximum height within the cave that these objects are placed")]
            public int SpawnHeightThresholdMax;
            [Space(10)]
            [Tooltip("Radius of free space required by the objects for valid placement")]
            public float ObjectRadius;
            [Tooltip("Random X scale range")]
            public Vector2 ScaleRangeX;
            [Tooltip("Random Y scale range")]
            public Vector2 ScaleRangeY;
            [Tooltip("Random Z scale range")]
            public Vector2 ScaleRangeZ;
            [Tooltip("If set, the generator will only use the X scale range (use this to keep the objects square)")]
            public bool constrainXZScale;

            [Space(10)]
            [Tooltip("Total number of objects to place on each chunk")]
            public int CountPerChunk;
            [Tooltip("Which surface to place these objects on")]
            public SurfacePlacement Surface;
            [Tooltip("This constrains objects to face cardinal directions only")]
            public bool ConstrainOrientationToWorldAxes;
            [Tooltip("If set, objects will only be placed at points which are within the normal limits below")]
            public bool UseNormalLimits;

            [Tooltip("Normal limit. This is the angle from the world up vector (y = 1)")]
            public float NormalMinLimit;
            [Tooltip("Normal limit. This is the angle from the world up vector (y = 1)")]
            public float NormalMaxLimit;
        }

        public enum SurfacePlacement
        {
            Ceiling,
            Floor,
            Walls,
        }

        public enum CaveStyle
        {
            Angular,
            Smooth,
            Cubes,
        }

        public struct ChunkUpdate
        {
            public ChunkUpdate(MovementDirection direction, Vector3Int chunkPos)
            {
                Direction = direction;
                ChunkPosition = chunkPos;
            }

            public MovementDirection Direction;
            public Vector3Int ChunkPosition;
        }
    }

    static class MovementDirectionExtensions
    {
        public static MovementDirection OppositeDirection(this MovementDirection direction)
        {
            switch (direction)
            {
                case MovementDirection.East:
                    return MovementDirection.West;

                case MovementDirection.West:
                    return MovementDirection.East;

                case MovementDirection.North:
                    return MovementDirection.South;

                case MovementDirection.South:
                    return MovementDirection.North;

                case MovementDirection.Up:
                    return MovementDirection.Down;

                case MovementDirection.Down:
                    return MovementDirection.Up;
            }

            return MovementDirection.North;
        }
    }

    public enum MovementDirection
    {
        North,
        East,
        South,
        West,
        Up,
        Down,
    }
}