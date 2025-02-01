using UnityEngine;
using Leap;
using System.Collections.Generic;

public class PianoScaler : MonoBehaviour
{
    public Transform[] pianoKeys; // Array to store all the piano key transforms
    public float baseHandSize = 0.1f; // The hand size used as a reference for scaling
    public float baseKeyScale = 1f; // The base scale of the keys
    public float baseKeySpacing = 0.1f; // The base spacing between keys

    private Controller leapController;

    void Start()
    {
        leapController = new Controller();
    }

    void Update()
    {
        Frame frame = leapController.Frame();
        List<Hand> hands = frame.Hands;

        if (hands.Count > 0)
        {
            float averageHandSize = CalculateAverageHandSize(hands);
            ScalePianoKeys(averageHandSize);
        }
    }

    float CalculateAverageHandSize(List<Hand> hands)
    {
        float totalSize = 0f;

        foreach (Hand hand in hands)
        {
            totalSize += hand.PalmWidth;
        }

        return totalSize / hands.Count;
    }

    void ScalePianoKeys(float handSize)
    {
        float scaleMultiplier = handSize / baseHandSize;
        float newKeyScale = baseKeyScale * scaleMultiplier;
        float newKeySpacing = baseKeySpacing * scaleMultiplier;

        for (int i = 0; i < pianoKeys.Length; i++)
        {
            // Scale the key
            pianoKeys[i].localScale = new Vector3(newKeyScale, pianoKeys[i].localScale.y, pianoKeys[i].localScale.z);

            // Adjust the position to account for the new spacing
            if (i > 0)
            {
                pianoKeys[i].localPosition = new Vector3(
                    pianoKeys[i - 1].localPosition.x + newKeySpacing,
                    pianoKeys[i].localPosition.y,
                    pianoKeys[i].localPosition.z
                );
            }
        }
    }
}
