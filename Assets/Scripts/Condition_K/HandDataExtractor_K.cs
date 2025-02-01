using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap;
using Leap.Unity;
using Leap.Unity.Interaction;
using TMPro;

public class HandDataExtractor_K : MonoBehaviour
{
    public LeapServiceProvider leapServiceProvider;

    // References to the InteractionButton for each piano key
    public InteractionButton keyC; // Left Thumb
    public InteractionButton keyD; // Left Index
    public InteractionButton keyE; // Left Middle
    public InteractionButton keyF; // Left Ring
    public InteractionButton keyG; // Left Pinky

    // References to the CustomInteractionGlow for each piano key
    private CustomInteractionGlow_K glowC;
    private CustomInteractionGlow_K glowD;
    private CustomInteractionGlow_K glowE;
    private CustomInteractionGlow_K glowF;
    private CustomInteractionGlow_K glowG;

    // Thresholds for each finger to trigger OnPress
    public float thresholdC = 10f; // Thumb
    public float thresholdD = 10f; // Index
    public float thresholdE = 10f; // Middle
    public float thresholdF = 10f; // Ring
    public float thresholdG = 10f; // Pinky

    // Track the state of each key press and error state
    private Dictionary<InteractionButton, bool> keyPressStates = new Dictionary<InteractionButton, bool>();
    private Dictionary<InteractionButton, bool> keyErrorStates = new Dictionary<InteractionButton, bool>();

    // References to the TMP text components for each panel
    public TextMeshProUGUI[] panelTexts;

    // Reference to the TMP text for error counter
    public TextMeshProUGUI errorCounterText;

    // Reference to the HeadlightTimer
    public HeadlightTimer_K headlightTimer;

    private string currentSequence = "";
    private int sequenceIndex = 0;
    private int errorCount = 0;
    private char nextKey;

    void Start()
    {
        if (leapServiceProvider == null)
        {
            Debug.LogError("LeapServiceProvider is not assigned.");
        }

        // Ensure all piano key references are assigned
        if (keyC == null || keyD == null || keyE == null || keyF == null || keyG == null)
        {
            Debug.LogError("One or more InteractionButton references are not assigned.");
        }

        // Get the CustomInteractionGlow components
        glowC = keyC.GetComponent<CustomInteractionGlow_K>();
        glowD = keyD.GetComponent<CustomInteractionGlow_K>();
        glowE = keyE.GetComponent<CustomInteractionGlow_K>();
        glowF = keyF.GetComponent<CustomInteractionGlow_K>();
        glowG = keyG.GetComponent<CustomInteractionGlow_K>();

        // Ensure all panel text references are assigned
        if (panelTexts.Length == 0)
        {
            Debug.LogError("No TextMeshProUGUI references are assigned.");
        }

        // Ensure error counter text reference is assigned
        if (errorCounterText == null)
        {
            Debug.LogError("ErrorCounterText is not assigned.");
        }

        // Ensure headlight timer reference is assigned
        if (headlightTimer == null)
        {
            Debug.LogError("HeadlightTimer is not assigned.");
        }

        // Initialize the error counter display
        UpdateErrorCounter();

        // Initialize key press and error states
        keyPressStates[keyC] = false;
        keyPressStates[keyD] = false;
        keyPressStates[keyE] = false;
        keyPressStates[keyF] = false;
        keyPressStates[keyG] = false;

        keyErrorStates[keyC] = false;
        keyErrorStates[keyD] = false;
        keyErrorStates[keyE] = false;
        keyErrorStates[keyF] = false;
        keyErrorStates[keyG] = false;
    }

