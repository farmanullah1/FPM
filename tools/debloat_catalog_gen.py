#!/usr/bin/env python3
"""Ports Win11Debloat's declarative data (MIT) into FPM's typed debloat catalog.

Reads only: reference/win11debloat/Config/{Features,Apps}.json and Regfiles/*.reg.
Writes: app/src/Core/Assets/debloat/catalog.json, docs/dev/win11debloat-feature-map.md.

Nothing here executes reference PowerShell. The catalog is data plus typed action records;
the FPM engine (D2) validates ids against it and never accepts free-form registry paths.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REF = ROOT / "reference" / "win11debloat"
OUT_CATALOG = ROOT / "app" / "src" / "Core" / "Assets" / "debloat" / "catalog.json"
OUT_MAP = ROOT / "docs" / "dev" / "win11debloat-feature-map.md"

HIVES = {
    "HKEY_CURRENT_USER": "HKCU",
    "HKEY_LOCAL_MACHINE": "HKLM",
    "HKEY_USERS": "HKU",
}

# Features with no .reg backing, mapped by hand from the reference script (file:line recorded).
MANUAL = {
    "EnableWindowsSandbox": {
        "actions": [{"type": "OptionalFeature", "name": "Containers-DisposableClientVM", "enable": True}],
        "revert": [{"type": "OptionalFeature", "name": "Containers-DisposableClientVM", "enable": False}],
        "detect": [{"type": "OptionalFeature", "name": "Containers-DisposableClientVM", "expectEnabled": True}],
        "referenceFile": "Scripts/Features/Invoke-Changes.ps1#EnableWindowsSandbox",
        "risk": "Medium",
    },
    "EnableWindowsSubsystemForLinux": {
        "actions": [
            {"type": "OptionalFeature", "name": "VirtualMachinePlatform", "enable": True},
            {"type": "OptionalFeature", "name": "Microsoft-Windows-Subsystem-Linux", "enable": True},
        ],
        "revert": [
            {"type": "OptionalFeature", "name": "Microsoft-Windows-Subsystem-Linux", "enable": False},
            {"type": "OptionalFeature", "name": "VirtualMachinePlatform", "enable": False},
        ],
        "detect": [
            {"type": "OptionalFeature", "name": "Microsoft-Windows-Subsystem-Linux", "expectEnabled": True},
            {"type": "OptionalFeature", "name": "VirtualMachinePlatform", "expectEnabled": True},
        ],
        "referenceFile": "Scripts/Features/Invoke-Changes.ps1#EnableWindowsSubsystemForLinux",
        "risk": "Medium",
    },
    "DisableWidgets": {
        "actions": [
            {"type": "AppxRemove", "packageId": "Microsoft.StartExperiencesApp"},
            {"type": "AppxRemove", "packageId": "MicrosoftWindows.Client.WebExperience"},
            {"type": "AppxRemove", "packageId": "Microsoft.WidgetsPlatformRuntime"},
        ],
        "revert": [],
        "revertNote": "Widgets packages are reinstalled from the Store; the Apps tab shows the guidance.",
        "detect": [
            {"type": "AppxAbsent", "packageId": "Microsoft.StartExperiencesApp"},
            {"type": "AppxAbsent", "packageId": "MicrosoftWindows.Client.WebExperience"},
            {"type": "AppxAbsent", "packageId": "Microsoft.WidgetsPlatformRuntime"},
        ],
        "referenceFile": "Scripts/Features/Invoke-Changes.ps1#DisableWidgets",
        "risk": "Medium",
    },
}

EXCLUDED = {
    "RemoveApps": "App selection is the Apps tab (D3), driven by catalog.apps; not a single tweak.",
    "Apps": "CLI/UI app-selection parameter; the Apps tab owns it.",
    "RemoveGamingApps": "App preset (catalog.appPresets 'Xbox gaming apps') handled by the Apps tab.",
    "RemoveHPApps": "App preset (HP apps) handled by the Apps tab; not a single tweak.",
    "CreateRestorePoint": "Pre-apply step in the D2 engine (IRestorePointService), not a tweak.",
    "ForceRemoveEdge": "Intentionally excluded: forcibly removing Edge is destructive and unsupported by FPM's safety contract.",
    "ClearStart": "Start layout replacement needs bundled templates and a file op outside the D1 action allow-list; deferred to a later card.",
    "ClearStartAllUsers": "All-users Start layout replacement, same reason as ClearStart.",
    "ReplaceStart": "Custom Start layout template import, same reason as ClearStart.",
    "ReplaceStartAllUsers": "All-users custom Start layout import, same reason as ClearStart.",
    "DisableStoreSearchSuggestions": "Edits the Store SQLite database (not a registry/file action type); intentionally excluded from D1.",
}

# FPM never offers these for removal; Win11Debloat lists the Store as "unsafe" and FPM drops it.
PROTECTED_PACKAGES = [
    "Microsoft.WindowsStore",
    "Microsoft.SecHealthUI",
    "Microsoft.Windows.ShellExperienceHost",
    "Microsoft.Windows.StartMenuExperienceHost",
    "Microsoft.DesktopAppInstaller",
    "Microsoft.VCLibs.140.00",
    "Microsoft.UI.Xaml",
]


def is_protected(package_id: str) -> bool:
    lowered = package_id.lower()
    return any(lowered == p.lower() or lowered.startswith(p.lower() + ".") for p in PROTECTED_PACKAGES)


RISK_OVERRIDES = {
    "DisableBitlockerAutoEncryption": "High",
    "DisableTelemetry": "Medium",
    "DisableWindowsUpdate": "High",
    "DisableDriverUpdates": "High",
    "DisableUpdateASAP": "High",
    "PreventUpdateAutoReboot": "High",
    "DisableDeliveryOptimization": "Medium",
    "DisableDefender": "High",
    "DisableAI_ServiceAutoStart": "Medium",
    "Disable_AI_Service_Auto_Start": "Medium",
    "EnableWindowsSandbox": "Medium",
    "EnableWindowsSubsystemForLinux": "Medium",
    "DisableWidgets": "Medium",
}

LOW_CATEGORIES = {"Appearance", "File Explorer", "Taskbar", "Start Menu & Search", "Multi-tasking", "Gaming"}
HIGH_CATEGORIES = {"Windows Update", "Optional Windows Features"}


def read_text(path: Path) -> str:
    raw = path.read_bytes()
    if raw.startswith(b"\xff\xfe"):
        return raw.decode("utf-16")
    if raw.startswith(b"\xef\xbb\xbf"):
        return raw.decode("utf-8-sig")
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("cp1252")


def parse_reg(path: Path) -> list[dict]:
    """Mirror of Get-RegFileOperations.ps1: store/remove value and remove key operations."""
    merged: list[str] = []
    index = 0
    raw_lines = read_text(path).replace("\r\n", "\n").split("\n")
    while index < len(raw_lines):
        line = raw_lines[index]
        while line.endswith("\\") and index + 1 < len(raw_lines):
            line = line[:-1] + raw_lines[index + 1]
            index += 1
        merged.append(line)
        index += 1

    actions: list[dict] = []
    key: tuple[str, str] | None = None
    deleted_key = False

    for raw_line in merged:
        line = raw_line.strip()
        if not line or line.startswith(";") or line.startswith("Windows Registry Editor"):
            continue

        section = re.match(r"^\[(-?)(.+)\]$", line)
        if section:
            deleted_key = section.group(1) == "-"
            full = section.group(2).strip()
            hive, _, rest = full.partition("\\")
            short = HIVES.get(hive)
            if short is None:
                raise SystemExit(f"Unsupported hive {hive} in {path.name}")
            if short == "HKU" and rest.upper().startswith("DEFAULT"):
                rest = "Default" + rest[len("Default"):]
            key = (short, rest)
            if deleted_key:
                actions.append({"type": "RegistryKeyDelete", "hive": short, "path": rest})
            continue

        if key is None or deleted_key:
            continue

        value = re.match(r'^(@|"[^"]+")=(.*)$', line)
        if not value:
            continue

        name = "" if value.group(1) == "@" else value.group(1).strip('"').replace('\\\\', '\\')
        data = value.group(2).strip()
        hive, sub = key

        if data == "-":
            actions.append({"type": "RegistryDelete", "hive": hive, "path": sub, "name": name})
            continue

        parsed = parse_data(data)
        if parsed is None:
            raise SystemExit(f"Unsupported registry data in {path.name}: {line[:80]}")
        value_type, typed = parsed
        actions.append({
            "type": "RegistrySet",
            "hive": hive,
            "path": sub,
            "name": name,
            "valueType": value_type,
            "value": typed,
        })

    return actions


def parse_data(data: str):
    if data.startswith('"') and data.endswith('"'):
        text = data[1:-1].replace('\\"', '"').replace("\\\\", "\\")
        return "String", text
    dword = re.match(r"^dword:([0-9a-fA-F]{1,8})$", data)
    if dword:
        return "DWord", int(dword.group(1), 16)
    qword = re.match(r"^qword:([0-9a-fA-F]{1,16})$", data)
    if qword:
        return "QWord", int(qword.group(1), 16)
    hexa = re.match(r"^hex(?:\((?P<kind>[0-9a-fA-F]+)\))?:(?P<body>[0-9a-fA-F,]+)$", data)
    if hexa:
        kind = (hexa.group("kind") or "3").lower()
        body = bytes(int(part, 16) for part in hexa.group("body").split(","))
        if kind == "2":
            return "ExpandString", body.decode("utf-16-le").rstrip("\x00")
        if kind == "7":
            parts = body.decode("utf-16-le").split("\x00")
            return "MultiString", [part for part in parts if part]
        if kind == "b":
            return "QWord", int.from_bytes(body, "little")
        return "Binary", body.hex()
    return None


def humanize(feature_id: str) -> str:
    spaced = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", feature_id)
    return spaced.replace("_", " ").strip()


def detect_for(actions: list[dict]) -> list[dict]:
    probes: list[dict] = []
    for action in actions:
        if action["type"] == "RegistrySet":
            probes.append({
                "type": "RegistryEquals",
                "hive": action["hive"],
                "path": action["path"],
                "name": action["name"],
                "valueType": action["valueType"],
                "value": action["value"],
            })
        elif action["type"] == "RegistryDelete":
            probes.append({
                "type": "RegistryAbsent",
                "hive": action["hive"],
                "path": action["path"],
                "name": action["name"],
            })
        elif action["type"] == "OptionalFeature":
            probes.append({
                "type": "OptionalFeature",
                "name": action["name"],
                "expectEnabled": action["enable"],
            })
        elif action["type"] == "AppxRemove":
            probes.append({"type": "AppxAbsent", "packageId": action["packageId"]})
        elif action["type"] == "RegistryKeyDelete":
            probes.append({"type": "RegistryKeyAbsent", "hive": action["hive"], "path": action["path"]})
    return probes


def main() -> None:
    features = json.loads(read_text(REF / "Config" / "Features.json"))["Features"]
    apps_doc = json.loads(read_text(REF / "Config" / "Apps.json"))
    categories = json.loads(read_text(REF / "Config" / "Features.json"))["Categories"]

    tweaks = []
    map_rows = []
    excluded_features = [
        {"featureId": key, "reason": reason} for key, reason in EXCLUDED.items()
    ]

    for feature in features:
        fid = feature["FeatureId"]
        if fid in EXCLUDED:
            map_rows.append((fid, "-", "intentionally-excluded", EXCLUDED[fid]))
            continue

        if fid in MANUAL:
            manual = MANUAL[fid]
            actions = manual["actions"]
            revert = manual["revert"]
            detect = manual["detect"]
            ref_file = manual["referenceFile"]
            risk = manual["risk"]
            revert_note = manual.get("revertNote", "")
        else:
            reg_name = feature.get("RegistryKey")
            if not reg_name:
                reason = "No declarative action mapped for D1."
                map_rows.append((fid, "-", "intentionally-excluded", reason))
                excluded_features.append({"featureId": fid, "reason": reason})
                continue
            reg_path = REF / "Regfiles" / reg_name
            if "HKEY_CLASSES_ROOT" in read_text(reg_path):
                reason = "Uses HKEY_CLASSES_ROOT, outside D1's hive allow-list (HKCU/HKLM/HKU\\Default)."
                map_rows.append((fid, "-", "intentionally-excluded", reason))
                excluded_features.append({"featureId": fid, "reason": reason})
                continue
            actions = parse_reg(reg_path)
            if any(a.get("hive") == "HKU" and not a["path"].startswith("Default") for a in actions):
                reason = "Writes under a service SID hive; D1 only allows HKU\\Default."
                map_rows.append((fid, "-", "intentionally-excluded", reason))
                excluded_features.append({"featureId": fid, "reason": reason})
                continue
            undo_name = feature.get("RegistryUndoKey")
            if undo_name:
                undo_path = REF / "Regfiles" / "Undo" / undo_name
                if not undo_path.exists():
                    undo_path = REF / "Regfiles" / undo_name
                revert = parse_reg(undo_path)
                revert_note = ""
            else:
                revert = [
                    {"type": "RegistryDelete", "hive": a["hive"], "path": a["path"], "name": a["name"]}
                    for a in actions if a["type"] == "RegistrySet"
                ]
                revert_note = "No reference undo file; D2's journal restores the exact previous values."
            detect = detect_for(actions)
            ref_file = f"Regfiles/{reg_name}"
            risk = ""
            revert_note = revert_note

        category = feature.get("Category") or "Other"
        risk = RISK_OVERRIDES.get(fid) or risk or (
            "Low" if category in LOW_CATEGORIES else "High" if category in HIGH_CATEGORIES else "Medium"
        )

        min_version = feature.get("MinVersion")
        max_version = feature.get("MaxVersion")
        os_gate = {}
        if min_version:
            bucket = "win11" if int(min_version) >= 22000 else "win10"
            os_gate[bucket] = {"minBuild": int(min_version)}
        if max_version:
            bucket = "win11" if int(max_version) >= 22000 else "win10"
            os_gate.setdefault(bucket, {})["maxBuild"] = int(max_version)

        if not revert and not revert_note:
            revert_note = "The reference has no undo file for this key deletion; the D2 journal restores it."

        shell = category in LOW_CATEGORIES or fid in {"DisableWidgets"}
        tweaks.append({
            "id": f"win11debloat.{fid}",
            "category": category,
            "title": humanize(fid),
            "description": f"Ported from Win11Debloat {ref_file}. Reversible; the engine snapshots the previous state.",
            "risk": risk,
            "os": os_gate,
            "requiresReboot": bool(feature.get("RequiresReboot")),
            "requiresExplorerRestart": shell,
            "appliesTo": "AllUsers" if any(a.get("hive") == "HKLM" for a in actions) else "CurrentUser",
            "actions": actions,
            "revert": revert,
            "revertNote": revert_note,
            "detect": detect,
            "tags": sorted({category.lower().replace(" ", "-"), "win11debloat"}),
            "referenceFile": ref_file,
        })
        map_rows.append((fid, f"win11debloat.{fid}", "ported", ref_file))

    app_rows = []
    excluded_apps = []
    for app in apps_doc["Apps"]:
        ids = app["AppId"] if isinstance(app["AppId"], list) else [app["AppId"]]
        if any(is_protected(package) for package in ids):
            excluded_apps.append({
                "id": ids[0],
                "reason": "Protected by FPM's safety contract (Windows core/Store); never offered.",
            })
            continue
        app_rows.append({
            "id": ids[0],
            "packageIds": ids,
            "recommendation": app.get("Recommendation", "optional"),
            "selectedByDefault": bool(app.get("SelectedByDefault")),
            "removalMethod": app.get("RemovalMethod", "Appx"),
            "title": ids[0].split(".")[-1],
            "referenceFile": "Config/Apps.json",
        })

    catalog = {
        "version": "1.0",
        "source": "Win11Debloat (MIT) - https://github.com/Raphire/Win11Debloat",
        "generatedBy": "tools/debloat_catalog_gen.py",
        "categories": [{"id": c["CategoryId"], "name": c["Name"]} for c in categories],
        "tweaks": tweaks,
        "apps": app_rows,
        "appPresets": [
            {"name": preset["Name"], "appIds": preset["AppIds"]}
            for preset in apps_doc.get("Presets", [])
        ],
        "excludedFeatures": excluded_features,
        "excludedApps": excluded_apps,
    }

    OUT_CATALOG.parent.mkdir(parents=True, exist_ok=True)
    OUT_CATALOG.write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")

    OUT_MAP.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Win11Debloat → FPM feature map (D1)",
        "",
        "Generated by `tools/debloat_catalog_gen.py` from the read-only reference at",
        "`reference/win11debloat` (MIT). Regenerate after updating the reference.",
        "",
        f"Reference features: **{len(features)}** · ported tweaks: **{len(tweaks)}** ·",
        f"excluded features: **{len(EXCLUDED)}** · apps: **{len(app_rows)}** ·",
        f"protected apps dropped: **{len(excluded_apps)}**.",
        "",
        "| Reference feature | FPM tweak id | Status | Source / reason |",
        "|---|---|---|---|",
    ]
    for fid, tweak_id, status, source in map_rows:
        lines.append(f"| `{fid}` | {tweak_id if tweak_id != '-' else '—'} | {status} | `{source}` |")
    lines += [
        "",
        "## Notes",
        "",
        "- Action types in this catalog: `RegistrySet`, `RegistryDelete`, `RegistryKeyDelete`,",
        "  `OptionalFeature`, `AppxRemove`; detect probes: `RegistryEquals`, `RegistryAbsent`,",
        "  `OptionalFeature`, `AppxAbsent`.",
        "- `revert` is the reference undo file where one exists; otherwise the engine's journal",
        "  restores the exact previous value (including “did not exist”).",
        "- App removal undo is reinstall guidance (Store/`winget`), per card D2.",
        "- No PowerShell from the reference is executed and no reference file is redistributed.",
        "",
    ]
    OUT_MAP.write_text("\n".join(lines), encoding="utf-8")

    print(f"wrote {OUT_CATALOG.relative_to(ROOT)}: {len(tweaks)} tweaks, {len(app_rows)} apps")
    print(f"wrote {OUT_MAP.relative_to(ROOT)}: {len(map_rows)} feature rows")


if __name__ == "__main__":
    main()
