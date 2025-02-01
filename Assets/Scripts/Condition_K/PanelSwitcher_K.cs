using UnityEngine;
using System.Collections;

public class PanelSwitcher_K : MonoBehaviour
{
    public GameObject[] panels; // Array to hold references to each panel
    public static int currentPanelIndex = 0; // Static variable to hold the current panel index
    private CanvasGroup[] panelCanvasGroups; // Array to hold CanvasGroups for fading

    void Start()
    {
        panelCanvasGroups = new CanvasGroup[panels.Length];
        for (int i = 0; i < panels.Length; i++)
        {
            panelCanvasGroups[i] = panels[i].GetComponent<CanvasGroup>();
        }
        ShowPanel(0); // Initialize by showing the first panel
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) { ShowPanel(0); }
        else if (Input.GetKeyDown(KeyCode.Alpha2)) { ShowPanel(1); }
        else if (Input.GetKeyDown(KeyCode.Alpha3)) { ShowPanel(2); }
        else if (Input.GetKeyDown(KeyCode.Alpha4)) { ShowPanel(3); }
        else if (Input.GetKeyDown(KeyCode.Alpha5)) { ShowPanel(4); }
    }

    public void ShowPanel(int index)
    {
        for (int i = 0; i < panels.Length; i++)
        {
            panels[i].SetActive(i == index);
            if (panelCanvasGroups[i] != null)
                panelCanvasGroups[i].alpha = (i == index) ? 1.0f : 0.0f; // Ensure full visibility when active
        }
        currentPanelIndex = index; // Update the static variable
    }

    public IEnumerator FadeOutPanel(int index, float duration)
    {
        if (index < 0 || index >= panelCanvasGroups.Length || panelCanvasGroups[index] == null)
            yield break;

        float counter = 0;
        while (counter < duration)
        {
            counter += Time.deltaTime;
            panelCanvasGroups[index].alpha = Mathf.Lerp(1.0f, 0.0f, counter / duration);
            yield return null;
        }
        panels[index].SetActive(false);
    }
}
