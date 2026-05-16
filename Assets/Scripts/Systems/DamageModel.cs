using System;
using UnityEngine;

public enum DamageShellType
{
    [InspectorName("Бронебойный")]
    ArmorPiercing,
    [InspectorName("Фугасный")]
    HighExplosive,
    [InspectorName("Удар/таран")]
    Impact
}

public enum DamageHitOutcome
{
    [InspectorName("Пробитие")]
    Penetration,
    [InspectorName("Непробитие")]
    NoPenetration,
    [InspectorName("Рикошет")]
    Ricochet,
    [InspectorName("Фугасный взрыв")]
    ExplosiveSplash,
    [InspectorName("Ударный урон")]
    ImpactDamage,
    [InspectorName("Промах")]
    Miss
}

[Serializable]
public class DamageShellPreset
{
    [InspectorName("Название")]
    public string displayNameRu = "Бронебойный";
    [InspectorName("Тип")]
    public DamageShellType shellType = DamageShellType.ArmorPiercing;
    [InspectorName("Калибр, мм")]
    public float caliberMm = 76f;
    [InspectorName("Старый общий урон")]
    [Tooltip("Оставлено для совместимости. Новая модель использует три отдельных урона ниже.")]
    public float damagePoints = 100f;
    [InspectorName("Урон корпусу при пробитии")]
    [Tooltip("Сколько прочности корпуса снимает снаряд, если бронелист пробит.")]
    public float hullDamageOnPenetration = 100f;
    [InspectorName("Урон бронелисту")]
    [Tooltip("Сколько прочности бронелиста снимает попадание. При пробитии удваивается, при непробитии идет как есть, при рикошете делится пополам.")]
    public float armorPlateDamage = 25f;
    [InspectorName("Пробитие, мм")]
    public float penetrationMm = 70f;
    [InspectorName("Радиус фугаса, м")]
    public float explosiveRadiusMeters = 4f;
    [InspectorName("Нормализация, град")]
    public float normalizationDegrees = 4f;
    [InspectorName("Разброс пробития")]
    [Range(0f, 0.5f)] public float penetrationRollSpread = 0.1f;
    [InspectorName("Цвет снаряда")]
    public Color projectileColor = Color.red;
}

public struct DamageHitContext
{
    public DamageShellType shellType;
    public string shellName;
    public string sourceName;
    public float caliberMm;
    public float damagePoints;
    public float hullDamageOnPenetration;
    public float armorPlateDamage;
    public float penetrationMm;
    public float explosiveRadiusMeters;
    public float normalizationDegrees;
    public float impactEnergyKJ;
    public float impactDamagePerKJ;
    public float impactSpeedMS;
    public float impactSourceMassKg;
    public float impactTargetMassKg;
    public float impactSourceDamageMultiplier;
    public float internalTravelDistance;
    public bool deferResultLogging;
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public Vector3 incomingDirection;
    public Vector3 velocity;
}

public struct DamageHitResult
{
    public DamageHitOutcome outcome;
    public string message;
    public string zoneId;
    public float armorMm;
    public float effectiveArmorMm;
    public float impactAngleDeg;
    public float penetrationMm;
    public float structureDamage;
    public float armorPlateDamage;
    public float remainingArmorPlateHp;
    public float maxArmorPlateHp;
    public float remainingStructureHp;
}

public struct ArmorSurface
{
    public string zoneId;
    public string displayNameRu;
    public float armorMm;
    public float baseArmorMm;
    public float armorIntegrity01;
    public float ricochetAngleDeg;
    public float overmatchCaliberMultiplier;
    public float structureDamageMultiplier;
    public float highExplosiveSurfaceDamageMultiplier;
    public float ramDamageMultiplier;

    public static ArmorSurface FromZone(ArmorZone zone)
    {
        return new ArmorSurface
        {
            zoneId = zone != null ? zone.zoneId : "",
            displayNameRu = zone != null ? zone.displayNameRu : "",
            armorMm = zone != null ? zone.armorMm : 0f,
            baseArmorMm = zone != null ? zone.armorMm : 0f,
            armorIntegrity01 = 1f,
            ricochetAngleDeg = zone != null ? zone.ricochetAngleDeg : 70f,
            overmatchCaliberMultiplier = zone != null ? zone.overmatchCaliberMultiplier : 3f,
            structureDamageMultiplier = zone != null ? zone.structureDamageMultiplier : 1f,
            highExplosiveSurfaceDamageMultiplier = zone != null ? zone.highExplosiveSurfaceDamageMultiplier : 0.35f,
            ramDamageMultiplier = zone != null ? zone.ramDamageMultiplier : 1f
        };
    }
}
