"""脱敏合成样本，输出到调用方指定的G盘测试目录；不分发系统字体。"""
import json
import sys
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.utils import ImageReader
from PIL import Image, ImageDraw
from pypdf import PdfReader, PdfWriter

root = Path(sys.argv[1]).resolve()
if root.drive.lower() == 'c:':
    raise ValueError('测试产物必须在G盘项目内')
root.mkdir(parents=True, exist_ok=True)
font_path = next((Path(__file__).resolve().parent.parent / 'desktop/app/dist/assets').glob('NotoSansSC*.ttf'))
pdfmetrics.registerFont(TTFont('TestCJK', str(font_path)))
expected = [
    ['脱敏招标样本', '项目负责人必须提供注册证书；不接受过期证明。', '印刷页码 7'],
    ['评分项目', '设计方案评分最高10分，资料缺失不得补造。', '成果名称', '提交期限', '设计图纸', '30日'],
    ['技术要求', '联合业务不等于联合体投标。', '最终截止时间应以补遗为准。'],
]
c = canvas.Canvas(str(root / 'text.pdf'))
for page, lines in enumerate(expected):
    c.setFont('TestCJK', 13)
    for i, text in enumerate(lines[:2] if page == 1 else lines):
        c.drawString(60, 780 - i * 34, text)
    if page == 1:
        for y in [680, 646, 612]:
            c.line(60, y, 510, y)
        for x in [60, 340, 510]:
            c.line(x, 612, x, 680)
        for row in range(2):
            for col in range(2):
                c.drawString([70, 350][col], 658 - row * 34, lines[2 + row * 2 + col])
    c.showPage()
c.save()

# 图片独占页，没有任何可复制文字，禁止被当作解析完成。
image = Image.new('RGB', (900, 220), 'white')
draw = ImageDraw.Draw(image)
draw.text((35, 65), 'SCANNED PAGE: QUALIFICATION - NOT A TEXT LAYER', fill='black')
image.save(root / 'scan-image.png')
c = canvas.Canvas(str(root / 'scan.pdf'))
c.drawImage(ImageReader(image), 50, 600, width=500, height=122)
c.showPage()
c.save()
writer = PdfWriter()
writer.add_page(PdfReader(root / 'text.pdf').pages[0])
writer.add_blank_page(width=595, height=842)
writer.add_page(PdfReader(root / 'scan.pdf').pages[0])
writer.write(root / 'mixed.pdf')
writer = PdfWriter()
writer.add_page(PdfReader(root / 'text.pdf').pages[0])
writer.encrypt('TEST-ONLY-PDF-PASSWORD')
writer.write(root / 'encrypted.pdf')
(root / 'damaged.pdf').write_bytes(b'%PDF-1.7\ninvalid objects\n')
writer = PdfWriter()
for _ in range(501):
    writer.add_blank_page(width=595, height=842)
writer.write(root / 'overpages.pdf')

# 长页完整保留首尾与Unicode；多块分割不能丢失末尾要求。
c = canvas.Canvas(str(root / 'long-page.pdf'), pagesize=(800, 35000))
c.setFont('Helvetica', 8)
long_lines = [f'L{i:03d}_' + 'X' * 100 for i in range(220)] + ['FINAL_REQUIRED_END']
for i, text in enumerate(long_lines):
    c.drawString(25, 34900 - i * 120, text)
c.showPage()
c.save()
c = canvas.Canvas(str(root / 'overtext.pdf'), pagesize=(800, 90000))
for page in range(13):
    c.setFont('Helvetica', 8)
    for i in range(500):
        c.drawString(25, 89900 - i * 160, 'Z' * 100)
    c.showPage()
c.save()
(root / 'fixtures.json').write_text(json.dumps({'expected': expected, 'longLines': long_lines}, ensure_ascii=False), encoding='utf-8')
print(json.dumps({'status': 'PDF_FIXTURES_OK', 'root': str(root), 'pdfs': 8}))
