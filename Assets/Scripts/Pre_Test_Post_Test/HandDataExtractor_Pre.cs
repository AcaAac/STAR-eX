using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Leap;
using Leap.Unity;
using Leap.Unity.Interaction;
using TMPro;
using System;

public class HandDataExtractor : MonoBehaviour
{
    public LeapServiceProvider leapServiceProvider;

    // References to the InteractionButton for each piano key
    public InteractionButton keyC; // Thumb
    public InteractionButton keyD; // Index
    public InteractionButton keyE; // Middle
    public InteractionButton keyF; // Ring
    public InteractionButton keyG; // Pinky

    // References to the CustomInteractionGlow for each piano key
    private CustomInteractionGlow_Pre glowC;
    private CustomInteractionGlow_Pre glowD;
    private CustomInteractionGlow_Pre glowE;
    private CustomInteractionGlow_Pre glowF;
    private CustomInteractionGlow_Pre glowG;
    public bool isSequenceStarted = false; // Add this flag to indicate if the sequence has started

    // New flag to indicate if the sequence is active (i.e., the timer has started)
    private bool sequenceActive = false;

    // Thresholds for each finger to trigger OnPress
    public float pressThresholdC = 10f; // Thumb
    public float pressThresholdD = 10f; // Index
    public float pressThresholdE = 10f; // Middle
    public float pressThresholdF = 10f; // Ring
    public float pressThresholdG = 10f; // Pinky

    // Rest thresholds for each finger to return to idle state
    public float restThresholdC = 5f; // Thumb
    public float restThresholdD = 5f; // Index
    public float restThresholdE = 5f; // Middle
    public float restThresholdF = 5f; // Ring
    public float restThresholdG = 5f; // Pinky

    // Track the state of each key press and error state
    private Dictionary<InteractionButton, bool> keyPressStates = new Dictionary<InteractionButton, bool>();
    private Dictionary<InteractionButton, bool> keyErrorStates = new Dictionary<InteractionButton, bool>();

    // Track the FSM state of each finger
    private Dictionary<InteractionButton, string> fingerStates = new Dictionary<InteractionButton, string>();

    // References to the TMP text components for each panel
    public TextMeshProUGUI[] panelTexts;

    // Reference to the TMP text for error counter
    public TextMeshProUGUI errorCounterText;

    // Reference to the TMP text for displaying the next key
    public TextMeshProUGUI nextKeyText;

    // Reference to the HeadlightTimer
    public HeadlightTimer_Pre headlightTimer;

    // Public variables for file path and sequence
    private string dataFilePath;
    private string currentSequence = "";
    private string sequenceToRecord = "";
    private int sequenceIndex = 0;
    private int errorCount = 0;
    private char nextKey;

    public bool isTiming = false;
    private bool showNextKey = false; // Flag to control the display of the next key

    // Variables for logging finger angles
    private List<float> thumbAngles = new List<float>();
    private List<float> indexAngles = new List<float>();
    private List<float> middleAngles = new List<float>();
    private List<float> ringAngles = new List<float>();
    private List<float> pinkyAngles = new List<float>();

    // Variables for logging timing data
    private List<float> keyTimestamps = new List<float>();
    private List<string> timestampKeys = new List<string>(); // Add this list

    private List<float> errorTimestamps = new List<float>();
    private List<string> errorKeys = new List<string>();

    private List<float> yellowGlowTimestamps = new List<float>(); // Add this list
    public float nextKeyGlowDuration = 2f; // Add this public variable to set the duration

    private float logInterval = 0.01f; // Log data every 0.1 seconds

