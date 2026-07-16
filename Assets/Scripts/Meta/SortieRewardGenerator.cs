using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class SortieMissionRequirement
{
    public string statId = "";
    public int requiredValue;
    public float weight = 1f;
    public bool hardGate;

    public string DisplayLabelRu => SortieRewardGenerator.GetStatLabelRu(statId);
}

public sealed class SortieMissionOffer
{
    public string offerId = "";
    public string missionProfile = "";
    public string missionProfileRu = "";
    public string missionArchetype = "";
    public string primaryActivity = "";
    public string primaryActivityRu = "";
    public string titleRu = "";
    public string seed = "";
    public long refreshWindow;
    public float estimatedSuccessFactor = 1f;
    public List<SortieMissionRequirement> requirements = new List<SortieMissionRequirement>();

    public string BuildRequirementSummary(ShipTreeEntryConfig ship)
    {
        if (requirements == null || requirements.Count == 0)
        {
            return "Требований нет";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < requirements.Count; i++)
        {
            SortieMissionRequirement requirement = requirements[i];
            if (requirement == null) continue;
            if (builder.Length > 0) builder.Append("; ");
            int value = SortieRewardGenerator.GetShipStatForRequirement(ship, requirement.statId);
            builder.Append(requirement.DisplayLabelRu);
            builder.Append(" ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.Append("/");
            builder.Append(requirement.requiredValue.ToString(CultureInfo.InvariantCulture));
        }

        return builder.Length > 0 ? builder.ToString() : "Требований нет";
    }
}

public sealed class SortieRewardLine
{
    public string layer = "";
    public string sourceId = "";
    public string itemId = "";
    public string displayNameRu = "";
    public int amount;
    public float valueFe;
    public float cargoTons;
}

public sealed class SortieMissionResult
{
    public string missionProfile = "";
    public string missionProfileRu = "";
    public string missionArchetype = "";
    public string primaryActivity = "";
    public string primaryActivityRu = "";
    public string titleRu = "";
    public string outcome = "";
    public float destroyedChance;
    public float damagedChance;
    public float damagePercent;
    public float expectedExtractedTotalFe;
    public float generatedMaterialFe;
    public float generatedIntangibleFe;
    public float awardedMaterialFe;
    public float awardedIntangibleFe;
    public float awardedTotalFe;
    public int freightAward;
    public int designExperienceAward;
    public float reputationFe;
    public int masteryPoints;
    public float cargoUsedTons;
    public string sourceSummary = "";
    public string reportText = "";
    public List<SortieRewardLine> materialRewards = new List<SortieRewardLine>();

    public bool Destroyed => string.Equals(outcome, "destroyed", StringComparison.OrdinalIgnoreCase);
    public bool Extracted => !Destroyed;
}

public static class SortieRewardGenerator
{
    private const string ProfileQuick = "quick";
    private const string ProfileNormal = "normal";
    private const string ProfileDanger = "danger";
    private const string ProfileElite = "elite";
    private const float IntangibleExtractionMultiplier = 2f;
    private const long MissionRefreshSeconds = 10L * 60L;

    private static readonly string[] MissionProfiles = { ProfileQuick, ProfileNormal, ProfileDanger, ProfileElite };
    private static readonly string[] OfferActivities =
    {
        "combat",
        "mining",
        "gas",
        "hunting",
        "salvage",
        "relic",
        "courier",
        "repair"
    };

    private static readonly Dictionary<string, float> ProfileSpread = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { ProfileQuick, 0.14f },
        { ProfileNormal, 0.20f },
        { ProfileDanger, 0.28f },
        { ProfileElite, 0.38f }
    };

