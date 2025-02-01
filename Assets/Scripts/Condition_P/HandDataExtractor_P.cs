using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Leap;
using Leap.Unity;
using Leap.Unity.Interaction;
using TMPro;
using System.IO.Ports;
using System.Threading;
using System;
using System.Linq;

public class HandDataExtractor_P : MonoBehaviour
{
    public LeapServiceProvider leapServiceProvider;

    // References to the InteractionButton for each piano key
    public InteractionButton keyC; // Thumb
    public InteractionButton keyD; // Index
    public InteractionButton keyE; // Middle
    public InteractionButton keyF; // Ring
    public InteractionButton keyG; // Pinky

    // References to the CustomInteractionGlow for each piano key
    private CustomInteractionGlow_P glowC;
    private CustomInteractionGlow_P glowD;
    private CustomInteractionGlow_P glowE;
    private CustomInteractionGlow_P glowF;
    private CustomInteractionGlow_P glowG;

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

    public float glowDuration = 2f; // Duration in seconds for how long the glow lasts

    // Reference to the HeadlightTimer
    public HeadlightTimer_P headlightTimer;

    // Public variables for file path and sequence
    public string dataFilePath;
    private string currentSequence = "";
    private string sequenceToRecord = "";
    private int sequenceIndex = 0;
    private int errorCount = 0;
    private char nextKey;

    public bool isTiming = false;
    private bool showNextKey = false; // Flag to control the display of the next key
    public bool sequenceActive = false; // Add this flag to indicate if the sequence is active

    private readonly object serialPortLock = new object(); // Add a lock object

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

    private InteractionButton currentPressedKey = null;

    public SerialPort serialPort; // Serial port for sending angles
    private Thread dataThread;
    private bool lastKeyPressed = false; 
    private InteractionButton lastKey = null;
    private bool dataThreadRunning = false;
    private string outgoingMessage = "";
    private string incomingMessage = "";

    public bool serialPortInitialized = false;

    private float logInterval = 0.01f; // Log data every 0.1 seconds

