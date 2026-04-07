using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrowingTree : MonoBehaviour
{
    public int cols = 20, rows = 20;
    public float cellSize = 1f;
    public GameObject wallPrefab, floorPrefab;
    public float stepDelay = 0.05f;

    bool[,] visited;
    GameObject[,,] wallObjects;

    List<Vector2Int> active = new();

    static readonly Vector2Int[] Dirs = {
        Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left
    };
    static readonly int[] Opposite = { 1, 0, 3, 2 };

    void Start() => StartCoroutine(Generate());

    IEnumerator Generate()
    {
        visited = new bool[cols, rows];
        wallObjects = new GameObject[cols, rows, 4];

        // Spawn all floors and walls upfront
        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                Vector3 pos = new Vector3(c * cellSize, 0, r * cellSize);
                Instantiate(floorPrefab, pos, Quaternion.identity, transform);

                // North wall
                wallObjects[c, r, 0] = Instantiate(wallPrefab,
                    pos + new Vector3(0, 0.5f, cellSize * 0.5f),
                    Quaternion.identity, transform);

                // South wall
                wallObjects[c, r, 1] = Instantiate(wallPrefab,
                    pos + new Vector3(0, 0.5f, -cellSize * 0.5f),
                    Quaternion.identity, transform);

                // East wall
                wallObjects[c, r, 2] = Instantiate(wallPrefab,
                    pos + new Vector3(cellSize * 0.5f, 0.5f, 0),
                    Quaternion.Euler(0, 90, 0), transform);

                // West wall
                wallObjects[c, r, 3] = Instantiate(wallPrefab,
                    pos + new Vector3(-cellSize * 0.5f, 0.5f, 0),
                    Quaternion.Euler(0, 90, 0), transform);
            }
        }

        Vector2Int start = new(0, 0);
        visited[start.x, start.y] = true;
        active.Add(start);

        while (active.Count > 0)
        {
            // Newest = last element
            int idx = active.Count - 1;
            Vector2Int cell = active[idx];
            var nbrs = GetUnvisitedNeighbors(cell);

            if (nbrs.Count == 0)
            {
                active.RemoveAt(idx);
            }
            else
            {
                int pick = Random.Range(0, nbrs.Count);
                var (next, dir) = nbrs[pick];

                // Remove wall between cell and next
                RemoveWall(cell.x, cell.y, dir);
                RemoveWall(next.x, next.y, Opposite[dir]);

                visited[next.x, next.y] = true;
                active.Add(next);
            }

            yield return new WaitForSeconds(stepDelay);
        }
    }

    void RemoveWall(int c, int r, int dir)
    {
        if (wallObjects[c, r, dir] != null)
        {
            Destroy(wallObjects[c, r, dir]);
            wallObjects[c, r, dir] = null;
        }
    }

    List<(Vector2Int cell, int dir)> GetUnvisitedNeighbors(Vector2Int c)
    {
        var result = new List<(Vector2Int, int)>();
        for (int d = 0; d < 4; d++)
        {
            Vector2Int n = c + Dirs[d];
            if (n.x >= 0 && n.x < cols && n.y >= 0 && n.y < rows && !visited[n.x, n.y])
                result.Add((n, d));
        }
        return result;
    }
}