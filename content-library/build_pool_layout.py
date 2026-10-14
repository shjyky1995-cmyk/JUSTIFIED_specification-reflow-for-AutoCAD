"""Build a private, source-ordered pool reference layout.

Requires python-docx. Source documents and generated JSON stay in the project's
ignored content-library/private directory; this script contains no business text.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path

from docx import Document
from docx.oxml.ns import qn
from docx.table import Table
from docx.text.paragraph import Paragraph


CHECKOUT = Path(__file__).resolve().parent.parent
ROOT = next((parent for parent in (CHECKOUT, *CHECKOUT.parents) if (parent / "说明文件").is_dir()), CHECKOUT)
SOURCE = "结构设计说明（构筑物）.docx"
CHAPTER = re.compile(r"^[一二三四五六七八九十]+、")
NUMBERING = re.compile(r"^\s*(?:\d+[、.)）]|[（(]\d+[)）]|[一二三四五六七八九十]+、)\s*")
PROJECT_LITERAL = re.compile(r"塔城|乌苏|新疆|济南|尉犁|腊山河|XXXX|本工程框架抗震等级|基本风压为|基本雪压为")
CONDITIONAL_PARAS = {6, 43, 49, 56, 74, 77, 111, 113, 165}


def document_units(path: Path) -> list[dict]:
    document = Document(path)
    units = []
    for child in document.element.body.iterchildren():
        if child.tag == qn("w:p"):
            units.append({"kind": "paragraph", "text": Paragraph(child, document).text.strip()})
        elif child.tag == qn("w:tbl"):
            table = Table(child, document)
            units.append({"kind": "table", "rows": [[cell.text.strip() for cell in row.cells] for row in table.rows]})
    return units


def choose_clauses(candidates: list[dict]) -> list[dict]:
    # The handoff sometimes retains both a literal and a parameterized variant.
    chosen = []
    for clause in candidates:
        template = clause["template"]
        if any(other["template"].startswith(template) and len(other["template"]) > len(template) for other in candidates if other is not clause):
            continue
        if not clause["fieldIds"] and any(other["fieldIds"] and template[:16] == other["template"][:16] for other in candidates if other is not clause):
            continue
        chosen.append(clause)
    return chosen


def paragraph_block(index: int, raw: str, candidates: list[dict]) -> dict:
    chosen = choose_clauses(candidates)
    usable = [item for item in chosen if item["usableAsText"] and not PROJECT_LITERAL.search(item["template"])]
    if usable:
        text = "".join(item["template"] for item in usable)
        prefix = NUMBERING.match(raw)
        if prefix and not NUMBERING.match(text):
            text = prefix.group(0) + text
        clause_ids = [item["id"] for item in usable]
        fields = sorted(set(field for item in usable for field in item["fieldIds"]))
        issue = "待核定：旧工程候选文字、规范版本及适用性"
    elif not PROJECT_LITERAL.search(raw) and raw:
        text = raw
        clause_ids = []
        fields = []
        issue = "待核定：原段落未形成可复用条款，须核对规范与工程适用性"
    else:
        text = f"【待核定：原稿第 {index} 段含特定工程事实，请改写为本工程资料】"
        clause_ids = []
        fields = []
        issue = "阻断：原工程事实未参数化"
    if index == 9:
        text = "8、根据本工程岩土工程勘察报告，地下水位及对结构的影响为{groundwater_level}。"
        fields = ["groundwater_level"]
        issue = "待核定：必须根据本工程岩土勘察报告填写，原工程地下水判断不沿用"
    elif index == 43:
        text = "地面以下构筑物外表面防腐做法为{anticorrosive_coating_thickness}环氧煤沥青防腐蚀涂层；场地土对建筑材料腐蚀性为{soil_corrosiveness}。近场效应应依据本工程地震安全性评价或地勘资料另行核定。"
        fields = ["anticorrosive_coating_thickness", "soil_corrosiveness"]
        issue = "条件：防腐做法与近场效应必须按本工程资料核对"
    elif index == 58:
        tail = next((item["template"].split("混凝土掺加外加剂后", 1)[1] for item in candidates if "混凝土掺加外加剂后" in item["template"]), "")
        text = "要求限制膨胀率{restricted_expansion_rate}，限制干缩率不大于{restricted_drying_shrinkage_rate}；抗裂防水剂、膨胀剂的牌号及具体用量根据试验确定；混凝土掺加外加剂后" + tail
        fields = ["restricted_expansion_rate", "restricted_drying_shrinkage_rate"]
        issue = "待核定：外加剂指标和引用标准版次"
    elif index == 64:
        text = raw.replace("机械连接接头等级二级", "机械连接接头等级{mechanical_splice_joint_grade}")
        fields = ["mechanical_splice_joint_grade"]
        issue = "待核定：连接方式与接头等级"
    if index in CONDITIONAL_PARAS and not issue.startswith("阻断"):
        issue = "条件：旧工程做法或框架内容，须确认适用于本工程"
    if PROJECT_LITERAL.search(text):
        raise ValueError(f"段落 {index} 残留旧工程信息")
    return {"kind": "paragraph", "sourcePara": index, "template": text, "clauseIds": clause_ids, "fieldIds": fields, "reviewNote": issue}


def table_block(index: int, rows: list[list[str]], ordinal: int) -> dict:
    copied = [list(row) for row in rows]
    if ordinal in (4, 5):
        for row in copied[1:]:
            for column in range(len(row)):
                row[column] = "【待核定：本工程取值】"
    for row in copied:
        for cell in row:
            if PROJECT_LITERAL.search(cell):
                raise ValueError(f"表格 {ordinal} 残留旧工程信息")
    review = "条件：框架构件代号表，仅在本工程包含相应框架构件时使用" if ordinal == 6 else "待核定：表格指标及适用性；不得直接视为现行规范"
    return {"kind": "table", "sourcePara": index, "rows": copied, "reviewNote": review}


def build(catalog_file: Path, source_file: Path, output_file: Path) -> dict:
    catalog = json.loads(catalog_file.read_text(encoding="utf-8"))
    source = next(item for item in catalog["sourceDigest"] if item["file"] == SOURCE)
    if hashlib.sha256(source_file.read_bytes()).hexdigest() != source["sha256"]:
        raise ValueError("原 DOCX 与候选资料包的来源摘要不一致，不能按段号拼接")
    by_para: dict[int, list[dict]] = defaultdict(list)
    for clause in catalog["clauses"]:
        for ref in clause["sources"]:
            if ref["sourceId"] == source["id"]:
                by_para[ref["para"]].append(clause)
    units = document_units(source_file)
    sections: list[dict] = []
    table_number = 0
    for index, unit in enumerate(units):
        if unit["kind"] == "paragraph" and CHAPTER.match(unit["text"]):
            sections.append({"id": f"pool-{len(sections) + 1:02d}", "title": unit["text"], "sourcePara": index, "blocks": []})
            continue
        if not sections or (unit["kind"] == "paragraph" and not unit["text"]):
            continue
        if unit["kind"] == "table":
            block = table_block(index, unit["rows"], table_number)
            table_number += 1
        else:
            block = paragraph_block(index, unit["text"], by_para[index])
        sections[-1]["blocks"].append(block)
    if len(sections) != 8 or table_number != 7:
        raise ValueError(f"原稿结构变化：{len(sections)} 章 / {table_number} 表")
    layout = {"schemaVersion": 1, "templateId": "tpl-struct-pool", "sourceId": source["id"], "sourceFile": SOURCE, "sections": sections}
    output_file.parent.mkdir(parents=True, exist_ok=True)
    output_file.write_text(json.dumps(layout, ensure_ascii=False, indent=2), encoding="utf-8")
    return {"chapters": len(sections), "paragraphs": sum(block["kind"] == "paragraph" for section in sections for block in section["blocks"]), "tables": table_number, "pending": sum("阻断" in block["reviewNote"] for section in sections for block in section["blocks"])}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", type=Path, default=ROOT / "content-library/private/catalog.json")
    parser.add_argument("--source", type=Path, default=ROOT / "说明文件" / SOURCE)
    parser.add_argument("--output", type=Path, default=ROOT / "content-library/private/pool-layout.json")
    args = parser.parse_args()
    print(json.dumps(build(args.catalog, args.source, args.output), ensure_ascii=False))
