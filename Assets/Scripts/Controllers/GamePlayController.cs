using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamePlayController : MonoBehaviour
{
    [Header("Shared Moving Content (Obstacles & Wild Wisards)")]
    [SerializeField] private Transform MovingBoardContent;
    private List<Obstacle> activePlacedObstacles = new List<Obstacle>();
    private List<Wisard> activeWildWisards = new List<Wisard>();

    [Header("Obstacle Global Pool")]
    [SerializeField] private Obstacle ObstacklePrefab;
    [SerializeField] private Transform ObstaclePoolContent;
    [SerializeField] private int initialObstaclePoolCount = 15;
    private List<Obstacle> ObsteclePool; // All inactive obstacles live here

    [Header("Wisard Global Pool")]
    [SerializeField] private Wisard WisardPrefab;
    [SerializeField] private Transform WisardPoolContent;
    [SerializeField] private int initialWisardPoolCount = 15;
    private List<Wisard> WisardPool;     // All inactive wisards live here

    [Header("Wild Wisard Spawning")]
    [SerializeField] private float wildWisardSpawnInterval = 8f;
    private Coroutine wildWisardSpawnCoroutine;

    [Header("Controllers")]
    [SerializeField] private ConveyorBeltController conveyorBeltController;
    [SerializeField] private PlayerController playerController;

    [Header("Difficulty Scaling (For Conveyor Belt)")]
    [Tooltip("Time in seconds when obstacle spawning reaches 100% maximum difficulty.")]
    [SerializeField] private float timeToMaxDifficulty = 90f;

    [Header("Round & Score State")]
    private int currentRound;
    [SerializeField] private float currentSurvivalTime;
    private bool isRoundActive;

    public Action<float> RoundFinished;

    public float NormalizedDifficultyProgress =>
        timeToMaxDifficulty > 0f ? Mathf.Clamp01(currentSurvivalTime / timeToMaxDifficulty) : 1f;

    public bool IsRoundActive()
    {
        return isRoundActive;
    }

    public float GetCurrentSurvivalTime()
    {
        return currentSurvivalTime;
    }

    private void Awake()
    {
        currentRound = -1;
        currentSurvivalTime = 0f;
        isRoundActive = false;

        if (MovingBoardContent == null)
        {
            GameObject movingObj = new GameObject("MovingBoardContent");
            MovingBoardContent = movingObj.transform;
            MovingBoardContent.SetParent(transform, false);
        }

        // For Obstacle pool
        if (ObstaclePoolContent == null)
        {
            ObstaclePoolContent = transform;
        }

        ObsteclePool = new List<Obstacle>();

        foreach (Transform child in ObstaclePoolContent)
        {
            if (child.TryGetComponent(out Obstacle obs))
            {
                obs.gameObject.SetActive(false);
                ObsteclePool.Add(obs);
            }
        }

        while (ObsteclePool.Count < initialObstaclePoolCount)
        {
            Obstacle newObstacle = Instantiate(ObstacklePrefab, ObstaclePoolContent);
            newObstacle.gameObject.SetActive(false);
            ObsteclePool.Add(newObstacle);
        }

        // For Wisard pool
        if (WisardPoolContent == null)
        {
            WisardPoolContent = transform;
        }

        WisardPool = new List<Wisard>();

        foreach (Transform child in WisardPoolContent)
        {
            if (child.TryGetComponent(out Wisard wisard))
            {
                wisard.gameObject.SetActive(false);
                WisardPool.Add(wisard);
            }
        }

        while (WisardPool.Count < initialWisardPoolCount)
        {
            Wisard newWisard = Instantiate(WisardPrefab, WisardPoolContent);
            newWisard.gameObject.SetActive(false);
            WisardPool.Add(newWisard);
        }
    }

    private void OnEnable()
    {
        ConveyorBeltController.ObstacleRequested += HandleObstacleRequest;
        ConveyorBeltController.ConveyorBeltClean += HandleConveyorBeltClean;
        Obstacle.ObstaclePlacedOnBoard += HandleObstaclePlacedOnBoard;

        Border.OnObstacleHitBoundary += HandleObstacleHitBoundary;
        Border.OnWisardHitBoundary += HandleWisardHitBoundary;

        PlayerController.WisardExpired += ReturnWisardToPool;
        PlayerController.PlayerWisardsClean += HandlePlayerWisardsClean;
        PlayerController.WildWisardSpelled += HandleWildWisardSpelled;
        PlayerController.PlayerDied += EndCurrentRound;
        PlayerController.WildWisardConsumed += ReturnWisardToPool;
    }

    private void OnDisable()
    {
        ConveyorBeltController.ObstacleRequested -= HandleObstacleRequest;
        ConveyorBeltController.ConveyorBeltClean -= HandleConveyorBeltClean;
        Obstacle.ObstaclePlacedOnBoard -= HandleObstaclePlacedOnBoard;

        Border.OnObstacleHitBoundary -= HandleObstacleHitBoundary;
        Border.OnWisardHitBoundary -= HandleWisardHitBoundary;

        PlayerController.WisardExpired -= ReturnWisardToPool;
        PlayerController.PlayerWisardsClean -= HandlePlayerWisardsClean;
        PlayerController.WildWisardSpelled -= HandleWildWisardSpelled;
        PlayerController.PlayerDied -= EndCurrentRound;
        PlayerController.WildWisardConsumed -= ReturnWisardToPool;
    }

    private void Start()
    {
        if (conveyorBeltController == null)
        {
            Debug.LogError("ASSIGN THE CONVEYOR_BELT!!!!");
        }

        if (playerController == null)
        {
            Debug.LogError("ASSIGN THE PLAYER_CONTROLLER!!!!");
        }
    }

    private void Update()
    {
        if (!isRoundActive) return;

        currentSurvivalTime += Time.deltaTime;
        conveyorBeltController.UpdateDifficulty(NormalizedDifficultyProgress);

        // Move all placed obstacles and wild Wisards toward the left
        if (GameManager.Instance != null && MovingBoardContent != null)
        {
            float speed = GameManager.Instance.GetCurrentSpeed();
            MovingBoardContent.position += Vector3.left * speed * Time.deltaTime;
        }
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void HandleObstacleRequest()
    {
        Obstacle obs = GetObstaclePrefab();
        conveyorBeltController.SpawnObstacle(obs);
    }

    private void HandleObstaclePlacedOnBoard(Obstacle placedObstacle)
    {
        if (placedObstacle == null) return;

        placedObstacle.transform.SetParent(MovingBoardContent, true);

        if (!activePlacedObstacles.Contains(placedObstacle))
        {
            activePlacedObstacles.Add(placedObstacle);
        }
    }

    private void HandleConveyorBeltClean(List<Obstacle> obstacles)
    {
        if (obstacles == null) return;

        foreach (Obstacle obs in obstacles)
        {
            ReturnObstacleToPool(obs);
        }
    }

    private void HandleObstacleHitBoundary(Obstacle obs)
    {
        ReturnObstacleToPool(obs);
    }

    private void HandleWisardHitBoundary(Wisard wisard)
    {
        ReturnWisardToPool(wisard);
    }

    private void HandlePlayerWisardsClean(List<Wisard> wisards)
    {
        if (wisards == null) return;

        foreach (Wisard wisard in wisards)
        {
            ReturnWisardToPool(wisard);
        }
    }

    private void HandleWildWisardSpelled(Wisard spelledWisard)
    {
        activeWildWisards.Remove(spelledWisard);
    }

    // =========================================================================
    // WILD WISARD SPAWNING (Random Lane Spawn Points -> MovingBoardContent)
    // =========================================================================

    private IEnumerator WildWisardSpawnRoutine()
    {
        while (isRoundActive)
        {
            yield return new WaitForSeconds(wildWisardSpawnInterval);

            if (isRoundActive)
            {
                SpawnWildWisardInRandomLane();
            }
        }
    }

    private void SpawnWildWisardInRandomLane()
    {
        Lane randomLane = playerController.GetRandomLane();
        if (randomLane == null || randomLane.WisardSpawnPoint == null) return;

        Wisard wildWisard = GetWisardPrefab();
        wildWisard.transform.position = randomLane.WisardSpawnPoint.position;
        wildWisard.transform.SetParent(MovingBoardContent, true);
        wildWisard.gameObject.SetActive(true);

        activeWildWisards.Add(wildWisard);
    }

    private void CleanMovingBoardContent()
    {
        for (int i = activeWildWisards.Count - 1; i >= 0; i--)
        {
            ReturnWisardToPool(activeWildWisards[i]);
        }
        activeWildWisards.Clear();

        for (int i = activePlacedObstacles.Count - 1; i >= 0; i--)
        {
            ReturnObstacleToPool(activePlacedObstacles[i]);
        }
        activePlacedObstacles.Clear();

        if (MovingBoardContent != null)
        {
            MovingBoardContent.localPosition = Vector3.zero;
        }
    }

    // =========================================================================
    // POOL METHODS (OBSTACLES & WISARDS)
    // =========================================================================

    public Obstacle GetObstaclePrefab()
    {
        if (ObsteclePool.Count > 0)
        {
            Obstacle result = ObsteclePool[0];
            ObsteclePool.RemoveAt(0);
            return result;
        }

        Obstacle newObstacle = Instantiate(ObstacklePrefab, ObstaclePoolContent);
        newObstacle.gameObject.SetActive(false);
        return newObstacle;
    }

    public void ReturnObstacleToPool(Obstacle obs)
    {
        if (obs == null) return;

        activePlacedObstacles.Remove(obs);
        obs.ResetObstacleState();
        obs.gameObject.SetActive(false);
        obs.transform.SetParent(ObstaclePoolContent, true);

        if (!ObsteclePool.Contains(obs))
        {
            ObsteclePool.Add(obs);
        }
        else
        {
            Debug.LogError("Why this obstacle is still a child of this class?");
        }
    }

    public void RemoveObstacleFromBoard(Obstacle obstacle)
    {
        if (obstacle == null)
        {
            Debug.LogError("Cannot remove a null obstacle from board.");
            return;
        }

        ReturnObstacleToPool(obstacle);
    }

    public Wisard GetWisardPrefab()
    {
        if (WisardPool.Count > 0)
        {
            Wisard result = WisardPool[0];
            WisardPool.RemoveAt(0);
            return result;
        }

        Wisard newWisard = Instantiate(WisardPrefab, WisardPoolContent);
        newWisard.gameObject.SetActive(false);
        return newWisard;
    }

    public void ReturnWisardToPool(Wisard wisard)
    {
        if (wisard == null) return;

        activeWildWisards.Remove(wisard);
        wisard.ResetWisard();
        wisard.gameObject.SetActive(false);
        wisard.transform.SetParent(WisardPoolContent, true);

        if (!WisardPool.Contains(wisard))
        {
            WisardPool.Add(wisard);
        }
        else
        {
            Debug.LogError("Why this wisard is still a child of this class?");
        }
    }

    // =========================================================================
    // ROUND START & END
    // =========================================================================

    public void StartRound(int roundNumber)
    {
        currentRound = roundNumber;
        currentSurvivalTime = 0f;
        isRoundActive = true;

        if (MovingBoardContent != null)
        {
            MovingBoardContent.localPosition = Vector3.zero;
        }

        // 1. Pull the first Wisard from the pool and spawn it at the middle lane's WisarPosition
        Wisard firstWisard = GetWisardPrefab();
        playerController.ActivatePlayer(firstWisard);

        // 2. Start spawning wild Wisards at random lane spawn points
        if (wildWisardSpawnCoroutine != null)
        {
            StopCoroutine(wildWisardSpawnCoroutine);
        }
        wildWisardSpawnCoroutine = StartCoroutine(WildWisardSpawnRoutine());

        // 3. Activate the conveyor belt
        conveyorBeltController.ActivateBelt();
    }

    public void EndCurrentRound()
    {
        if (!isRoundActive) return;
        isRoundActive = false;

        // 1. Stop wild Wisard spawning and clean all moving board objects
        if (wildWisardSpawnCoroutine != null)
        {
            StopCoroutine(wildWisardSpawnCoroutine);
            wildWisardSpawnCoroutine = null;
        }
        CleanMovingBoardContent();

        // 2. Send all PlayerController Wisards back to pool
        playerController.InactivatePlayer();

        // 3. Stop conveyor belt and return belt obstacles to pool
        conveyorBeltController.InactivateBelt();

        // 4. Notify GameManager with the final survival time
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleRoundFinished(currentSurvivalTime);
        }

        RoundFinished?.Invoke(currentSurvivalTime);
    }

    public IReadOnlyList<Obstacle> GetActivePlacedObstacles()
    {
        return activePlacedObstacles;
    }
}