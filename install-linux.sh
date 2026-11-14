#!/usr/bin/env bash
# Install or update the latest OFFICIAL PTMagicians/PTMagic release.
# Usage: bash install-linux.sh [/absolute/installation/directory]
# Default installation directory: /var/opt/ptmagic
set -euo pipefail

if [[ $# -gt 1 ]]; then
  echo "Usage: bash install-linux.sh [/absolute/installation/directory]" >&2
  exit 2
fi
INSTALL_DIR="${1:-/var/opt/ptmagic}"
if [[ "${INSTALL_DIR:0:1}" != "/" || "$INSTALL_DIR" == "/" ]]; then
  echo "Installation directory must be an absolute path other than /" >&2
  exit 2
fi
command -v python3 >/dev/null || { echo "Python 3 is required" >&2; exit 1; }
command -v dotnet >/dev/null || { echo "The .NET / ASP.NET Core runtime is required" >&2; exit 1; }

python3 - "$INSTALL_DIR" <<'PY'
import hashlib
import json
import os
import shutil
import stat
import subprocess
import sys
import tempfile
import urllib.request
import uuid
import zipfile
from pathlib import Path, PurePosixPath

destination = Path(sys.argv[1]).absolute()
if destination == Path('/') or destination.is_symlink() or (destination.exists() and not destination.is_dir()):
    sys.exit('Invalid installation directory (symlinks are not supported)')
if not destination.parent.is_dir():
    sys.exit('Parent directory must exist: ' + str(destination.parent))
if destination.parent.is_symlink():
    sys.exit('Refusing symlink parent directory')
if os.path.realpath(destination.parent) != str(destination.parent):
    sys.exit('Use a canonical destination path without symlinked ancestors')

headers = {'User-Agent': 'PTMagic-Linux-Installer', 'Accept': 'application/vnd.github+json'}
# Never use Hojou development builds as an installation source.
api = 'https://api.github.com/repos/PTMagicians/PTMagic/releases/latest'
with urllib.request.urlopen(urllib.request.Request(api, headers=headers), timeout=30) as response:
    release = json.load(response)
assets = [
    a for a in release.get('assets', [])
    if a['name'].lower().endswith('.zip')
    and a['name'].lower().startswith(('ptm_', 'ptmagic'))
]
if len(assets) != 1:
    sys.exit('Cannot uniquely identify the official release ZIP; installation unchanged')
print('Official PTMagicians release:', release['tag_name'])
print('Destination:', destination)
print('Stop PTMagic and Monitor before installing/updating.')
print('This installer does not stop processes or modify systemd services.')

def preserve(relative):
    parts = [p.lower() for p in relative.parts]
    name = relative.name.lower()
    return (any(p in {'logs', '_logs', 'data', '_data', '_presets'} for p in parts)
            or name.startswith(('settings.', 'appsettings.'))
            or name in {'appsettings.json', 'nlog.config'})

def validate_runtime(root):
    configs = [root / 'PTMagic.runtimeconfig.json', root / 'Monitor' / 'Monitor.runtimeconfig.json']
    needed = {}
    for config in configs:
        if not config.is_file():
            sys.exit('Release missing runtime configuration: ' + config.name)
        data = json.loads(config.read_text(encoding='utf-8-sig'))
        opts = data.get('runtimeOptions', {})
        frameworks = opts.get('frameworks') or ([opts['framework']] if 'framework' in opts else [])
        if not frameworks:
            sys.exit('Release missing framework requirements: ' + config.name)
        for f in frameworks:
            name, version = f['name'], f['version']
            major = version.split('.')[0]
            needed[(name, major)] = version
    out = subprocess.run(['dotnet', '--list-runtimes'], capture_output=True, text=True, check=True).stdout
    for (name, major), version in needed.items():
        if not any(line.startswith(name + ' ' + major + '.') for line in out.splitlines()):
            sys.exit('Missing ' + name + ' runtime major version ' + major +
                     ' (release requests ' + version + '). Install the matching .NET runtime first.')

def reject_links(tree):
    for folder, dirs, files in os.walk(tree, followlinks=False):
        for name in dirs + files:
            candidate = Path(folder) / name
            if candidate.is_symlink():
                sys.exit('Existing installation contains symlink: ' + str(candidate))

with tempfile.TemporaryDirectory(prefix='.ptmagic-download-', dir=destination.parent) as scratch:
    archive_path = Path(scratch) / 'release.zip'
    url = assets[0]['browser_download_url']
    if not url.startswith('https://github.com/PTMagicians/PTMagic/releases/download/'):
        sys.exit('Unexpected official release asset URL')
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=120) as response, archive_path.open('wb') as output:
        shutil.copyfileobj(response, output)
    checksum = assets[0].get('digest') or ''
    if checksum.startswith('sha256:'):
        digest = hashlib.sha256()
        with archive_path.open('rb') as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b''):
                digest.update(chunk)
        if digest.hexdigest().lower() != checksum[7:].lower():
            sys.exit('Release checksum mismatch; installation unchanged')

    with zipfile.ZipFile(archive_path) as archive:
        entries = [i for i in archive.infolist() if not i.is_dir()]
        paths = [PurePosixPath(i.filename) for i in entries]
        if len(entries) > 20000 or sum(i.file_size for i in entries) > 4 * 1024**3:
            sys.exit('Release ZIP exceeds extraction safety limits')
        for info, path in zip(entries, paths):
            if (path.is_absolute() or '..' in path.parts or '\\' in info.filename
                    or stat.S_ISLNK(info.external_attr >> 16)):
                sys.exit('Unsafe release archive path or symlink')
        candidates = [
            p.parent for p in paths if p.name == 'PTMagic.dll'
            and PurePosixPath(*(p.parent.parts + ('Monitor', 'Monitor.dll'))) in paths
        ]
        if len(candidates) != 1:
            sys.exit('Unrecognized official release layout; installation unchanged')
        root = candidates[0]
        selected = [
            (i, Path(*p.parts[len(root.parts):]))
            for i, p in zip(entries, paths)
            if len(p.parts) > len(root.parts) and p.parts[:len(root.parts)] == root.parts
        ]
        if len({str(rel).lower() for _, rel in selected}) != len(selected):
            sys.exit('Duplicate release paths; installation unchanged')
        # Release ZIPs place example defaults outside the PTMagic application folder.
        # Copy their contents to the installation root, never overwriting existing files.
        default_roots = {
            p.parts[:idx + 1]
            for p in paths
            for idx, part in enumerate(p.parts[:-1])
            if part == '_default_settings_PT_2.x'
        }
        if len(default_roots) != 1:
            sys.exit('Cannot locate release example defaults; installation unchanged')
        defaults_root = next(iter(default_roots))
        default_entries = [
            (i, Path(*p.parts[len(defaults_root):]))
            for i, p in zip(entries, paths)
            if len(p.parts) > len(defaults_root)
            and p.parts[:len(defaults_root)] == defaults_root
        ]
        if not default_entries or len({str(rel).lower() for _, rel in default_entries}) != len(default_entries):
            sys.exit('Missing or duplicate example default files; installation unchanged')
        # Stage on the same filesystem as the final directory so directory renames are atomic.
        stage = destination.parent / ('.ptmagic-stage-' + uuid.uuid4().hex)
        backup = destination.parent / (destination.name + '.backup-' + uuid.uuid4().hex[:10])
        previous_moved = False
        installed = False
        try:
            if destination.exists():
                reject_links(destination)
                shutil.copytree(destination, stage, symlinks=False)
            else:
                stage.mkdir()
            updated = preserved = 0
            for info, relative in selected:
                target = stage / relative
                if target.exists() and preserve(relative):
                    preserved += 1
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(info) as source, target.open('wb') as output:
                    shutil.copyfileobj(source, output)
                mode = (info.external_attr >> 16) & 0o777
                os.chmod(target, mode or 0o644)
                updated += 1
            default_copied = default_preserved = 0
            for info, relative in default_entries:
                target = stage / relative
                # Existing user files and application files always take precedence.
                if target.exists():
                    default_preserved += 1
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(info) as source, target.open('wb') as output:
                    shutil.copyfileobj(source, output)
                mode = (info.external_attr >> 16) & 0o777
                os.chmod(target, mode or 0o644)
                default_copied += 1
            validate_runtime(stage)
            if destination.exists():
                destination.rename(backup)
                previous_moved = True
            try:
                stage.rename(destination)
                installed = True
            except BaseException:
                if previous_moved:
                    backup.rename(destination)
                    previous_moved = False
                raise
            print('Installed', updated, 'release files; preserved', preserved, 'settings/data files.')
            print('Example defaults copied:', default_copied, '; existing files preserved:', default_preserved)
            if previous_moved:
                print('Previous installation backup:', backup)
        finally:
            if stage.exists():
                shutil.rmtree(stage)
            if not installed and previous_moved and not destination.exists():
                backup.rename(destination)

print('Next steps:')
print('  1. Configure PTMagic/settings.general.json and settings.analyzer.json if new.')
print('  2. Review Monitor settings, credentials, and firewall/port configuration.')
print('  3. Start PTMagic and Monitor using dotnet from the installation directory.')
print('     cd "' + str(destination) + '" && dotnet PTMagic.dll')
print('     cd "' + str(destination / 'Monitor') + '" && dotnet Monitor.dll')
print('Run the installer again for a later official release; stop both apps first.')
PY
