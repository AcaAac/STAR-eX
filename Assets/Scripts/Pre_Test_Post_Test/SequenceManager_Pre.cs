using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SequenceManager_Pre : MonoBehaviour
{
    public Image[] sequenceImages; // Array to hold references to the 5 images
    public TextMeshProUGUI[] sequenceTexts; // Array to hold references to the TMP texts on each image
    public TextMeshProUGUI timerText; // Reference to the TMP text for the timer
    public TextMeshProUGUI errorText; // Reference to the TMP text for the error count

    private int currentSequenceIndex = 0; // Index of the currently displayed sequence
    private string currentSequence; // The current sequence string
    private int currentCharIndex = 0; // Index of the current character in the sequence
    private float timer = 0f; // Timer to track the time taken
    private int errorCount = 0; // Count of errors made

    private bool isTiming = false; // Flag to check if timing is active

    void Start()
    {
        // Initialize the first sequence
        InitializeSequence(0);
    }

    void Update()
    {
        if (isTiming)
        {
            timer += Time.deltaTime;
            UpdateTimerText();
        }

        // Check for key presses
        if (Input.GetKeyDown(KeyCode.A)) CheckKeyPress('A');
        if (Input.GetKeyDown(KeyCode.B)) CheckKeyPress('B');
        if (Input.GetKeyDown(KeyCode.C)) CheckKeyPress('C');
        if (Input.GetKeyDown(KeyCode.D)) CheckKeyPress('D');
        if (Input.GetKeyDown(KeyCode.E)) CheckKeyPress('E');
    }

    void InitializeSequence(int index)
    {
        // Hide all sequences
        foreach (var img in sequenceImages)
        {
            img.gameObject.SetActive(false);
        }

        // Show the current sequence
        currentSequenceIndex = index;
        sequenceImages[index].gameObject.SetActive(true);
        currentSequence = sequenceTexts[index].text;
        currentCharIndex = 0;
        timer = 0f;
        errorCount = 0;
        isTiming = true;

        UpdateTimerText();
        UpdateErrorText();
    }

    void CheckKeyPress(char keyPressed)
    {
        if (currentSequence[currentCharIndex] == keyPressed)
        {
            // Correct key pressed
            currentCharIndex++;
            if (currentCharIndex >= currentSequence.Length)
            {
                // Sequence completed
                isTiming = false;
                Debug.Log("Sequence Completed!");
                Debug.Log("Time taken: " + timer);
                Debug.Log("Errors made: " + errorCount);
            }
        }
        else
        {
            // Wrong key pressed
            errorCount++;
            ShowErrorFeedback();
            UpdateErrorText();
        }
    }

    void UpdateTimerText()
    {
        timerText.text = "Time: " + timer.ToString("F2") + "s";
    }

    void UpdateErrorText()
    {
        errorText.text = "Errors: " + errorCount;
    }

    void ShowErrorFeedback()
    {
        // Implement visual feedback for wrong key press
        Debug.Log("Wrong key pressed!");
    }
}
