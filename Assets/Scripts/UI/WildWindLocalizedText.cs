using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public sealed class WildWindLocalizedText : MonoBehaviour
{
    public string key = "";

    private Text target;

    private void Awake()
    {
        target = GetComponent<Text>();
        Refresh();
    }

    private void OnEnable()
    {
        WildWindLocalization.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        WildWindLocalization.LanguageChanged -= Refresh;
    }

    public void SetKey(string localizationKey)
    {
        key = localizationKey;
        Refresh();
    }

    public void Refresh()
    {
        if (target == null)
        {
            target = GetComponent<Text>();
        }

        if (target == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            target.text = "";
            return;
        }

        target.text = WildWindLocalization.Get(key);
    }
}
