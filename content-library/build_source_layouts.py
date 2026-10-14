"""Build private source-ordered layouts without dropping fixed source text.

All source text, captured old values and comparison evidence stay under private/.
Only replace auditable spans; putting the original values back must reconstruct
each paragraph/cell exactly. No engineering/standards approval is inferred.
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

PROFILES = {
    "tpl-struct-pool": "结构设计说明（构筑物）.docx",
    "tpl-struct-frame": "结构设计说明（框架）.docx",
    "tpl-struct-pool-frame": "结构设计说明（构筑物加框架）.docx",
    "tpl-arch-standard": "建筑设计说明厂房.docx",
    "tpl-plumb-standard": "工艺设计说明总图.docx",
    "tpl-elec-standard": "电气设计说明总图.docx",
    "tpl-hvac-standard": "暖通设计说明.docx",
}
FIELD = re.compile(r"\{([a-z][a-z0-9_]*)\}")
ADJACENT = re.compile(r"(?:\{[a-z][a-z0-9_]*\}){2,}")
CHAPTER = re.compile(r"^[一二三四五六七八九十百]+、")
NUMBERING = re.compile(r"^\s*(?:\d+[、.)）]|[（(]\d+[)）])\s*")
FIXED_FIELDS = {'main_rebar_grades','embedded_part_steel_grade','welding_electrode_grades','rebar_grade_hpb','rebar_grade_hrb','electrode_grade_hpb','electrode_grade_hrb'}
OLD_NAMES = re.compile(r"塔城地区乌苏市|济南起步区|济南新旧动能转换起步区|济南市|尉犁县|腊山河|兴济河|乌苏市|XXXX|山东省|新疆(?:维吾尔自治区)?")

def units(path: Path):
    doc = Document(path)
    for index, node in enumerate(doc.element.body):
        if node.tag == qn("w:p"):
            yield index, "paragraph", Paragraph(node, doc).text.strip(), None
        elif node.tag == qn("w:tbl"):
            table = Table(node, doc)
            if node.xpath('.//w:gridSpan|.//w:vMerge|.//w:tbl'):
                raise ValueError(f"{path.name} 第 {index} 处存在尚未处理的合并/嵌套表格，不能扁平化")
            rows = [[cell.text.strip() for cell in row.cells] for row in table.rows]
            widths = [float(column.width or 0) for column in table.columns]
            if not widths or any(w <= 0 for w in widths): widths = [1.] * len(rows[0])
            yield index, "table", rows, widths

def normalized(text):
    chars, positions = [], []
    for i, ch in enumerate(text):
        if not ch.isspace(): chars.append(ch); positions.append(i)
    return "".join(chars), positions

def build(catalog_file: Path, source_dir: Path, output_dir: Path):
    catalog = json.loads(catalog_file.read_text(encoding="utf-8"))
    definitions = {f['id']: f for f in catalog['fields']}
    layouts, reports = [], []
    for template_id, filename in PROFILES.items():
        source = next(s for s in catalog['sourceDigest'] if s['file'] == filename)
        path = source_dir / filename
        if hashlib.sha256(path.read_bytes()).hexdigest() != source['sha256']:
            raise ValueError(f"来源摘要不一致：{filename}")
        related = defaultdict(list)
        for clause in catalog['clauses']:
            for ref in clause['sources']:
                if ref['sourceId'] == source['id']: related[ref['para']].append(clause)
        local_fields, evidence = {}, []
        def local(label, index, suffix):
            key = f"src_{source['id'].lower()}_p{index}_{suffix}"
            local_fields[key] = {"id": key, "label": label, "unit": "", "scope": "note", "reviewStatus": "pending", "aliases": []}
            return key

        def parameterize(raw, index, candidates, suffix="p", force_value_label=None):
            spans = []
            norm, positions = normalized(raw)
            def add(a, b, field):
                if field in FIXED_FIELDS: return False
                # Units stay in fixed text; step 01 accepts the numeric value.
                unit = definitions.get(field, {}).get('unit', '')
                value = raw[a:b].strip()
                if unit and re.fullmatch(r'[≤≥]?\s*[0-9.]+\s*'+re.escape(unit), value):
                    b = a + raw[a:b].rfind(unit)
                    while b > a and raw[b-1].isspace(): b -= 1
                if a >= b or any(a < y and b > x for x, y, _ in spans): return False
                spans.append((a, b, field)); return True
            if force_value_label and raw:
                add(0, len(raw), local(force_value_label, index, suffix))
            else:
                # Split the old combined project/name and elevation fields explicitly.
                if template_id.startswith('tpl-struct'):
                    name = re.search(r'本工程为(.+?)\s+(\S+)\s*分项', raw)
                    if name:
                        add(name.start(1), name.end(1), 'project_name')
                        add(name.start(2), name.end(2), 'subproject_name')
                    elevation = re.search(r'相当于\s*(.+?高程基准)\s+([^，。；]+?)m(?=[。；，]|$)', raw)
                    if elevation:
                        add(elevation.start(1), elevation.end(1), 'elevation_datum')
                        add(elevation.start(2), elevation.end(2), 'elevation_zero')
                    # Keep independent facts separate instead of swallowing a seismic conclusion.
                    soil = re.search(r'场地土对建筑材料具([^；。]+)', raw)
                    if soil: add(soil.start(1), soil.end(1), 'soil_corrosiveness')
                    coating = re.search(r'地面以下构筑物外表面(涂刷[^；。]+)', raw)
                    if coating: add(coating.start(1), coating.end(1), 'external_anticorrosion_coating')
                    if raw.startswith('混凝土：'):
                        patterns = [
                            (r'基础垫层\s*([^；]+)', 'concrete_grade_pad'),
                            (r'(C\d+)\s*[，,]水胶比', 'concrete_grade_main'),
                            (r'水胶比不大于\s*([0-9.]+)', 'concrete_water_binder_ratio'),
                            (r'水溶性氯离子最大含量\s*([0-9.]+)', 'concrete_chloride_max_content'),
                            (r'最大碱含量\s*([0-9.]+)', 'concrete_max_alkali_content'),
                            (r'抗硫酸盐等级\s*(KS\d+)', 'concrete_sulfate_resistance_grade'),
                            (r'56d 电通量（C）\s*(≤[0-9]+)', 'concrete_56d_coulomb'),
                            (r'膨胀加强带\s*(C\d+)', 'concrete_strength_expansion_strip'),
                        ]
                        for pattern, field in patterns:
                            match = re.search(pattern, raw)
                            if match: add(match.start(1), match.end(1), field)
                for clause in sorted(candidates, key=lambda c: len(c['template']), reverse=True):
                    text = clause['template']
                    # Adjacent fields cannot be separated reliably from an old value.
                    for match in list(ADJACENT.finditer(text)):
                        ids = FIELD.findall(match.group())
                        key = local(' / '.join(definitions.get(f, {'label':f})['label'] for f in ids), index, f'{suffix}_combined_{hashlib.sha1(chr(124).join(ids).encode()).hexdigest()[:8]}')
                        text = text.replace(match.group(), '{'+key+'}')
                    text, _ = normalized(text)
                    ids = FIELD.findall(text)
                    if not ids: continue
                    pieces = FIELD.split(text)
                    if sum(len(pieces[i]) for i in range(0, len(pieces), 2)) < 4: continue
                    pattern = ''.join('(.*?)' if i % 2 else re.escape(piece) for i, piece in enumerate(pieces))
                    matches = list(re.finditer(pattern, norm))
                    if len(matches) != 1: continue
                    match = matches[0]
                    for n, field in enumerate(ids, 1):
                        a, b = match.span(n)
                        if a < b: add(positions[a], positions[b-1]+1, field)
                # A specific fact with no reusable variables is itself an editable
                # project statement, not a reason to discard surrounding paragraphs.
                for n, clause in enumerate(candidates):
                    if clause['kind'] != '单项目事实' or clause['fieldIds'] or '建设单位提供的设计任务书' in clause['template']: continue
                    text, _ = normalized(clause['template'])
                    a = norm.find(text)
                    if text and a >= 0:
                        add(positions[a], positions[a+len(text)-1]+1, local('本工程资料 '+raw[:22], index, f'{suffix}_fact{n}'))
                # Values embedded in site descriptions often do not match a whole
                # catalog sentence. Parameterize their exact spans independently.
                location = re.search(r'^(.+?)位于([^。]+)', raw)
                if location and re.search(r'本工程|本项目|工程|项目|厂区', location[1]) and not NUMBERING.match(location[1]):
                    add(location.start(1), location.end(1), 'project_name')
                    add(location.start(2), location.end(2), 'project_location')
                rules = [
                    (r'占地面积(?:约|为)?([^，。；]+)', 'site_area', '占地面积'),
                    (r'建设规模([^，。；]+)', 'treatment_scale_with_unit', '建设规模及单位'),
                    (r'变化系数([^，。；]+)', 'variation_coefficient', '变化系数'),
                    (r'建筑高度为([^（，。；]+)', 'building_height', '建筑高度'),
                    (r'(未见地下水)', 'groundwater_observation', '本工程地下水勘察情况'),
                    (r'(可不考虑地下水对建筑物的影响)', 'groundwater_effect', '本工程地下水影响结论'),
                ]
                for pattern, field_suffix, label in rules:
                    for match in re.finditer(pattern, raw):
                        add(match.start(1), match.end(1), local(label, index, f'{suffix}_{field_suffix}'))
                for n, match in enumerate(OLD_NAMES.finditer(raw)):
                    add(match.start(), match.end(), local('本工程对应名称或地区', index, f'{suffix}_name{n}'))
            text = raw
            values = {}
            for a, b, field in sorted(spans, reverse=True):
                # Repeated original values can differ even when the catalog shares
                # a field ID. Keep each occurrence independently reversible.
                if field in values and values[field] != raw[a:b]: field = local(definitions.get(field, {'label': field})['label'], index, f'{suffix}_at{a}')
                values[field] = raw[a:b]
                text = text[:a] + '{'+field+'}' + text[b:]
            reconstructed = FIELD.sub(lambda m: values[m[1]], text)
            if reconstructed != raw: raise ValueError(f"固定正文校验失败：{filename}:{index}:{suffix}")
            evidence.append({"sourcePara": index, "position": suffix, "original": raw, "template": text, "oldValues": values, "fixedTextExact": True})
            return text

        sections, title, table_count, para_count = [], "", 0, 0
        for index, kind, value, widths in units(path):
            if kind == 'paragraph':
                if not value: continue
                if not title:
                    title = value
                    continue
                heading = bool(CHAPTER.match(value)) or (template_id == 'tpl-hvac-standard' and bool(re.match(r'^\d+、.{1,24}$', value)))
                if heading:
                    sections.append({"id": f"{source['id'].lower()}-{len(sections)+1:02d}", "title": value, "sourcePara": index, "blocks": []})
                    continue
                if not sections: sections.append({"id": f"{source['id'].lower()}-intro", "title": "说明正文", "sourcePara": index, "blocks": []})
                candidates = related[index]
                text = parameterize(value, index, candidates)
                # Preserve all conditional content, but require project review for
                # old engineering conclusions that cannot be resolved by filling values.
                sensitive = bool(re.search(r'未见地下水|可不考虑地下水|近场效应|本图纸为变更|本工程.*(?:位于|采用|为)|本项目', value))
                review = '条件：请核对本工程事实与适用性' if sensitive or any(c['kind']=='条件文字' for c in candidates) else '来源正文；规范版本由设计人员统一核对'
                sections[-1]['blocks'].append({"kind":"paragraph", "sourcePara": index, "template":text, "clauseIds":[c['id'] for c in candidates], "fieldIds":list(dict.fromkeys(FIELD.findall(text))), "reviewNote":review})
                para_count += 1
            else:
                if not sections: raise ValueError('标题之前存在表格，需显式处理')
                rows = value
                headers = rows[0]
                project_table = any('活荷载' in h or h == '工程项目' or '设计选材' in h or '工程防水' in h for h in headers) or template_id in ('tpl-hvac-standard','tpl-plumb-standard','tpl-elec-standard')
                updated = []
                for r, row in enumerate(rows):
                    cells = []
                    for c, cell in enumerate(row):
                        # Keep headings, row labels, units and explanatory notes. Only
                        # project value cells become fields; normative tables stay whole.
                        editable = project_table and r > 0 and c > 0 and cell.strip() and headers[c] not in ('备注','项次','工程构件','适用部位')
                        label = f"{row[0]} / {headers[c]}" if editable else None
                        cells.append(parameterize(cell, index, [], f'r{r}c{c}', label))
                    updated.append(cells)
                sections[-1]['blocks'].append({"kind":"table", "sourcePara":index, "rows":updated, "columnWidths":widths, "reviewNote":"来源表格；填写工程取值后核对指标与适用性"})
                table_count += 1
        used = set(FIELD.findall(json.dumps(sections, ensure_ascii=False)))
        all_fields = {**definitions, **local_fields,
            'subproject_name': {'id':'subproject_name','label':'分项 / 单体名称','unit':'','scope':'project','reviewStatus':'pending','aliases':[]},
            'elevation_zero': {'id':'elevation_zero','label':'±0.000 对应高程','unit':'m','scope':'project','reviewStatus':'pending','aliases':[]},
            'external_anticorrosion_coating': {'id':'external_anticorrosion_coating','label':'地下外表面防护做法','unit':'','scope':'note','reviewStatus':'pending','aliases':[]},
        }
        missing = used-all_fields.keys()
        if missing: raise ValueError(f'字段未定义：{missing}')
        reference_values = {}
        for position in evidence:
            for key, value in position['oldValues'].items():
                reference_values.setdefault(key, value)
        layout = {"referenceValues": reference_values, "schemaVersion":1,"templateId":template_id,"sourceId":source['id'],"sourceFile":filename,"sourceTitle":title,"sourceHash":source['sha256'],"fields":[all_fields[k] for k in sorted(used)],"sections":sections}
        layouts.append(layout)
        reports.append({"templateId":template_id,"sourceFile":filename,"chapters":len(sections),"paragraphs":para_count,"tables":table_count,"fields":len(used),"fixedTextExact":True,"positions":evidence})
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir/'source-layouts.json').write_text(json.dumps(layouts, ensure_ascii=False, indent=2),encoding='utf-8')
    (output_dir/'source-fidelity-audit.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
    return [{k:v for k,v in r.items() if k!='positions'} for r in reports]

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--catalog',type=Path,required=True)
    parser.add_argument('--sources',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    print(json.dumps(build(args.catalog,args.sources,args.output),ensure_ascii=False))
