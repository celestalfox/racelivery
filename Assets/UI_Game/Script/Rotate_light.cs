using UnityEngine;

public class Rotate : MonoBehaviour
{
    public enum Direction { Gauche, Droite }

    [SerializeField] private Direction direction = Direction.Gauche;
    [SerializeField] private float speed = 90f; // degrés par seconde

    void Update()
    {
        // En Unity, une valeur positive sur Z tourne vers la gauche (anti-horaire)
        float sign = direction == Direction.Gauche ? 1f : -1f;
        transform.Rotate(0f, 0f, sign * speed * Time.deltaTime);
    }
}