    private static readonly Dictionary<string, float> ProfileDestroyBase = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { ProfileQuick, 0.005f },
        { ProfileNormal, 0.025f },
        { ProfileDanger, 0.055f },
        { ProfileElite, 0.085f }
    };

    private static readonly Dictionary<string, float> ProfileDestroyPressure = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { ProfileQuick, 0.06f },
        { ProfileNormal, 0.32f },
        { ProfileDanger, 0.55f },
        { ProfileElite, 0.78f }
    };

    private static readonly Dictionary<string, float> ProfileDamagedBase = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { ProfileQuick, 0.05f },
        { ProfileNormal, 0.12f },
        { ProfileDanger, 0.20f },
        { ProfileElite, 0.30f }
    };

    private static readonly Dictionary<string, float> AutomatonItemMassTons = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { "automaton_relay", 0.010f },
        { "automaton_contact_comb", 0.012f },
        { "automaton_coil", 0.014f },
        { "automaton_brass_valve", 0.019f },
        { "automaton_mainspring", 0.028f },
        { "automaton_pressure_gauge", 0.032f },
        { "automaton_calibration_gear", 0.035f },
        { "automaton_servo_joint", 0.040f },
        { "automaton_optic_lens", 0.045f },
        { "automaton_gyroscope", 0.048f },
        { "automaton_logic_drum", 0.055f },
        { "automaton_command_cylinder", 0.063f },
        { "automaton_servo_core", 0.068f },
        { "automaton_clock_brain", 0.075f }
    };

    private static readonly Dictionary<string, float> RelicClassValueFe = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { "cheap", 60000f },
        { "medium", 180000f },
        { "valuable", 520000f },
        { "expensive", 880000f }
    };

    private static bool loaded;
    private static readonly Dictionary<string, float> itemValues = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> itemCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, MissionProfileRow> missionRowsByKey = new Dictionary<string, MissionProfileRow>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<SortieMissionRequirement>> requirementsByArchetype = new Dictionary<string, List<SortieMissionRequirement>>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<OreDepositRow> oreDeposits = new List<OreDepositRow>();
    private static readonly List<LeviathanButcheryRow> leviathanButcheryRows = new List<LeviathanButcheryRow>();
    private static readonly List<RelicDecodeRow> relicDecodeRows = new List<RelicDecodeRow>();

    public static SortieMissionOffer CreateQuickOffer(ShipTreeEntryConfig ship, DockedDevelopmentShipState slot, SessionConfigDatabase config, DateTime utcNow)
    {
        EnsureLoaded();
        string activity = GetPrimaryActivity(ship);
        long window = GetRefreshWindow(utcNow);
        SortieMissionOffer offer = new SortieMissionOffer
        {
            offerId = "quick:" + (slot != null ? slot.slotIndex.ToString(CultureInfo.InvariantCulture) : "0"),
            missionProfile = ProfileQuick,
            missionProfileRu = "Быстрая",
            missionArchetype = "quick_adaptive",
            primaryActivity = activity,
            primaryActivityRu = GetActivityLabelRu(activity),
            titleRu = "Быстрый вылет: " + GetActivityLabelRu(activity),
            seed = BuildSeed("quick", ship != null ? ship.shipId : "", slot != null ? slot.generation : 0, slot != null ? slot.sortiesRemaining : 0, window, activity),
            refreshWindow = window,
            estimatedSuccessFactor = 1f
        };

        return offer;
    }

    public static List<SortieMissionOffer> BuildOrdinaryOffers(ShipTreeEntryConfig ship, SessionConfigDatabase config, DateTime utcNow)
    {
        EnsureLoaded();
        List<SortieMissionOffer> offers = new List<SortieMissionOffer>();
        if (ship == null)
        {
            return offers;
        }

        long window = GetRefreshWindow(utcNow);
        int seed = StableSeedToInt("mission-list:" + ship.shipId + ":" + window.ToString(CultureInfo.InvariantCulture));
        System.Random random = new System.Random(seed);
        string[] profiles =
        {
            ProfileNormal, ProfileNormal, ProfileNormal, ProfileNormal, ProfileNormal, ProfileNormal,
            ProfileDanger, ProfileDanger, ProfileDanger, ProfileDanger, ProfileDanger,
            ProfileElite, ProfileElite, ProfileElite, ProfileElite
        };

        for (int i = 0; i < profiles.Length; i++)
        {
            string profile = profiles[i];
            string activity = OfferActivities[(i + random.Next(0, OfferActivities.Length)) % OfferActivities.Length];
            string archetype = BuildArchetype(profile, activity);
            List<SortieMissionRequirement> requirements = CloneRequirements(archetype);
            float success = EstimateMissionSuccess(ship, requirements, profile);
            string offerSeed = BuildSeed("offer", ship.shipId, i, seed, window, profile + ":" + activity);
            offers.Add(new SortieMissionOffer
            {
                offerId = profile + ":" + activity + ":" + i.ToString(CultureInfo.InvariantCulture) + ":" + window.ToString(CultureInfo.InvariantCulture),
                missionProfile = profile,
                missionProfileRu = GetProfileLabelRu(profile),
                missionArchetype = archetype,
                primaryActivity = activity,
                primaryActivityRu = GetActivityLabelRu(activity),
                titleRu = BuildOfferTitle(profile, activity, i, offerSeed),
                seed = offerSeed,
                refreshWindow = window,
                estimatedSuccessFactor = success,
                requirements = requirements
            });
        }

        return offers;
    }

    public static SortieMissionResult GenerateSortie(ShipTreeEntryConfig ship, SortieMissionOffer offer, SessionConfigDatabase config)
    {
        EnsureLoaded();
        if (ship == null)
        {
            return BuildEmptyResult("Корабль не выбран.");
        }

        if (offer == null)
        {
            offer = CreateQuickOffer(ship, null, config, DateTime.UtcNow);
        }

        MissionProfileRow row = GetMissionRow(ship, offer.missionProfile, offer.primaryActivity);
        row.missionArchetype = offer.missionArchetype;
        row.primaryActivity = offer.primaryActivity;
        row.primaryActivityRu = offer.primaryActivityRu;
        if (!string.Equals(offer.missionProfile, ProfileQuick, StringComparison.OrdinalIgnoreCase))
        {
            row.missionSuccessFactor = Mathf.Clamp01(offer.estimatedSuccessFactor);
        }

        System.Random random = new System.Random(StableSeedToInt(offer.seed));
        float spread = GetDictionaryValue(ProfileSpread, row.missionProfile, 0.2f);
        float factor = Triangular(random, 1f - spread, 1f + spread, 1f);
        float materialTarget = row.materialValueFe * factor * StableFloat(offer.seed + ":material", 0.90f, 1.10f);
        float intangibleTarget = row.intangibleValueFe * factor * StableFloat(offer.seed + ":intangible", 0.92f, 1.08f);

        SortieMissionResult result = new SortieMissionResult
        {
            missionProfile = row.missionProfile,
            missionProfileRu = GetProfileLabelRu(row.missionProfile),
            missionArchetype = row.missionArchetype,
            primaryActivity = row.primaryActivity,
            primaryActivityRu = GetActivityLabelRu(row.primaryActivity),
            titleRu = offer.titleRu,
            expectedExtractedTotalFe = row.extractedTotalValueFe > 0f ? row.extractedTotalValueFe : materialTarget + intangibleTarget * IntangibleExtractionMultiplier
        };

        GenerateMaterialRewards(ship, row.primaryActivity, row.missionProfile, materialTarget, random, offer.seed, config, result);
        if (result.generatedMaterialFe <= 0f && materialTarget > 0f)
        {
            GenerateServiceRewards(row.primaryActivity, materialTarget, random, offer.seed, config, result);
        }

        result.generatedIntangibleFe = Mathf.Max(0f, intangibleTarget);
        ResolveOutcome(row, random, offer.seed, result);

        float intangibleMultiplier = result.Destroyed ? 1f : IntangibleExtractionMultiplier;
        result.awardedMaterialFe = result.Destroyed ? 0f : result.generatedMaterialFe;
        result.awardedIntangibleFe = result.generatedIntangibleFe * intangibleMultiplier;
        result.awardedTotalFe = result.awardedMaterialFe + result.awardedIntangibleFe;

        float intangibleScale = row.intangibleValueFe > 0f ? result.generatedIntangibleFe / row.intangibleValueFe : factor;
        result.freightAward = RoundCurrency(row.freightFe * intangibleScale * intangibleMultiplier);
        result.reputationFe = Mathf.Max(0f, row.reputationFe * intangibleScale * intangibleMultiplier);
        int xp = Mathf.RoundToInt(row.baseShipXp * intangibleScale * intangibleMultiplier);
        int mastery = Mathf.RoundToInt(row.baseMasteryPoints * intangibleScale * intangibleMultiplier);
        result.designExperienceAward = Mathf.Max(0, xp + mastery);
        result.masteryPoints = Mathf.Max(0, mastery);
        result.reportText = BuildReportText(result, config);
        return result;
    }

    public static bool ValidateGeneratorForTests(SessionConfigDatabase config, out string summary)
    {
        EnsureLoaded();
        summary = "";
        List<ShipTreeEntryConfig> ships = config != null ? config.shipTreeEntries : null;
        if (ships == null || ships.Count == 0)
        {
            summary = "ship catalog is empty";
            return false;
        }

        int generated = 0;
        int failed = 0;
        int ratioOutliers = 0;
        int destroyed = 0;
        int materialRows = 0;
        float ratioSum = 0f;
        string[] profiles = { ProfileQuick, ProfileNormal, ProfileDanger, ProfileElite };
        DateTime validationBaseTime = new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 100; i++)
        {
            ShipTreeEntryConfig ship = ships[(i * 17) % ships.Count];
            if (ship == null || !ship.IsDevelopmentRosterShip)
            {
                continue;
            }

            string profile = profiles[i % profiles.Length];
            SortieMissionOffer offer;
            if (string.Equals(profile, ProfileQuick, StringComparison.OrdinalIgnoreCase))
            {
                offer = CreateQuickOffer(ship, null, config, validationBaseTime.AddMinutes(i));
            }
            else
            {
                List<SortieMissionOffer> offers = BuildOrdinaryOffers(ship, config, validationBaseTime.AddMinutes(i));
                offer = null;
                for (int offerIndex = 0; offerIndex < offers.Count; offerIndex++)
                {
                    if (string.Equals(offers[offerIndex].missionProfile, profile, StringComparison.OrdinalIgnoreCase))
                    {
                        offer = offers[offerIndex];
                        break;
                    }
                }

                if (offer == null && offers.Count > 0)
                {
                    offer = offers[0];
                }
            }

            if (offer == null)
            {
                failed++;
                continue;
            }

            SortieMissionResult result = GenerateSortie(ship, offer, config);
            SortieMissionResult repeat = GenerateSortie(ship, offer, config);
            generated++;
            materialRows += result.materialRewards != null ? result.materialRewards.Count : 0;
            if (result.Destroyed) destroyed++;

            float expected = Mathf.Max(1f, result.expectedExtractedTotalFe);
            float generatedExtracted = result.generatedMaterialFe + result.generatedIntangibleFe * IntangibleExtractionMultiplier;
            float ratio = generatedExtracted / expected;
            ratioSum += ratio;

            float tolerance = string.Equals(result.missionProfile, ProfileQuick, StringComparison.OrdinalIgnoreCase) ? 0.35f : 0.85f;
            if (ratio < 1f - tolerance || ratio > 1f + tolerance)
            {
                ratioOutliers++;
            }

            if (!string.Equals(result.reportText, repeat.reportText, StringComparison.Ordinal)
                || Math.Abs(result.awardedTotalFe - repeat.awardedTotalFe) > 0.01f)
            {
                failed++;
            }

            if (result.Destroyed && result.awardedMaterialFe > 0.01f)
            {
                failed++;
            }

            if (result.Extracted && result.awardedMaterialFe + 0.01f < result.generatedMaterialFe)
            {
                failed++;
            }

            if (result.cargoUsedTons > Mathf.Max(0.1f, ship.cargoCapacityTons) * 1.05f)
            {
                failed++;
            }
        }

        float ratioAvg = generated > 0 ? ratioSum / generated : 0f;
        summary = "generated=" + generated.ToString(CultureInfo.InvariantCulture)
            + ", failed=" + failed.ToString(CultureInfo.InvariantCulture)
            + ", ratioOutliers=" + ratioOutliers.ToString(CultureInfo.InvariantCulture)
            + ", destroyed=" + destroyed.ToString(CultureInfo.InvariantCulture)
            + ", materialRows=" + materialRows.ToString(CultureInfo.InvariantCulture)
            + ", avgRatio=" + ratioAvg.ToString("0.000", CultureInfo.InvariantCulture);
        return generated >= 80 && failed == 0 && ratioOutliers <= Mathf.CeilToInt(generated * 0.20f) && materialRows > 0;
    }

    public static string GetStatLabelRu(string statId)
    {
        switch ((statId ?? "").Trim().ToLowerInvariant())
        {
            case "warfare": return "Вооружение";
            case "mining": return "Руда";
            case "gas": return "Облака";
            case "harvesting": return "Облака";
            case "hunting": return "Охота";
            case "salvage": return "Сальваж";
            case "relic": return "Реликты";
            case "hacking": return "Взлом";
            case "survey": return "Сканирование";
            case "repair": return "Ремонт";
            case "courier": return "Логистика";
            case "cargo": return "Груз";
            case "defense": return "Живучесть";
            case "mobility": return "Мобильность";
            case "stealth": return "Маскировка";
            default: return string.IsNullOrWhiteSpace(statId) ? "Стат" : statId;
        }
    }

    public static int GetShipStatForRequirement(ShipTreeEntryConfig ship, string statId)
    {
        if (ship == null)
        {
            return 0;
        }

        switch ((statId ?? "").Trim().ToLowerInvariant())
        {
            case "warfare": return ClampRating(ship.warfareRating >= 0 ? ship.warfareRating : ship.firepower);
            case "mining": return ClampPositiveActivity(ship.miningRating);
            case "gas":
            case "harvesting": return ClampPositiveActivity(ship.harvestingRating);
            case "hunting": return ClampPositiveActivity(ship.huntingRating);
            case "salvage": return ClampPositiveActivity(ship.salvageRating);
            case "relic": return Mathf.Max(ClampPositiveActivity(ship.hackingRating), ClampPositiveActivity(ship.surveyRating));
            case "hacking": return ClampPositiveActivity(ship.hackingRating);
            case "survey": return ClampPositiveActivity(ship.surveyRating);
            case "repair": return ClampPositiveActivity(ship.repairRating);
            case "courier": return Mathf.Max(ClampPositiveActivity(ship.cargo), ClampRating(ship.mobilityRating >= 0 ? ship.mobilityRating : ship.speed));
            case "cargo": return Mathf.Clamp(Mathf.RoundToInt(ship.cargoCapacityTons), 0, 100);
            case "defense": return ClampRating(ship.defenseRating >= 0 ? ship.defenseRating : Mathf.RoundToInt((ship.armor + ship.durability) * 0.5f));
            case "mobility": return ClampRating(ship.mobilityRating >= 0 ? ship.mobilityRating : Mathf.RoundToInt((ship.speed + ship.maneuverability) * 0.5f));
            case "stealth": return ClampRating(ship.stealthRating >= 0 ? ship.stealthRating : 50);
            default: return 0;
        }
    }

    private static void GenerateMaterialRewards(
        ShipTreeEntryConfig ship,
        string activity,
        string profile,
        float targetFe,
        System.Random random,
        string seed,
        SessionConfigDatabase config,
        SortieMissionResult result)
    {
        if (targetFe <= 0f || result == null)
        {
            return;
        }

        string normalizedActivity = NormalizeActivity(activity);
        if (normalizedActivity == "mining")
        {
            GenerateOreRewards(ship, profile, targetFe, random, seed, config, result);
            return;
        }

        if (normalizedActivity == "gas")
        {
            GenerateSourceRewards(ship, "harvesting", profile, targetFe, random, seed, config, result);
            return;
        }

        if (normalizedActivity == "hunting")
        {
            GenerateSourceRewards(ship, "hunting", profile, targetFe, random, seed, config, result);
            return;
        }

        if (normalizedActivity == "salvage" || normalizedActivity == "combat")
        {
            GenerateSourceRewards(ship, "salvage", profile, targetFe * (normalizedActivity == "combat" ? 0.65f : 1f), random, seed, config, result);
            return;
        }

        if (normalizedActivity == "relic")
        {
            GenerateRelicRewards(ship, profile, targetFe, random, seed, config, result);
            return;
        }

        GenerateServiceRewards(normalizedActivity, targetFe, random, seed, config, result);
    }

    private static void GenerateOreRewards(
        ShipTreeEntryConfig ship,
        string profile,
        float targetFe,
        System.Random random,
        string seed,
        SessionConfigDatabase config,
        SortieMissionResult result)
    {
        int mining = ClampPositiveActivity(ship != null ? ship.miningRating : 0);
        float cargoTons = Mathf.Max(0.25f, ship != null ? ship.cargoCapacityTons : 1f);
        if (mining <= 0 || cargoTons <= 0f)
        {
            return;
        }

        List<WeightedOre> candidates = new List<WeightedOre>();
        float hint = SourceHint(profile, mining, ship != null ? ship.rank : 1);
        for (int i = 0; i < oreDeposits.Count; i++)
        {
            OreDepositRow ore = oreDeposits[i];
            if (ore.minMining > mining) continue;
            float closeness = 1f / (1f + Mathf.Abs(ore.minMining - hint) / 18f);
            float value = Mathf.Max(1f, CalculateOreDepositValuePerKg(ore));
            float weight = (ore.baseWeight + ore.luckyWeight * RandomRange(random, 0f, 1f)) * closeness * (1f + value / 160f);
            candidates.Add(new WeightedOre { ore = ore, weight = Mathf.Max(0.01f, weight) });
        }

        if (candidates.Count == 0)
        {
            GenerateSourceRewards(ship, "mining", profile, targetFe, random, seed, config, result);
            return;
        }

        float loadFactor = Mathf.Clamp01(0.55f + mining * 0.0045f);
        float crusherLeft = cargoTons * loadFactor * (0.75f + mining / 50f) * StableFloat(seed + ":crusher", 0.88f, 1.08f);
        float cargoLeft = cargoTons * StableFloat(seed + ":cargo", 0.78f, 1f);
        int chunks = 1 + random.Next(0, profile == ProfileQuick || profile == ProfileNormal ? 2 : 4);
        float remaining = targetFe;
        for (int i = 0; i < chunks + 3; i++)
        {
            if (remaining <= targetFe * 0.04f || crusherLeft <= 0f || cargoLeft <= 0f)
            {
                break;
            }

            OreDepositRow ore = PickOre(random, candidates);
            float valuePerKg = Mathf.Max(1f, CalculateOreDepositValuePerKg(ore));
            float usefulShare = Mathf.Clamp01((100f - ore.wastePct) / 100f);
            float desiredFe = Mathf.Min(remaining, targetFe / Mathf.Max(1, chunks) * StableFloat(seed + ":" + ore.oreId + ":" + i, 0.60f, 1.55f));
            float rawTons = desiredFe / Mathf.Max(1f, valuePerKg * 1000f);
            rawTons = Mathf.Min(rawTons, crusherLeft / Mathf.Max(0.01f, ore.crusherWearPerTon));
            rawTons = Mathf.Min(rawTons, cargoLeft / Mathf.Max(0.01f, usefulShare));
            if (rawTons <= 0f) continue;

            crusherLeft -= rawTons * ore.crusherWearPerTon;
            float usefulTons = rawTons * usefulShare;
            cargoLeft -= usefulTons;
            float valueFe = rawTons * 1000f * valuePerKg;
            string itemId = ResolveOreDepositItemId(ore, config);
            AddRewardLine(result, config, "material", ore.oreId, itemId, Mathf.RoundToInt(usefulTons * 1000f), valueFe, usefulTons);
            remaining = Mathf.Max(0f, targetFe - result.generatedMaterialFe);
        }
    }

    private static void GenerateSourceRewards(
        ShipTreeEntryConfig ship,
        string activity,
        string profile,
        float targetFe,
        System.Random random,
        string seed,
        SessionConfigDatabase config,
        SortieMissionResult result)
    {
        int rank = Mathf.Clamp(ship != null && ship.rank > 0 ? ship.rank : ship != null ? ship.treeTier : 1, 1, 10);
        int rating = GetSourceRating(ship, activity, profile, rank, random);
        float cargoLimitTons = Mathf.Max(0.25f, ship != null ? ship.cargoCapacityTons : 1f) * StableFloat(seed + ":cargo-limit", 0.76f, 1.02f);
        float cargoLeft = cargoLimitTons;
        int finds = Mathf.Clamp(1 + rating / 34 + rank / 4 + random.Next(0, 2), 1, profile == ProfileElite ? 6 : 4);
        float remaining = targetFe;
        for (int i = 0; i < finds + 4; i++)
        {
            if (remaining <= targetFe * 0.035f || cargoLeft <= 0f)
            {
                break;
            }

            QuickSortieRewardSourceConfig source = SelectSourceWeighted(config, activity, rating, random);
            string itemId = ResolveSourceItemId(config, source);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                continue;
            }

            float unitValue = Mathf.Max(1f, GetSourceUnitValue(config, source, itemId));
            float desiredFe = Mathf.Min(remaining, targetFe / Mathf.Max(1, finds) * StableFloat(seed + ":" + itemId + ":" + i, 0.50f, 1.45f));
            int amount = Mathf.Max(1, Mathf.RoundToInt(desiredFe / unitValue));
            float cargoTons = EstimateRewardCargoTons(itemId, amount);
            if (cargoTons > cargoLeft)
            {
                float scale = cargoLeft / Mathf.Max(0.001f, cargoTons);
                amount = Mathf.Max(1, Mathf.FloorToInt(amount * scale));
                cargoTons = EstimateRewardCargoTons(itemId, amount);
            }

            float valueFe = amount * unitValue;
            AddRewardLine(result, config, "material", source != null ? source.sourceId : activity, itemId, amount, valueFe, cargoTons);
            cargoLeft -= cargoTons;
            remaining = Mathf.Max(0f, targetFe - result.generatedMaterialFe);
        }
    }

    private static void GenerateRelicRewards(
        ShipTreeEntryConfig ship,
        string profile,
        float targetFe,
        System.Random random,
        string seed,
        SessionConfigDatabase config,
        SortieMissionResult result)
    {
        int score = Mathf.Max(ClampPositiveActivity(ship != null ? ship.hackingRating : 0), ClampPositiveActivity(ship != null ? ship.surveyRating : 0));
        if (score <= 0 || relicDecodeRows.Count == 0)
        {
            return;
        }

        string targetClass = profile == ProfileQuick ? "cheap" : profile == ProfileNormal ? "medium" : profile == ProfileDanger ? "valuable" : "expensive";
        int attempts = Mathf.Max(1, 1 + score / 18);
        if (profile == ProfileQuick) attempts = Mathf.Max(1, attempts - 2);
        if (profile == ProfileElite) attempts += 2;
        float remaining = targetFe;
        for (int i = 0; i < attempts + 4; i++)
        {
            if (remaining <= targetFe * 0.03f) break;
            RelicDecodeRow relic = PickRelic(random, targetClass, score);
            if (relic == null) break;
            float classValue = GetDictionaryValue(RelicClassValueFe, relic.relicClass, 60000f);
            float value = Mathf.Min(classValue * StableFloat(seed + ":" + relic.relicId + ":" + i, 0.82f, 1.20f), remaining * StableFloat(seed + ":take:" + i, 0.75f, 1.25f));
            value = Mathf.Max(1f, value);
            AddRewardLine(result, config, "material", relic.relicId, GetRelicItemId(relic.relicClass), 1, value, relic.typicalWeightKg / 1000f);
            remaining = Mathf.Max(0f, targetFe - result.generatedMaterialFe);
        }
    }

    private static void GenerateServiceRewards(
        string activity,
        float targetFe,
        System.Random random,
        string seed,
        SessionConfigDatabase config,
        SortieMissionResult result)
    {
        string normalized = NormalizeActivity(activity);
        string[] itemIds;
        float[] shares;
        if (normalized == "combat")
        {
            itemIds = new[] { "munition_bundle", "weapon", "mechanisms" };
            shares = new[] { 0.50f, 0.30f, 0.20f };
        }
        else if (normalized == "repair")
        {
            itemIds = new[] { "mechanisms", "munition_bundle", "perfcards", "nobel" };
            shares = new[] { 0.55f, 0.20f, 0.15f, 0.10f };
        }
        else
        {
            itemIds = new[] { "mechanisms", "munition_bundle", "weapon", "perfcards" };
            shares = new[] { 0.45f, 0.25f, 0.15f, 0.15f };
        }

        for (int i = 0; i < itemIds.Length; i++)
        {
            string itemId = itemIds[i];
            if (config != null && config.GetItem(itemId) == null)
            {
                continue;
            }

            float value = Mathf.Max(1f, GetItemValue(itemId, 120f));
            float desired = targetFe * shares[i] * StableFloat(seed + ":" + itemId, 0.82f, 1.18f);
            int amount = Mathf.Max(1, Mathf.RoundToInt(desired / value));
            float valueFe = amount * value;
            float cargo = amount * (itemId == "perfcards" || itemId == "nobel" ? 0.001f : 0.005f);
            AddRewardLine(result, config, "material", normalized + "_contract", itemId, amount, valueFe, cargo);
        }
    }

    private static void ResolveOutcome(MissionProfileRow row, System.Random random, string seed, SortieMissionResult result)
    {
        float success = Mathf.Clamp01(row.missionSuccessFactor);
        float miss = Mathf.Max(0f, 1f - success);
        float destroyedChance = Mathf.Min(0.92f, GetDictionaryValue(ProfileDestroyBase, row.missionProfile, 0.025f) + miss * miss * GetDictionaryValue(ProfileDestroyPressure, row.missionProfile, 0.32f));
        float damagedChance = Mathf.Min(0.75f, GetDictionaryValue(ProfileDamagedBase, row.missionProfile, 0.12f) + miss * 0.45f);
        double roll = random.NextDouble();
        result.destroyedChance = destroyedChance;
        result.damagedChance = damagedChance;
        if (roll < destroyedChance)
        {
            result.outcome = "destroyed";
            result.damagePercent = 100f;
        }
        else if (roll < destroyedChance + damagedChance)
        {
            result.outcome = "extracted_damaged";
            result.damagePercent = StableFloat(seed + ":damage:" + roll.ToString(CultureInfo.InvariantCulture), 18f, 72f);
        }
        else
        {
            result.outcome = "extracted_clean";
            result.damagePercent = StableFloat(seed + ":scratch:" + roll.ToString(CultureInfo.InvariantCulture), 0f, 16f);
        }
    }

    private static void AddRewardLine(SortieMissionResult result, SessionConfigDatabase config, string layer, string sourceId, string itemId, int amount, float valueFe, float cargoTons)
    {
        if (result == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return;
        }

        for (int i = 0; i < result.materialRewards.Count; i++)
        {
            SortieRewardLine existing = result.materialRewards[i];
            if (existing != null && string.Equals(existing.itemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                existing.amount += amount;
                existing.valueFe += valueFe;
                existing.cargoTons += Mathf.Max(0f, cargoTons);
                result.generatedMaterialFe += Mathf.Max(0f, valueFe);
                result.cargoUsedTons += Mathf.Max(0f, cargoTons);
                return;
            }
        }

        result.materialRewards.Add(new SortieRewardLine
        {
            layer = layer,
            sourceId = sourceId ?? "",
            itemId = itemId,
            displayNameRu = config != null ? config.GetItemNameRu(itemId) : itemId,
            amount = amount,
            valueFe = Mathf.Max(0f, valueFe),
            cargoTons = Mathf.Max(0f, cargoTons)
        });
        result.generatedMaterialFe += Mathf.Max(0f, valueFe);
        result.cargoUsedTons += Mathf.Max(0f, cargoTons);
    }

    private static MissionProfileRow GetMissionRow(ShipTreeEntryConfig ship, string profile, string activityOverride)
    {
        string shipId = ship != null ? ship.shipId : "";
        string key = profile + "|" + shipId;
        MissionProfileRow source;
        if (!missionRowsByKey.TryGetValue(key, out source))
        {
            source = BuildFallbackMissionRow(ship, profile);
        }

        MissionProfileRow row = source.Clone();
        row.primaryActivity = NormalizeActivity(activityOverride);
        row.primaryActivityRu = GetActivityLabelRu(row.primaryActivity);
        if (string.IsNullOrWhiteSpace(row.primaryActivity))
        {
            row.primaryActivity = GetPrimaryActivity(ship);
            row.primaryActivityRu = GetActivityLabelRu(row.primaryActivity);
        }

        if (!string.Equals(row.primaryActivity, source.primaryActivity, StringComparison.OrdinalIgnoreCase))
        {
            float activityScore = GetActivityScore(ship, row.primaryActivity);
            float sourceScore = Mathf.Max(1f, source.primaryActivityScore);
            float scale = Mathf.Clamp(0.62f + activityScore / sourceScore * 0.38f, 0.45f, 1.22f);
            row.materialValueFe *= scale;
            row.intangibleValueFe *= Mathf.Clamp(0.76f + scale * 0.24f, 0.65f, 1.18f);
            row.freightFe *= Mathf.Clamp(0.75f + scale * 0.25f, 0.65f, 1.20f);
            row.reputationFe *= Mathf.Clamp(0.75f + scale * 0.25f, 0.65f, 1.20f);
            row.extractedTotalValueFe = row.materialValueFe + row.intangibleValueFe * IntangibleExtractionMultiplier;
        }

        return row;
    }

    private static MissionProfileRow BuildFallbackMissionRow(ShipTreeEntryConfig ship, string profile)
    {
        int rank = Mathf.Clamp(ship != null && ship.rank > 0 ? ship.rank : ship != null ? ship.treeTier : 1, 1, 10);
        string activity = GetPrimaryActivity(ship);
        float activityScore = Mathf.Max(8f, GetActivityScore(ship, activity));
        float rankPower = Mathf.Pow(rank + 1f, 2.05f);
        float profileMultiplier = profile == ProfileQuick ? 32f : profile == ProfileNormal ? 2500f : profile == ProfileDanger ? 4200f : 7200f;
        float total = rankPower * profileMultiplier * (0.65f + activityScore / 140f);
        float materialShare = IsMostlyMaterialActivity(activity) ? 0.62f : 0.22f;
        float material = total * materialShare / (materialShare + (1f - materialShare) * IntangibleExtractionMultiplier);
        float intangible = Mathf.Max(1f, (total - material) / IntangibleExtractionMultiplier);
        float freight = intangible * (activity == "repair" ? 0.18f : activity == "courier" ? 0.38f : activity == "combat" ? 0.32f : 0.24f);
        return new MissionProfileRow
        {
            missionProfile = profile,
            shipId = ship != null ? ship.shipId : "",
            rank = rank,
            missionArchetype = profile == ProfileQuick ? "quick_adaptive" : BuildArchetype(profile, activity),
            primaryActivity = activity,
            primaryActivityRu = GetActivityLabelRu(activity),
            primaryActivityScore = activityScore,
            materialValueFe = material,
            intangibleValueFe = intangible,
            extractedTotalValueFe = material + intangible * IntangibleExtractionMultiplier,
            freightFe = freight,
            reputationFe = intangible * (activity == "courier" || activity == "repair" ? 0.28f : 0.08f),
            baseShipXp = Mathf.Max(2, rank * 4),
            baseMasteryPoints = Mathf.Max(1, rank * 2),
            missionSuccessFactor = profile == ProfileQuick ? 1f : 0.75f
        };
    }

    private static string BuildReportText(SortieMissionResult result, SessionConfigDatabase config)
    {
        if (result == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(result.titleRu + " - " + GetOutcomeLabelRu(result.outcome));
        builder.Append("FE: ");
        builder.Append(Mathf.RoundToInt(result.awardedTotalFe).ToString("N0", CultureInfo.InvariantCulture));
        builder.Append(" | груз ");
        builder.Append(result.cargoUsedTons.ToString("0.0", CultureInfo.InvariantCulture));
        builder.AppendLine(" т");

        if (result.Destroyed)
        {
            builder.AppendLine("Материальное потеряно: корабль не вышел из зоны.");
        }
        else
        {
            builder.Append("Материальное: ");
            builder.AppendLine(BuildRewardList(result.materialRewards, 8));
        }

        builder.Append("Нематериальное: ");
        bool any = false;
        if (result.freightAward > 0)
        {
            builder.Append("фрахты x");
            builder.Append(result.freightAward.ToString(CultureInfo.InvariantCulture));
            any = true;
        }

        if (result.designExperienceAward > 0)
        {
            if (any) builder.Append(", ");
            builder.Append("опыт x");
            builder.Append(result.designExperienceAward.ToString(CultureInfo.InvariantCulture));
            any = true;
        }

        if (result.reputationFe > 0.5f)
        {
            if (any) builder.Append(", ");
            builder.Append("репутация ");
            builder.Append(Mathf.RoundToInt(result.reputationFe).ToString(CultureInfo.InvariantCulture));
            builder.Append(" FE");
            any = true;
        }

        if (!any)
        {
            builder.Append("-");
        }

        builder.AppendLine();
        builder.Append("Риск: уничтожение ");
        builder.Append((result.destroyedChance * 100f).ToString("0.#", CultureInfo.InvariantCulture));
        builder.Append("%, повреждения ");
        builder.Append((result.damagedChance * 100f).ToString("0.#", CultureInfo.InvariantCulture));
        builder.Append("%.");
        if (!string.IsNullOrWhiteSpace(result.sourceSummary))
        {
            builder.AppendLine();
            builder.Append(result.sourceSummary);
        }

        return builder.ToString();
    }

    private static string BuildRewardList(List<SortieRewardLine> rewards, int max)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return "-";
        }

        StringBuilder builder = new StringBuilder();
        int count = Mathf.Clamp(max, 1, 32);
        for (int i = 0; i < rewards.Count && i < count; i++)
        {
            SortieRewardLine reward = rewards[i];
            if (reward == null) continue;
            if (builder.Length > 0) builder.Append(", ");
            builder.Append(string.IsNullOrWhiteSpace(reward.displayNameRu) ? reward.itemId : reward.displayNameRu);
            builder.Append(" x");
            builder.Append(reward.amount.ToString(CultureInfo.InvariantCulture));
        }

        if (rewards.Count > count)
        {
            builder.Append(", +");
            builder.Append((rewards.Count - count).ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        string folder = GetPortConfigFolder();
        LoadResourceValues(Path.Combine(folder, "resource_value_reference.csv"));
        LoadMissionRows(Path.Combine(folder, "mission_profile_ship_profit.csv"));
        LoadRequirements(Path.Combine(folder, "mission_archetype_requirements.csv"));
        LoadOreDeposits(Path.Combine(folder, "ore_deposits.csv"));
        LoadLeviathanButchery(Path.Combine(folder, "leviathan_butchery.csv"));
        LoadRelics(Path.Combine(folder, "relic_decode.csv"));
    }

    private static string GetPortConfigFolder()
    {
        string assetsPath = Application.dataPath;
        string projectRoot = Directory.GetParent(assetsPath) != null ? Directory.GetParent(assetsPath).FullName : assetsPath;
        return Path.Combine(projectRoot, "Docs", "Balance", "PortConfigs");
    }

    private static void LoadResourceValues(string path)
    {
        if (!File.Exists(path)) return;
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            string itemId = Get(row, "item_id");
            if (string.IsNullOrWhiteSpace(itemId)) continue;
            itemValues[itemId] = ParseFloat(Get(row, "value_fe_per_unit"), 1f);
            itemCategories[itemId] = Get(row, "category");
        }
    }

    private static void LoadMissionRows(string path)
    {
        if (!File.Exists(path)) return;
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            MissionProfileRow profile = new MissionProfileRow
            {
                missionProfile = Get(row, "mission_profile"),
                shipId = Get(row, "ship_id"),
                rank = ParseInt(Get(row, "rank"), 1),
                missionArchetype = Get(row, "mission_archetype"),
                primaryActivity = NormalizeActivity(Get(row, "primary_activity")),
                primaryActivityRu = Get(row, "primary_activity_ru"),
                primaryActivityScore = ParseFloat(Get(row, "primary_activity_score"), 0f),
                materialValueFe = ParseFloat(Get(row, "material_value_fe"), 0f),
                intangibleValueFe = ParseFloat(Get(row, "intangible_value_fe"), 0f),
                extractedTotalValueFe = ParseFloat(Get(row, "extracted_total_value_fe"), ParseFloat(Get(row, "total_value_fe"), 0f)),
                freightFe = ParseFloat(Get(row, "freight_fe"), 0f),
                reputationFe = ParseFloat(Get(row, "reputation_fe"), 0f),
                baseShipXp = ParseInt(Get(row, "base_ship_xp"), 0),
                baseMasteryPoints = ParseInt(Get(row, "base_mastery_points"), 0),
                missionSuccessFactor = ParseFloat(Get(row, "mission_success_factor"), 1f)
            };

            if (string.IsNullOrWhiteSpace(profile.missionProfile) || string.IsNullOrWhiteSpace(profile.shipId))
            {
                continue;
            }

            missionRowsByKey[profile.missionProfile + "|" + profile.shipId] = profile;
        }
    }

    private static void LoadRequirements(string path)
    {
        if (!File.Exists(path)) return;
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            string archetype = Get(row, "mission_archetype");
            if (string.IsNullOrWhiteSpace(archetype)) continue;
            List<SortieMissionRequirement> list;
            if (!requirementsByArchetype.TryGetValue(archetype, out list))
            {
                list = new List<SortieMissionRequirement>();
                requirementsByArchetype[archetype] = list;
            }

            list.Add(new SortieMissionRequirement
            {
                statId = NormalizeRequirementStat(Get(row, "stat_id")),
                requiredValue = Mathf.Clamp(ParseInt(Get(row, "required_value"), 0), 0, 100),
                weight = Mathf.Max(0.01f, ParseFloat(Get(row, "weight"), 1f)),
                hardGate = string.Equals(Get(row, "hard_gate"), "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Get(row, "hard_gate"), "true", StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private static void LoadOreDeposits(string path)
    {
        if (!File.Exists(path)) return;
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            OreDepositRow ore = new OreDepositRow
            {
                oreId = Get(row, "ore_id"),
                localNameRu = Get(row, "local_name_ru"),
                minMining = ParseFloat(Get(row, "min_mining"), 1f),
                crusherWearPerTon = ParseFloat(Get(row, "crusher_wear_per_ton"), 1f),
                baseWeight = ParseFloat(Get(row, "base_weight"), 1f),
                luckyWeight = ParseFloat(Get(row, "lucky_weight"), 0f),
                wastePct = ParseFloat(Get(row, "waste_pct"), 50f)
            };

            AddOreShare(row, ore, "iron_pct", "iron");
            AddOreShare(row, ore, "copper_pct", "copper");
            AddOreShare(row, ore, "magnesium_pct", "magnesium");
            AddOreShare(row, ore, "quartz_pct", "quartz");
            AddOreShare(row, ore, "charcoal_pct", "charcoal");
            AddOreShare(row, ore, "calcite_pct", "calcite");
            AddOreShare(row, ore, "sylvine_pct", "sylvine");
            AddOreShare(row, ore, "claudium_pct", "claudium");
            AddOreShare(row, ore, "monazite_pct", "monazite");
            AddOreShare(row, ore, "gems_pct", "gems");

            if (!string.IsNullOrWhiteSpace(ore.oreId))
            {
                oreDeposits.Add(ore);
            }
        }
    }

    private static void LoadLeviathanButchery(string path)
    {
        if (!File.Exists(path)) return;
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            LeviathanButcheryRow leviathan = new LeviathanButcheryRow
            {
                leviathanId = Get(row, "leviathan_id"),
                massTons = ParseFloat(Get(row, "mass_t"), 1f)
            };

            if (!string.IsNullOrWhiteSpace(leviathan.leviathanId))
            {
                leviathanButcheryRows.Add(leviathan);
            }
        }
    }

    private static void LoadRelics(string path)
    {
        if (!File.Exists(path)) return;
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            RelicDecodeRow relic = new RelicDecodeRow
            {
                relicId = Get(row, "relic_id"),
                relicClass = Get(row, "class"),
                localNameRu = Get(row, "local_name_ru"),
                typicalWeightKg = ParseFloat(Get(row, "typical_weight_kg"), 30f)
            };
            if (!string.IsNullOrWhiteSpace(relic.relicId))
            {
                relicDecodeRows.Add(relic);
            }
        }
    }

    private static void AddOreShare(Dictionary<string, string> row, OreDepositRow ore, string column, string itemId)
    {
        float share = ParseFloat(Get(row, column), 0f) / 100f;
        if (share <= 0f) return;
        ore.composition.Add(new ItemShare { itemId = itemId, share = share });
    }

    private static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
    {
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0) yield break;

        List<string> headers = ParseCsvLine(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            List<string> values = ParseCsvLine(lines[i]);
            Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int column = 0; column < headers.Count; column++)
            {
                string header = headers[column].Trim('\uFEFF');
                row[header] = column < values.Count ? values[column] : "";
            }

            yield return row;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        StringBuilder builder = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char current = line[i];
            if (current == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (current == ',' && !inQuotes)
            {
                values.Add(builder.ToString().Trim());
                builder.Length = 0;
                continue;
            }

            builder.Append(current);
        }

        values.Add(builder.ToString().Trim());
        return values;
    }

    private static List<SortieMissionRequirement> CloneRequirements(string archetype)
    {
        List<SortieMissionRequirement> source;
        List<SortieMissionRequirement> result = new List<SortieMissionRequirement>();
        if (!requirementsByArchetype.TryGetValue(archetype ?? "", out source))
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            SortieMissionRequirement requirement = source[i];
            if (requirement == null) continue;
            result.Add(new SortieMissionRequirement
            {
                statId = requirement.statId,
                requiredValue = requirement.requiredValue,
                weight = requirement.weight,
                hardGate = requirement.hardGate
            });
        }

        return result;
    }

    private static QuickSortieRewardSourceConfig SelectSourceWeighted(SessionConfigDatabase config, string activityId, int rating, System.Random random)
    {
        if (config == null || config.quickSortieRewardSources == null)
        {
            return null;
        }

        string normalized = activityId == "gas" ? "harvesting" : activityId;
        rating = Mathf.Clamp(rating, 1, 100);
        int overreach = 5 + Mathf.RoundToInt(rating * 0.10f) + random.Next(0, 7);
        List<WeightedSource> candidates = new List<WeightedSource>();
        float total = 0f;
        for (int i = 0; i < config.quickSortieRewardSources.Count; i++)
        {
            QuickSortieRewardSourceConfig source = config.quickSortieRewardSources[i];
            if (source == null || !string.Equals(source.activityId, normalized, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int distance = Mathf.Abs(source.minRating - rating);
            float weight = 0f;
            if (source.minRating <= rating)
            {
                weight = source.weight / (1f + distance / 18f);
            }
            else if (source.minRating <= rating + overreach)
            {
                weight = source.weight * 0.16f / (1f + distance / 7f);
            }

            if (weight <= 0f) continue;
            candidates.Add(new WeightedSource { source = source, weight = weight });
            total += weight;
        }

        if (candidates.Count == 0 || total <= 0f)
        {
            return null;
        }

        double roll = random.NextDouble() * total;
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= candidates[i].weight;
            if (roll <= 0d)
            {
                return candidates[i].source;
            }
        }

        return candidates[candidates.Count - 1].source;
    }

    private static string ResolveSourceItemId(SessionConfigDatabase config, QuickSortieRewardSourceConfig source)
    {
        if (source == null || config == null)
        {
            return "";
        }

        switch ((source.sourceKind ?? "").Trim().ToLowerInvariant())
        {
            case "ore":
                OreTypeConfig ore = config.GetOreType(source.sourceId);
                return ore != null ? ore.oreItemId : "";
            case "gas":
                GasCondensateTypeConfig gas = config.GetGasCondensateType(source.sourceId);
                return gas != null ? gas.condensateItemId : "";
            case "leviathan":
                LeviathanTypeConfig leviathan = config.GetLeviathanType(source.sourceId);
                return leviathan != null ? leviathan.carcassItemId : "";
            case "item":
                return config.GetItem(source.sourceId) != null ? source.sourceId : "";
            default:
                return "";
        }
    }

    private static string ResolveOreDepositItemId(OreDepositRow ore, SessionConfigDatabase config)
    {
        if (config != null && config.oreTypes != null && config.oreTypes.Count > 0)
        {
            OreTypeConfig best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < config.oreTypes.Count; i++)
            {
                OreTypeConfig candidate = config.oreTypes[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.oreItemId)) continue;
                float value = CalculateOreTypeValuePerKg(candidate) * Mathf.Max(1f, candidate.baseValue);
                float score = Mathf.Abs(value - CalculateOreDepositValuePerKg(ore));
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (best != null)
            {
                return best.oreItemId;
            }
        }

        return "windshale_ore";
    }

    private static float GetSourceUnitValue(SessionConfigDatabase config, QuickSortieRewardSourceConfig source, string itemId)
    {
        if (source == null)
        {
            return GetItemValue(itemId, 100f);
        }

        switch ((source.sourceKind ?? "").Trim().ToLowerInvariant())
        {
            case "ore":
                OreTypeConfig ore = config != null ? config.GetOreType(source.sourceId) : null;
                return ore != null ? Mathf.Max(1f, CalculateOreTypeValuePerKg(ore) * Mathf.Max(1f, ore.baseValue)) : GetItemValue(itemId, 40f);
            case "gas":
                GasCondensateTypeConfig gas = config != null ? config.GetGasCondensateType(source.sourceId) : null;
                return gas != null ? Mathf.Max(1f, CalculateGasValuePerKg(gas)) : GetItemValue(itemId, 80f);
            case "leviathan":
                LeviathanTypeConfig leviathan = config != null ? config.GetLeviathanType(source.sourceId) : null;
                return leviathan != null ? Mathf.Max(1f, CalculateLeviathanCarcassValuePerKg(leviathan)) : GetItemValue(itemId, 120f);
            default:
                return GetItemValue(itemId, 100f);
        }
    }

    private static float CalculateOreTypeValuePerKg(OreTypeConfig ore)
    {
        if (ore == null || ore.composition == null || ore.composition.Count == 0)
        {
            return 25f;
        }

        float value = 0f;
        for (int i = 0; i < ore.composition.Count; i++)
        {
            OreMineralCompositionConfig part = ore.composition[i];
            if (part == null) continue;
            value += Mathf.Max(0f, part.share) * GetItemValue(part.mineralItemId, 25f);
        }

        return Mathf.Max(1f, value);
    }

    private static float CalculateOreDepositValuePerKg(OreDepositRow ore)
    {
        if (ore == null || ore.composition == null || ore.composition.Count == 0)
        {
            return 25f;
        }

        float value = 0f;
        for (int i = 0; i < ore.composition.Count; i++)
        {
            ItemShare part = ore.composition[i];
            value += Mathf.Max(0f, part.share) * GetItemValue(part.itemId, 25f);
        }

        return Mathf.Max(1f, value);
    }

    private static float CalculateGasValuePerKg(GasCondensateTypeConfig gas)
    {
        if (gas == null || gas.composition == null || gas.composition.Count == 0)
        {
            return 80f;
        }

        float value = 0f;
        for (int i = 0; i < gas.composition.Count; i++)
        {
            GasCondensateCompositionConfig part = gas.composition[i];
            if (part == null) continue;
            value += Mathf.Max(0f, part.share) * GetItemValue(part.itemId, 80f);
        }

        return Mathf.Max(1f, value);
    }

    private static float CalculateLeviathanCarcassValuePerKg(LeviathanTypeConfig leviathan)
    {
        if (leviathan == null || leviathan.composition == null || leviathan.composition.Count == 0)
        {
            return 120f;
        }

        float value = 0f;
        for (int i = 0; i < leviathan.composition.Count; i++)
        {
            LeviathanButcheryCompositionConfig part = leviathan.composition[i];
            if (part == null) continue;
            value += Mathf.Max(0f, part.share) * GetItemValue(part.itemId, 120f);
        }

        return Mathf.Max(1f, value);
    }

    private static float EstimateRewardCargoTons(string itemId, int amount)
    {
        if (amount <= 0) return 0f;
        float each;
        if (AutomatonItemMassTons.TryGetValue(itemId ?? "", out each))
        {
            return each * amount;
        }

        if (itemId != null && itemId.StartsWith("relic_", StringComparison.OrdinalIgnoreCase))
        {
            return 0.06f * amount;
        }

        return amount / 1000f;
    }

    private static int GetSourceRating(ShipTreeEntryConfig ship, string activity, string profile, int rank, System.Random random)
    {
        if (!string.Equals(profile, ProfileQuick, StringComparison.OrdinalIgnoreCase))
        {
            float center = profile == ProfileNormal ? 42f : profile == ProfileDanger ? 62f : 82f;
            return Mathf.Clamp(Mathf.RoundToInt(center + RandomRange(random, -8f, 8f)), 1, 100);
        }

        int rating = Mathf.RoundToInt(GetActivityScore(ship, activity) * 0.82f + rank * 3.4f);
        return Mathf.Clamp(rating, 1, 100);
    }

    private static float SourceHint(string profile, float score, int rank)
    {
        if (profile == ProfileQuick) return Mathf.Min(score, 8f + rank * 5.5f);
        if (profile == ProfileNormal) return Mathf.Min(score, 42f);
        if (profile == ProfileDanger) return Mathf.Min(score, 62f);
        return Mathf.Min(score, 82f);
    }

    private static float EstimateMissionSuccess(ShipTreeEntryConfig ship, List<SortieMissionRequirement> requirements, string profile)
    {
        if (string.Equals(profile, ProfileQuick, StringComparison.OrdinalIgnoreCase))
        {
            return 1f;
        }

        if (requirements == null || requirements.Count == 0)
        {
            return 0.78f;
        }

        float totalWeight = 0f;
        float score = 0f;
        bool hardFailed = false;
        for (int i = 0; i < requirements.Count; i++)
        {
            SortieMissionRequirement requirement = requirements[i];
            if (requirement == null) continue;
            int value = GetShipStatForRequirement(ship, requirement.statId);
            float required = Mathf.Max(1f, requirement.requiredValue);
            float ratio = Mathf.Clamp(value / required, 0f, 1.25f);
            score += ratio * requirement.weight;
            totalWeight += requirement.weight;
            if (requirement.hardGate && value < requirement.requiredValue)
            {
                hardFailed = true;
            }
        }

        float normalized = totalWeight > 0f ? score / totalWeight : 0.8f;
        float success = Mathf.Clamp01(0.08f + normalized * 0.82f);
        if (hardFailed)
        {
            success *= profile == ProfileElite ? 0.45f : profile == ProfileDanger ? 0.58f : 0.72f;
        }

        return Mathf.Clamp(success, 0.05f, 0.98f);
    }

    private static string GetPrimaryActivity(ShipTreeEntryConfig ship)
    {
        if (ship == null)
        {
            return "courier";
        }

        string best = "courier";
        float bestScore = Mathf.Max(0f, ship.cargo) * 0.35f;
        ConsiderActivity(ship, "combat", ship.warfareRating >= 0 ? ship.warfareRating : ship.firepower, ref best, ref bestScore);
        ConsiderActivity(ship, "mining", ship.miningRating, ref best, ref bestScore);
        ConsiderActivity(ship, "gas", ship.harvestingRating, ref best, ref bestScore);
        ConsiderActivity(ship, "hunting", ship.huntingRating, ref best, ref bestScore);
        ConsiderActivity(ship, "salvage", ship.salvageRating, ref best, ref bestScore);
        ConsiderActivity(ship, "relic", Mathf.Max(ship.hackingRating, ship.surveyRating), ref best, ref bestScore);
        ConsiderActivity(ship, "repair", ship.repairRating, ref best, ref bestScore);
        return best;
    }

    private static void ConsiderActivity(ShipTreeEntryConfig ship, string activity, int rating, ref string best, ref float bestScore)
    {
        if (rating <= 0) return;
        float score = rating;
        if (activity == "combat") score *= 0.95f;
        if (activity == "courier") score *= 0.80f;
        if (score > bestScore)
        {
            best = activity;
            bestScore = score;
        }
    }

    private static float GetActivityScore(ShipTreeEntryConfig ship, string activity)
    {
        if (ship == null) return 0f;
        switch (NormalizeActivity(activity))
        {
            case "combat": return ClampRating(ship.warfareRating >= 0 ? ship.warfareRating : ship.firepower);
            case "mining": return ClampPositiveActivity(ship.miningRating);
            case "gas": return ClampPositiveActivity(ship.harvestingRating);
            case "hunting": return ClampPositiveActivity(ship.huntingRating);
            case "salvage": return Mathf.Max(ClampPositiveActivity(ship.salvageRating), ClampRating(ship.warfareRating >= 0 ? ship.warfareRating : ship.firepower) * 0.65f);
            case "relic": return Mathf.Max(ClampPositiveActivity(ship.hackingRating), ClampPositiveActivity(ship.surveyRating));
            case "repair": return ClampPositiveActivity(ship.repairRating);
            case "courier": return Mathf.Max(ClampPositiveActivity(ship.cargo), ClampRating(ship.mobilityRating >= 0 ? ship.mobilityRating : ship.speed));
            default: return 0f;
        }
    }

    private static string BuildArchetype(string profile, string activity)
    {
        string suffix;
        switch (NormalizeActivity(activity))
        {
            case "mining": suffix = "ore"; break;
            case "gas": suffix = "gas"; break;
            case "hunting": suffix = "hunting"; break;
            case "salvage": suffix = "salvage"; break;
            case "relic": suffix = "relic"; break;
            case "repair": suffix = "repair"; break;
            case "combat": suffix = "combat"; break;
            default: suffix = "courier"; break;
        }

        return (profile ?? ProfileNormal).Trim().ToLowerInvariant() + "_" + suffix;
    }

    private static string BuildOfferTitle(string profile, string activity, int index, string seed)
    {
        string[] normalNames = { "контракт", "маршрут", "зона", "сводка", "рейд" };
        string[] dangerNames = { "опасный пояс", "штормовой заказ", "прорыв", "чёрная отметка", "жёсткий рейс" };
        string[] eliteNames = { "элитный узел", "глубинный вылет", "флагманская цель", "закрытый контракт", "край неба" };
        string[] names = profile == ProfileElite ? eliteNames : profile == ProfileDanger ? dangerNames : normalNames;
        int pick = Mathf.Abs(StableSeedToInt(seed + ":title")) % names.Length;
        return GetProfileLabelRu(profile) + ": " + GetActivityLabelRu(activity) + " - " + names[pick];
    }

    private static string GetProfileLabelRu(string profile)
    {
        switch ((profile ?? "").Trim().ToLowerInvariant())
        {
            case ProfileQuick: return "Быстрая";
            case ProfileNormal: return "Обычная";
            case ProfileDanger: return "Опасная";
            case ProfileElite: return "Элитная";
            default: return profile ?? "";
        }
    }

    private static string GetActivityLabelRu(string activity)
    {
        switch (NormalizeActivity(activity))
        {
            case "combat": return "бой";
            case "mining": return "руда";
            case "gas": return "облака";
            case "hunting": return "левиафаны";
            case "salvage": return "сальваж";
            case "relic": return "реликты";
            case "repair": return "ремонт";
            case "courier": return "логистика";
            default: return string.IsNullOrWhiteSpace(activity) ? "миссия" : activity;
        }
    }

    private static string GetOutcomeLabelRu(string outcome)
    {
        switch ((outcome ?? "").Trim().ToLowerInvariant())
        {
            case "destroyed": return "корабль потерян";
            case "extracted_damaged": return "вышел с повреждениями";
            case "extracted_clean": return "чистый выход";
            default: return outcome ?? "";
        }
    }

    private static string NormalizeActivity(string activity)
    {
        string value = (activity ?? "").Trim().ToLowerInvariant();
        if (value == "harvesting") return "gas";
        if (value == "warfare") return "combat";
        if (value == "hacking" || value == "survey") return "relic";
        return value;
    }

    private static string NormalizeRequirementStat(string stat)
    {
        string value = (stat ?? "").Trim().ToLowerInvariant();
        if (value == "harvesting") return "gas";
        return value;
    }

    private static bool IsMostlyMaterialActivity(string activity)
    {
        string normalized = NormalizeActivity(activity);
        return normalized == "mining" || normalized == "gas" || normalized == "hunting" || normalized == "salvage" || normalized == "relic";
    }

    private static int ClampPositiveActivity(int rating)
    {
        return rating > 0 ? Mathf.Clamp(rating, 1, 100) : 0;
    }

    private static int ClampRating(int rating)
    {
        return Mathf.Clamp(rating, 0, 100);
    }

    private static float GetItemValue(string itemId, float fallback)
    {
        float value;
        return itemValues.TryGetValue(itemId ?? "", out value) ? Mathf.Max(1f, value) : Mathf.Max(1f, fallback);
    }

    private static float GetDictionaryValue(Dictionary<string, float> source, string key, float fallback)
    {
        float value;
        return source != null && source.TryGetValue(key ?? "", out value) ? value : fallback;
    }

    private static int RoundCurrency(float value)
    {
        int amount = Mathf.Max(0, Mathf.RoundToInt(value));
        if (amount >= 10000) return Mathf.RoundToInt(amount / 100f) * 100;
        if (amount >= 1000) return Mathf.RoundToInt(amount / 50f) * 50;
        if (amount >= 100) return Mathf.RoundToInt(amount / 10f) * 10;
        return amount;
    }

    private static float RandomRange(System.Random random, float minInclusive, float maxInclusive)
    {
        if (random == null) random = new System.Random(17);
        if (maxInclusive <= minInclusive) return minInclusive;
        return minInclusive + (float)random.NextDouble() * (maxInclusive - minInclusive);
    }

    private static float Triangular(System.Random random, float low, float high, float mode)
    {
        if (random == null) random = new System.Random(17);
        float u = (float)random.NextDouble();
        float c = (mode - low) / Mathf.Max(0.0001f, high - low);
        if (u <= c)
        {
            return low + Mathf.Sqrt(u * (high - low) * (mode - low));
        }

        return high - Mathf.Sqrt((1f - u) * (high - low) * (high - mode));
    }

    private static long GetRefreshWindow(DateTime utcNow)
    {
        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        long seconds = Math.Max(0L, (long)(utcNow.ToUniversalTime() - epoch).TotalSeconds);
        return seconds / MissionRefreshSeconds;
    }

    private static string BuildSeed(string prefix, string shipId, int a, int b, long window, string tail)
    {
        return prefix + ":" + (shipId ?? "") + ":" + a.ToString(CultureInfo.InvariantCulture) + ":" + b.ToString(CultureInfo.InvariantCulture) + ":" + window.ToString(CultureInfo.InvariantCulture) + ":" + (tail ?? "");
    }

    private static int StableSeedToInt(string text)
    {
        unchecked
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            byte[] hash = SHA256.Create().ComputeHash(bytes);
            int seed = BitConverter.ToInt32(hash, 0);
            return seed == int.MinValue ? 17 : Math.Abs(seed);
        }
    }

    private static float StableFloat(string text, float low, float high)
    {
        int seed = StableSeedToInt(text);
        float normalized = (seed % 1000000) / 999999f;
        return low + (high - low) * normalized;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
        string value;
        return row != null && row.TryGetValue(key, out value) ? value : "";
    }

    private static int ParseInt(string value, int fallback)
    {
        int result;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
    }

    private static float ParseFloat(string value, float fallback)
    {
        float result;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback;
    }

    private static OreDepositRow PickOre(System.Random random, List<WeightedOre> entries)
    {
        if (entries == null || entries.Count == 0) return null;
        float total = 0f;
        for (int i = 0; i < entries.Count; i++) total += Mathf.Max(0f, entries[i].weight);
        if (total <= 0f) return entries[0].ore;
        double roll = random.NextDouble() * total;
        for (int i = 0; i < entries.Count; i++)
        {
            roll -= Mathf.Max(0f, entries[i].weight);
            if (roll <= 0d) return entries[i].ore;
        }

        return entries[entries.Count - 1].ore;
    }

    private static RelicDecodeRow PickRelic(System.Random random, string targetClass, int score)
    {
        if (relicDecodeRows.Count == 0) return null;
        int targetOrder = RelicClassOrder(targetClass);
        float total = 0f;
        float[] weights = new float[relicDecodeRows.Count];
        for (int i = 0; i < relicDecodeRows.Count; i++)
        {
            RelicDecodeRow relic = relicDecodeRows[i];
            int order = RelicClassOrder(relic.relicClass);
            float gate = Mathf.Clamp(score / (25f + order * 22f), 0.15f, 1f);
            float closeness = 1f / (1f + Mathf.Abs(order - targetOrder));
            weights[i] = gate * closeness * RandomRange(random, 0.8f, 1.3f);
            total += weights[i];
        }

        if (total <= 0f) return relicDecodeRows[0];
        double roll = random.NextDouble() * total;
        for (int i = 0; i < relicDecodeRows.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0d) return relicDecodeRows[i];
        }

        return relicDecodeRows[relicDecodeRows.Count - 1];
    }

    private static int RelicClassOrder(string relicClass)
    {
        switch ((relicClass ?? "").Trim().ToLowerInvariant())
        {
            case "cheap": return 0;
            case "medium": return 1;
            case "valuable": return 2;
            case "expensive": return 3;
            default: return 0;
        }
    }

    private static string GetRelicItemId(string relicClass)
    {
        switch ((relicClass ?? "").Trim().ToLowerInvariant())
        {
            case "medium": return "sp_500";
            case "valuable": return "sp_2000";
            case "expensive": return "sp_5000";
            default: return "book_fragments";
        }
    }

    private static SortieMissionResult BuildEmptyResult(string text)
    {
        return new SortieMissionResult
        {
            titleRu = "Вылет",
            outcome = "blocked",
            reportText = text ?? ""
        };
    }

    private sealed class MissionProfileRow
    {
        public string missionProfile = "";
        public string shipId = "";
        public int rank;
        public string missionArchetype = "";
        public string primaryActivity = "";
        public string primaryActivityRu = "";
        public float primaryActivityScore;
        public float materialValueFe;
        public float intangibleValueFe;
        public float extractedTotalValueFe;
        public float freightFe;
        public float reputationFe;
        public int baseShipXp;
        public int baseMasteryPoints;
        public float missionSuccessFactor = 1f;

        public MissionProfileRow Clone()
        {
            return (MissionProfileRow)MemberwiseClone();
        }
    }

    private sealed class OreDepositRow
    {
        public string oreId = "";
        public string localNameRu = "";
        public float minMining;
        public float crusherWearPerTon = 1f;
        public float baseWeight = 1f;
        public float luckyWeight;
        public float wastePct = 50f;
        public List<ItemShare> composition = new List<ItemShare>();
    }

    private sealed class LeviathanButcheryRow
    {
        public string leviathanId = "";
        public float massTons;
    }

    private sealed class RelicDecodeRow
    {
        public string relicId = "";
        public string relicClass = "";
        public string localNameRu = "";
        public float typicalWeightKg = 30f;
    }

    private sealed class ItemShare
    {
        public string itemId = "";
        public float share;
    }

    private struct WeightedOre
    {
        public OreDepositRow ore;
        public float weight;
    }

    private struct WeightedSource
    {
        public QuickSortieRewardSourceConfig source;
        public float weight;
    }
}
