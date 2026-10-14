"""给 Core Console 准备 TEXTBOX 批量测量脚本，或读取宿主返回的字形边界。

仅测试工具；不加载插件，也不修改生产字体/安全配置。
python scripts/capture-table-font-measurements.py prepare artifacts/t28-visual
python scripts/capture-table-font-measurements.py collect artifacts/t28-visual
"""
import json
import sys
from pathlib import Path


def main(mode, location):
    directory = Path(location).resolve()
    mapping = directory / 'host-measurements.json'
    existing = json.loads(mapping.read_text(encoding='utf-8')) if mapping.exists() else []
    if mode == 'prepare':
        requested = {}
        known = {e['Key'] for e in existing}
        for paper in ('A1', 'A2', 'A3'):
            for e in json.loads((directory / f'{paper}-measure-requests.json').read_text(encoding='utf-8-sig')):
                if e['Key'] not in known:
                    requested[e['Key']] = e
        entries = list(requested.values())
        (directory / 'host-request-batch.json').write_text(json.dumps(entries, ensure_ascii=False), encoding='utf-8')
        output = (directory / 'host-textbox.tsv').as_posix()
        lines = [f'(setq t28fd (open "{output}" "w"))']
        for i, e in enumerate(entries):
            text = e['Text'].replace('\\', '\\\\').replace('"', '\\"')
            lines.append('(setq t28bb (textbox (list (cons 0 "TEXT") (cons 7 "TSSD") '
                         f'(cons 1 "{text}") (cons 40 {e["Height"]}) (cons 41 {e["WidthFactor"]}))))')
            lines.append(f'(write-line (strcat "{i}" "|" (rtos (caar t28bb) 2 12) "|" (rtos (cadar t28bb) 2 12) '
                         '"|" (rtos (caadr t28bb) 2 12) "|" (rtos (cadadr t28bb) 2 12)) t28fd)')
        lines.extend(['(close t28fd)', '(princ "T28_TEXTBOX_CAPTURE_OK")', '_.QUIT', '_Y', ''])
        (directory / 'host-measure.scr').write_text('\n'.join(lines), encoding='gbk')
        print(f'HOST_MEASURE_REQUESTS {len(entries)}')
    else:
        entries = json.loads((directory / 'host-request-batch.json').read_text(encoding='utf-8'))
        results = (directory / 'host-textbox.tsv').read_text(encoding='gbk').splitlines()
        assert len(results) == len(entries), '宿主测量必须完整返回，不能用模拟值补齐'
        for line in results:
            i, x0, y0, x1, y1 = line.split('|')
            e = entries[int(i)]
            e['Ink'] = dict(MinX=float(x0), MinY=float(y0), MaxX=float(x1), MaxY=float(y1))
            existing.append(e)
        mapping.write_text(json.dumps(existing, ensure_ascii=False, indent=2), encoding='utf-8')
        print(f'HOST_MEASURE_CAPTURED {len(results)} total={len(existing)}')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
