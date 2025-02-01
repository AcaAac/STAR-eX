using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap;
using Leap.Unity;
using Leap.Unity.Interaction;
// i have another code snippet which sends angles to command line
// the point of this code is to read those angles, compare them to threshold and execute the OnPress() from the InteractButton.cs
public class FlexionTrigger : MonoBehaviour
{
    // Start is called before the first frame update
    public LeapServiceProvider leapServiceProvider;
    public float threshold = 90;
    public InteractionButton button;
    public float thumbAngle;
    public float indexAngle;
    public float middleAngle;
    public float ringAngle;
    public float pinkyAngle;

    void Start()
    {
        // read the angles from command line
        // compare them to threshold
        // if the angle is greater than the threshold, execute the OnPress() from the InteractButton.cs
        if (leapServiceProvider == null)
        {
            Debug.LogError("LeapServiceProvider is not assigned.");
        }


        
    }

    // Update is called once per frame
    void Update()
    {
        
        
    }
}
