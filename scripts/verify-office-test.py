"""对最终公开ZIP全新解压、核对CRC/清单，报告保存在本机不提交。"""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

root, destination = (Path(value).resolve() for value in sys.argv[1:])
if destination.exists():
    raise ValueError('需要全新解压目录，不能覆盖旧程序或资料')
destination.mkdir(parents=True)
def sha(file):
    digest = hashlib.sha256()
    with file.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()
reports = []
assets = json.loads((root / 'release-assets.json').read_text(encoding='utf-8'))
for asset in assets:
    source = root / asset['file']
    if source.stat().st_size != asset['bytes'] or sha(source) != asset['sha256']:
        raise ValueError('下载包哈希不一致: ' + asset['file'])
    # 原框架含较深的原生依赖目录；短目录兼容Windows默认MAX_PATH。
    folder = 'note' if source.name.startswith('EngiSpace-Design') else 'bid' if source.name.startswith('EngiSpace-Bid') else 'cad' if source.name.startswith('CAD-') else 'content'
    fresh = destination / folder
    with zipfile.ZipFile(source) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError('重复ZIP条目')
        for name in names:
            file = fresh / name
            if not file.resolve().is_relative_to(fresh) or '\\' in name:
                raise ValueError('非法ZIP路径: ' + name)
            if any(part.lower() in {'portable-data', '.env', '.git', 'local', 'private'} for part in Path(name).parts):
                raise ValueError('包含运行数据或私有目录: ' + name)
        if archive.testzip():
            raise ValueError('ZIP CRC失败')
        archive.extractall(fresh)
    manifest = fresh / 'manifest.sha256'
    count = 0
    if manifest.exists():
        files = {}
        for line in manifest.read_text(encoding='ascii').splitlines():
            digest, name = line.split('  ', 1)
            name = name.replace('\\', '/')
            file = (fresh / 'client' / name).resolve()
            if name in files or not file.is_relative_to(fresh / 'client') or sha(file) != digest:
                raise ValueError('成品清单校验失败: ' + name)
            files[name] = digest
        actual = {file.relative_to(fresh / 'client').as_posix() for file in (fresh / 'client').rglob('*') if file.is_file()}
        if actual != set(files):
            raise ValueError('成品文件集不一致')
        count = len(files)
        build = json.loads((fresh / 'BUILD.json').read_text(encoding='utf-8-sig'))
        if build['files'] != count or build['privateContent'] or not build['candidate'] or build['productionReady']:
            raise ValueError('候选状态或公开标志错误')
    for manifest in fresh.glob('*.bundle/SHA256.json'):
        for entry in json.loads(manifest.read_text(encoding='utf-8-sig')):
            file = (manifest.parent / entry['File'].replace('\\', '/')).resolve()
            if not file.is_relative_to(manifest.parent) or sha(file).upper() != entry['Hash'].upper():
                raise ValueError('CAD清单校验失败')
            count += 1
    reports.append({'file': source.name, 'files': count, 'zipEntries': len(names), 'sha256': asset['sha256'], 'fresh': str(fresh)})
(destination / 'verification.json').write_text(json.dumps(reports, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'status': 'OFFICE_FRESH_EXTRACT_OK', 'reports': reports}, ensure_ascii=False))
