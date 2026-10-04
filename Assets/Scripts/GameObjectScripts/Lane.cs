using System;
using UnityEngine;

public class Lane : MonoBehaviour
{
    public int laneIndex;

    [Header("Lane Positions")]
    public Transform WisarPosition;     // Where controlled/spelled Wisards stand (middle lane on round start)
    public Transform WisardSpawnPoint;  // Where wild Wisards spawn randomly during the round

    [SerializeField] private Collider2D SpellArea;

    // Informs PlayerController when a wild Wisard enters/exits the SpellArea
    public static Action<Wisard, Lane> WisardEnteredZone;
    public static Action<Wisard, Lane> WisardExitedZone;

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

        spriteRenderer.sprite = laneSprite;

        Vector2 spriteSize = laneSprite.bounds.size;

        visualTransform.localScale = new Vector3(
            laneWidth / spriteSize.x,
            laneHeight / spriteSize.y,
            1f
        );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out Wisard wisard))
        {
            WisardEnteredZone?.Invoke(wisard, this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out Wisard wisard))
        {
            WisardExitedZone?.Invoke(wisard, this);
        }
    }
}