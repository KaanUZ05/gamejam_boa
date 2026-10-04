using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Lanes")]
    [SerializeField] private Lane[] lanes;
    [SerializeField] private GamePlayController gamePlayController;

    [Header("Wisard Settings")]
    [SerializeField] private int maxWisardQuota = 2;
    [SerializeField] private float wisardLifeSpan = 30f;
    [SerializeField] private float laneMoveDuration = 0.2f;

    [Header("Health Settings (Per Wisard)")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float invulnerabilityDuration = 2f;

    [Header("Health & Current State")]
    [SerializeField] private int currentHealth; // Displays the currently controlled Wisard's health in Inspector
    [SerializeField] private List<Wisard> wisardsList = new List<Wisard>();
    [SerializeField] private Wisard currentWisard;
    [SerializeField] private int rage;
    [SerializeField] private int maxRage = 3;
    public int Rage => rage;

    private bool isPlayerActive;

    // Tracks which lane index each controlled Wisard is sitting in
    private Dictionary<Wisard, int> wisardLaneIndex = new Dictionary<Wisard, int>();

    // Tracks health and invulnerability per individual Wisard
    private Dictionary<Wisard, int> wisardHealths = new Dictionary<Wisard, int>();
    private HashSet<Wisard> invulnerableWisards = new HashSet<Wisard>();
    private Dictionary<Wisard, Coroutine> invulnerabilityCoroutines = new Dictionary<Wisard, Coroutine>();

    // Active coroutines for movement and lifespan
    private Dictionary<Wisard, Coroutine> moveCoroutines = new Dictionary<Wisard, Coroutine>();
    private Dictionary<Wisard, Coroutine> lifeCoroutines = new Dictionary<Wisard, Coroutine>();

    // Events to return Wisards to GamePlayController's WisardPool
    public static Action<Wisard> WisardExpired;                 // Single Wisard 30s timer ended or died
    public static Action<List<Wisard>> PlayerWisardsClean;      // Round ended: send all controlled Wisards to pool
    public static Action<Wisard> WildWisardSpelled;             // Tells GamePlayController this wild Wisard is now owned by Player
    public static Action<Wisard> WildWisardConsumed;
    public static Action PlayerDied;                            // Informs GamePlayController when ALL controlled Wisards die

    private void Awake()
    {
        isPlayerActive = false;
        currentHealth = maxHealth;

        if (lanes != null)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i] != null)
                {
                    lanes[i].laneIndex = i;
                }
            }
        }
    }

    private void Update()
    {
        if (!isPlayerActive || Keyboard.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ChangeCurrentWisard();
        }

        if (Keyboard.current.shiftKey.wasPressedThisFrame ||
            Keyboard.current.leftShiftKey.wasPressedThisFrame ||
            Keyboard.current.rightShiftKey.wasPressedThisFrame)
        {
            SpellWisard();
        }

        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            MoveCurrentWisard(-1); // Move 1 lane up (towards index 0)
        }

        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            MoveCurrentWisard(1); // Move 1 lane down (towards lanes.Length - 1)
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            UseFireballSkill();
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            UseLaneClearSkill();
        }
    }

    // =========================================================================
    // ROUND START & END (Called by GamePlayController)
    // =========================================================================

    public void ActivatePlayer(Wisard initialWisard)
    {
        ResetPlayerWisards();
        currentHealth = maxHealth;
        rage = 0;

        if (initialWisard == null || lanes == null || lanes.Length == 0)
        {
            Debug.LogError("Cannot spawn initial Wisard: Wisard or Lanes array is missing!");
            return;
        }

        isPlayerActive = true;

        // Ensure quota is at least 2 so 1 initial + 1 spelled Wisard works even if Inspector had 1
        if (maxWisardQuota < 2)
        {
            maxWisardQuota = 2;
        }

        // Spawn the first Wisard into the MIDDLE lane's WisarPosition
        int middleLaneIdx = lanes.Length / 2;
        Lane middleLane = lanes[middleLaneIdx];

        RestoreWisardVisualAndCollider(initialWisard);
        initialWisard.transform.SetParent(transform, true);
        initialWisard.transform.position = middleLane.WisarPosition.position;
        initialWisard.gameObject.SetActive(true);

        wisardsList.Add(initialWisard);
        wisardLaneIndex[initialWisard] = middleLaneIdx;
        wisardHealths[initialWisard] = maxHealth;
        currentWisard = initialWisard;
        SyncInspectorHealth();

        Coroutine lifeRoutine = StartCoroutine(WisardLifeSpanRoutine(initialWisard));
        lifeCoroutines[initialWisard] = lifeRoutine;
    }

    public void InactivatePlayer()
    {
        isPlayerActive = false;
        ResetPlayerWisards();
    }

    public void ResetPlayerWisards()
    {
        StopAllCoroutines();
        moveCoroutines.Clear();
        lifeCoroutines.Clear();
        invulnerabilityCoroutines.Clear();
        invulnerableWisards.Clear();
        wisardHealths.Clear();
        wisardLaneIndex.Clear();
        currentWisard = null;

        if (wisardsList.Count > 0)
        {
            for (int i = 0; i < wisardsList.Count; i++)
            {
                RestoreWisardVisualAndCollider(wisardsList[i]);
            }

            PlayerWisardsClean?.Invoke(new List<Wisard>(wisardsList));
            wisardsList.Clear();
        }
    }

    public Lane GetRandomLane()
    {
        if (lanes == null || lanes.Length == 0) return null;
        int randomIdx = UnityEngine.Random.Range(0, lanes.Length);
        return lanes[randomIdx];
    }

    // =========================================================================
    // SPELLING & LIFESPAN
    // =========================================================================

    private void SpellWisard()
    {
        if (lanes == null)
        {
            return;
        }

        Wisard newWisard = null;
        Lane enteredLane = null;

        // Directly check each Lane's SpellArea child collider for a wild Wisard
        for (int i = 0; i < lanes.Length; i++)
        {
            if (lanes[i] == null) continue;

            Wisard found = lanes[i].GetWisardInSpellArea(wisardsList);
            if (found != null)
            {
                newWisard = found;
                enteredLane = lanes[i];
                break;
            }
        }

        if (newWisard == null || enteredLane == null)
        {
            return;
        }

        // aslo audio source nee to react.
        AudioManager.Instance.PlayOrbSFX();

        // Orb her durumda rage verir.
        GainRage(1);

        // Zaten maksimum Wisard sayısındaysak:
        // rage'i al ama üçüncü Wisard'ı bağlama.
        if (wisardsList.Count >= maxWisardQuota)
        {
            WildWisardConsumed?.Invoke(newWisard);
            return;
        }

        // Yer varsa normal şekilde Wisard'ı bağla.
        WildWisardSpelled?.Invoke(newWisard);

        // Reparent from MovingBoardContent to PlayerController so it stops scrolling left
        RestoreWisardVisualAndCollider(newWisard);
        newWisard.transform.SetParent(transform, true);
        wisardsList.Add(newWisard);
        wisardHealths[newWisard] = maxHealth;

        int targetLaneIdx = enteredLane.laneIndex;

        // If we already have a controlled Wisard in this same lane, send the new one 1 lane up or down
        if (currentWisard != null && wisardLaneIndex.ContainsKey(currentWisard))
        {
            int currentLaneIdx = wisardLaneIndex[currentWisard];
            if (targetLaneIdx == currentLaneIdx)
            {
                if (currentLaneIdx - 1 >= 0)
                {
                    targetLaneIdx = currentLaneIdx - 1; // 1 lane up
                }
                else if (currentLaneIdx + 1 < lanes.Length)
                {
                    targetLaneIdx = currentLaneIdx + 1; // 1 lane down
                }
            }
        }

        if (currentWisard == null)
        {
            currentWisard = newWisard;
            SyncInspectorHealth();
        }

        StartLaneMove(newWisard, targetLaneIdx);

        Coroutine lifeRoutine = StartCoroutine(WisardLifeSpanRoutine(newWisard));
        lifeCoroutines[newWisard] = lifeRoutine;

        // Make the newly collected Wisard transparent and invulnerable for 2 seconds
        if (invulnerabilityCoroutines.ContainsKey(newWisard) && invulnerabilityCoroutines[newWisard] != null)
        {
            StopCoroutine(invulnerabilityCoroutines[newWisard]);
        }
        Coroutine invulnRoutine = StartCoroutine(InvulnerabilityRoutine(newWisard));
        invulnerabilityCoroutines[newWisard] = invulnRoutine;
    }

    private IEnumerator WisardLifeSpanRoutine(Wisard wisard)
    {
        float timer = wisardLifeSpan;

        while (timer > 0f)
        {
            // Only count down when we have 2 wisards AND this is the Wisard we do NOT control!
            if (wisardsList.Count > 1 && wisard != currentWisard)
            {
                timer -= Time.deltaTime;
            }
            else if (wisardsList.Count <= 1)
            {
                // Reset timer back to full when down to 1 Wisard
                timer = wisardLifeSpan;
            }

            yield return null;
        }

        RemoveExpiredWisard(wisard);
    }

    private void RemoveExpiredWisard(Wisard expiredWisard)
    {
        if (expiredWisard == null) return;

        if (moveCoroutines.ContainsKey(expiredWisard) && moveCoroutines[expiredWisard] != null)
        {
            StopCoroutine(moveCoroutines[expiredWisard]);
        }

        if (lifeCoroutines.ContainsKey(expiredWisard) && lifeCoroutines[expiredWisard] != null)
        {
            StopCoroutine(lifeCoroutines[expiredWisard]);
        }

        if (invulnerabilityCoroutines.ContainsKey(expiredWisard) && invulnerabilityCoroutines[expiredWisard] != null)
        {
            StopCoroutine(invulnerabilityCoroutines[expiredWisard]);
        }

        moveCoroutines.Remove(expiredWisard);
        lifeCoroutines.Remove(expiredWisard);
        invulnerabilityCoroutines.Remove(expiredWisard);
        invulnerableWisards.Remove(expiredWisard);
        wisardHealths.Remove(expiredWisard);
        wisardLaneIndex.Remove(expiredWisard);
        wisardsList.Remove(expiredWisard);

        // If the Wisard that died/expired was the one we were controlling,
        // automatically switch control to the remaining Wisard!
        if (currentWisard == expiredWisard)
        {
            if (wisardsList.Count > 0)
            {
                currentWisard = wisardsList[0];
            }
            else
            {
                currentWisard = null;
            }
        }

        SyncInspectorHealth();
        RestoreWisardVisualAndCollider(expiredWisard);
        WisardExpired?.Invoke(expiredWisard);
    }

    // =========================================================================
    // SWITCHING WISARD & COROUTINE LANE MOVEMENT
    // =========================================================================

    private void ChangeCurrentWisard()
    {
        if (wisardsList.Count <= 1) return;

        int index = wisardsList.IndexOf(currentWisard);
        int nextIndex = (index + 1) % wisardsList.Count;
        currentWisard = wisardsList[nextIndex];
        SyncInspectorHealth();

        UIManager.Instance.UpdateHealth(currentWisard.GetHealth());
    }

    private void MoveCurrentWisard(int direction)
    {
        if (currentWisard == null || lanes == null || lanes.Length == 0) return;
        if (!wisardLaneIndex.ContainsKey(currentWisard)) return;

        int currentIdx = wisardLaneIndex[currentWisard];
        int targetIdx = currentIdx + direction;

        if (targetIdx < 0 || targetIdx >= lanes.Length) return;

        Wisard otherWisard = GetOtherWisardInLane(targetIdx);
        if (otherWisard != null)
        {
            StartLaneMove(otherWisard, currentIdx);
        }

        StartLaneMove(currentWisard, targetIdx);
    }

    private Wisard GetOtherWisardInLane(int laneIdx)
    {
        foreach (KeyValuePair<Wisard, int> pair in wisardLaneIndex)
        {
            if (pair.Value == laneIdx && pair.Key != currentWisard)
            {
                return pair.Key;
            }
        }
        return null;
    }

    private void StartLaneMove(Wisard wisard, int targetLaneIdx)
    {
        wisardLaneIndex[wisard] = targetLaneIdx;

        if (moveCoroutines.ContainsKey(wisard) && moveCoroutines[wisard] != null)
        {
            StopCoroutine(moveCoroutines[wisard]);
        }

        Vector3 targetPos = lanes[targetLaneIdx].WisarPosition.position;
        moveCoroutines[wisard] = StartCoroutine(MoveWisardToLaneRoutine(wisard, targetPos));
    }

    private IEnumerator MoveWisardToLaneRoutine(Wisard wisard, Vector3 targetPosition)
    {
        Vector3 startPos = wisard.transform.position;
        Vector3 endPos = new Vector3(targetPosition.x, targetPosition.y, startPos.z);

        float elapsed = 0f;

        while (elapsed < laneMoveDuration)
        {
            if (wisard == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / laneMoveDuration;
            wisard.transform.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }

        if (wisard != null)
        {
            wisard.transform.position = endPos;
        }

        moveCoroutines.Remove(wisard);
    }

    // =========================================================================
    // HEALTH, INVULNERABILITY & DEATH MECHANICS (Per Individual Wisard)
    // =========================================================================

    public void TakeDamage(int amount, Wisard damagedWisard)
    {
        if (!isPlayerActive || damagedWisard == null)
        {
            return;
        }

        if (!wisardsList.Contains(damagedWisard))
        {
            return;
        }

        if (invulnerableWisards.Contains(damagedWisard))
        {
            return;
        }

        if (amount <= 0)
        {
            Debug.LogError("Damage amount must be greater than 0.");
            return;
        }

        if (!wisardHealths.ContainsKey(damagedWisard))
        {
            wisardHealths[damagedWisard] = maxHealth;
        }

        wisardHealths[damagedWisard] -= amount;
        UIManager.Instance.UpdateHealth(wisardHealths[damagedWisard]);
        

        int remainingWisardHealth = wisardHealths[damagedWisard];
        SyncInspectorHealth();

        Debug.Log(damagedWisard.name + " took " + amount + " damage. Remaining health: " + remainingWisardHealth);

        // Audio source need to react.
        AudioManager.Instance.PlayHurtSFX();

        if (remainingWisardHealth <= 0)
        {
            HandleWisardDeath(damagedWisard);
            return;
        }

        if (invulnerabilityCoroutines.ContainsKey(damagedWisard) && invulnerabilityCoroutines[damagedWisard] != null)
        {
            StopCoroutine(invulnerabilityCoroutines[damagedWisard]);
        }
        Coroutine invulnRoutine = StartCoroutine(InvulnerabilityRoutine(damagedWisard));
        invulnerabilityCoroutines[damagedWisard] = invulnRoutine;
    }

    private void HandleWisardDeath(Wisard deadWisard)
    {
        Debug.Log(deadWisard.name + " died and returned to the pool!");

        // Removes deadWisard from wisardsList, switches currentWisard to the survivor if needed,
        // and invokes WisardExpired so GamePlayController returns it to the pool!
        RemoveExpiredWisard(deadWisard);

        // Only freeze controls and trigger PlayerDied if NO Wisards remain on the board!
        if (wisardsList.Count == 0)
        {
            HandlePlayerDeath();
        }
        else
        {
            Debug.Log("Continuing with remaining Wisard: " + currentWisard.name);
        }
    }

    private IEnumerator InvulnerabilityRoutine(Wisard damagedWisard)
    {
        if (damagedWisard == null)
        {
            Debug.LogError("Damaged Wisard is null.");
            yield break;
        }

        invulnerableWisards.Add(damagedWisard);

        SpriteRenderer renderer = damagedWisard.spriteRenderer;
        Collider2D wisardCollider = damagedWisard.wisardCollider;

        if (renderer == null)
        {
            Debug.LogError("Wisard SpriteRenderer is missing.");
        }

        if (wisardCollider == null)
        {
            Debug.LogError("Wisard Collider2D is missing.");
        }

        if (renderer != null)
        {
            Color color = renderer.color;
            color.a = 0.5f;
            renderer.color = color;
        }

        if (wisardCollider != null)
        {
            wisardCollider.enabled = false;
        }

        yield return new WaitForSeconds(invulnerabilityDuration);

        if (renderer != null)
        {
            Color color = renderer.color;
            color.a = 1f;
            renderer.color = color;
        }

        if (wisardCollider != null)
        {
            wisardCollider.enabled = true;
        }

        invulnerableWisards.Remove(damagedWisard);
        invulnerabilityCoroutines.Remove(damagedWisard);
    }

    private void RestoreWisardVisualAndCollider(Wisard wisard)
    {
        if (wisard == null) return;

        if (wisard.spriteRenderer != null)
        {
            Color color = wisard.spriteRenderer.color;
            color.a = 1f;
            wisard.spriteRenderer.color = color;
        }

        if (wisard.wisardCollider != null)
        {
            wisard.wisardCollider.enabled = true;
        }
    }

    private void SyncInspectorHealth()
    {
        if (currentWisard != null && wisardHealths.TryGetValue(currentWisard, out int hp))
        {
            currentHealth = hp;
        }
        else
        {
            currentHealth = 0;
        }
    }

    private void HandlePlayerDeath()
    {
        Debug.Log("All Wisards died! Round Over.");

        isPlayerActive = false;
        PlayerDied?.Invoke();
    }

    // =========================================================================
    // RAGE AND FIREBALL
    // =========================================================================

    private void GainRage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        rage = Mathf.Clamp(rage + amount, 0, maxRage);
      
        UIManager.Instance.UpdateRage(rage);

        Debug.Log("Player rage: " + rage);
    }

    private Obstacle FindClosestObstacleAhead()
    {
        if (currentWisard == null)
        {
            Debug.LogError("Cannot find obstacle: current Wisard is null.");
            return null;
        }

        if (!wisardLaneIndex.TryGetValue(currentWisard, out int currentLaneIndex))
        {
            Debug.LogError("Current Wisard does not have a lane index.");
            return null;
        }

        IReadOnlyList<Obstacle> obstacles =
            gamePlayController.GetActivePlacedObstacles();

        Obstacle closestObstacle = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < obstacles.Count; i++)
        {
            Obstacle obstacle = obstacles[i];

            if (obstacle == null || !obstacle.gameObject.activeInHierarchy)
            {
                continue;
            }

            // Sadece Wisard'ın önündeki obstacle'lar.
            if (obstacle.transform.position.x <= currentWisard.transform.position.x)
            {
                continue;
            }

            int obstacleStartLane = obstacle.PlacedStartLane;
            int obstacleEndLane =
                obstacleStartLane + obstacle.HeightInLanes - 1;

            // Obstacle mevcut Wisard'ın lane'ine değmiyorsa geç.
            if (currentLaneIndex < obstacleStartLane ||
                currentLaneIndex > obstacleEndLane)
            {
                continue;
            }

            float distance =
                obstacle.transform.position.x -
                currentWisard.transform.position.x;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestObstacle = obstacle;
            }
        }

        return closestObstacle;
    }

    private void UseFireballSkill()
    {
        if (!isPlayerActive)
        {
            return;
        }

        if (currentWisard == null)
        {
            return;
        }

        if (rage < 1)
        {
            Debug.Log("Not enough rage for fireball.");
            return;
        }

        currentWisard.PlayShootAnimation();

        rage -= 1;
        
        UIManager.Instance.UpdateRage(rage);

        Obstacle target = FindClosestObstacleAhead();

        if (target != null)
        {
            gamePlayController.RemoveObstacleFromBoard(target);
        }

        // Audio manager need to react
        AudioManager.Instance.PlayFireBallSFX();

        Debug.Log("Fireball used. Remaining rage: " + rage);
    }

    private void DestroyAllObstaclesInCurrentLane()
    {
        if (currentWisard == null)
        {
            return;
        }

        if (!wisardLaneIndex.TryGetValue(currentWisard, out int currentLaneIndex))
        {
            return;
        }

        currentWisard.PlayShootAnimation();

        IReadOnlyList<Obstacle> obstacles =
            gamePlayController.GetActivePlacedObstacles();

        List<Obstacle> obstaclesToRemove = new List<Obstacle>();

        for (int i = 0; i < obstacles.Count; i++)
        {
            Obstacle obstacle = obstacles[i];

            if (obstacle == null || !obstacle.gameObject.activeInHierarchy)
            {
                continue;
            }

            int obstacleStartLane = obstacle.PlacedStartLane;
            int obstacleEndLane =
                obstacleStartLane + obstacle.HeightInLanes - 1;

            bool overlapsCurrentLane =
                currentLaneIndex >= obstacleStartLane &&
                currentLaneIndex <= obstacleEndLane;

            if (overlapsCurrentLane)
            {
                obstaclesToRemove.Add(obstacle);
            }
        }

        for (int i = 0; i < obstaclesToRemove.Count; i++)
        {
            gamePlayController.RemoveObstacleFromBoard(
                obstaclesToRemove[i]
            );
        }
    }

    private void UseLaneClearSkill()
    {
        if (!isPlayerActive)
        {
            return;
        }

        if (currentWisard == null)
        {
            return;
        }

        if (rage < 3)
        {
            Debug.Log("Not enough rage for lane clear.");
            return;
        }

        rage -= 3;

        UIManager.Instance.UpdateRage(rage);

        DestroyAllObstaclesInCurrentLane();

        Debug.Log("Lane clear used. Remaining rage: " + rage);
    }
}