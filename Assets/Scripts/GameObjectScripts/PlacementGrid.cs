using UnityEngine;

public class PlacementGrid : MonoBehaviour
{
    [SerializeField] private float gridStartX = -10f;
    [SerializeField] private int columnCount = 20;
    [SerializeField] private int laneCount = 4;
    [SerializeField] private float cellSize = 1.5f;
    [SerializeField] private Transform topLane;

    public float CellSize => cellSize;
    public float GridStartX => gridStartX;
    public int ColumnCount => columnCount;

    public int WorldXToColumn(float worldX)
    {
        float relativeX = worldX - gridStartX;
        return Mathf.FloorToInt(relativeX / cellSize);
    }

    public float ColumnToWorldX(int column)
    {
        return gridStartX + (column * cellSize) + (cellSize / 2f);
    }

    public bool IsValidColumn(int column)
    {
        return column >= 0 && column < columnCount;
    }

    public int WorldXToStartColumn(float worldX, int widthInCells)
    {
        float relativeX = (worldX - gridStartX) / cellSize;

        return Mathf.RoundToInt(
            relativeX - (widthInCells / 2f)
        );
    }

    public int WorldYToStartLane(float worldY, int heightInLanes)
    {
        float gridTopY =
            topLane.position.y + (cellSize / 2f);

        float relativeY =
            (gridTopY - worldY) / cellSize;

        return Mathf.RoundToInt(
            relativeY - (heightInLanes / 2f)
        );
    }

    public Vector2 GetPlacementCenter(
        int startLane,
        int startColumn,
        int widthInCells,
        int heightInLanes)
    {
        float x =
            gridStartX +
            (startColumn * cellSize) +
            (widthInCells * cellSize / 2f);

        float gridTopY =
            topLane.position.y + (cellSize / 2f);

        float y =
            gridTopY -
            (startLane * cellSize) -
            (heightInLanes * cellSize / 2f);

        return new Vector2(x, y);
    }

    public bool IsInsideGrid(
        int startLane,
        int startColumn,
        int widthInCells,
        int heightInLanes)
    {
        if (startLane < 0 || startColumn < 0)
        {
            return false;
        }

        if (startLane + heightInLanes > laneCount)
        {
            return false;
        }

        if (startColumn + widthInCells > columnCount)
        {
            return false;
        }

        return true;
    }
}