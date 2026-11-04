
using System;
using System.Collections.Generic;
using UnityEngine;

public class BasementGenerator : MonoBehaviour
{
    [Header("Dungeon Size")]
    public int width = 40;
    public int depth = 40;
    public float cellSize = 3f;

    [Header("Rooms")]
    public int roomCount = 8;
    public int minRoomSize = 5;
    public int maxRoomSize = 9;
    public int corridorWidth = 2;

    [Header("Wall Settings")]
    public float wallHeight = 4f;
    public float wallThickness = 0.35f;
    public float floorThickness = 0.4f;

    [Header("Materials")]
    public Material wallMaterial;
    public Material floorMaterial;

    [Header("Optional Prefabs")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;

    private bool[,] floorGrid;
    private Transform generatedRoot;

    private System.Random rng;

    private struct Room
    {
        public int x, z, w, d;

        public int CenterX => x + w / 2;
        public int CenterZ => z + d / 2;
    }

    private List<Room> rooms = new List<Room>();

    private void Start()
    {
        GenerateBasement();
    }

    public void GenerateBasement()
    {
        width = Mathf.Max(10, width);
        depth = Mathf.Max(10, depth);

        // A new random layout every scene load.
        rng = new System.Random(
            unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode())
        );

        floorGrid = new bool[width, depth];
        rooms.Clear();

        if (generatedRoot != null)
            Destroy(generatedRoot.gameObject);

        GameObject root = new GameObject("GeneratedBasement");
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);

        GenerateRooms();
        ConnectRooms();
        BuildFloor();
        BuildWalls();
        CreateSpawnPoint();

