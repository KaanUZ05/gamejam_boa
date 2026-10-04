using System;
using UnityEngine;

public class Wisard : MonoBehaviour
{
    [SerializeField] private WisardData data;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D wisardCollider;

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
        // Only react if THIS specific Wisard instance is the one that hit the border
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
}