using UnityEngine;

public sealed class WildWindHudWindowFrame : MonoBehaviour
{
    [Header("Window Frame")]
    [SerializeField] private RectTransform modalLayer;
    [SerializeField] private RectTransform fadeLayer;
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private RectTransform headerRoot;
    [SerializeField] private RectTransform titleRoot;
    [SerializeField] private RectTransform closeButtonRoot;
    [SerializeField] private RectTransform contentRoot;

    public RectTransform ModalLayer => modalLayer;
    public RectTransform FadeLayer => fadeLayer;
    public RectTransform WindowRoot => windowRoot;
    public RectTransform HeaderRoot => headerRoot;
    public RectTransform TitleRoot => titleRoot;
    public RectTransform CloseButtonRoot => closeButtonRoot;
    public RectTransform ContentRoot => contentRoot;

    public void ResolveReferences()
    {
        modalLayer = modalLayer != null ? modalLayer : GetComponent<RectTransform>();
        fadeLayer = fadeLayer != null ? fadeLayer : FindRect("Modal Fade");
        windowRoot = windowRoot != null ? windowRoot : FindRect("Window Root");
        headerRoot = headerRoot != null ? headerRoot : FindRect("Header");
        titleRoot = titleRoot != null ? titleRoot : FindRect("Title");
        closeButtonRoot = closeButtonRoot != null ? closeButtonRoot : FindRect("Close Button");
        contentRoot = contentRoot != null ? contentRoot : FindRect("Content");
    }

    private RectTransform FindRect(string childName)
    {
        RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            RectTransform candidate = children[i];
            if (candidate != null && candidate.name == childName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        ResolveReferences();
    }
}
