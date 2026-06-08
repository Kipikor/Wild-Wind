using UnityEngine;

public sealed class WildWindSettingsRoot : MonoBehaviour
{
    [Header("Settings Modules")]
    [SerializeField, InspectorName("Controls")] private WildWindControlSettings controls;

    public WildWindControlSettings Controls
    {
        get
        {
            if (controls == null)
            {
                controls = GetComponentInChildren<WildWindControlSettings>(true);
            }

            return controls;
        }
    }

    public void Configure(WildWindControlSettings newControls)
    {
        controls = newControls;
    }

    private void OnValidate()
    {
        if (controls == null)
        {
            controls = GetComponentInChildren<WildWindControlSettings>(true);
        }
    }
}
