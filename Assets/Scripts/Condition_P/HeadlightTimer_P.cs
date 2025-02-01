using UnityEngine;
using TMPro;
using System.Collections;
using System.Diagnostics;

public class HeadlightTimer_P : MonoBehaviour
{
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;
    public TMP_Text timerText;
    public float countdownTime = 3.0f;
    public float flashDuration = 0.2f; // Duration of each flash

    public float timer;
    private bool isTiming;
    private bool countdownStarted;

    // Add references to the piano keys
    public GameObject[] pianoKeys;
    private CustomInteractionGlow_P[] keyGlowScripts;

    // Reference to the PanelSwitcher to get the current sequence
    public PanelSwitcher_P panelSwitcher;

    // Process to keep track of the running executable
    public Process executableProcess;

    // References for visual cue
    public TMP_Text timesUpText;

    public float RanOutTime = 60.0f; // Default to 60 seconds

    void Start()
    {
        if (redLight == null || yellowLight == null || greenLight == null || timerText == null || pianoKeys.Length == 0 || panelSwitcher == null || timesUpText == null)
        {
            UnityEngine.Debug.LogError("One or more GameObjects are not assigned in the inspector.");
            return;
        }

        // Initialize the lights and visual cue elements
        ResetLights();
        ResetVisualCues();
        timerText.text = "Press Enter to Start";

        // Get the glow scripts from the child objects of the piano keys
        keyGlowScripts = new CustomInteractionGlow_P[pianoKeys.Length];
        for (int i = 0; i < pianoKeys.Length; i++)
        {
            Transform childTransform = pianoKeys[i].transform.Find("Virtual_" + pianoKeys[i].name);
            if (childTransform != null)
            {
                keyGlowScripts[i] = childTransform.GetComponent<CustomInteractionGlow_P>();
                if (keyGlowScripts[i] == null)
                {
                    UnityEngine.Debug.LogError("CustomInteractionGlow component is missing on key: " + pianoKeys[i].name);
                }
            }
            else
            {
                UnityEngine.Debug.LogError("Child object Virtual_" + pianoKeys[i].name + " is missing for key: " + pianoKeys[i].name);
            }
        }
    }

