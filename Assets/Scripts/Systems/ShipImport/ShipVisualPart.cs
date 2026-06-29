using UnityEngine;

[DisallowMultipleComponent]
public class ShipVisualPart : MonoBehaviour
{
    [Header("Blender import")]
    [InspectorName("Source object")]
    public string sourceObjectName = "";

    [InspectorName("Role")]
    public ShipVisualPartRole role = ShipVisualPartRole.Unknown;

    [InspectorName("Category")]
    public string category = "";

    [InspectorName("Manifest path")]
    public string manifestPath = "";

    public bool IsHull => role == ShipVisualPartRole.Hull;
    public bool IsWeapon => role == ShipVisualPartRole.Weapon;
    public bool IsEquipment => role == ShipVisualPartRole.Equipment;
}
