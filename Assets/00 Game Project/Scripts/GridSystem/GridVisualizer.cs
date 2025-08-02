using UnityEngine;
using System.Collections.Generic;

public class GridVisualizer : MonoBehaviour
{
    [Header("Grid Visualization")]
    public bool showGrid = true;
    public int gridWidth = 50;
    public int gridHeight = 50;
    public Color gridColor = Color.white;
    public float lineAlpha = 0.3f;

    [Header("Runtime Visualization")]
    public bool showInPlayMode = true;
    public Material lineMaterial;

    [Header("Grid Center")]
    public bool centerGrid = true;
    public Vector3Int gridCenter = Vector3Int.zero;

    private Grid grid;
    private List<LineRenderer> gridLines = new List<LineRenderer>();
    private GameObject gridLinesParent;

    void Start()
    {
        grid = GetComponent<Grid>();

        if (showInPlayMode)
        {
            CreateRuntimeGrid();
        }
    }

    void CreateRuntimeGrid()
    {
        gridLinesParent = new GameObject("Grid Lines");
        gridLinesParent.transform.SetParent(transform);

        if (lineMaterial == null)
        {
            lineMaterial = CreateDefaultLineMaterial();
        }

        int startX = centerGrid ? gridCenter.x - gridWidth / 2 : 0;
        int endX = centerGrid ? gridCenter.x + gridWidth / 2 : gridWidth;
        int startZ = centerGrid ? gridCenter.z - gridHeight / 2 : 0;
        int endZ = centerGrid ? gridCenter.z + gridHeight / 2 : gridHeight;

        // Horizontale lijnen
        for (int z = startZ; z <= endZ; z++)
        {
            Vector3 start = grid.CellToWorld(new Vector3Int(startX, 0, z));
            Vector3 end = grid.CellToWorld(new Vector3Int(endX, 0, z));
            CreateLine(start, end);
        }

        // Verticale lijnen
        for (int x = startX; x <= endX; x++)
        {
            Vector3 start = grid.CellToWorld(new Vector3Int(x, 0, startZ));
            Vector3 end = grid.CellToWorld(new Vector3Int(x, 0, endZ));
            CreateLine(start, end);
        }
    }

    void CreateLine(Vector3 start, Vector3 end)
    {
        GameObject lineObj = new GameObject("Grid Line");
        lineObj.transform.SetParent(gridLinesParent.transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = lineMaterial;

        // Unity 6: Gebruik colorGradient in plaats van color
        Gradient gradient = new Gradient();
        Color lineColor = new Color(gridColor.r, gridColor.g, gridColor.b, lineAlpha);
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(lineColor, 0.0f), new GradientColorKey(lineColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(lineAlpha, 0.0f), new GradientAlphaKey(lineAlpha, 1.0f) }
        );
        lr.colorGradient = gradient;

        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        gridLines.Add(lr);
    }

    Material CreateDefaultLineMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(gridColor.r, gridColor.g, gridColor.b, lineAlpha);
        return mat;
    }

    public void ToggleGridVisibility()
    {
        showInPlayMode = !showInPlayMode;
        if (gridLinesParent != null)
        {
            gridLinesParent.SetActive(showInPlayMode);
        }
    }

    public void RegenerateGrid()
    {
        ClearRuntimeGrid();
        if (showInPlayMode)
        {
            CreateRuntimeGrid();
        }
    }

    void ClearRuntimeGrid()
    {
        if (gridLinesParent != null)
        {
            DestroyImmediate(gridLinesParent);
            gridLines.Clear();
        }
    }

    void OnDrawGizmos()
    {
        if (!showGrid) return;

        grid = grid ?? GetComponent<Grid>();
        if (grid == null) return;

        Color originalColor = Gizmos.color;
        Gizmos.color = new Color(gridColor.r, gridColor.g, gridColor.b, lineAlpha);

        int startX = centerGrid ? gridCenter.x - gridWidth / 2 : 0;
        int endX = centerGrid ? gridCenter.x + gridWidth / 2 : gridWidth;
        int startZ = centerGrid ? gridCenter.z - gridHeight / 2 : 0;
        int endZ = centerGrid ? gridCenter.z + gridHeight / 2 : gridHeight;

        // Horizontale lijnen
        for (int z = startZ; z <= endZ; z++)
        {
            Vector3 start = grid.CellToWorld(new Vector3Int(startX, 0, z));
            Vector3 end = grid.CellToWorld(new Vector3Int(endX, 0, z));
            Gizmos.DrawLine(start, end);
        }

        // Verticale lijnen
        for (int x = startX; x <= endX; x++)
        {
            Vector3 start = grid.CellToWorld(new Vector3Int(x, 0, startZ));
            Vector3 end = grid.CellToWorld(new Vector3Int(x, 0, endZ));
            Gizmos.DrawLine(start, end);
        }

        Gizmos.color = originalColor;
    }

    void OnDestroy()
    {
        ClearRuntimeGrid();
    }
}
