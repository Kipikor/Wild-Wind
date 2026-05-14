using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GasCloud : MonoBehaviour
{
    private static readonly List<GasCloud> ActiveClouds = new List<GasCloud>();

    [Header("Gas Cloud")]
    public string cloudId = "";
    public string cloudTypeId = "";
    public string displayName = "";
    public string condensateItemId = "";
    public float condensateLitersPerCubicMeter = 0.005f;
    public float initialVolumeLiters = 100f;
    public float remainingVolumeLiters = 100f;
    public Color visualColor = new Color(0.75f, 0.85f, 1f, 0.35f);

    private GasCloudManager manager;
    private GasCloudState state;
    private Transform visual;

    public bool IsDepleted => remainingVolumeLiters <= 0.001f;
    public float CurrentRadiusMeters => CalculateRadiusMeters(remainingVolumeLiters, condensateLitersPerCubicMeter);

    public static float CalculateRadiusMeters(float volumeLiters, float condensateLitersPerCubicMeter)
    {
        if (volumeLiters <= 0f || condensateLitersPerCubicMeter <= 0f) return 0f;

        float cloudVolumeM3 = volumeLiters / condensateLitersPerCubicMeter;
        return Mathf.Pow(cloudVolumeM3 * 3f / (4f * Mathf.PI), 1f / 3f);
    }

    public static GasCloud FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        for (int i = 0; i < ActiveClouds.Count; i++)
        {
            GasCloud cloud = ActiveClouds[i];
            if (cloud != null && cloud.cloudId == id && !cloud.IsDepleted)
            {
                return cloud;
            }
        }

        return null;
    }

    public static GasCloud FindRandomOverlapping(Vector3 position, float harvesterRadius)
    {
        List<GasCloud> candidates = null;
        for (int i = 0; i < ActiveClouds.Count; i++)
        {
            GasCloud cloud = ActiveClouds[i];
            if (cloud == null || cloud.IsDepleted) continue;
            if (!cloud.IntersectsHarvestRadius(position, harvesterRadius)) continue;

            candidates ??= new List<GasCloud>();
            candidates.Add(cloud);
        }

        if (candidates == null || candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public void Initialize(GasCloudConfig config, GasCloudTypeConfig type, GasCloudState progressState, GasCloudManager owner)
    {
        manager = owner;
        state = progressState;

        cloudId = config != null ? config.id : cloudId;
        cloudTypeId = config != null ? config.cloudTypeId : cloudTypeId;
        displayName = config != null && !string.IsNullOrWhiteSpace(config.localNameRu) ? config.localNameRu : cloudId;
        initialVolumeLiters = config != null ? Mathf.Max(0f, config.initialVolumeLiters) : Mathf.Max(0f, initialVolumeLiters);

        if (type != null)
        {
            condensateItemId = type.condensateItemId;
            condensateLitersPerCubicMeter = Mathf.Max(0.0001f, type.condensateLitersPerCubicMeter);
            visualColor = type.color;
        }

        if (state != null)
        {
            if (!state.initialized)
            {
                state.initialized = true;
                state.remainingVolumeLiters = initialVolumeLiters;
            }

            remainingVolumeLiters = Mathf.Clamp(state.remainingVolumeLiters, 0f, initialVolumeLiters);
        }
        else
        {
            remainingVolumeLiters = initialVolumeLiters;
        }

        ApplyVisual();
    }

    public bool IntersectsHarvestRadius(Vector3 position, float harvesterRadius)
    {
        if (IsDepleted) return false;

        float allowedDistance = Mathf.Max(0f, harvesterRadius) + CurrentRadiusMeters;
        return Vector3.Distance(transform.position, position) <= allowedDistance;
    }

    public float HarvestLiters(float sampledCubicMeters)
    {
        if (sampledCubicMeters <= 0f || IsDepleted) return 0f;

        float requestedLiters = sampledCubicMeters * Mathf.Max(0.0001f, condensateLitersPerCubicMeter);
        float harvestedLiters = Mathf.Min(requestedLiters, remainingVolumeLiters);
        remainingVolumeLiters = Mathf.Max(0f, remainingVolumeLiters - harvestedLiters);

        if (state != null)
        {
            state.initialized = true;
            state.remainingVolumeLiters = remainingVolumeLiters;
        }

        ApplyVisual();
        return harvestedLiters;
    }

    private void OnEnable()
    {
        if (!ActiveClouds.Contains(this))
        {
            ActiveClouds.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveClouds.Remove(this);
    }

    private void ApplyVisual()
    {
        if (visual == null)
        {
            Transform existing = transform.Find("Visual");
            visual = existing != null ? existing : transform;
        }

        float radius = CurrentRadiusMeters;
        transform.localScale = Vector3.one;
        if (visual != null)
        {
            visual.localScale = Vector3.one * Mathf.Max(0.1f, radius * 2f);
        }

        Renderer renderer = visual != null ? visual.GetComponent<Renderer>() : GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = visualColor;
        }

        gameObject.SetActive(!IsDepleted);
        manager?.NotifyCloudChanged(this);
    }
}
