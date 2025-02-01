using UnityEngine;
using TMPro;
using System.Collections;
using Leap.Unity.Interaction;
using System.IO.Ports;
using System.Threading;
using System;

public class HeadlightTimer_K : MonoBehaviour
{
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;
    public TMP_Text timerText;
    public float countdownTime = 3.0f;

    public TMP_Text[] panelTexts;
    public InteractionButton[] interactionButtons;
    public CustomInteractionGlow_K[] keyGlows;
    public float keyPressDuration = 0.5f; // Duration for which each key remains pressed

    public TMP_Text nextKeyText; // Reference to the TMP_Text for displaying the next key

    private float timer;
    private bool isTiming;
    private bool countdownStarted;

    public float glowDuration = 0.5f; // Duration for which the key glows
    private SerialPort serialPort;
    private Thread dataThread;
    private string outgoingMessage = string.Empty;
    private string incomingMessage = string.Empty;

    public PanelSwitcher_K panelSwitcher;

    void Start()
    {
        if (redLight == null || yellowLight == null || greenLight == null || timerText == null || panelTexts.Length != 5 || interactionButtons.Length != 5 || keyGlows.Length != 5 || nextKeyText == null)
        {
            Debug.LogError("One or more GameObjects or TMP_Text components are not assigned in the inspector.");
            return;
        }

        ResetLights();
        timerText.text = "Press Enter to Start";

        serialPort = new SerialPort("COM5", 256000);
        serialPort.Open();

        dataThread = new Thread(DataThread);
        dataThread.Start();
    }

    void Update()
    {
        if (!countdownStarted && Input.GetKeyDown(KeyCode.Return))
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                serialPort = new SerialPort("COM5", 256000);
                serialPort.Open();
                dataThread = new Thread(DataThread);
                dataThread.Start();
            }
            StartCoroutine(StartCountdown());
        }

        if (isTiming)
        {
            timer += Time.deltaTime;
            UpdateTimerText(timer);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetHeadlightAnimation();
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
            if (dataThread != null && dataThread.IsAlive)
            {
                dataThread.Abort();
            }
        }

        int currentPanelIndex = PanelSwitcher_K.currentPanelIndex;
        string currentPanelText = panelTexts[currentPanelIndex].text;
        Debug.Log("Current Panel Text: " + currentPanelText);
    }

    IEnumerator StartCountdown()
    {
        countdownStarted = true;

        if (panelSwitcher != null)
        {
            StartCoroutine(panelSwitcher.FadeOutPanel(PanelSwitcher_K.currentPanelIndex, countdownTime));
        }

        redLight.SetActive(true);
        timerText.text = "";
        yield return new WaitForSeconds(countdownTime);

        redLight.SetActive(false);
        yellowLight.SetActive(true);
        yield return new WaitForSeconds(countdownTime);

        yellowLight.SetActive(false);
        greenLight.SetActive(true);
        StartTimer();
    }

    void StartTimer()
    {
        timer = 0.0f;
        isTiming = true;
        StartCoroutine(GlowKeysInSequence());
    }

    public void StopTimer()
    {
        isTiming = false;
        timerText.text = string.Format("Final Time: {0:00}:{1:00}:{2:00}", Mathf.FloorToInt(timer / 60F), Mathf.FloorToInt(timer % 60F), Mathf.FloorToInt((timer * 100F) % 100F));

        // Send $6\n$ state when the timer ends
        SendEndStateCommand();
    }

    void UpdateTimerText(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60F);
        int seconds = Mathf.FloorToInt(time % 60F);
        int milliseconds = Mathf.FloorToInt((time * 100F) % 100F);
        timerText.text = string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds);
    }

    public void ResetHeadlightAnimation()
    {
        StopAllCoroutines();
        ResetLights();
        timer = 0.0f;
        isTiming = false;
        countdownStarted = false;
        timerText.text = "Press Enter to Start";

        if (panelSwitcher != null)
        {
            panelSwitcher.ShowPanel(PanelSwitcher_K.currentPanelIndex);
        }

        nextKeyText.text = ""; // Clear the next key display
    }

    private void ResetLights()
    {
        redLight.SetActive(false);
        yellowLight.SetActive(false);
        greenLight.SetActive(false);
    }

    private IEnumerator GlowKeysInSequence()
    {
        int currentPanelIndex = PanelSwitcher_K.currentPanelIndex;
        string sequence = panelTexts[currentPanelIndex].text;

        for (int i = 0; i < sequence.Length; i++)
        {
            char key = sequence[i];
            int keyIndex = KeyToIndex(key);
            if (keyIndex >= 0 && keyIndex < interactionButtons.Length)
            {
                interactionButtons[keyIndex].OnPress();
                keyGlows[keyIndex].HandlePress();
                keyGlows[keyIndex].GoodGlow = glowDuration;  // Set the GoodGlow duration
                SendServoCommand(keyIndex);
                Debug.Log("Sent command to Arduino for key: " + key);

                nextKeyText.text = "Next Key: " + key; // Update the next key display

                yield return new WaitForSeconds(keyPressDuration);

                interactionButtons[keyIndex].OnUnpress();
                // Note: Do not call HandleUnpress here as it will be handled by the GoodGlow logic
            }
        }

        StopTimer();
        nextKeyText.text = "Sequence Complete"; // Indicate sequence completion
    }

    private int KeyToIndex(char key)
    {
        switch (key)
        {
            case 'A': return 0; // Virtual_A (Thumb)
            case 'B': return 1; // Virtual_B (Index)
            case 'C': return 2; // Virtual_C (Middle)
            case 'D': return 3; // Virtual_D (Ring)
            case 'E': return 4; // Virtual_E (Pinky)
            default: return -1;
        }
    }

    private void SendServoCommand(int keyIndex)
    {
        // int angleChange = (keyIndex <= 2) ? -50 : 50;
        int angleChange = 0;
        if(keyIndex == 0 || keyIndex == 2){
            angleChange = -50;
        }
        else if(keyIndex == 1){
            angleChange = -40;
        }
        else{
            angleChange = 50;
        
        }
        outgoingMessage = keyIndex + "," + angleChange + "\n";
    }

    private void SendEndStateCommand()
    {
        outgoingMessage = "$6\n$";
    }

    private void DataThread()
    {
        while (true)
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
                    Debug.Log("Received: " + incomingMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error with serial port operations: " + ex.Message);
                TryReopenSerialPort(); // Attempt to reopen the serial port on error
            }

            Thread.Sleep(200);
        }
    }

    private void OpenSerialPort()
    {
        if (serialPort != null && !serialPort.IsOpen)
        {
            try
            {
                serialPort.Open();
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to open serial port: " + ex.Message);
            }
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

    private void OnDestroy()
    {
        if (dataThread != null && dataThread.IsAlive)
        {
            dataThread.Abort();
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}