    void Start()
    {
        glowC = keyC.GetComponent<CustomInteractionGlow_P>();
        glowD = keyD.GetComponent<CustomInteractionGlow_P>();
        glowE = keyE.GetComponent<CustomInteractionGlow_P>();
        glowF = keyF.GetComponent<CustomInteractionGlow_P>();
        glowG = keyG.GetComponent<CustomInteractionGlow_P>();

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
                    if (hand.IsLeft)
                    {
                        ProcessFinger(hand, hand.GetThumb(), keyC, glowC, pressThresholdC, restThresholdC, keyC, thumbAngles, 0);
                        ProcessFinger(hand, hand.GetIndex(), keyD, glowD, pressThresholdD, restThresholdD, keyD, indexAngles, 1);
                        ProcessFinger(hand, hand.GetMiddle(), keyE, glowE, pressThresholdE, restThresholdE, keyE, middleAngles, 2);
                        ProcessFinger(hand, hand.GetRing(), keyF, glowF, pressThresholdF, restThresholdF, keyF, ringAngles, 3);
                        ProcessFinger(hand, hand.GetPinky(), keyG, glowG, pressThresholdG, restThresholdG, keyG, pinkyAngles, 4);
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
                if (serialPortInitialized && serialPort.IsOpen)
                {
                    CloseSerialPort(); // Close the serial port when "R" is pressed
                }
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                ResetErrors();
                showNextKey = true;
                sequenceToRecord = currentSequence;
                string newFilePath = DetermineDataFilePath("AM");
                dataFilePath = newFilePath;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ResetErrors();
                showNextKey = true;
                sequenceToRecord = currentSequence;
                string newFilePath = DetermineDataFilePath("P");
                dataFilePath = newFilePath;

                if (serialPortInitialized && serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        StopDataThread();
        if (serialPort.IsOpen)
        {
            serialPort.Close();
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

    private void ProcessFinger(Hand hand, Finger finger, InteractionButton pianoKey, CustomInteractionGlow_P glow, float pressThreshold, float restThreshold, InteractionButton keyButton, List<float> angleList, int fingerIndex)
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

        // Log the angle only if timing is active
        if (isTiming)
        {
            if (angleList.Count == 0)
            {
                //UnityEngine.Debug.LogError("Recording of angles has started");
            }
            angleList.Add(angle);
        }

        bool isPressed = (finger.Type == Finger.FingerType.TYPE_THUMB || finger.Type == Finger.FingerType.TYPE_INDEX || finger.Type == Finger.FingerType.TYPE_MIDDLE) ? angle < pressThreshold : angle > pressThreshold;
        bool isReset = (finger.Type == Finger.FingerType.TYPE_THUMB || finger.Type == Finger.FingerType.TYPE_INDEX || finger.Type == Finger.FingerType.TYPE_MIDDLE) ? angle >= restThreshold : angle <= restThreshold;

        switch (fingerStates[pianoKey])
        {
            case "Idle":
                if (isPressed && (currentPressedKey == null || currentPressedKey == pianoKey))
                {
                    bool isCorrect = CheckSequence(keyButton);
                    glow.HandlePress(isCorrect); // Pass correctness to the glow handler
                    pianoKey.OnPress.Invoke();
                    keyPressStates[pianoKey] = true;
                    fingerStates[pianoKey] = "Pressed";
                    currentPressedKey = pianoKey;

                    // Record the time of the key press
                    if (isTiming)
                    {
                        keyTimestamps.Add(headlightTimer.timer);
                        timestampKeys.Add(nextKey.ToString()); // Add key at the timestamp
                    }

                    if (finger.Type == Finger.FingerType.TYPE_THUMB)
                    {
                        SendFingerState(0, 1);
                    }
                    else if (finger.Type == Finger.FingerType.TYPE_INDEX)
                    {
                        SendFingerState(1, 1);
                    }
                    else if (finger.Type == Finger.FingerType.TYPE_MIDDLE)
                    {
                        SendFingerState(2, 1);
                    }
                    else if (finger.Type == Finger.FingerType.TYPE_RING)
                    {
                        SendFingerState(3, 1);
                    }
                    else if (finger.Type == Finger.FingerType.TYPE_PINKY)
                    {
                        SendFingerState(4, 1);
                    }
                }
                else if (isPressed && (currentPressedKey != null && currentPressedKey != pianoKey))
                {
                    if (isTiming && !keyErrorStates[pianoKey])
                    {
                        errorCount++;
                        keyErrorStates[pianoKey] = true;
                        UpdateErrorCounter();
                        LogError(pianoKey); // Log the error with timestamp and key
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
                    currentPressedKey = null;

                    if (IsLastKeyInSequence(pianoKey))
                    {
                        headlightTimer.StopTimer();
                        SaveFingerAnglesToFile(); // Save the data when the sequence ends
                        ResetSequence();
                    }
                    CheckForUniversalIdleState();

                    if (finger.Type == Finger.FingerType.TYPE_THUMB)
                    {
                        SendFingerState(0, 0);
                    }
                    else if (finger.Type == Finger.FingerType.TYPE_INDEX)
                    {
                        SendFingerState(1, 0);
                    }
                    else if (finger.Type == Finger.FingerType.TYPE_MIDDLE)
                    {
                        SendFingerState(2, 0);
                    }
                    else if (finger.Type == Finger.FingerType.TYPE_RING)
                    {
                        SendFingerState(3, 0);
                    }
                    else if (finger.Type == Finger.FingerType.TYPE_PINKY)
                    {
                        SendFingerState(4, 0);
                    }
                }
                else
                {
                    // Send angle data continuously while the finger is pressed
                    if (isTiming)
                    {
                        if (finger.Type == Finger.FingerType.TYPE_THUMB)
                        {
                            SendFingerState(0, 1);
                        }
                        else if (finger.Type == Finger.FingerType.TYPE_INDEX)
                        {
                            SendFingerState(1, 1);
                        }
                        else if (finger.Type == Finger.FingerType.TYPE_MIDDLE)
                        {
                            SendFingerState(2, 1);
                        }
                        else if (finger.Type == Finger.FingerType.TYPE_RING)
                        {
                            SendFingerState(3, 1);
                        }
                        else if (finger.Type == Finger.FingerType.TYPE_PINKY)
                        {
                            SendFingerState(4, 1);
                        }
                    }
                }
                break;
        }
    }

    private bool IsLastKeyInSequence(InteractionButton keyButton)
    {
        char pressedKey = GetKeyChar(keyButton);
        return sequenceIndex == currentSequence.Length;
    }

    private void SendFingerState(int fingerIndex, int eventFlag)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            string data = $"{fingerIndex},{eventFlag}\n";
            outgoingMessage = data;
            Debug.Log($"Sending finger {fingerIndex} state: {data}");
        }
        else
        {
            //UnityEngine.Debug.LogError("Serial port is not open. Unable to send finger state data.");
        }
    }

    private void CheckForUniversalIdleState()
    {
        bool anyFingerPressed = false;

        foreach (var state in fingerStates.Values)
        {
            if (state == "Pressed")
            {
                anyFingerPressed = true;
                break;
            }
        }

        if (!anyFingerPressed)
        {
            // Remove the call to SendIdleState
            //SendIdleState();
        }
    }

    // Remove the SendIdleState method
    /*
    private void SendIdleState()
    {
        string data = $"5\n"; // Represents the universally idle state
        UnityEngine.Debug.Log("Sending idle state to Arduino.");
        if (serialPort.IsOpen)
        {
            outgoingMessage = data;
        }
        // UnityEngine.Debug.LogError("All fingers are idle.");
    }
    */

    public void ResetAllGlows()
    {
        foreach (var key in keyPressStates.Keys)
        {
            if (keyPressStates[key])
            {
                key.OnUnpress.Invoke();
                CustomInteractionGlow_P glow = key.GetComponent<CustomInteractionGlow_P>();
                if (glow != null)
                {
                    glow.HandleUnpress();
                }
                keyPressStates[key] = false;
                fingerStates[key] = "Idle"; // Ensure the state is reset
            }
        }
        currentPressedKey = null;
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

    public void ResetAll()
    {
        ResetSequence();
        ResetErrors();
        headlightTimer.ResetHeadlightAnimation();
        StopExecutable();
        sequenceActive = false;
        ResetAllGlows();
        // Remove the call to SendEndSignal
        //SendEndSignal(); // Send integer 6 when resetting
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
                TriggerNextKeyGlow(nextKey); // Ensure the next key glow is updated
            }
            else
            {
                nextKey = '\0'; // Sequence has ended, set nextKey to null character
                UnityEngine.Debug.Log("Sequence has ended.");
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

    void UpdateErrorCounter()
    {
        errorCounterText.text = "Errors: " + errorCount;
    }

    string GetCurrentTMPText()
    {
        int currentPanelIndex = PanelSwitcher_P.currentPanelIndex;
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

    public void UpdateNextKeyDisplay()
    {
        if (nextKey != 0)  // Assuming default uninitialized char is 0
        {
            // Update the next key display
            nextKeyText.text = "Next Key: " + nextKey;

            // Trigger yellow glow on the next key
            TriggerNextKeyGlow(nextKey);
        }
        else
        {
            nextKeyText.text = "Complete or Start Sequence";
        }
    }

    private void TriggerNextKeyGlow(char nextKey)
    {
        InteractionButton nextKeyButton = GetKeyButton(nextKey);
        if (nextKeyButton != null)
        {
            CustomInteractionGlow_P glow = nextKeyButton.GetComponent<CustomInteractionGlow_P>();
            if (glow != null)
            {
                glow.HandleNextKeyGlow(glowDuration); // Use the public variable to set the duration
                //UnityEngine.Debug.Log($"TriggerNextKeyGlow: Triggered yellow glow for key {nextKey}");
            }
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

    public void StartRecording()
    {
        isTiming = true;
        sequenceActive = true; // Set the sequence active flag
        UpdateNextKeyDisplay(); // Show the next key and log the initial timestamp
        if (keyTimestamps.Count == 0)
        {
            keyTimestamps.Add(0); // Log initial timestamp as 0
            timestampKeys.Add(nextKey.ToString()); // Log the initial key
        }
        TriggerNextKeyGlow(nextKey); // Ensure the next key glow is updated
    }

    private void StopExecutable()
    {
        if (headlightTimer.executableProcess != null && !headlightTimer.executableProcess.HasExited)
        {
            headlightTimer.executableProcess.Kill();
            headlightTimer.executableProcess = null;
        }
    }

    private void SaveFingerAnglesToFile()
    {
        string baseFilePath = Path.Combine(dataFilePath, sequenceToRecord + "_data.txt");
        string uniqueFilePath = GenerateUniqueFilePath(baseFilePath);

        using (StreamWriter writer = new StreamWriter(uniqueFilePath))
        {
            writer.WriteLine("SEQUENCE - " + sequenceToRecord);
            writer.WriteLine("THUMB ANGLES - " + FormatAnglesAsIntegers(thumbAngles));
            writer.WriteLine("INDEX ANGLES - " + FormatAnglesAsIntegers(indexAngles));
            writer.WriteLine("MIDDLE ANGLES - " + FormatAnglesAsIntegers(middleAngles));
            writer.WriteLine("RING ANGLES - " + FormatAnglesAsIntegers(ringAngles));
            writer.WriteLine("PINKY ANGLES - " + FormatAnglesAsIntegers(pinkyAngles));
            writer.WriteLine("TIMESTAMPS - " + string.Join(", ", keyTimestamps));
            writer.WriteLine("TIMESTAMP KEYS - " + string.Join(", ", timestampKeys)); // Add timestamp keys
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
        errorTimestamps.Clear();
        errorKeys.Clear();
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

    private string DetermineDataFilePath(string testingCondition)
    {
        string dataPathFile = "C:\\Users\\afons\\OneDrive\\Documentos\\5 Ano 2 Semestre\\Thesis\\data_path.txt";
        if (File.Exists(dataPathFile))
        {
            string basePath = File.ReadAllText(dataPathFile).Trim();
            string path = basePath.Replace("Pre", "Training").Replace("Post", "Training");
            path = path.Replace("K Condition", $"{testingCondition} Condition");
            Debug.Log("Data file path set to: " + path);
            return path;
        }
        else
        {
            Debug.LogError("Data path file not found.");
            return string.Empty;
        }
    }

    private string GenerateUniqueFilePath(string baseFilePath)
    {
        string directory = Path.GetDirectoryName(baseFilePath);
        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(baseFilePath);
        string extension = Path.GetExtension(baseFilePath);
        
        int fileCount = 1;
        string newFilePath = baseFilePath;

        while (File.Exists(newFilePath))
        {
            newFilePath = Path.Combine(directory, $"{filenameWithoutExtension}({fileCount}){extension}");
            fileCount++;
        }

        return newFilePath;
    }

    // New method to handle ending the sequence due to timeout
    public void EndSequenceDueToTimeout()
    {
        sequenceActive = false;
        isTiming = false;
        SaveFingerAnglesToFile();
        ResetSequence();
        // Remove the call to SendEndSignal
        //SendEndSignal(); // Send integer 6 when sequence ends
    }

    private string GetFingerName(int fingerIndex)
    {
        switch (fingerIndex)
        {
            case 0: return "Thumb";
            case 1: return "Index";
            case 2: return "Middle";
            case 3: return "Ring";
            case 4: return "Pinky";
            default: return "Unknown";
        }
    }

    public void StartSendingAngles()
    {
        isTiming = true;
    }

    public void StopSendingAngles()
    {
        isTiming = false;
    }

    public void InitializeSerialPort()
    {
        lock (serialPortLock)
        {
            if (serialPort == null)
            {
                serialPort = new SerialPort("COM5", 256000); // Replace with your serial port name and baud rate
            }

            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
                serialPort.Open();
                //execute a serial flush
                serialPort.BaseStream.Flush();
                serialPortInitialized = true;
                StartDataThread();
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to open serial port: " + ex.Message);
            }
        }
    }

    public void CloseSerialPort()
    {
        lock (serialPortLock)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    serialPort.BaseStream.Flush(); // Ensure all data is written
                    serialPort.Close();
                    Debug.Log("Serial port closed successfully.");
                }
                catch (Exception ex)
                {
                    Debug.LogError("Failed to close serial port: " + ex.Message);
                }
            }
        }
    }

    // Start data thread for serial communication
    private void StartDataThread()
    {
        dataThreadRunning = true;
        dataThread = new Thread(DataThread);
        dataThread.Start();
    }

    private void StopDataThread()
    {
        dataThreadRunning = false;
        if (dataThread != null && dataThread.IsAlive)
        {
            dataThread.Join();
        }
    }

    private void DataThread()
    {
        while (dataThreadRunning)
        {
            lock (serialPortLock)
            {
                try
                {
                    if (serialPort == null || !serialPort.IsOpen)
                    {
                        OpenSerialPort(); // Attempt to open the serial port if not already open
                    }

                    if (!string.IsNullOrEmpty(outgoingMessage) && serialPort.IsOpen)
                    {
                        serialPort.Write(outgoingMessage);
                        Debug.Log("Sending to Arduino: " + outgoingMessage);
                        outgoingMessage = string.Empty;
                    }

                    if (serialPort.IsOpen && serialPort.BytesToRead > 0)
                    {
                        incomingMessage = serialPort.ReadExisting();
                        // Debug.Log("Received: " + incomingMessage);
                    }
                }
                catch (Exception ex)
                {
                    // Debug.LogError("Error with serial port operations: " + ex.Message);
                    TryReopenSerialPort(); // Attempt to reopen the serial port on error
                }
            }

            Thread.Sleep(200);
        }
    }    
    private void TryReopenSerialPort()
    {
        if (serialPort != null)
        {
            if (serialPort.IsOpen)
            {
                try
                {
                    serialPort.Close();
                }
                catch (Exception ex)
                {
                    Debug.LogError("Failed to close serial port: " + ex.Message);
                }
            }
            OpenSerialPort();
        }
    }

    public void OpenSerialPort()
    {
        try
        {
            if (serialPort == null)
            {
                serialPort = new SerialPort("COM5", 256000); // Replace with your serial port name and baud rate
            }

            if (!serialPort.IsOpen)
            {
                serialPort.Open();
                serialPort.DiscardInBuffer(); // Clear any data in the input buffer
                serialPort.DiscardOutBuffer(); // Clear any data in the output buffer
                Debug.Log("Serial port opened successfully.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to open serial port: " + ex.Message);
        }
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
                    if (hand.IsLeft)
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
