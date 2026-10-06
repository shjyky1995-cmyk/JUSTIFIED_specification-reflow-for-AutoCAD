"""在G盘全新解压并检查成品；不会把业务数据放进ZIP。"""
import hashlib
import json
import sys
import zipfile
from pathlib import Path

bundle, fresh, report = [Path(p).resolve() for p in sys.argv[1:]]
if any(p.drive.lower() == 'c:' for p in [bundle, fresh, report]):
    raise ValueError('成品及验证目录必须在G盘项目内')
if fresh.exists():
    raise ValueError('全新解压目录已存在，拒绝覆盖')
archive = bundle.with_suffix(bundle.suffix + '.zip')
def sha(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()
with zipfile.ZipFile(archive) as z:
    for info in z.infolist():
        target = (fresh / info.filename).resolve()
        if not target.is_relative_to(fresh) or 'portable-data' in target.parts:
            raise ValueError('ZIP包含非法路径或业务数据')
    bad = z.testzip()
    if bad:
        raise ValueError('ZIP CRC错误: ' + bad)
    entries = len(z.infolist())
    z.extractall(fresh)
lines = (bundle / 'manifest.sha256').read_text(encoding='ascii').splitlines()
files = {}
for line in lines:
    digest, relative = line.split('  ', 1)
    relative = relative.replace('\\', '/')
    if relative in files:
        raise ValueError('重复清单项')
    files[relative] = digest
    for root in [bundle, fresh]:
        target = (root / 'client' / relative).resolve()
        if not target.is_relative_to(root / 'client') or sha(target) != digest:
            raise ValueError('清单哈希不一致: ' + relative)
actual = {p.relative_to(fresh / 'client').as_posix() for p in (fresh / 'client').rglob('*') if p.is_file()}
if actual != set(files):
    raise ValueError('文件集合与清单不一致')
build = json.loads((fresh / 'BUILD.json').read_text(encoding='utf-8-sig'))
if build['files'] != len(files) or build['privateContent'] or not build['candidate'] or build['productionReady']:
    raise ValueError('候选元数据不正确')
for path in ['resources/worker/UglyToad.PdfPig.dll', 'resources/app/dist-electron/electron/bid-source.js', 'resources/app/dist-electron/electron/bid-embedded.js', 'third-party/PdfPig-LICENSE.txt', 'third-party/PdfPig-ATTRIBUTION.md', 'EngiSpace.exe']:
    if not (fresh / 'client' / path).is_file():
        raise ValueError('缺少成品依赖: ' + path)
if build.get('biddingFramework'):
    for path in ['EngiSpace-Bidding.exe', 'resources/app.asar', 'resources/app.asar.unpacked/node_modules/better-sqlite3/build/Release/better_sqlite3.node', 'resources/openxml-tools/win32-x64/openxmlhelper.exe', 'resources/agent-tools/win32-x64/bin/rg.exe', 'resources/agent-tools/win32-x64/bin/fd.exe', 'resources/agent-tools/win32-x64/bin/jq.exe', 'third-party/LICENSE', 'third-party/NOTICE', 'third-party/UPSTREAM.json', 'third-party/HtmlToOpenXml-LICENSE.txt']:
        if not (fresh / 'client/resources/bidding-framework' / path).is_file():
            raise ValueError('缺少独立框架依赖/许可: ' + path)
    source = fresh / '对应源码.zip'
    if not source.is_file():
        raise ValueError('缺少对应源码')
    with zipfile.ZipFile(source) as z:
        required = 'desktop/bidding-framework/client/electron/engispace-entry.cjs'
        embedded = ['desktop/app/electron/bid-embedded.ts', 'desktop/bidding-framework/client/electron/engispace-backend.cjs', 'desktop/bidding-framework/client/electron/services/engispaceWire.cjs']
        if required not in z.namelist() or not all(p in z.namelist() for p in embedded) or z.testzip():
            raise ValueError('对应源码入口/CRC错误')
result = {'status': 'BID_PACKAGE_OK', 'files': len(files), 'zipEntries': entries, 'zipSha256': sha(archive), 'zipBytes': archive.stat().st_size, 'revision': build['revision'], 'fresh': str(fresh), 'privateContent': False}
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False))
