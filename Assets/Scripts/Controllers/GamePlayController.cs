using System;
using System.Collections.Generic;
using UnityEngine;

public class GamePlayController : MonoBehaviour
{
    [Header("Obstacle Global Pool")]
    [SerializeField] private Obstacle ObstacklePrefab;
    [SerializeField] private Transform ObstaclePoolContent;
    [SerializeField] private int initialObstaclePoolCount = 15;
    private List<Obstacle> ObsteclePool; // All inactive obstacles live here

    [Header("Wisard Global Pool")]
    [SerializeField] private Wisard WisardPrefab;
    [SerializeField] private Transform WisardPoolContent;
    [SerializeField] private int initialWisardPoolCount = 15;
    private List<Wisard> WisardPool; // All inactive wisards live here

    [Header("Controllers")]
    [SerializeField] private ConveyorBeltController conveyorBeltController;

    [Header("Lanes")]
    [SerializeField] private Lane lanes;

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

    private void Awake()
    {
        currentRound = -1;
        currentSurvivalTime = 0f;
        isRoundActive = false;

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
        Border.OnObstacleHitBoundary += HandleObstacleHitBoundary;
    }

    private void OnDisable()
    {
        ConveyorBeltController.ObstacleRequested -= HandleObstacleRequest;
        ConveyorBeltController.ConveyorBeltClean -= HandleConveyorBeltClean;
        Border.OnObstacleHitBoundary -= HandleObstacleHitBoundary;
    }

    private void Start()
    {
        if (conveyorBeltController == null)
        {
            Debug.LogError("ASSIGN THE CONVEYOR_BELT!!!!");
        }
    }

    private void Update()
    {
        if (!isRoundActive) return;

        currentSurvivalTime += Time.deltaTime;
        conveyorBeltController.UpdateDifficulty(NormalizedDifficultyProgress);
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    /// <summary>
    /// Handler for ConveyorBeltController.ObstacleRequested
    /// </summary>
    private void HandleObstacleRequest()
    {
        Obstacle obs = GetObstaclePrefab();
        conveyorBeltController.SpawnObstacle(obs);
    }

    /// <summary>
    /// Handler for ConveyorBeltController.ConveyorBeltClean
    /// </summary>
    private void HandleConveyorBeltClean(List<Obstacle> obstacles)
    {
        if (obstacles == null) return;

        foreach (Obstacle obs in obstacles)
        {
            ReturnObstacleToPool(obs);
        }
    }

    /// <summary>
    /// Handler for Border.OnObstacleHitBoundary
    /// </summary>
    private void HandleObstacleHitBoundary(Obstacle obs)
    {
        ReturnObstacleToPool(obs);
    }

    // =========================================================================
    // POOL METHODS
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

    public void StartRound(int roundNumber)
    {
        currentRound = roundNumber;
        currentSurvivalTime = 0f;
        isRoundActive = true;

        conveyorBeltController.ActivateBelt();
    }

    public void EndCurrentRound()
    {
        if (!isRoundActive) return;
        isRoundActive = false;

        conveyorBeltController.InactivateBelt();

        RoundFinished?.Invoke(currentSurvivalTime);
    }
}