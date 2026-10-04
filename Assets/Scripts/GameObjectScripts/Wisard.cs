using System;
using UnityEngine;

public class Wisard : MonoBehaviour
{
    [SerializeField] private WisardData data;
    public SpriteRenderer spriteRenderer;
    public Collider2D wisardCollider;

    private Animator _animator;

    private void Awake()
    {
        if (wisardCollider == null)
        {
            wisardCollider = GetComponent<Collider2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        Border.OnWisardHitBoundary += HandleWisardHitBoundary;
    }

    private void OnDisable()
    {
        Border.OnWisardHitBoundary -= HandleWisardHitBoundary;
    }

    private void Start()
    {
        if (data != null)
        {
            Initialize(data);
        }
    }

    public void Initialize(WisardData wisardData)
    {
        this.data = wisardData;
        transform.localScale = Vector3.one;

        if (wisardCollider != null)
        {
            wisardCollider.enabled = true;
        }
    }

    private void HandleWisardHitBoundary(Wisard hitWisard)
    {
        if (hitWisard != this) return;

        ResetWisard();
    }

    public void ResetWisard()
    {
        transform.localScale = Vector3.one;

        if (wisardCollider != null)
        {
            wisardCollider.enabled = true;
        }
    }

    // Works if Obstacle or Wisard Collider has "Is Trigger" CHECKED
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleObstacleHit(other.gameObject);
    }

    // Works if BOTH Obstacle and Wisard Colliders have "Is Trigger" UNCHECKED
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleObstacleHit(collision.gameObject);
    }

    private void TryHandleObstacleHit(GameObject hitObject)
    {
        Obstacle obstacle = hitObject.GetComponentInParent<Obstacle>();
        if (obstacle == null)
        {
            return;
        }

        // Only controlled Wisards (children of PlayerController) take damage!
        PlayerController playerController = GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            // This is a wild unspelled Wisard in MovingBoardContent, ignore damage
            return;
        }

        playerController.TakeDamage(obstacle.DamageAmount, this);
    }
}