    void Update()
    {
        if (leapServiceProvider != null)
        {
            Frame frame = leapServiceProvider.CurrentFrame;
            if (frame != null && frame.Hands.Count > 0)
            {
                foreach (Hand hand in frame.Hands)
                {
                    if (hand.IsLeft) // Only process the left hand
                    {
                        // Calculate angles and trigger buttons
                        TriggerPianoKey(hand.GetThumb(), hand, keyC, glowC, thresholdC, keyC);
                        TriggerPianoKey(hand.GetIndex(), hand, keyD, glowD, thresholdD, keyD);
                        TriggerPianoKey(hand.GetMiddle(), hand, keyE, glowE, thresholdE, keyE);
                        TriggerPianoKey(hand.GetRing(), hand, keyF, glowF, thresholdF, keyF);
                        TriggerPianoKey(hand.GetPinky(), hand, keyG, glowG, thresholdG, keyG);
                    }
                }
            }

            // Check if the TMP text has changed and reset error count if necessary
            string newSequence = GetCurrentTMPText();
            if (newSequence != currentSequence)
            {
                currentSequence = newSequence;
                ResetSequence();
            }

            // Check for "0" key press to reset errors
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                ResetErrors();
            }

            // Check for "R" key press to reset everything
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetAll();
            }
        }
    }

    void TriggerPianoKey(Finger finger, Hand hand, InteractionButton pianoKey, CustomInteractionGlow_K glow, float threshold, InteractionButton keyButton)
    {
        Vector3 direction1, direction2;

        if (finger.Type == Finger.FingerType.TYPE_THUMB)
        {
            // For the thumb, calculate angle between proximal and distal
            direction1 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_PROXIMAL).Direction);
            direction2 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_DISTAL).Direction);
        }
        else
        {
            // For other fingers, calculate angle between metacarpal and proximal
            direction1 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_METACARPAL).Direction);
            direction2 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_PROXIMAL).Direction);
        }

        // Normalize the direction vectors with respect to the hand's palm direction
        Vector3 handDirection = Utils.ToVector3(hand.Direction);
        direction1 = Quaternion.FromToRotation(handDirection, Vector3.forward) * direction1;
        direction2 = Quaternion.FromToRotation(handDirection, Vector3.forward) * direction2;

        Vector3 crossProduct = Vector3.Cross(direction1, direction2);
        float angle = Vector3.Angle(direction1, direction2);
        if (crossProduct.z < 0) angle = -angle;  // Adjust for the correct angle sign

        bool isPressed = (finger.Type == Finger.FingerType.TYPE_THUMB || finger.Type == Finger.FingerType.TYPE_INDEX || finger.Type == Finger.FingerType.TYPE_MIDDLE) ? 
                            angle > threshold : angle < threshold;

        Debug.Log($"KeyChar: {keyButton.name}, IsPressed: {isPressed}, PianoKey.isPressed: {pianoKey.isPressed}, KeyPressed: {keyPressStates[pianoKey]}, KeyError: {keyErrorStates[pianoKey]}");

        if (isPressed && !keyPressStates[pianoKey])
        {
            Debug.Log("Key Press Detected");
            ResetAllGlows(); // Reset all other glows
            pianoKey.OnPress.Invoke();  // Trigger the OnPress method on the piano key
            glow.HandlePress(); // Trigger the glow effect
            keyPressStates[pianoKey] = true;

            CheckSequence(keyButton); // Check the sequence for this key press

            Debug.Log($"After Key Press: KeyChar: {keyButton.name}, NextKey: {nextKey}, CurrentPressedKey: {pianoKey}, KeyPressed: {keyPressStates[pianoKey]}, KeyError: {keyErrorStates[pianoKey]}");
        }
        else if (!isPressed && keyPressStates[pianoKey])
        {
            Debug.Log("Key Release Detected");
            pianoKey.OnUnpress.Invoke();  // Trigger the OnUnpress method on the piano key
            glow.HandleUnpress(); // Stop the glow effect
            keyPressStates[pianoKey] = false;
            keyErrorStates[pianoKey] = false; // Reset error state on key release

            Debug.Log($"After Key Release: KeyChar: {keyButton.name}, NextKey: {nextKey}, CurrentPressedKey: {pianoKey}, KeyPressed: {keyPressStates[pianoKey]}, KeyError: {keyErrorStates[pianoKey]}");
        }
    }

    void ResetAllGlows()
    {
        Debug.Log("Resetting all glows");

        // Collect keys to be reset
        var keysToReset = new List<InteractionButton>();

        foreach (var key in keyPressStates.Keys)
        {
            if (keyPressStates[key])
            {
                keysToReset.Add(key);
            }
        }

        // Perform the reset operations
        foreach (var key in keysToReset)
        {
            key.OnUnpress.Invoke();
            CustomInteractionGlow_K glow = key.GetComponent<CustomInteractionGlow_K>();
            if (glow != null)
            {
                glow.HandleUnpress();
            }
            keyPressStates[key] = false;
        }
    }

    void ResetSequence()
    {
        sequenceIndex = 0;
        errorCount = 0;
        if (currentSequence.Length > 0)
        {
            nextKey = currentSequence[sequenceIndex];
        }
        Debug.Log("Sequence reset. Current sequence: " + currentSequence);
        UpdateErrorCounter();
    }

    void ResetErrors()
    {
        errorCount = 0;
        UpdateErrorCounter();
        Debug.Log("Errors reset to 0.");
    }

    void ResetAll()
    {
        ResetSequence();
        ResetErrors();
        headlightTimer.ResetHeadlightAnimation();
    }

    void CheckSequence(InteractionButton pressedButton)
    {
        char pressedKey = GetKeyChar(pressedButton);
        Debug.Log($"Checking Sequence: Pressed key: {pressedKey}, Expected key: {nextKey}");

        if (pressedKey == nextKey)
        {
            sequenceIndex++;
            if (sequenceIndex < currentSequence.Length)
            {
                nextKey = currentSequence[sequenceIndex];
            }
            else
            {
                Debug.Log($"Sequence completed with {errorCount} errors.");
                headlightTimer.StopTimer(); // Stop the timer when the sequence is completed
                ResetSequence(); // Reset the sequence for a new round
            }
        }
        else if (!keyErrorStates[pressedButton])
        {
            errorCount++;
            keyErrorStates[pressedButton] = true; // Mark this key as having an error
            UpdateErrorCounter();
            Debug.Log($"Error count incremented to: {errorCount}");
        }
    }

    void UpdateErrorCounter()
    {
        errorCounterText.text = "Errors: " + errorCount;
        Debug.Log($"Updated Error Counter: {errorCount}");
    }

    string GetCurrentTMPText()
    {
        int currentPanelIndex = PanelSwitcher_K.currentPanelIndex;
        if (currentPanelIndex >= 0 && currentPanelIndex < panelTexts.Length)
        {
            return panelTexts[currentPanelIndex].text;
        }
        return ""; // Default to empty string if the index is out of range
    }

    char GetKeyChar(InteractionButton button)
    {
        if (button == keyC) return 'A';
        if (button == keyD) return 'B';
        if (button == keyE) return 'C';
        if (button == keyF) return 'D';
        if (button == keyG) return 'E';
        return ' ';
    }
}
