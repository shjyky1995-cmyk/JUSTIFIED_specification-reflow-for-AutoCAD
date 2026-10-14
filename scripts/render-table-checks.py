"""将核心测试的排版结果绘成检查图，并输出可由 CAD 打开的 DXF。

字宽来自测试中的模拟测量；这些图片不代表 DSS、真实 SHX 或打印已通过。
运行：python scripts/render-table-checks.py artifacts/t28-visual
"""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def dxf_text(text):
    return ''.join(c if ord(c) < 128 else r'\U+' + format(ord(c), '04X') for c in text)


def render(directory, host_measured=False):
    directory = Path(directory)
    label = 'CAD 字体测量检查图（非 DSS 截图）' if host_measured else '模拟字宽检查图（非真实 CAD 截图）'
    summaries = []
    for paper in ('A1', 'A2', 'A3'):
        layout = json.loads((directory / (paper + '-layout.json')).read_text(encoding='utf-8-sig'))
        template = json.loads((directory / (paper + '-template.json')).read_text(encoding='utf-8-sig'))
        bounds = template['PageBounds']
        width, height = bounds['Right'] - bounds['Left'], bounds['Top'] - bounds['Bottom']
        columns = template['Columns']
        scale = 4
        font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 15)
        title_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
        dxf = ['0', 'SECTION', '2', 'HEADER', '9', '$ACADVER', '1', 'AC1009', '0', 'ENDSEC',
               '0', 'SECTION', '2', 'TABLES', '0', 'TABLE', '2', 'STYLE', '70', '1',
               '0', 'STYLE', '2', 'TSSD', '70', '0', '40', '0', '41', '0.75', '50', '0',
               '71', '0', '42', '4.5', '3', 'tssdeng.shx', '4', 'tssdchn.shx',
               '0', 'ENDTAB', '0', 'ENDSEC', '0', 'SECTION', '2', 'ENTITIES']
        counts = {'paper': paper, 'pages': len(layout['pages']), 'texts': 0, 'lines': 0,
                  'measurement': 'AutoCAD 2021 TEXTBOX captured; not DSS' if host_measured else 'simulated; DXF is layout inspection only'}

        def entity(kind, values):
            dxf.extend(['0', kind, '8', '0'])
            for key, value in values:
                dxf.extend([str(key), str(value)])

        for pi, page in enumerate(layout['pages']):
            canvas = Image.new('RGB', (round(width * scale), round(height * scale) + 68), 'white')
            draw = ImageDraw.Draw(canvas)
            draw.text((20, 12), f'{paper} 第{pi + 1}页 · {label}', font=title_font, fill='#374151')
            origin = page['pageOffset']['x']

            def xy(x, y):
                return (x * scale, -y * scale + 68)

            # 示意图框与图签；红色带是必须留空的区域。
            safe_bottom = min(c['Bottom'] for c in columns)
            draw.rectangle((0, -safe_bottom * scale + 68, width * scale, height * scale + 68), fill='#fff1f2')
            draw.rectangle((5 * scale, 5 * scale + 68, (width - 5) * scale, (height - 5) * scale + 68), outline='#9ca3af', width=2)
            draw.rectangle(((width - 180) * scale, (height - 25) * scale + 68, (width - 5) * scale, (height - 5) * scale + 68), outline='#9ca3af', width=2)
            draw.text((15 * scale, (height - 15) * scale + 68), '图框/图签仅为示意；红色区域不得落入说明', font=font, fill='#b91c1c')
            # DXF 也带上示意边框，便于在 CAD 中确认整体位置。
            for a, b in [((5, -5), (width - 5, -5)), ((5, -5), (5, -height + 5)),
                         ((5, -height + 5), (width - 5, -height + 5)), ((width - 5, -height + 5), (width - 5, -5)),
                         ((width - 180, -height + 25), (width - 5, -height + 25)),
                         ((width - 180, -height + 25), (width - 180, -height + 5))]:
                entity('LINE', [(10, origin + a[0]), (20, a[1]), (11, origin + b[0]), (21, b[1])])
            for column in page['columns']:
                geo = columns[column['columnIndex']]
                left = geo['Left']
                draw.rectangle((*xy(left, geo['Top']), *xy(geo['Right'], geo['Bottom'])), outline='#93c5fd', width=2)
                for edge in column.get('lines', []):
                    a, b = edge['start'], edge['end']
                    draw.line((*xy(left + a['x'], a['y']), *xy(left + b['x'], b['y'])), fill='#111827', width=2)
                    entity('LINE', [(10, origin + left + a['x']), (20, a['y']), (11, origin + left + b['x']), (21, b['y'])])
                    counts['lines'] += 1
                for row in column.get('tableTexts', []) + column['rows']:
                    visual = row.get('visualLine')
                    if not visual:
                        continue
                    for run in visual['renderRuns']:
                        style = run['resolvedStyle']
                        text = run['text']
                        x = left + run['relativeOrigin']['x']
                        y = row['baseline'] + run['baselineOffset']
                        ink = run['inkBounds']
                        # 按测试提供的实际占位宽高绘制文字，避免系统字体再次改变布局。
                        bbox = font.getbbox(text)
                        tile = Image.new('RGBA', (max(1, bbox[2] - bbox[0]), max(1, bbox[3] - bbox[1])), (255, 255, 255, 0))
                        ImageDraw.Draw(tile).text((-bbox[0], -bbox[1]), text, font=font, fill='#111827')
                        tile = tile.resize((max(1, round(run['measuredAdvance'] * scale)), max(1, round(style['textHeight'] * scale))), Image.Resampling.LANCZOS)
                        canvas.paste(tile, (round(x * scale), round(-(y + ink['maxY']) * scale + 68)), tile)
                        entity('TEXT', [(10, origin + x), (20, y), (40, style['textHeight']), (41, style['widthFactor']), (7, 'TSSD'), (1, dxf_text(text))])
                        counts['texts'] += 1
            canvas.save(directory / f'{paper}-page-{pi + 1}.png')
        dxf.extend(['0', 'ENDSEC', '0', 'EOF'])
        (directory / f'{paper}-layout-inspection.dxf').write_text('\n'.join(dxf) + '\n', encoding='ascii')
        summaries.append(counts)
    (directory / 'visual-summary.json').write_text(json.dumps(summaries, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(summaries, ensure_ascii=False))


if __name__ == '__main__':
    render(sys.argv[1], "--host-measured" in sys.argv)