    void Start()
    {
        if (leapServiceProvider == null)
        {
            // Debug.LogError("LeapServiceProvider is not assigned.");
        }

        if (keyC == null || keyD == null || keyE == null || keyF == null || keyG == null)
        {
            // Debug.LogError("One or more InteractionButton references are not assigned.");
        }

        glowC = keyC.GetComponent<CustomInteractionGlow_Pre>();
        glowD = keyD.GetComponent<CustomInteractionGlow_Pre>();
        glowE = keyE.GetComponent<CustomInteractionGlow_Pre>();
        glowF = keyF.GetComponent<CustomInteractionGlow_Pre>();
        glowG = keyG.GetComponent<CustomInteractionGlow_Pre>();

        if (panelTexts.Length == 0)
        {
            // Debug.LogError("No TextMeshProUGUI references are assigned.");
        }

        if (errorCounterText == null)
        {
            // Debug.LogError("ErrorCounterText is not assigned.");
        }

        if (nextKeyText == null)
        {
            // Debug.LogError("NextKeyText is not assigned.");
        }

        if (headlightTimer == null)
        {
            // Debug.LogError("HeadlightTimer is not assigned.");
        }

        UpdateErrorCounter();
        InitializeKeyStates();
        ResetSequence();

        StartCoroutine(LogDataCoroutine());
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
                    if (hand.IsRight)
                    {
                        ProcessFinger(hand, hand.GetThumb(), keyC, glowC, pressThresholdC, restThresholdC, keyC, thumbAngles);
                        ProcessFinger(hand, hand.GetIndex(), keyD, glowD, pressThresholdD, restThresholdD, keyD, indexAngles);
                        ProcessFinger(hand, hand.GetMiddle(), keyE, glowE, pressThresholdE, restThresholdE, keyE, middleAngles);
                        ProcessFinger(hand, hand.GetRing(), keyF, glowF, pressThresholdF, restThresholdF, keyF, ringAngles);
                        ProcessFinger(hand, hand.GetPinky(), keyG, glowG, pressThresholdG, restThresholdG, keyG, pinkyAngles);
                    }
                }
            }

            string newSequence = GetCurrentTMPText();
            if (newSequence != currentSequence)
            {
                currentSequence = newSequence;
                ResetSequence();
            }

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                ResetErrors();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetAll();
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                ResetErrors();
                showNextKey = true;
                sequenceToRecord = currentSequence;
                UpdateNextKeyDisplay();
            }
        }
    }

    private void InitializeKeyStates()
    {
        InteractionButton[] keys = { keyC, keyD, keyE, keyF, keyG };
        foreach (var key in keys)
        {
            keyPressStates[key] = false;
            keyErrorStates[key] = false;
            fingerStates[key] = "Idle";
        }
    }

    private void ProcessFinger(Hand hand, Finger finger, InteractionButton pianoKey, CustomInteractionGlow_Pre glow, float pressThreshold, float restThreshold, InteractionButton keyButton, List<float> angleList)
    {
        if (!sequenceActive) return; // Ignore finger processing if sequence is not active

        Vector3 direction1, direction2;

        if (finger.Type == Finger.FingerType.TYPE_THUMB)
        {
            direction1 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_PROXIMAL).Direction);
            direction2 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_DISTAL).Direction);
        }
        else
        {
            direction1 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_METACARPAL).Direction);
            direction2 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_PROXIMAL).Direction);
        }

        Vector3 handDirection = Utils.ToVector3(hand.Direction);
        direction1 = Quaternion.FromToRotation(handDirection, Vector3.forward) * direction1;
        direction2 = Quaternion.FromToRotation(handDirection, Vector3.forward) * direction2;

        Vector3 crossProduct = Vector3.Cross(direction1, direction2);
        float angle = Vector3.Angle(direction1, direction2);
        if (crossProduct.z < 0) angle = -angle;

        angleList.Add(angle); // Log the angle

        bool isPressed = (finger.Type == Finger.FingerType.TYPE_THUMB || finger.Type == Finger.FingerType.TYPE_INDEX || finger.Type == Finger.FingerType.TYPE_MIDDLE) ? angle >= pressThreshold : angle <= pressThreshold;
        bool isReset = (finger.Type == Finger.FingerType.TYPE_THUMB || finger.Type == Finger.FingerType.TYPE_INDEX || finger.Type == Finger.FingerType.TYPE_MIDDLE) ? angle <= restThreshold : angle >= restThreshold;

        string fingerName = finger.Type.ToString().Replace("TYPE_", "").ToLower();

        switch (fingerStates[pianoKey])
        {
            case "Idle":
                if (isPressed)
                {
                    bool isCorrect = CheckSequence(keyButton);
                    glow.HandlePress(isCorrect); // Pass correctness to the glow handler
                    pianoKey.OnPress.Invoke();
                    keyPressStates[pianoKey] = true;
                    fingerStates[pianoKey] = "Pressed";

                    // Record the time of the key press
                    if (isTiming)
                    {
                        keyTimestamps.Add(headlightTimer.timer);
                        timestampKeys.Add(nextKey.ToString()); // Add key at the timestamp
                    }
                }
                break;

            case "Pressed":
                if (isReset)
                {
                    glow.HandleUnpress();
                    pianoKey.OnUnpress.Invoke();
                    keyPressStates[pianoKey] = false;
                    fingerStates[pianoKey] = "Idle";
                    keyErrorStates[pianoKey] = false; // Reset error state on key release
                }
                break;
        }
    }

    public void ResetAllGlows()
    {
        CustomInteractionGlow_Pre[] glows = { glowC, glowD, glowE, glowF, glowG };
        foreach (var glow in glows)
        {
            if (glow != null)
            {
                glow.HandleUnpress();
                glow.StopAllCoroutines(); // Ensure all coroutines are stopped
            }
        }
    }

    void ResetSequence()
    {
        sequenceIndex = 0;
        showNextKey = false; // Reset the flag
        nextKeyText.text = ""; // Clear the next key display
        if (currentSequence.Length > 0)
        {
            nextKey = currentSequence[sequenceIndex];
        }
        UpdateErrorCounter();
    }

    void ResetErrors()
    {
        errorCount = 0;
        UpdateErrorCounter();
    }

    void ResetAll()
    {
        ResetSequence();
        ResetErrors();
        headlightTimer.ResetHeadlightAnimation();
        ResetAllGlows(); // Add this line
        isSequenceStarted = false; // Reset the sequence started flag
        sequenceActive = false; // Reset the sequence active flag
    }

    void UpdateErrorCounter()
    {
        errorCounterText.text = "Errors: " + errorCount;
    }

    string GetCurrentTMPText()
    {
        int currentPanelIndex = PanelSwitcher_Pre.currentPanelIndex;
        if (currentPanelIndex >= 0 && currentPanelIndex < panelTexts.Length)
        {
            return panelTexts[currentPanelIndex].text;
        }
        return "";
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

    private void TriggerNextKeyGlow(char nextKey)
    {
        InteractionButton nextKeyButton = GetKeyButton(nextKey);
        if (nextKeyButton != null)
        {
            CustomInteractionGlow_Pre glow = nextKeyButton.GetComponent<CustomInteractionGlow_Pre>();
            if (glow != null)
            {
                glow.HandleNextKeyGlow(nextKeyGlowDuration); // Use the public variable to set the duration
                yellowGlowTimestamps.Add(Time.time); // Log the yellow glow timestamp
                UnityEngine.Debug.Log($"TriggerNextKeyGlow: Triggered yellow glow for key {nextKey}");
            }
        }
    }


    public void UpdateNextKeyDisplay()
    {
        if (!isSequenceStarted) return; // Do nothing if the sequence has not started

        if (nextKey != 0)  // Assuming default uninitialized char is 0
        {
            // Update the next key display
            nextKeyText.text = "Next Key: " + nextKey;

            // Record the time when the next key is displayed
            if (isTiming)
            {
                keyTimestamps.Add(headlightTimer.timer);
                timestampKeys.Add(nextKey.ToString()); // Add key at the timestamp
            }

            // Trigger yellow glow on the next key
            TriggerNextKeyGlow(nextKey);
        }
        else
        {
            nextKeyText.text = "Complete or Start Sequence";
        }
    }

    bool CheckSequence(InteractionButton pressedButton)
    {
        char pressedKey = GetKeyChar(pressedButton);

        if (pressedKey == nextKey)
        {
            sequenceIndex++;

            if (sequenceIndex < currentSequence.Length)
            {
                nextKey = currentSequence[sequenceIndex];
                if (showNextKey) // Only update display if Enter was pressed
                {
                    UpdateNextKeyDisplay();
                }
            }
            else
            {
                headlightTimer.StopTimer();
                SaveFingerAnglesToFile(); // Save the data when the sequence ends
                ResetSequence();
            }

            // Trigger the yellow glow for the next key even if it is the same key again
            if (showNextKey)
            {
                TriggerNextKeyGlow(nextKey);
            }
            return true; // Correct key press
        }
        else
        {
            if (isTiming && !keyErrorStates[pressedButton])
            {
                errorCount++;
                keyErrorStates[pressedButton] = true;
                UpdateErrorCounter();
                LogError(pressedButton); // Log the error with timestamp and key
            }
            return false; // Incorrect key press
        }
    }

    private InteractionButton GetKeyButton(char keyChar)
    {
        switch (keyChar)
        {
            case 'A': return keyC;
            case 'B': return keyD;
            case 'C': return keyE;
            case 'D': return keyF;
            case 'E': return keyG;
            default: return null;
        }
    }

    void OnApplicationQuit()
    {
        StopExecutable();
    }

    private void StopExecutable()
    {
        if (headlightTimer.executableProcess != null && !headlightTimer.executableProcess.HasExited)
        {
            headlightTimer.executableProcess.Kill();
            headlightTimer.executableProcess = null;
            UnityEngine.Debug.Log("Executable has been stopped.");
        }
    }

    private void SaveFingerAnglesToFile()
{
    // Ensure the latest data path is read
    dataFilePath = ReadDataFilePath();

    string fileName = $"{sequenceToRecord}_data.txt";
    string filePath = Path.Combine(dataFilePath, fileName);

    using (StreamWriter writer = new StreamWriter(filePath))
    {
        writer.WriteLine("SEQUENCE - " + sequenceToRecord);
        writer.WriteLine("THUMB ANGLES - " + FormatAnglesWithSystemTime(thumbAngles));
        writer.WriteLine("INDEX ANGLES - " + FormatAnglesWithSystemTime(indexAngles));
        writer.WriteLine("MIDDLE ANGLES - " + FormatAnglesWithSystemTime(middleAngles));
        writer.WriteLine("RING ANGLES - " + FormatAnglesWithSystemTime(ringAngles));
        writer.WriteLine("PINKY ANGLES - " + FormatAnglesWithSystemTime(pinkyAngles));
        writer.WriteLine("TIMESTAMPS - " + string.Join(", ", keyTimestamps));
        writer.WriteLine("TIMESTAMP KEYS - " + string.Join(", ", timestampKeys)); // Add timestamp keys
        writer.WriteLine("YELLOW TIMESTAMPS - " + string.Join(", ", yellowGlowTimestamps)); // Log yellow glow timestamps
        writer.WriteLine("ERROR TIMESTAMPS - " + string.Join(", ", errorTimestamps));
        writer.WriteLine("ERROR KEYS - " + string.Join(", ", errorKeys));
        writer.WriteLine("TOTAL TIME - " + headlightTimer.timer.ToString("F2"));
    }

    // Clear the lists for the next recording
    thumbAngles.Clear();
    indexAngles.Clear();
    middleAngles.Clear();
    ringAngles.Clear();
    pinkyAngles.Clear();
    keyTimestamps.Clear();
    timestampKeys.Clear();
    yellowGlowTimestamps.Clear(); // Clear yellow timestamps
    errorTimestamps.Clear();
    errorKeys.Clear();
}

private string FormatAnglesWithSystemTime(List<float> angles)
{
    List<string> angleData = new List<string>();
    foreach (float angle in angles)
    {
        string timeStampedAngle = $"{DateTime.Now.ToString("HH:mm:ss.fff")}: {Mathf.RoundToInt(angle)}";
        angleData.Add(timeStampedAngle);
    }
    return string.Join(", ", angleData);
}

    private string FormatAnglesAsIntegers(List<float> angles)
    {
        List<int> intAngles = new List<int>();
        foreach (float angle in angles)
        {
            intAngles.Add(Mathf.RoundToInt(angle));
        }
        return string.Join(", ", intAngles);
    }

    private void LogError(InteractionButton pianoKey)
    {
        if (isTiming)
        {
            errorTimestamps.Add(headlightTimer.timer);
            errorKeys.Add(GetKeyChar(pianoKey).ToString());
        }
    }

    // New method to start the sequence
    public void StartSequence()
    {
        sequenceActive = true;
        UnityEngine.Debug.Log("Sequence is now active.");
    }

    private string ReadDataFilePath()
    {
        string dataPathFile = "C:\\Users\\afons\\OneDrive\\Documentos\\5 Ano 2 Semestre\\Thesis\\data_path.txt";
        if (File.Exists(dataPathFile))
        {
            string path = File.ReadAllText(dataPathFile).Trim();
            UnityEngine.Debug.Log("Data file path set to: " + path);
            return path;
        }
        else
        {
            UnityEngine.Debug.LogError("Data path file not found.");
            return string.Empty;
        }
    }

    // New method to handle ending the sequence due to timeout
    public void EndSequenceDueToTimeout()
    {
        UnityEngine.Debug.Log("Sequence ended due to timeout.");
        sequenceActive = false;
        isSequenceStarted = false;
        SaveFingerAnglesToFile(); // Save the data even if the sequence is incomplete
        ResetSequence();
    }

    private IEnumerator LogDataCoroutine()
    {
        while (true)
        {
            LogCurrentAngles();
            yield return new WaitForSeconds(logInterval); // Adjust the interval as needed
        }
    }

    private void LogCurrentAngles()
    {
        if (leapServiceProvider != null)
        {
            Frame frame = leapServiceProvider.CurrentFrame;
            if (frame != null && frame.Hands.Count > 0)
            {
                foreach (Hand hand in frame.Hands)
                {
                    if (hand.IsRight)
                    {
                        LogFingerAngles(hand, hand.GetThumb(), thumbAngles);
                        LogFingerAngles(hand, hand.GetIndex(), indexAngles);
                        LogFingerAngles(hand, hand.GetMiddle(), middleAngles);
                        LogFingerAngles(hand, hand.GetRing(), ringAngles);
                        LogFingerAngles(hand, hand.GetPinky(), pinkyAngles);
                    }
                }
            }
        }
    }

    private void LogFingerAngles(Hand hand, Finger finger, List<float> angleList)
    {
        Vector3 direction1, direction2;

        if (finger.Type == Finger.FingerType.TYPE_THUMB)
        {
            direction1 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_PROXIMAL).Direction);
            direction2 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_DISTAL).Direction);
        }
        else
        {
            direction1 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_METACARPAL).Direction);
            direction2 = Utils.ToVector3(finger.Bone(Bone.BoneType.TYPE_PROXIMAL).Direction);
        }

        Vector3 handDirection = Utils.ToVector3(hand.Direction);
        direction1 = Quaternion.FromToRotation(handDirection, Vector3.forward) * direction1;
        direction2 = Quaternion.FromToRotation(handDirection, Vector3.forward) * direction2;

        Vector3 crossProduct = Vector3.Cross(direction1, direction2);
        float angle = Vector3.Angle(direction1, direction2);
        if (crossProduct.z < 0) angle = -angle;

        angleList.Add(angle); // Log the angle
    }
}
