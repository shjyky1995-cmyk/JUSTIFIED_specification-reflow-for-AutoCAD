"""组装已核验候选的跨电脑测试包；只选明确授权的正文与脱敏样本。"""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--desktop-directory', help='已构建且校验过的桌面载荷目录')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
output = Path(args.output).resolve()
if not output.is_relative_to(root / 'artifacts'):
    raise ValueError('交付目录必须在项目 artifacts 内')
output.mkdir(parents=True, exist_ok=True)
guide = root / 'docs/OFFICE_TEST_GUIDE.md'
desktop = output / 'EngiSpace-DesignNote-0.2.0-preview.3-office.zip'
if not desktop.is_file():
    if not args.desktop_directory:
        raise ValueError('请先构建桌面公开正文包，或指定其载荷目录')
    payload = Path(args.desktop_directory).resolve()
    build = json.loads((payload / 'BUILD.json').read_text(encoding='utf-8-sig'))
    if build['privateContent'] or not build.get('bundledContent'):
        raise ValueError('桌面载荷必须是用户授权的公开正文包')
    with zipfile.ZipFile(desktop, 'x', compression=zipfile.ZIP_DEFLATED) as archive:
        for file in sorted(payload.rglob('*')):
            if file.is_file():
                archive.write(file, file.relative_to(payload).as_posix())
assets = [desktop]
inputs = {
    'EngiSpace-Bidding-0.4.0-bid-preview.3-office.zip': root / '测试文件/勘察设计投标试用/EngiSpace-0.4.0-bid-preview.3.zip',
    'CAD-Tables-0.2.0-preview.2-office.zip': root / '测试文件/CAD表格试用/CAD-tables-0.2.0-table-preview.2-77d9645.zip',
}
for name, source in inputs.items():
    target = output / name
    if target.exists():
        raise ValueError('拒绝覆盖交付包: ' + name)
    shutil.copyfile(source, target)
    assets.append(target)
for target in assets:
    with zipfile.ZipFile(target, 'a', compression=zipfile.ZIP_DEFLATED) as archive:
        if 'OFFICE_TEST_GUIDE.md' in archive.namelist():
            raise ValueError('测试指南已添加，拒绝产生重复条目')
        archive.write(guide, 'OFFICE_TEST_GUIDE.md')
        if target.name.startswith('CAD-'):
            archive.write(root / '测试文件/CAD表格试用/复杂工程表格试用.docx', '复杂工程表格试用.docx')
content = output / 'Project-Content-And-Samples-office.zip'
with zipfile.ZipFile(content, 'x', compression=zipfile.ZIP_DEFLATED) as archive:
    archive.write(root / 'content-library/README.md', 'content-library/README.md')
    for directory in ['content-library/shared', 'content-library/sources', 'docs/design-sources']:
        for file in sorted((root / directory).rglob('*')):
            if file.is_file():
                archive.write(file, file.relative_to(root).as_posix())
    archive.write(guide, 'OFFICE_TEST_GUIDE.md')
    for name in ['复杂工程表格试用.docx', '表格试用样本.docx']:
        archive.write(root / '测试文件/CAD表格试用' / name, 'samples/' + name)
    for name in ['文字型PDF试用样本.pdf', '扫描PDF未识别提示样本.pdf']:
        archive.write(root / '测试文件/勘察设计投标试用' / name, 'samples/' + name)
    archive.writestr('samples/脱敏招标试用.txt', '跨电脑测试样本，不含真实项目。\n项目负责人须提供注册证书。\n不接受过期证明。\n请编制勘察工作安排与质量控制章节。\n')
assets.append(content)
shutil.copyfile(guide, output / 'OFFICE_TEST_GUIDE.md')
def sha(file):
    digest = hashlib.sha256()
    with file.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()
manifest = [{'file': file.name, 'bytes': file.stat().st_size, 'sha256': sha(file)} for file in assets]
(output / 'SHA256SUMS.txt').write_text(''.join(f"{item['sha256']}  {item['file']}\n" for item in manifest), encoding='ascii')
(output / 'release-assets.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'status': 'OFFICE_ASSETS_PREPARED', 'assets': manifest}, ensure_ascii=False))
