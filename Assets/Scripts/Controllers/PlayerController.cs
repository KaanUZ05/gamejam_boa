using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Lanes")]
    [SerializeField] private Lane[] lanes;

    [Header("Wisard Settings")]
    [SerializeField] private int maxWisardQuota = 2;
    [SerializeField] private float wisardLifeSpan = 30f;
    [SerializeField] private float laneMoveDuration = 0.2f;

    [Header("Current State")]

    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float invulnerabilityDuration = 2f;

    [Header("Health State")]
    [SerializeField] private int currentHealth;
    private bool isInvulnerable = false;
    private List<Wisard> wisardsList = new List<Wisard>();
    private Wisard currentWisard;
    [SerializeField] private int rage;

    private bool isPlayerActive;

    // Wild Wisards currently inside a Lane's SpellArea waiting to be spelled
    private List<Wisard> spellableWisards = new List<Wisard>();
    private Dictionary<Wisard, Lane> spellableWisardLanes = new Dictionary<Wisard, Lane>();

    // Tracks which lane index each controlled Wisard is sitting in
    private Dictionary<Wisard, int> wisardLaneIndex = new Dictionary<Wisard, int>();

    // Active coroutines for movement and lifespan
    private Dictionary<Wisard, Coroutine> moveCoroutines = new Dictionary<Wisard, Coroutine>();
    private Dictionary<Wisard, Coroutine> lifeCoroutines = new Dictionary<Wisard, Coroutine>();

    // Events to return Wisards to GamePlayController's WisardPool
    public static Action<Wisard> WisardExpired;                 // Single Wisard 30s timer ended
    public static Action<List<Wisard>> PlayerWisardsClean;      // Round ended: send all controlled Wisards to pool
    public static Action<Wisard> WildWisardSpelled;             // Tells GamePlayController this wild Wisard is now owned by Player

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

    private void OnEnable()
    {
        Lane.WisardEnteredZone += HandleWisardEntered;
        Lane.WisardExitedZone += HandleWisardExited;
    }

    private void OnDisable()
    {
        Lane.WisardEnteredZone -= HandleWisardEntered;
        Lane.WisardExitedZone -= HandleWisardExited;
    }

    private void Update()
    {
        if (!isPlayerActive || Keyboard.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ChangeCurrentWisard();
        }

        if (Keyboard.current.shiftKey.wasPressedThisFrame)
        {
            SpellWisard();
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            MoveCurrentWisard(-1); // Move 1 lane up (towards index 0)
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            MoveCurrentWisard(1); // Move 1 lane down (towards lanes.Length - 1)
        }
    }

    // =========================================================================
    // ROUND START & END (Called by GamePlayController)
    // =========================================================================

    public void ActivatePlayer(Wisard initialWisard)
    {
        ResetPlayerWisards();
        currentHealth = maxHealth;
        isInvulnerable = false;

        if (initialWisard == null || lanes == null || lanes.Length == 0)
        {
            Debug.LogError("Cannot spawn initial Wisard: Wisard or Lanes array is missing!");
            return;
        }

        isPlayerActive = true;

        // Spawn the first Wisard into the MIDDLE lane's WisarPosition
        int middleLaneIdx = lanes.Length / 2;
        Lane middleLane = lanes[middleLaneIdx];

        initialWisard.transform.SetParent(transform, true);
        initialWisard.transform.position = middleLane.WisarPosition.position;
        initialWisard.gameObject.SetActive(true);

        wisardsList.Add(initialWisard);
        wisardLaneIndex[initialWisard] = middleLaneIdx;
        currentWisard = initialWisard;

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
        wisardLaneIndex.Clear();
        spellableWisards.Clear();
        spellableWisardLanes.Clear();
        currentWisard = null;

        if (wisardsList.Count > 0)
        {
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
    // TRIGGER ZONE HANDLERS
    // =========================================================================

    private void HandleWisardEntered(Wisard wisard, Lane lane)
    {
        if (!isPlayerActive || wisard == null || wisardsList.Contains(wisard)) return;

        if (!spellableWisards.Contains(wisard))
        {
            spellableWisards.Add(wisard);
        }
        spellableWisardLanes[wisard] = lane;
    }

    private void HandleWisardExited(Wisard wisard, Lane lane)
    {
        if (wisard == null) return;

        spellableWisards.Remove(wisard);
        spellableWisardLanes.Remove(wisard);
    }

    // =========================================================================
    // SPELLING & LIFESPAN
    // =========================================================================

    private void SpellWisard()
    {
        if (wisardsList.Count >= maxWisardQuota || spellableWisards.Count == 0)
        {
            return;
        }

        Wisard newWisard = spellableWisards[0];
        Lane enteredLane = spellableWisardLanes[newWisard];

        spellableWisards.RemoveAt(0);
        spellableWisardLanes.Remove(newWisard);

        if (newWisard == null || enteredLane == null) return;

        // Inform GamePlayController that this wild Wisard is now controlled by PlayerController
        WildWisardSpelled?.Invoke(newWisard);

        // Reparent from MovingBoardContent to PlayerController so it stops scrolling left
        newWisard.transform.SetParent(transform, true);
        wisardsList.Add(newWisard);

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
        }

        StartLaneMove(newWisard, targetLaneIdx);

        Coroutine lifeRoutine = StartCoroutine(WisardLifeSpanRoutine(newWisard));
        lifeCoroutines[newWisard] = lifeRoutine;
    }

    private IEnumerator WisardLifeSpanRoutine(Wisard wisard)
    {
        float timer = wisardLifeSpan;

        while (timer > 0f)
        {
            // Only count down when we have 2 wisards!
            if (wisardsList.Count > 1)
            {
                timer -= Time.deltaTime;
            }
            else
            {
                timer = wisardLifeSpan;
            }

            yield return null;
        }

        RemoveExpiredWisard(wisard);
    }

    private void RemoveExpiredWisard(Wisard expiredWisard)
    {
        if (moveCoroutines.ContainsKey(expiredWisard) && moveCoroutines[expiredWisard] != null)
        {
            StopCoroutine(moveCoroutines[expiredWisard]);
        }

        moveCoroutines.Remove(expiredWisard);
        lifeCoroutines.Remove(expiredWisard);
        wisardLaneIndex.Remove(expiredWisard);
        wisardsList.Remove(expiredWisard);

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

    public void TakeDamage(int amount, Wisard damagedWisard)
    {
        if (!isPlayerActive)
        {
            return;
        }

        if (isInvulnerable)
        {
            return;
        }

        if (amount <= 0)
        {
            Debug.LogError("Damage amount must be greater than 0.");
            return;
        }

        currentHealth -= amount;

        Debug.Log("Player took " + amount + " damage. Current health: " + currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            HandlePlayerDeath();
            return;
        }

        StartCoroutine(
            InvulnerabilityRoutine(damagedWisard)
        );
    }

    private IEnumerator InvulnerabilityRoutine(Wisard damagedWisard)
    {
        isInvulnerable = true;

        if (damagedWisard == null)
        {
            Debug.LogError("Damaged Wisard is null.");
            isInvulnerable = false;
            yield break;
        }

        SpriteRenderer renderer = damagedWisard.SpriteRenderer;
        Collider2D wisardCollider = damagedWisard.Collider;

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

        isInvulnerable = false;
    }

    private void HandlePlayerDeath()
    {
        Debug.Log("Player died.");

        isPlayerActive = false;
    }
}