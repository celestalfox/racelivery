using System.Collections;
using UnityEngine;
using CarContollingScripts;

/// <summary>
/// Put this on any object that has a Collider (water, cliff edge, barrier, lava...).
/// When the car touches it, the death screen is shown.
///
/// Setup: add a collider, tick "Is Trigger" (Reset() does it automatically when you
/// add this script), then add this script. The car is detected by its CarController,
/// so no tag is needed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeathZone : MonoBehaviour
{
    [Tooltip("Seconds to wait before the death screen appears (0 = instantly).")]
    public float delay = 0f;

    [Tooltip("Zero the car's speed the moment it hits (useful with a delay).")]
    public bool stopCar = false;

    bool fired;

    void Reset()
    {
        // Runs when the component is first added: make the collider a trigger.
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) => Hit(other);

    // Also works if you leave the collider solid (Is Trigger off)
    void OnCollisionEnter(Collision collision) => Hit(collision.collider);

    void Hit(Collider other)
    {
        if (fired) return;

        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;                  // not the player's car

        fired = true;

        if (stopCar)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (delay <= 0f) GameFlow.ShowDeath();
        else StartCoroutine(ShowAfterDelay());
    }

    IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        GameFlow.ShowDeath();
    }
}
