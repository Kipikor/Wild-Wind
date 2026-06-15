import bpy
import json
import os


OUT_PATH = os.path.join(os.path.dirname(bpy.data.filepath), "NewFactionSafeRenders", "new_factions_safe_verification.json")
FACTIONS = {
    "Devourer": {
        "prefix": "ULP_Devourer_",
        "starter": "ULP_Devourer_StarterHarpoonSkiff_ROOT",
        "feature_terms": ["Harpoon"],
        "branches": {
            "frigate": ["HarpoonFrigate", "BrawlerFrigate", "ButcherFrigate"],
            "cruiser": ["HarpoonCruiser", "CannonCruiser", "RipperCruiser"],
            "battleship": ["LeviathanHunterBattleship", "LeviathanProcessorBattleship"],
        },
    },
    "ClockworkArk": {
        "prefix": "ULP_ClockworkArk_",
        "starter": "ULP_ClockworkArk_StarterXRayPod_ROOT",
        "feature_terms": ["XRay"],
        "branches": {
            "frigate": ["XRayScoutFrigate", "AutomatonTenderFrigate", "EscortFrigate"],
            "cruiser": ["ModularSurveyCruiser", "AutomatonRecoveryCruiser", "CombatArkCruiser"],
            "battleship": ["MechanizedCitadelBattleship", "AutomatonFactoryBattleship"],
        },
    },
    "StoneVault": {
        "prefix": "ULP_StoneVault_",
        "starter": "ULP_StoneVault_StarterMagnetDigger_ROOT",
        "feature_terms": ["Magnet"],
        "branches": {
            "frigate": ["MagnetScoutFrigate", "SingleGunDiggerFrigate", "OreTugFrigate"],
            "cruiser": ["ArmoredGunCruiser", "MagnetHaulerCruiser", "RefineryCruiser"],
            "battleship": ["SiegeDiggerBattleship", "OreFoundryBattleship"],
        },
    },
}


def child_mesh_names(root):
    obj = bpy.data.objects.get(root)
    if not obj:
        return []
    return [o.name for o in obj.children_recursive if o.type == "MESH"]


result = {}
all_ok = True
for faction, spec in FACTIONS.items():
    faction_report = {
        "starter_exists": bpy.data.objects.get(spec["starter"]) is not None,
        "starter_feature_hits": [],
        "class_branch_counts": {},
        "branch_rank_status": {},
        "feature_missing_roots": [],
        "root_total": 0,
    }
    if faction_report["starter_exists"]:
        names = child_mesh_names(spec["starter"])
        faction_report["starter_feature_hits"] = [n for n in names if any(term in n for term in spec["feature_terms"])]
    for cls, branches in spec["branches"].items():
        faction_report["class_branch_counts"][cls] = len(branches)
        for branch in branches:
            roots = []
            missing_ranks = []
            for rank in range(2, 11):
                root_name = f"{spec['prefix']}{branch}_Polished_R{rank:02d}_ROOT"
                root_obj = bpy.data.objects.get(root_name)
                if root_obj:
                    roots.append(root_name)
                    child_names = child_mesh_names(root_name)
                    if not any(any(term in child for term in spec["feature_terms"]) for child in child_names):
                        faction_report["feature_missing_roots"].append(root_name)
                else:
                    missing_ranks.append(rank)
            faction_report["branch_rank_status"][branch] = {
                "roots": len(roots),
                "missing_ranks": missing_ranks,
            }
            faction_report["root_total"] += len(roots)
    faction_report["root_total"] += 1 if faction_report["starter_exists"] else 0
    faction_report["ok"] = (
        faction_report["starter_exists"]
        and len(faction_report["starter_feature_hits"]) > 0
        and faction_report["class_branch_counts"] == {"frigate": 3, "cruiser": 3, "battleship": 2}
        and faction_report["root_total"] == 73
        and all(v["roots"] == 9 and not v["missing_ranks"] for v in faction_report["branch_rank_status"].values())
        and not faction_report["feature_missing_roots"]
    )
    all_ok = all_ok and faction_report["ok"]
    result[faction] = faction_report

result["all_ok"] = all_ok
os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
with open(OUT_PATH, "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)
print("ULP_NEW_FACTIONS_SAFE_VERIFY_DONE")
print(json.dumps(result, ensure_ascii=False))
if not all_ok:
    raise SystemExit(2)
