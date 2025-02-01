using Leap.Unity;
using Leap.Unity.Interaction;
using UnityEngine;
using System.Collections;

public class CustomInteractionGlow_K : MonoBehaviour
{
    [Tooltip("If enabled, the object will lerp to its hoverColor when a hand is nearby.")]
    public bool useHover = true;

    [Tooltip("If enabled, the object will use its primaryHoverColor when the primary hover of an InteractionHand.")]
    public bool usePrimaryHover = false;

    [Header("InteractionBehaviour Colors")]
    public Color defaultColor = Color.Lerp(Color.black, Color.white, 0.1F);
    public Color suspendedColor = Color.red;
    public Color hoverColor = Color.Lerp(Color.black, Color.white, 0.7F);
    public Color primaryHoverColor = Color.Lerp(Color.black, Color.white, 0.8F);

    [Header("InteractionButton Colors")]
    [Tooltip("This color only applies if the object is an InteractionButton or InteractionSlider.")]
    public Color pressedColor = Color.white;

    private Material[] _materials;
    private InteractionButton _intButton;

    [SerializeField]
    private Rend[] rends;

    [System.Serializable]
    public class Rend
    {
        public int materialID = 0;
        public Renderer renderer;
    }

    public float GoodGlow; // Duration the glow should linger

    void Start()
    {
        _intButton = GetComponent<InteractionButton>();

        if (_intButton == null)
        {
            Debug.LogError("InteractionButton component is missing.");
            return;
        }

        if (rends.Length > 0)
        {
            _materials = new Material[rends.Length];

            for (int i = 0; i < rends.Length; i++)
            {
                _materials[i] = rends[i].renderer.materials[rends[i].materialID];
            }
        }
    }

    void Update()
    {
        if (_materials != null)
        {
            Color targetColor = defaultColor;

            if (_intButton.isPrimaryHovered && usePrimaryHover)
            {
                targetColor = primaryHoverColor;
            }
            else
            {
                if (_intButton.isHovered && useHover)
                {
                    float glow = _intButton.closestHoveringControllerDistance.Map(0F, 0.2F, 1F, 0.0F);
                    targetColor = Color.Lerp(defaultColor, hoverColor, glow);
                }
            }

            if (_intButton.isSuspended)
            {
                targetColor = suspendedColor;
            }

            for (int i = 0; i < _materials.Length; i++)
            {
                _materials[i].color = Color.Lerp(_materials[i].color, targetColor, 30F * Time.deltaTime);
            }
        }
    }

    public void HandlePress()
    {
        if (_materials != null)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                _materials[i].color = pressedColor;
            }
        }

        // Start lingering glow effect
        StartCoroutine(LingerGlow());
    }

    public void HandleUnpress()
    {
        if (_materials != null)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                _materials[i].color = defaultColor;
            }
        }
    }

    private IEnumerator LingerGlow()
    {
        yield return new WaitForSeconds(GoodGlow);
        HandleUnpress();
    }
}
