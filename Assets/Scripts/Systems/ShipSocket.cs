using UnityEngine;

[DisallowMultipleComponent]
public class ShipSocket : MonoBehaviour
{
    [Header("Сокет сборки")]
    [InspectorName("Идентификатор слота")]
    [Tooltip("Технический идентификатор слота. Обычно совпадает со слотом в корпусе или модуле.")]
    public string slotId = "slot";

    [InspectorName("Тип слота")]
    [Tooltip("Произвольный тип слота: engine_main, propeller_main, claudium_loop, utility и так далее.")]
    public string slotTypeId = "module";

    [InspectorName("Точка установки")]
    [Tooltip("Куда будет поставлен визуальный префаб модуля. Если поле пустое, используется Transform этого объекта.")]
    public Transform mountPoint;

    public Transform MountPoint => mountPoint != null ? mountPoint : transform;

    public bool Matches(ShipSlotDefinition slot)
    {
        if (slot == null) return false;
        if (!string.IsNullOrWhiteSpace(slot.slotTypeId) && slotTypeId != slot.slotTypeId) return false;
        return slotId == slot.slotId || slot.slotId.EndsWith(":" + slotId);
    }
}
