using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShipVisualDefinition : MonoBehaviour
{
    [Header("Blender source")]
    public string shipId = "";
    public string sourceRootName = "";
    public string sourceBlendPath = "";
    public string sourceManifestPath = "";
    public string sourceModelPath = "";

    [Header("Catalog")]
    public string faction = "";
    public string branch = "";
    public string rank = "";
    public int tier;
    public bool isStarter;
    public string shipClass = "";

    [Header("Import summary")]
    public string exportedAtUtc = "";
    public string importedAtUtc = "";
    public int hullPartCount;
    public int weaponPartCount;
    public int equipmentPartCount;
    public int detailPartCount;
    public int unknownPartCount;
    public int unmatchedManifestPartCount;

    public List<ShipVisualPartRecord> parts = new List<ShipVisualPartRecord>();

    public void RebuildSummaryFromChildren()
    {
        hullPartCount = 0;
        weaponPartCount = 0;
        equipmentPartCount = 0;
        detailPartCount = 0;
        unknownPartCount = 0;

        ShipVisualPart[] visualParts = GetComponentsInChildren<ShipVisualPart>(true);
        for (int i = 0; i < visualParts.Length; i++)
        {
            ShipVisualPart part = visualParts[i];
            if (part == null) continue;

            switch (part.role)
            {
                case ShipVisualPartRole.Hull:
                    hullPartCount++;
                    break;
                case ShipVisualPartRole.Weapon:
                    weaponPartCount++;
                    break;
                case ShipVisualPartRole.Equipment:
                    equipmentPartCount++;
                    break;
                case ShipVisualPartRole.Detail:
                    detailPartCount++;
                    break;
                default:
                    unknownPartCount++;
                    break;
            }
        }
    }
}

[Serializable]
public class ShipVisualPartRecord
{
    public string objectName = "";
    public string path = "";
    public ShipVisualPartRole role = ShipVisualPartRole.Unknown;
    public string category = "";
    public bool matchedInPrefab;
}
