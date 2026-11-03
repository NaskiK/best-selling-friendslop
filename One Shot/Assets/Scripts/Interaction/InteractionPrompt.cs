using TMPro;
using UnityEngine;

public class InteractionPrompt : MonoBehaviour
{
    public static InteractionPrompt Instance { get; private set; }

    [SerializeField] private TMP_Text label;
    private object owner;

    private void Awake()
    {
        Instance = this;
        label.gameObject.SetActive(false);
    }

    public void Show(object source, string text)
    {
        owner = source;
        label.text = text;
        label.gameObject.SetActive(true);
    }

    public void Hide(object source)
    {
        if (owner != source) return;
        owner = null;
        label.gameObject.SetActive(false);
    }
}