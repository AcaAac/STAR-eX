using System.Collections; // Necessary for IEnumerator
using UnityEngine;

public class CustomInteractionGlow_Pre : MonoBehaviour
{
    private Renderer keyRenderer;
    private Color originalColor;
    public Color pressColor = Color.green;
    public Color unpressColor = Color.red;
    public Color nextKeyColor = Color.yellow; // Add this for the yellow glow
    public float glowDuration = 2f; // Duration in seconds for how long the glow lasts

    void Start()
    {
        keyRenderer = GetComponent<Renderer>();
        if (keyRenderer != null)
        {
            originalColor = keyRenderer.material.color;
        }
    }

    public void HandlePress(bool isCorrect)
    {
        if (keyRenderer != null)
        {
            keyRenderer.material.color = isCorrect ? pressColor : unpressColor;
        }
        // Debug.Log("HandlePress called. isCorrect: " + isCorrect);
        StartCoroutine(GlowDuration(glowDuration));
    }

    public void HandleUnpress()
    {
        if (keyRenderer != null)
        {
            keyRenderer.material.color = originalColor;
        }
        StopAllCoroutines(); // Ensure immediate unpress stops all coroutines
    }


    public void HandleNextKeyGlow(float duration)
    {
        if (keyRenderer != null)
        {
            keyRenderer.material.color = nextKeyColor;
        }
        // Debug.Log($"HandleNextKeyGlow: Glow duration is {duration} seconds");
        StartCoroutine(GlowDuration(duration));
    }

    private IEnumerator GlowDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        // Do not reset the color here; it will be handled when the correct key is pressed
    }
}
