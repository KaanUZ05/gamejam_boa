using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConveyorBeltController : MonoBehaviour
{
    private List<Obstacle> ActiveObstacles;

    [Header("References & Prefab")]
    [SerializeField] private Obstacle ObstacklePrefab;
    [SerializeField] private Obstacle placeholderObstacle;

    [Header("Belt Movement & Capacity")]
    [SerializeField] private float beltSpeed = 3f;
    [SerializeField] private float itemSpacing = 1.2f;
    [SerializeField] private int maxCapacity = 6;

    [Header("Spawning")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform endPoint;

    [Header("Spawn Progression Tuning")]
    [SerializeField] private float lowTierBias = 2.0f;
    [SerializeField] private float lateGameBoost = 2.5f;

    private Obstacle currentlyDraggedObstacle;

    private bool isBeltActive; // GamePlayController controls that activity.
    private float currentDifficultyProgress = 0f;
    private Coroutine spawnCoroutine;

    public static Action ObstacleRequested;                    // Asks GamePlayController to send 1 obstacle
    public static Action<List<Obstacle>> ConveyorBeltClean;    // Sends remaining belt obstacles back to pool

    private void Awake()
    {
        isBeltActive = false;
        ActiveObstacles = new List<Obstacle>();

        if (placeholderObstacle == null && ObstacklePrefab != null)
        {
            placeholderObstacle = CreatePlaceholderFromPrefab();
        }
        else if (placeholderObstacle != null)
        {
            placeholderObstacle.gameObject.SetActive(false);
        }
    }

    private Obstacle CreatePlaceholderFromPrefab()
    {
        Obstacle placeholder = Instantiate(ObstacklePrefab, transform);
        placeholder.name = "Placeholder_Obstacle";

        SpriteRenderer sr = placeholder.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        if (placeholder.TryGetComponent(out Collider2D col)) col.enabled = false;

        placeholder.gameObject.SetActive(false);
        return placeholder;
    }

    public void ActivateBelt()
    {
        ResetBelt();
        currentDifficultyProgress = 0f;
        isBeltActive = true;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    public void InactivateBelt()
    {
        isBeltActive = false;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        ResetBelt();
    }

    public void UpdateDifficulty(float progress)
    {
        currentDifficultyProgress = progress;
    }

    private IEnumerator SpawnRoutine()
    {
        while (isBeltActive)
        {
            yield return new WaitForSeconds(spawnInterval);

            if (CanSpawn())
            {
                ObstacleRequested?.Invoke(); // <-- Asks GamePlayController for an obstacle!
            }
        }
    }

    private bool CanSpawn()
    {
        if (!isBeltActive) return false;

        if (ActiveObstacles.Count >= maxCapacity)
        {
            return false;
        }

        if (ActiveObstacles.Count > 0)
        {
            Obstacle newestObstacle = ActiveObstacles[ActiveObstacles.Count - 1];
            float distFromSpawn = Mathf.Abs(newestObstacle.transform.position.x - spawnPoint.position.x);
            if (distFromSpawn < itemSpacing)
            {
                return false;
            }
        }

        return true;
    }

    public void SpawnObstacle(Obstacle nextObstacle)
    {
        if (nextObstacle == null) return;

        nextObstacle.transform.SetParent(transform, true);

        Vector3 spawnPos = spawnPoint.position;
        spawnPos.z = 0f;
        nextObstacle.transform.position = spawnPos;

        // ÖNCE yeni data'yı ver.
        if (DataManager.Instance != null)
        {
            int selectedId = GetWeightedObstacleId();
            ObstacleData data =
                DataManager.Instance.GetObstacleDataById(selectedId);

            nextObstacle.Initialize(data);
        }

        // EN SON görünür yap.
        nextObstacle.gameObject.SetActive(true);

        ActiveObstacles.Add(nextObstacle);
    }

    private int GetWeightedObstacleId()
    {
        int count = DataManager.Instance != null ? DataManager.Instance.GetObstacleCount() : 0;
        if (count <= 1) return 0;

        int maxUnlockedId = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Lerp(1f, count - 1, currentDifficultyProgress)),
            1,
            count - 1
        );

        float totalWeight = 0f;
        float[] weights = new float[maxUnlockedId + 1];

        for (int id = 0; id <= maxUnlockedId; id++)
        {
            float tierNormalized = (float)id / (count - 1);
            float baseWeight = Mathf.Pow(1f - tierNormalized * 0.75f, lowTierBias);
            float timeMultiplier = Mathf.Lerp(
                1f - tierNormalized,
                1f + (tierNormalized * lateGameBoost),
                currentDifficultyProgress
            );

            weights[id] = Mathf.Max(0.01f, baseWeight * timeMultiplier);
            totalWeight += weights[id];
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int id = 0; id <= maxUnlockedId; id++)
        {
            cumulative += weights[id];
            if (roll <= cumulative)
            {
                return id;
            }
        }

        return 0;
    }

    private void Update()
    {
        if (!isBeltActive) return;
        MoveAndCumulateBelt();
    }

    private void MoveAndCumulateBelt()
    {
        for (int i = 0; i < ActiveObstacles.Count; i++)
        {
            Obstacle current = ActiveObstacles[i];

            float limitX = (i == 0)
                ? endPoint.position.x
                : ActiveObstacles[i - 1].transform.position.x - itemSpacing;

            Vector3 pos = current.transform.position;
            pos.x = Mathf.MoveTowards(pos.x, limitX, beltSpeed * Time.deltaTime);
            pos.y = spawnPoint.position.y;
            pos.z = 0f;

            current.transform.position = pos;
        }
    }

    public void RemoveObstacleFromBelt(Obstacle item)
    {
        if (currentlyDraggedObstacle != null && currentlyDraggedObstacle != item)
        {
            Debug.LogError("Why you have more than one Obstacles free??????????");
        }

        int index = ActiveObstacles.IndexOf(item);
        if (index < 0) return;

        currentlyDraggedObstacle = item;

        placeholderObstacle.transform.SetParent(transform, true);
        placeholderObstacle.transform.position = item.transform.position;
        placeholderObstacle.gameObject.SetActive(true);

        ActiveObstacles[index] = placeholderObstacle;
        Debug.Log("Obstacle is removed from the belt (space held by placeholder).");
    }

    public void ReturnObstacleToPlaceholder(Obstacle item)
    {
        if (currentlyDraggedObstacle != item)
        {
            Debug.LogError("Why you have more than one Obstacles free??????????");
            return;
        }

        int index = ActiveObstacles.IndexOf(placeholderObstacle);
        if (index >= 0)
        {
            item.transform.SetParent(transform, true);
            item.transform.position = placeholderObstacle.transform.position;
            ActiveObstacles[index] = item;
        }

        placeholderObstacle.gameObject.SetActive(false);
        currentlyDraggedObstacle = null;
    }

    public void ConfirmObstaclePlacement(Obstacle item)
    {
        if (currentlyDraggedObstacle == item)
        {
            ActiveObstacles.Remove(placeholderObstacle);
            placeholderObstacle.gameObject.SetActive(false);
            currentlyDraggedObstacle = null;
        }
        else
        {
            Debug.LogError("Why you have more than one Obstacles free??????????");
            ActiveObstacles.Remove(item);
        }

        Debug.Log("Obstacle placed on board. Belt slot freed!");
    }

    public void ResetBelt()
    {
        if (placeholderObstacle != null)
        {
            placeholderObstacle.gameObject.SetActive(false);
            ActiveObstacles.Remove(placeholderObstacle);
        }

        if (currentlyDraggedObstacle != null)
        {
            ActiveObstacles.Add(currentlyDraggedObstacle);
            currentlyDraggedObstacle = null;
        }

        if (ActiveObstacles.Count > 0)
        {
            ConveyorBeltClean?.Invoke(new List<Obstacle>(ActiveObstacles));
            ActiveObstacles.Clear();
        }
    }
}