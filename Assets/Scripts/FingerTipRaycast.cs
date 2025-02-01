using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap;
using Leap.Unity;
using Leap.Unity.Interaction;

public class FingerTipRaycast : MonoBehaviour
{
    public LeapServiceProvider leapServiceProvider;
    public LayerMask buttonLayerMask;
    public float raycastDistance = 0.05f;

    void Start()
    {
        if (leapServiceProvider == null)
        {
            Debug.LogError("LeapServiceProvider is not assigned.");
        }
    }

    void Update()
    {
        if (leapServiceProvider != null)
        {
            Frame frame = leapServiceProvider.CurrentFrame;
            List<Hand> hands = frame.Hands;

            foreach (Hand hand in hands)
            {
                CheckFingerRaycast(hand.GetThumb(), "Thumb");
                CheckFingerRaycast(hand.GetIndex(), "Index");
                CheckFingerRaycast(hand.GetMiddle(), "Middle");
                CheckFingerRaycast(hand.GetRing(), "Ring");
                CheckFingerRaycast(hand.GetPinky(), "Pinky");
            }
        }
    }

    void CheckFingerRaycast(Finger finger, string fingerName)
    {
        Vector3 fingerTipPosition = new Vector3(finger.TipPosition.x, finger.TipPosition.y, finger.TipPosition.z);
        Vector3 fingerDirection = new Vector3(finger.Direction.x, finger.Direction.y, finger.Direction.z);

        // Draw the ray in the Scene view
        Debug.DrawRay(fingerTipPosition, fingerDirection * raycastDistance, Color.red);

        RaycastHit hit;
        if (Physics.Raycast(fingerTipPosition, fingerDirection, out hit, raycastDistance, buttonLayerMask))
        {
            Debug.Log($"{fingerName} is pointing at {hit.collider.gameObject.name}");
            InteractionButton button = hit.collider.GetComponent<InteractionButton>();
            if (button != null && !button.isPressed)
            {
                button.OnPress.Invoke();
                Debug.Log($"{fingerName} pressed the button {button.name}");
            }
        }
    }
}