        Debug.Log("Connected basement generated!");
    }

    private void GenerateRooms()
    {
        int attempts = roomCount * 100;

        for (int i = 0; i < attempts && rooms.Count < roomCount; i++)
        {
            int w = rng.Next(minRoomSize, maxRoomSize + 1);
            int d = rng.Next(minRoomSize, maxRoomSize + 1);

            if (w >= width - 2 || d >= depth - 2)
                continue;

            int x = rng.Next(1, width - w - 1);
            int z = rng.Next(1, depth - d - 1);

            Room candidate = new Room
            {
                x = x,
                z = z,
                w = w,
                d = d
            };

            bool overlaps = false;

            foreach (Room room in rooms)
            {
                bool separated =
                    candidate.x + candidate.w + 2 <= room.x ||
                    room.x + room.w + 2 <= candidate.x ||
                    candidate.z + candidate.d + 2 <= room.z ||
                    room.z + room.d + 2 <= candidate.z;

                if (!separated)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                rooms.Add(candidate);
                CarveRoom(candidate);
            }
        }

        if (rooms.Count == 0)
        {
            Room fallback = new Room
            {
                x = width / 2 - 3,
                z = depth / 2 - 3,
                w = 6,
                d = 6
            };

            rooms.Add(fallback);
            CarveRoom(fallback);
        }
    }

    private void CarveRoom(Room room)
    {
        for (int x = room.x; x < room.x + room.w; x++)
        {
            for (int z = room.z; z < room.z + room.d; z++)
            {
                floorGrid[x, z] = true;
            }
        }
    }

    private void ConnectRooms()
    {
        // Every room after the first connects to
        // its nearest previously generated room.
        for (int i = 1; i < rooms.Count; i++)
        {
            Room current = rooms[i];

            int nearest = 0;
            int shortestDistance = int.MaxValue;

            for (int j = 0; j < i; j++)
            {
                Room previous = rooms[j];

                int distance =
                    Mathf.Abs(current.CenterX - previous.CenterX) +
                    Mathf.Abs(current.CenterZ - previous.CenterZ);

                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearest = j;
                }
            }

            Room target = rooms[nearest];

            bool horizontalFirst = rng.Next(0, 2) == 0;

            if (horizontalFirst)
            {
                CarveHorizontal(
                    current.CenterX, target.CenterX, current.CenterZ
                );

                CarveVertical(
                    current.CenterZ, target.CenterZ, target.CenterX
                );
            }
            else
            {
                CarveVertical(
                    current.CenterZ, target.CenterZ, current.CenterX
                );

                CarveHorizontal(
                    current.CenterX, target.CenterX, target.CenterZ
                );
            }
        }
    }

    private void CarveHorizontal(int x1, int x2, int z)
    {
        int start = Mathf.Min(x1, x2);
        int end = Mathf.Max(x1, x2);

        for (int x = start; x <= end; x++)
        {
            for (int offset = 0; offset < corridorWidth; offset++)
            {
                int cz = z + offset - corridorWidth / 2;

                if (InsideGrid(x, cz))
                    floorGrid[x, cz] = true;
            }
        }
    }

    private void CarveVertical(int z1, int z2, int x)
    {
        int start = Mathf.Min(z1, z2);
        int end = Mathf.Max(z1, z2);

        for (int z = start; z <= end; z++)
        {
            for (int offset = 0; offset < corridorWidth; offset++)
            {
                int cx = x + offset - corridorWidth / 2;

                if (InsideGrid(cx, z))
                    floorGrid[cx, z] = true;
            }
        }
    }

    private bool InsideGrid(int x, int z)
    {
        return x >= 0 && x < width &&
               z >= 0 && z < depth;
    }

    private Vector3 CellPosition(int x, int z)
    {
        return new Vector3(
            (x - width / 2f + 0.5f) * cellSize,
            0,
            (z - depth / 2f + 0.5f) * cellSize
        );
    }

    private void BuildFloor()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (!floorGrid[x, z])
                    continue;

                Vector3 position = CellPosition(x, z);
                position.y = -floorThickness / 2f;

                CreateBlock(
                    floorPrefab,
                    "Stone Floor",
                    position,
                    new Vector3(cellSize, floorThickness, cellSize),
                    floorMaterial
                );
            }
        }
    }

    private void BuildWalls()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                if (!floorGrid[x, z])
                    continue;

                Vector3 center = CellPosition(x, z);

                if (!InsideGrid(x + 1, z) || !floorGrid[x + 1, z])
                    CreateWall(center + Vector3.right * cellSize / 2f,
                        cellSize, true);

                if (!InsideGrid(x - 1, z) || !floorGrid[x - 1, z])
                    CreateWall(center + Vector3.left * cellSize / 2f,
                        cellSize, true);

                if (!InsideGrid(x, z + 1) || !floorGrid[x, z + 1])
                    CreateWall(center + Vector3.forward * cellSize / 2f,
                        cellSize, false);

                if (!InsideGrid(x, z - 1) || !floorGrid[x, z - 1])
                    CreateWall(center + Vector3.back * cellSize / 2f,
                        cellSize, false);
            }
        }
    }

    private void CreateWall(Vector3 position, float length, bool alongZ)
    {
        position.y = wallHeight / 2f;

        Vector3 scale = alongZ
            ? new Vector3(wallThickness, wallHeight, length)
            : new Vector3(length, wallHeight, wallThickness);

        CreateBlock(
            wallPrefab,
            "Stone Wall",
            position,
            scale,
            wallMaterial
        );
    }

    private void CreateBlock(
        GameObject prefab,
        string objectName,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject obj;

        if (prefab != null)
        {
            obj = Instantiate(prefab, generatedRoot);
        }
        else
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.SetParent(generatedRoot, false);
        }

        obj.name = objectName;
        obj.transform.position = position;
        obj.transform.localScale = scale;

        Renderer renderer = obj.GetComponent<Renderer>();

        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        if (obj.GetComponent<Collider>() == null)
            obj.AddComponent<BoxCollider>();
    }

    private void CreateSpawnPoint()
    {
        Room firstRoom = rooms[0];

        GameObject spawn = new GameObject("PlayerSpawn");

        spawn.transform.SetParent(generatedRoot, false);
        spawn.transform.position = CellPosition(
            firstRoom.CenterX,
            firstRoom.CenterZ
        );

        Debug.Log("Player spawn point created!");
    }
}