    void Update()
    {
        if (!countdownStarted && Input.GetKeyDown(KeyCode.Return))
        {
            StartCoroutine(StartCountdown(true)); // StartCountdown with true to open serial port
        }
        else if (!countdownStarted && Input.GetKeyDown(KeyCode.Backspace))
        {
            StartCoroutine(StartCountdown(false)); // StartCountdown with false to skip serial port opening
        }

        if (isTiming)
        {
            timer += Time.deltaTime;
            UpdateTimerText(timer);

            if (timer >= RanOutTime)
            {
                StopTimer(); // This will now handle ending the sequence due to timeout
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetHeadlightAnimation();
            StopExecutable();
            var handDataExtractor = FindObjectOfType<HandDataExtractor_P>();
            handDataExtractor.StopSendingAngles();
            // Ensure the serial port is properly closed and flushed
            handDataExtractor.CloseSerialPort();
            // Reopen the serial port with proper flushing
            handDataExtractor.OpenSerialPort();
        }
    }

    // Modify the StartCountdown coroutine to accept a parameter
    IEnumerator StartCountdown(bool startDataStreaming = true)
    {
        countdownStarted = true;

        // Fade out the current panel
        yield return StartCoroutine(FadeOutPanel(panelSwitcher.panels[PanelSwitcher_P.currentPanelIndex]));

        // Show red light
        redLight.SetActive(true);
        timerText.text = "";
        // Open the serial port when the yellow light is shown
        if (startDataStreaming)
        {
            //execute a general serial flush
            var handDataExtractor = FindObjectOfType<HandDataExtractor_P>();
            handDataExtractor.InitializeSerialPort();
            
        }
        yield return new WaitForSeconds(countdownTime);

        // Show yellow light
        redLight.SetActive(false);
        yellowLight.SetActive(true);

        yield return new WaitForSeconds(countdownTime);

        // Show green light and start timer
        yellowLight.SetActive(false);
        greenLight.SetActive(true);
        StartTimer();

        // Conditionally start sending angles
        if (startDataStreaming)
        {
            FindObjectOfType<HandDataExtractor_P>().StartSendingAngles();
        }
    }
    public void StartTimer()
    {
        timer = 0.0f;
        isTiming = true;
        UnityEngine.Debug.LogError("Recording of angles has started");
        var handDataExtractor = FindObjectOfType<HandDataExtractor_P>();
        handDataExtractor.isTiming = true;
        handDataExtractor.sequenceActive = true; // Set sequenceActive flag here
        handDataExtractor.StartRecording(); // Notify HandDataExtractor that the timer has started
    }

    public void StopTimer()
    {
        isTiming = false;
        var handDataExtractor = FindObjectOfType<HandDataExtractor_P>();
        handDataExtractor.isTiming = false;
        handDataExtractor.sequenceActive = false;
        handDataExtractor.ResetAllGlows();
        // handDataExtractor.SendEndSignal(); // Send integer 6 when timer stops

        timerText.text = string.Format("Final Time: {0:00}:{1:00}:{2:00}", Mathf.FloorToInt(timer / 60F), Mathf.FloorToInt(timer % 60F), Mathf.FloorToInt((timer * 100F) % 100F));

        StartCoroutine(TimesUpAnimation());

        handDataExtractor.EndSequenceDueToTimeout();
    }


    public void ResetHeadlightAnimation()
    {
        StopAllCoroutines();
        ResetLights();
        ResetVisualCues();
        timer = 0.0f;
        isTiming = false;
        countdownStarted = false;
        timerText.text = "Press Enter to Start";

        // Ensure all glows are reset
        var handDataExtractor = FindObjectOfType<HandDataExtractor_P>();
        handDataExtractor.ResetAllGlows();

        // Optionally reset panel visibility when resetting the timer
        if (panelSwitcher != null)
        {
            panelSwitcher.ShowPanel(PanelSwitcher_P.currentPanelIndex);
        }
    }

    void UpdateTimerText(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60F);
        int seconds = Mathf.FloorToInt(time % 60F);
        int milliseconds = Mathf.FloorToInt((time * 100F) % 100F);
        timerText.text = string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds);
    }

    private void ResetLights()
    {
        redLight.SetActive(false);
        yellowLight.SetActive(false);
        greenLight.SetActive(false);
    }

    private void ResetVisualCues()
    {
        // Reset "Time's Up!" text visibility
        CanvasGroup canvasGroup = timesUpText.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
        }
    }

    private void StopExecutable()
    {
        if (executableProcess != null && !executableProcess.HasExited)
        {
            executableProcess.Kill();
            executableProcess = null;
            UnityEngine.Debug.Log("Executable has been stopped.");
        }
    }

    void OnApplicationQuit()
    {
        StopExecutable();
    }

    IEnumerator FadeOutPanel(GameObject panel)
    {
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            yield break;
        }

        float fadeDuration = countdownTime; // Adjust as necessary
        float startAlpha = canvasGroup.alpha;
        float rate = 1.0f / fadeDuration;
        float progress = 0.0f;

        while (progress < 1.0f)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, progress);
            progress += rate * Time.deltaTime;
            yield return null;
        }

        canvasGroup.alpha = 0;
    }

    IEnumerator TimesUpAnimation()
    {
        CanvasGroup canvasGroup = timesUpText.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            yield break;
        }

        float pulseDuration = 0.5f;
        float maxAlpha = 1.0f;
        float minAlpha = 0.0f;

        // Make "Time's Up!" text visible
        canvasGroup.alpha = maxAlpha;

        // Pulse the text a few times
        for (int i = 0; i < 3; i++)
        {
            // Fade out
            float startAlpha = maxAlpha;
            float endAlpha = minAlpha;
            float progress = 0.0f;
            while (progress < 1.0f)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                progress += Time.deltaTime / pulseDuration;
                yield return null;
            }
            canvasGroup.alpha = endAlpha;

            // Fade in
            startAlpha = minAlpha;
            endAlpha = maxAlpha;
            progress = 0.0f;
            while (progress < 1.0f)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                progress += Time.deltaTime / pulseDuration;
                yield return null;
            }
            canvasGroup.alpha = endAlpha;
        }

        // Hide the text
        canvasGroup.alpha = 0;
    }
}
