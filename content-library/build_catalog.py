"""Build a private, reviewable desktop content package from the handoff files."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
ALIASES = {
    "masonry_mortar_grade_internal": "mortar_grade_internal",
    "masonry_mortar_grade_external": "mortar_grade_external",
    "below_grade_wall_mortar_grade": "mortar_grade_below_ground",
    "masonry_mortar_grade_underground": "mortar_grade_below_ground",
    "roof_coating_thickness": "roof_pu_coating_thickness",
    "geotechnical_investigation_unit": "geotechnical_survey_institute",
    "geotechnical_report_no": "geotechnical_survey_no",
    "pool_calc_software": "pool_structure_calculation_software",
    "structural_calc_software": "structural_calculation_software",
    "concrete_grade_expansion_band": "concrete_strength_expansion_strip",
}
DISCIPLINES = {
    "结构": "structural",
    "建筑": "architecture",
    "工艺": "process",
    "电气": "electrical",
    "暖通": "hvac",
}
PLACEHOLDER = re.compile(r"\{([a-z][a-z0-9_]*)\}")
PROJECT_DOCUMENT_TITLE = re.compile(r"《[^》]*(?:项目|工程)[^》]*》")
SUSPECT_REFS = {"GB3098", "GB/T501410", "GB/T 501410", "GB50666-2011", "03S402"}
REFERENCE_CORRECTIONS = {
    "C0094": ("GB3098", "GB30982-2014", "https://std.samr.gov.cn/gb/search/gbDetailed?id=71F772D7F40ED3A7E05397BE0A0AB82A"),
    "C1217": ("GB 3098", "GB 30982-2014", "https://std.samr.gov.cn/gb/search/gbDetailed?id=71F772D7F40ED3A7E05397BE0A0AB82A"),
    "C0131": ("GB50666-2011", "GB 50204-2015", "https://www.ndls.org.cn/standard/detail/415d5bc42cbf491da12139d8f9b4760f"),
    "C0805": ("03S402", "25S402", "https://ebook.chinabuilding.com.cn/zbooklib/book/detail/show?bookID=155808&SiteID=1"),
    "C0908": ("GB/T 501410", "GB/T 51410-2020", "https://ebook.chinabuilding.com.cn/zbooklib/book/detail/show?SiteID=1&bookID=126493"),
}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def read_chapters(path: Path) -> list[dict[str, str]]:
    chapters: list[dict[str, str]] = []
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        match = re.match(r"\| (CH\d{2}) \| ([^|]+) \|", line)
        if match:
            chapters.append({"id": match.group(1), "title": match.group(2).strip()})
    if len(chapters) != 25 or len({x["id"] for x in chapters}) != 25:
        raise ValueError("coverage.md 必须提供 25 个唯一章节 ID")
    return chapters


def digest(path: Path) -> str:
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def canonical(field_id: str) -> str:
    return ALIASES.get(field_id, field_id)


def normalize_template(text: str) -> tuple[str, list[str]]:
    changed: set[str] = set()

    def replace(match: re.Match[str]) -> str:
        field_id = match.group(1)
        replacement = canonical(field_id)
        if replacement != field_id:
            changed.add(field_id)
        return "{" + replacement + "}"

    return PLACEHOLDER.sub(replace, text), sorted(changed)


def has_unparameterized_project_document(text: str, text_type: str) -> bool:
    if text_type != "单项目事实":
        return False
    return any(len(match.group(0)) > 25 and "{project_name}" not in match.group(0) for match in PROJECT_DOCUMENT_TITLE.finditer(text))


def build(input_dir: Path, output_dir: Path, sources_dir: Path) -> dict:
    source_rows = read_csv(input_dir / "sources.csv")
    field_rows = read_csv(input_dir / "project_fields.csv")
    chapters = read_chapters(input_dir / "coverage.md")
    source_ids: dict[str, str] = {}
    source_digest: list[dict] = []
    failures: list[str] = []
    extracted: dict[str, set[int]] = {}
    for index, row in enumerate(source_rows, 1):
        name = row["文件名"].strip()
        source_id = f"S{index:03d}"
        if name in source_ids:
            failures.append(f"重复来源文件名：{name}")
        source_ids[name] = source_id
        extraction = input_dir / "_work" / "extracted" / (Path(name).stem + ".json")
        if extraction.is_file():
            extracted[name] = {item["para"] for item in json.loads(extraction.read_text(encoding="utf-8-sig"))}
        path = sources_dir / name
        if not path.is_file():
            failures.append(f"来源文件缺失：{name}")
            sha = None
        else:
            sha = digest(path)
            if path.stat().st_size != int(row["文件大小(字节)"]):
                failures.append(f"来源文件大小与清单不符：{name}")
        source_digest.append({
            "id": source_id,
            "file": name,
            "sha256": sha,
            "discipline": row["专业"],
            "versionRelation": row["版本关系"],
            "rightsStatus": "待核定",
        })

    fields_by_id: dict[str, dict] = {}
    field_aliases: defaultdict[str, set[str]] = defaultdict(set)
    for row in field_rows:
        raw_id = row["field_id"].strip()
        field_id = canonical(raw_id)
        field_aliases[field_id].add(raw_id)
        label = row["字段名称"].strip()
        unit = row["单位"].strip()
        if field_id not in fields_by_id or raw_id == field_id:
            fields_by_id[field_id] = {
                "id": field_id,
                "label": label,
                "unit": "" if unit == "-" else unit,
                "scope": "project_or_note",
                "reviewStatus": "pending",
            }
    for field_id, aliases in field_aliases.items():
        fields_by_id[field_id]["aliases"] = sorted(aliases - {field_id})

    clauses: list[dict] = []
    flagged: defaultdict[str, list[str]] = defaultdict(list)
    corrections: list[dict] = []
    seen_ids: set[str] = set()
    raw_refs: set[str] = set()
    for line_number, line in enumerate((input_dir / "clauses.jsonl").read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line.strip():
            continue
        raw = json.loads(line)
        raw_refs.update(raw["refs"])
        clause_id = raw["clause_id"]
        if clause_id in seen_ids:
            failures.append(f"重复条款 ID：{clause_id}")
        seen_ids.add(clause_id)
        if raw["chapter_id"] not in {x["id"] for x in chapters}:
            failures.append(f"{clause_id} 章节不存在：{raw['chapter_id']}")
        original = raw["text"]
        corrected_text = original
        refs = list(raw["refs"])
        correction = REFERENCE_CORRECTIONS.get(clause_id)
        if correction:
            old, new, evidence = correction
            if old not in corrected_text:
                failures.append(f"{clause_id} 预期修正的原文字段未找到：{old}")
            corrected_text = corrected_text.replace(old, new)
            refs = [new.replace(" ", "") if re.sub(r"\s+", "", ref) == re.sub(r"\s+", "", old) else ref for ref in refs]
            corrections.append({"clauseId": clause_id, "from": old, "to": new, "evidence": evidence})
        template, aliases = normalize_template(corrected_text)
        field_ids = sorted(set(PLACEHOLDER.findall(template)))
        for field_id in field_ids:
            if field_id not in fields_by_id:
                failures.append(f"{clause_id} 未定义字段：{field_id}")
        declared = {canonical(value) for value in raw["placeholders"]}
        if declared != set(field_ids):
            failures.append(f"{clause_id} 占位符清单与文字不一致")
        codes = sorted({DISCIPLINES[name] for name in raw["disciplines"]})
        sources = []
        for source in raw["sources"]:
            if source["file"] not in source_ids:
                failures.append(f"{clause_id} 未登记来源：{source['file']}")
                continue
            if source["file"] in extracted and source["para"] not in extracted[source["file"]]:
                failures.append(f"{clause_id} 来源段落不存在：{source['file']}:{source['para']}")
            sources.append({"sourceId": source_ids[source["file"]], "para": source["para"]})
        if not sources:
            failures.append(f"{clause_id} 无可用来源")
        flags: list[str] = []
        if refs or raw["text_type"] == "待核定规范引用":
            flags.append("reference")
        if raw["text_type"] == "条件文字":
            flags.append("condition")
        if raw["table"] is not None:
            flags.append("table")
        if raw["note"]:
            flags.append("source_note")
        project_literal = has_unparameterized_project_document(template, raw["text_type"])
        if project_literal:
            flags.append("possible_project_literal")
        if aliases:
            flags.append("field_alias_normalized")
        if correction:
            flags.append("corrected_reference")
        if any(re.sub(r"\s+", "", value).upper() in {re.sub(r"\s+", "", x).upper() for x in SUSPECT_REFS} for value in refs):
            flags.append("suspect_reference")
        for flag in flags:
            flagged[flag].append(clause_id)
        clauses.append({
            "id": clause_id,
            "chapterId": raw["chapter_id"],
            "disciplines": codes,
            "kind": raw["text_type"],
            "template": template,
            "originalTemplate": original if aliases or correction else None,
            "fieldIds": field_ids,
            "refs": refs,
            "table": raw["table"],
            "sources": sources,
            "note": (raw["note"] + (f"；核对修正：{correction[0]}→{correction[1]}（{correction[2]}）" if correction else "")).strip("；"),
            "reviewStatus": "pending",
            "flags": flags,
            "usableAsText": raw["table"] is None and not project_literal and "suspect_reference" not in flags,
        })
    if failures:
        raise ValueError("内容包校验失败：\n" + "\n".join(failures[:50]))

    version_material = json.dumps({"chapters": chapters, "fields": sorted(fields_by_id.values(), key=lambda value: value["id"]), "clauses": clauses, "sourceDigest": source_digest}, ensure_ascii=False, sort_keys=True).encode("utf-8")
    package_id = "legacy-handoff-2026-09-27-" + hashlib.sha256(version_material).hexdigest()[:12]
    package = {
        "schemaVersion": 1,
        "packageId": package_id,
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "title": "旧工程设计说明候选资料",
        "reviewStatus": "pending",
        "chapters": chapters,
        "fields": sorted(fields_by_id.values(), key=lambda value: value["id"]),
        "clauses": clauses,
        "sourceDigest": source_digest,
    }
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / "catalog.json").write_text(json.dumps(package, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    audit = {
        "sourceFiles": len(source_rows),
        "sourceFilesVerified": sum(bool(item["sha256"]) for item in source_digest),
        "candidateClauses": len(clauses),
        "packageId": package_id,
        "originalFields": len(field_rows),
        "canonicalFields": len(fields_by_id),
        "chapters": len(chapters),
        "rawDistinctReferences": len(raw_refs),
        "normalizedDistinctReferences": len({ref for clause in clauses for ref in clause["refs"]}),
        "disabledTextClauses": sum(not clause["usableAsText"] for clause in clauses),
        "disciplineCounts": dict(Counter(code for clause in clauses for code in clause["disciplines"])),
        "flags": {key: {"count": len(ids), "clauseIds": ids} for key, ids in sorted(flagged.items())},
        "aliases": {key: sorted(values - {key}) for key, values in sorted(field_aliases.items()) if values - {key}},
        "referenceCorrections": corrections,
        "zeroValueDefaults": True,
    }
    (output_dir / "audit.json").write_text(json.dumps(audit, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return audit


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, default=ROOT / "artifacts/content-handoff")
    parser.add_argument("--output", type=Path, default=ROOT / "content-library/private")
    parser.add_argument("--sources-dir", type=Path, default=ROOT / "说明文件")
    args = parser.parse_args()
    audit = build(args.input, args.output, args.sources_dir)
    print(json.dumps({key: value for key, value in audit.items() if key != "flags"}, ensure_ascii=False))
    print("flags:", {key: value["count"] for key, value in audit["flags"].items()})


if __name__ == "__main__":
    main()
