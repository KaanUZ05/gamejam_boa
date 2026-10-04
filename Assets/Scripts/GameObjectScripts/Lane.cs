using System;
using System.Collections.Generic;
using UnityEngine;

public class Lane : MonoBehaviour
{
    public int laneIndex;

    [Header("Lane Positions")]
    public Transform WisarPosition;     // Where controlled/spelled Wisards stand
    public Transform WisardSpawnPoint;  // Where wild Wisards spawn randomly

    [Header("Spell Zone Sub-Object Collider")]
    [SerializeField] private Collider2D SpellArea; // Drag your spellZone child Collider2D here!

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform visualTransform;
    [SerializeField] private Sprite laneSprite;

    [SerializeField] private float laneWidth = 20f;
    [SerializeField] private float laneHeight = 1.5f;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            Debug.LogError("Lane SpriteRenderer is not assigned.");
            return;
        }

        if (visualTransform == null)
        {
            Debug.LogError("Lane Visual Transform is not assigned.");
            return;
        }

        if (laneSprite == null)
        {
            Debug.LogError("Lane Sprite is not assigned for: " + name);
            return;
        }

        if (SpellArea == null)
        {
            Debug.LogError("SpellArea (spellZone) Collider2D is not assigned on: " + name);
        }

        spriteRenderer.sprite = laneSprite;

        Vector2 spriteSize = laneSprite.bounds.size;

        visualTransform.localScale = new Vector3(
            laneWidth / spriteSize.x,
            laneHeight / spriteSize.y,
            1f
        );
    }

    public Wisard GetWisardInSpellArea(List<Wisard> alreadyOwnedWisards)
    {
        if (SpellArea == null) return null;

        // Force Unity 2D to sync collider positions with MovingBoardContent.position!
        Physics2D.SyncTransforms();

        Bounds bounds = SpellArea.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);

        for (int i = 0; i < hits.Length; i++)
        {
            Wisard wisard = hits[i].GetComponentInParent<Wisard>();
            if (wisard != null && !alreadyOwnedWisards.Contains(wisard))
            {
                return wisard;
            }
        }

        return null;
    }
}