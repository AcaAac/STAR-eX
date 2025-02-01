using UnityEngine;
using TMPro;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System;
using System.Threading;

public class HeadlightTimer_Pre : MonoBehaviour
{
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;
    public TMP_Text timerText;
    public float countdownTime = 3.0f;
    public float flashDuration = 0.2f;

    public float timer;
    private bool isTiming;
    private bool countdownStarted;

    // Path to the executable
    public string executablePath = "C:\\Users\\afons\\OneDrive\\Documentos\\QuestionPrompting.exe";

    // Process to keep track of the running executable
    public Process executableProcess;

    // References for visual cue
    public TMP_Text timesUpText;

    // Reference to the NextKeyText in HandDataExtractor
    private TextMeshProUGUI nextKeyText;

    private bool isExecutableStarted = false;
    private bool isExecutableCompleted = false;

    // Synchronization context for marshaling to the main thread
    private SynchronizationContext unitySynchronizationContext;

    // Add the public float RanOutTime
    public float RanOutTime = 60.0f; // Default to 60 seconds

    void Start()
    {
        if (redLight == null || yellowLight == null || greenLight == null || timerText == null || timesUpText == null)
        {
            UnityEngine.Debug.LogError("One or more GameObjects are not assigned in the inspector.");
            return;
        }

        unitySynchronizationContext = SynchronizationContext.Current;
        ResetLights();
        timerText.text = "Press Enter to Start";
    }

    void Update()
    {
        if (!countdownStarted && !isExecutableStarted && Input.GetKeyDown(KeyCode.Return) && !isExecutableCompleted)
        {
            StartExecutableProcess();
        }
        else if (!countdownStarted && Input.GetKeyDown(KeyCode.Return) && isExecutableCompleted)
        {
            StartCoroutine(StartCountdown());
        }

        if (isTiming)
        {
            timer += Time.deltaTime;
            UpdateTimerText(timer);

            // Check if the timer has exceeded RanOutTime
            if (timer >= RanOutTime)
            {
                StopTimer();
                FindObjectOfType<HandDataExtractor>().EndSequenceDueToTimeout(); // End the sequence due to timeout
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetHeadlightAnimation();
        }
    }

    void StartExecutableProcess()
    {
        if (!string.IsNullOrEmpty(executablePath))
        {
            executableProcess = new Process();
            executableProcess.StartInfo.FileName = executablePath;
            executableProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal; // Show window for debugging
            executableProcess.EnableRaisingEvents = true; // Enable events so we can handle process exit
            executableProcess.Exited += OnExecutableProcessExited;

            try
            {
                executableProcess.Start();
                isExecutableStarted = true;
                UnityEngine.Debug.Log("Executable started.");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("Failed to start executable: " + ex.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogError("Executable path is not set.");
        }
    }

    void OnExecutableProcessExited(object sender, System.EventArgs e)
    {
        UnityEngine.Debug.Log("Executable completed.");
        isExecutableStarted = false;
        isExecutableCompleted = true;

        // Marshal the coroutine call to the main thread
        unitySynchronizationContext.Post(_ =>
        {
            StartCoroutine(CheckForSignalFile());
        }, null);
    }

    IEnumerator CheckForSignalFile()
    {
        string doneSignalPath = "C:\\Users\\afons\\OneDrive\\Documentos\\5 Ano 2 Semestre\\Thesis\\done_signal.txt";
        UnityEngine.Debug.Log("Started checking for signal file.");

        while (!File.Exists(doneSignalPath))
        {
            UnityEngine.Debug.Log("Signal file not found. Waiting...");
            yield return new WaitForSeconds(1); // Wait for 1 second before checking again
        }

        UnityEngine.Debug.Log("Done signal received.");
        StartCoroutine(StartCountdown());
    }

    IEnumerator StartCountdown()
    {
        countdownStarted = true;

        // Show red light
        redLight.SetActive(true);
        timerText.text = "";
        yield return new WaitForSeconds(countdownTime);

        // Show yellow light
        redLight.SetActive(false);
        yellowLight.SetActive(true);
        yield return new WaitForSeconds(countdownTime);

        // Show green light and start timer after it disappears
        yellowLight.SetActive(false);
        greenLight.SetActive(true);
        yield return new WaitForSeconds(countdownTime);
        greenLight.SetActive(false); // Hide green light and start timer

        StartTimer();
    }

    void StartTimer()
    {
        timer = 0.0f;
        isTiming = true;
        var handDataExtractor = FindObjectOfType<HandDataExtractor>();
        handDataExtractor.isTiming = true;
        handDataExtractor.isSequenceStarted = true;
        handDataExtractor.StartSequence(); // Call StartSequence to activate sequence parsing

        nextKeyText = FindObjectOfType<HandDataExtractor>().nextKeyText;
        if (nextKeyText != null)
        {
            nextKeyText.gameObject.SetActive(true);
            FindObjectOfType<HandDataExtractor>().UpdateNextKeyDisplay();
        }
    }

    public void StopTimer()
    {
        isTiming = false;
        FindObjectOfType<HandDataExtractor>().isTiming = false;
        timerText.text = string.Format("Final Time: {0:00}:{1:00}:{2:00}", Mathf.FloorToInt(timer / 60F), Mathf.FloorToInt(timer % 60F), Mathf.FloorToInt((timer * 100F) % 100F));

        StartCoroutine(TimesUpAnimation());

        if (nextKeyText != null)
        {
            nextKeyText.gameObject.SetActive(false);
        }
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
        // isExecutableStarted = false;  // Do not reset this to allow only one .exe run per session
        timerText.text = "Press Enter to Start";

        var handDataExtractor = FindObjectOfType<HandDataExtractor>();
        if (handDataExtractor != null)
        {
            handDataExtractor.ResetAllGlows(); // Add this line
        }
    }


    private void ResetLights()
    {
        redLight.SetActive(false);
        yellowLight.SetActive(false);
        greenLight.SetActive(false);
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

        canvasGroup.alpha = maxAlpha;

        for (int i = 0; i < 3; i++)
        {
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

        canvasGroup.alpha = 0;
    }

    void OnApplicationQuit()
    {
        StopExecutable();
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
}
