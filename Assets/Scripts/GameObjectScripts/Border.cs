using System;
using UnityEngine;

public class Border : MonoBehaviour
{
    public static Action<Obstacle> OnObstacleHitBoundary;
    public static Action<Wisard> OnWisardHitBoundary;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("A trigger dedected on the border");
        if (other.TryGetComponent(out Obstacle obs))
        {
            OnObstacleHitBoundary?.Invoke(obs);
        }
        else if (other.TryGetComponent(out Wisard wisard))
        {
            OnWisardHitBoundary?.Invoke(wisard);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.TryGetComponent(out Obstacle obs))
        {
            OnObstacleHitBoundary?.Invoke(obs);
        }
        else if (collision.collider.TryGetComponent(out Wisard wisard))
        {
            OnWisardHitBoundary?.Invoke(wisard);
        }
    }
}