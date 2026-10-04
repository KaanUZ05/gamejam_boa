using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class Obstacle : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private Camera mainCamera;
    private BoxCollider2D obstacleCollider;
    private PlacementGrid placementGrid;
    private Transform previousParent;
    private Vector3 previousPosition;
    private int placedStartLane = -1;
    private int placedStartColumn = -1;

    public int PlacedStartLane => placedStartLane;
    public int PlacedStartColumn => placedStartColumn;
    public int WidthInCells => data != null ? data.widthInCells : 0;
    public int HeightInLanes => data != null ? data.heightInLanes : 0;
    public int DamageAmount
    {
        get
        {
            if (data == null)
            {
                Debug.LogError("ObstacleData is null. Cannot get damage amount.");
                return 0;
            }

            return data.damageAmount;
        }
    }

    [SerializeField] private ObstacleData data;
    [SerializeField] private SpriteRenderer closedSpriteRenderer;
    [SerializeField] private SpriteRenderer openSpriteRenderer;
    [SerializeField] private Transform openVisualTransform;
    [SerializeField] private LayerMask laneLayerMask;
    [SerializeField] private LayerMask inventoryLayerMask;
    [SerializeField] private Transform closedVisualTransform;
    [Header("Conveyor Visual Size")]
    [SerializeField] private float conveyorMaxWidth = 0.9f;
    [SerializeField] private float conveyorMaxHeight = 0.9f;
    private bool isOnDrag = false;

    // Informs GamePlayController to parent this obstacle to MovingBoardContent
    public static Action<Obstacle> ObstaclePlacedOnBoard;

    private void Awake()
    {
        mainCamera = Camera.main;
        obstacleCollider = GetComponent<BoxCollider2D>();
        placementGrid = FindFirstObjectByType<PlacementGrid>();
    }

    private void OnEnable()
    {
        Border.OnObstacleHitBoundary += HandleObstacleHitBoundary;
    }

    private void OnDisable()
    {
        Border.OnObstacleHitBoundary -= HandleObstacleHitBoundary;
    }

    private void Start()
    {
        if (data != null)
        {
            Initialize(data);
        }
    }

    public void Initialize(ObstacleData data)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (obstacleCollider == null)
        {
            obstacleCollider = GetComponent<BoxCollider2D>();
        }

        if (placementGrid == null)
        {
            placementGrid = FindFirstObjectByType<PlacementGrid>();
        }

        this.data = data;

        transform.localScale = Vector3.one;

        ApplyClosedVisual();

        openSpriteRenderer.sprite = data.obstacleSprite;

        closedSpriteRenderer.gameObject.SetActive(true);
        openSpriteRenderer.gameObject.SetActive(false);

        openVisualTransform.localScale = Vector3.one;

        if (obstacleCollider != null)
        {
            obstacleCollider.size = Vector2.one;
            obstacleCollider.enabled = true;
        }
    }

    private void HandleObstacleHitBoundary(Obstacle hitObstacle)
    {
        // Make sure only the specific obstacle that hit the border resets itself
        if (hitObstacle != this) return;

        ResetObstacleState();
    }


    public void ResetObstacleState()
    {
        isOnDrag = false;

        placedStartLane = -1;
        placedStartColumn = -1;

        if (data != null)
        {
            if (closedSpriteRenderer != null)
            {
                ApplyClosedVisual();
                closedSpriteRenderer.gameObject.SetActive(true);
            }

            if (openSpriteRenderer != null)
            {
                openSpriteRenderer.sprite = data.obstacleSprite;
                openSpriteRenderer.gameObject.SetActive(false);
            }
        }

        if (openVisualTransform != null)
        {
            openVisualTransform.localScale = Vector3.one;
        }

        transform.localScale = Vector3.one;

        if (obstacleCollider != null)
        {
            obstacleCollider.size = Vector2.one;
            obstacleCollider.enabled = true;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isOnDrag = false;
        previousParent = transform.parent;
        previousPosition = transform.position;

        if (previousParent == null)
        {
            Debug.Log("Obstacle cannot be without a parent!!");
            return;
        }
        else if (previousParent.TryGetComponent(out InventoryController inventory))
        {
            Debug.Log("Obstacle was on an inventory.");
            inventory.RemoveObstacleFromInventory();
        }
        else if (previousParent.TryGetComponent(out ConveyorBeltController belt))
        {
            Debug.Log("Obstacle was on the belt.");
            belt.RemoveObstacleFromBelt(this);
        }
        else
        {
            // Once placed on the board, its parent is MovingBoardContent, 
            // so it cannot be picked up or dragged again.
            Debug.LogError("Wrong hierarchy!!!");
            return;
        }

        ApplyObstacleVisual();

        obstacleCollider.size = new Vector2(
            data.widthInCells * placementGrid.CellSize,
            data.heightInLanes * placementGrid.CellSize
        );

        isOnDrag = true;
        transform.SetParent(null, true);

        // Drag sırasında obstacle kendi collider'ına takılmasın.
        obstacleCollider.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isOnDrag)
        {
            return;
        }

        transform.position = ScreenToWorld2D(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isOnDrag) return;

        Vector2 dropPosition = transform.position;

        Collider2D hitLane =
            Physics2D.OverlapPoint(dropPosition, laneLayerMask);

        if (hitLane != null)
        {
            Debug.Log("Lane detected: " + hitLane.name);
            if (TryPlaceObstacle())
            {
                PlaceOnGrid();

                // Notify the belt using previousParent (since transform.parent is null while dragging)
                if (previousParent != null && previousParent.TryGetComponent(out ConveyorBeltController belt))
                {
                    belt.ConfirmObstaclePlacement(this);
                }

                obstacleCollider.enabled = true;
                isOnDrag = false;

                // GamePlayController listens to this and sets parent to MovingBoardContent
                ObstaclePlacedOnBoard?.Invoke(this);
                return;
            }
        }
        else
        {
            Debug.Log("No lane detected.");
        }

        Collider2D hitInventory =
            Physics2D.OverlapPoint(dropPosition, inventoryLayerMask);

        if (hitInventory != null)
        {
            Debug.Log("Obstacle was placed on an inventory.");

            if (previousParent != null && previousParent.TryGetComponent(out ConveyorBeltController belt))
            {
                belt.ConfirmObstaclePlacement(this);
            }

            transform.SetParent(hitInventory.transform, true);

            obstacleCollider.enabled = true;
            isOnDrag = false;
            return;
        }

        ReturnToPreviousPosition();
        obstacleCollider.enabled = true;
        isOnDrag = false;
    }

    private Vector3 ScreenToWorld2D(Vector2 screenPosition)
    {
        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    -mainCamera.transform.position.z
                )
            );

        worldPosition.z = 0f;

        return worldPosition;
    }

    private bool TryPlaceObstacle()
    {
        int startLane =
            placementGrid.WorldYToStartLane(
                transform.position.y,
                data.heightInLanes
            );

        int startColumn =
            placementGrid.WorldXToStartColumn(
                transform.position.x,
                data.widthInCells
            );

        // Önce grid sınırları içerisinde mi?
        if (!placementGrid.IsInsideGrid(
                startLane,
                startColumn,
                data.widthInCells,
                data.heightInLanes))
        {
            return false;
        }

        Vector2 snappedPosition =
            placementGrid.GetPlacementCenter(
                startLane,
                startColumn,
                data.widthInCells,
                data.heightInLanes
            );

        Vector2 checkSize = new Vector2(
            data.widthInCells * placementGrid.CellSize * 0.95f,
            data.heightInLanes * placementGrid.CellSize * 0.95f
        );

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                snappedPosition,
                checkSize,
                0f
            );

        for (int i = 0; i < hits.Length; i++)
        {
            Obstacle otherObstacle =
                hits[i].GetComponentInParent<Obstacle>();

            if (otherObstacle == null)
            {
                continue;
            }

            if (otherObstacle == this)
            {
                continue;
            }

            if (!otherObstacle.gameObject.activeInHierarchy)
            {
                continue;
            }

            // Board'a gerçekten yerleştirilmiş obstacle ise placement'ı reddet.
            if (otherObstacle.PlacedStartLane >= 0)
            {
                return false;
            }
        }

        return true;
    }

    private void PlaceOnGrid()
    {
        int startLane =
            placementGrid.WorldYToStartLane(
                transform.position.y,
                data.heightInLanes
            );

        int startColumn =
            placementGrid.WorldXToStartColumn(
                transform.position.x,
                data.widthInCells
            );

        Vector2 snappedPosition =
            placementGrid.GetPlacementCenter(
                startLane,
                startColumn,
                data.widthInCells,
                data.heightInLanes
            );

        transform.position = new Vector3(
            snappedPosition.x,
            snappedPosition.y,
            0f
        );

        placedStartLane = startLane;
        placedStartColumn = startColumn;
    }

    private void ReturnToPreviousPosition()
    {
        // If it came from the belt, return it to the moving placeholder's position!
        if (previousParent != null && previousParent.TryGetComponent(out ConveyorBeltController belt))
        {
            belt.ReturnObstacleToPlaceholder(this);
        }
        else
        {
            transform.SetParent(previousParent, true);
            transform.position = previousPosition;
        }
        closedSpriteRenderer.gameObject.SetActive(true);
        openSpriteRenderer.gameObject.SetActive(false);

        openVisualTransform.localScale = Vector3.one;

        obstacleCollider.size = Vector2.one; // extra security
    }

    private void ApplyObstacleVisual()
    {
        if (data == null)
        {
            Debug.LogError("ObstacleData is null.");
            return;
        }

        if (data.obstacleSprite == null)
        {
            Debug.LogError(
                "Obstacle Sprite is null for obstacle data: " + data.name
            );
            return;
        }

        closedSpriteRenderer.gameObject.SetActive(false);
        openSpriteRenderer.gameObject.SetActive(true);

        openSpriteRenderer.sprite = data.obstacleSprite;

        Vector2 spriteSize = openSpriteRenderer.sprite.bounds.size;

        float targetWidth =
            data.widthInCells * placementGrid.CellSize;

        float targetHeight =
            data.heightInLanes * placementGrid.CellSize;

        openVisualTransform.localScale = new Vector3(
            targetWidth / spriteSize.x,
            targetHeight / spriteSize.y,
            1f
        );
    }

    private void ApplyClosedVisual()
    {
        if (data == null || data.conveyorSprite == null)
        {
            return;
        }

        closedSpriteRenderer.sprite = data.conveyorSprite;

        Vector2 spriteSize =
            closedSpriteRenderer.sprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        float scaleX =
            conveyorMaxWidth / spriteSize.x;

        float scaleY =
            conveyorMaxHeight / spriteSize.y;

        // Aspect ratio bozulmasın.
        float uniformScale =
            Mathf.Min(scaleX, scaleY);

        closedVisualTransform.localScale =
            new Vector3(
                uniformScale,
                uniformScale,
                1f
            );
    }
}