using UnityEngine;

public class Wisard : MonoBehaviour
{
    private WisardData data;
    public SpriteRenderer SpriteRenderer =>
        GetComponent<SpriteRenderer>();

    public Collider2D Collider =>
        GetComponent<Collider2D>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Obstacle obstacle = collision.gameObject.GetComponent<Obstacle>();

        if (obstacle == null)
        {
            return;
        }

        PlayerController playerController =
            GetComponentInParent<PlayerController>();

        if (playerController == null)
        {
            Debug.LogError("PlayerController could not be found for Wisard.");
            return;
        }

        playerController.TakeDamage(obstacle.DamageAmount, this);
    }
}
