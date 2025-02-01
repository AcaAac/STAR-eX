using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FSMVisualizer : MonoBehaviour
{
    public Image universalIdleState;
    public Image thumbFlexedState, thumbRestingState;
    public Image indexFlexedState, indexRestingState;
    public Image middleFlexedState, middleRestingState;
    public Image ringFlexedState, ringRestingState;
    public Image pinkyFlexedState, pinkyRestingState;

    private Dictionary<string, Image> stateCircles;
    private Image currentState;

    void Start()
    {
        stateCircles = new Dictionary<string, Image>
        {
            { "UniversalIdleState", universalIdleState },
            { "ThumbFlexedState", thumbFlexedState },
            { "ThumbRestingState", thumbRestingState },
            { "IndexFlexedState", indexFlexedState },
            { "IndexRestingState", indexRestingState },
            { "MiddleFlexedState", middleFlexedState },
            { "MiddleRestingState", middleRestingState },
            { "RingFlexedState", ringFlexedState },
            { "RingRestingState", ringRestingState },
            { "PinkyFlexedState", pinkyFlexedState },
            { "PinkyRestingState", pinkyRestingState }
        };

        SetState("UniversalIdleState");
    }

    public void SetState(string newState)
    {
        if (currentState != null)
        {
            currentState.color = Color.white; // Reset color of the previous state
        }

        if (stateCircles.ContainsKey(newState))
        {
            currentState = stateCircles[newState];
            currentState.color = Color.yellow; // Highlight the new state
        }
        else
        {
            Debug.LogError("State " + newState + " does not exist.");
        }
    }
}
