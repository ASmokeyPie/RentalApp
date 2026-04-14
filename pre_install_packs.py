#!/usr/bin/env python3
"""
Pre-install NuGet workload packs that fail to download via dotnet's HTTP client
through Docker Desktop's virtualised network. Large packs (~50-400 MB) get their
TCP connection dropped at random points; downloading in 8 MB Range chunks avoids
this by keeping each individual transfer small.
"""

import os
import subprocess
import sys
import traceback
import urllib.request
import zipfile

DOTNET_ROOT = '/usr/share/dotnet'
CHUNK = 8 * 1024 * 1024  # 8 MB per Range request

# Determine the SDK feature band (e.g. 10.0.103 -> 10.0.100)
sdk_ver = subprocess.check_output(['dotnet', '--version']).decode().strip()
parts = sdk_ver.split('.')
feature_band = f'{parts[0]}.{parts[1]}.{(int(parts[2]) // 100) * 100}'
print(f'SDK: {sdk_ver}  feature band: {feature_band}')


def download_chunked(url, output_path):
    """Download url to output_path using 8 MB Range chunks with per-chunk retry."""
    with urllib.request.urlopen(urllib.request.Request(url, method='HEAD')) as r:
        total = int(r.headers['Content-Length'])
    mb = total / (1024 * 1024)
    chunks = -(-total // CHUNK)  # ceiling division
    print(f'  {mb:.1f} MB  ({chunks} chunks)')

    with open(output_path, 'wb') as f:
        start = 0
        chunk_num = 0
        while start < total:
            end = min(start + CHUNK - 1, total - 1)
            chunk_num += 1
            for attempt in range(5):
                try:
                    req = urllib.request.Request(
                        url, headers={'Range': f'bytes={start}-{end}'}
                    )
                    with urllib.request.urlopen(req, timeout=60) as r:
                        data = r.read()
                    break
                except Exception as e:
                    if attempt == 4:
                        raise RuntimeError(
                            f'chunk {chunk_num} failed after 5 attempts: {e}'
                        ) from e
                    print(f'    chunk {chunk_num} attempt {attempt + 1} failed: {e}, retrying...')
            f.write(data)
            start = end + 1


def pre_install(pack, ver):
    pack_lower = pack.lower()
    url = (
        f'https://api.nuget.org/v3-flatcontainer'
        f'/{pack_lower}/{ver}/{pack_lower}.{ver}.nupkg'
    )
    pack_dir = f'{DOTNET_ROOT}/packs/{pack}/{ver}'
    record_dir = (
        f'{DOTNET_ROOT}/metadata/workloads/{feature_band}'
        f'/InstalledPacks/v1/{pack}/{ver}'
    )

    os.makedirs(pack_dir, exist_ok=True)
    os.makedirs(record_dir, exist_ok=True)

    nupkg = f'/tmp/{pack_lower}.{ver}.nupkg'
    print(f'Pre-installing {pack} {ver}')
    download_chunked(url, nupkg)

    with zipfile.ZipFile(nupkg) as z:
        z.extractall(pack_dir)

    open(os.path.join(record_dir, feature_band), 'w').close()
    os.remove(nupkg)
    print(f'  done')


try:
    # The .NET 10 SDK workload installs runtime packs for both .NET 9 and .NET 10
    # (multi-targeting support). The .NET 9 AOT and Mono packs, and the .NET 10
    # AOT packs, are large enough to fail over Docker Desktop's network.

    for arch in ['x86', 'x64', 'arm', 'arm64']:
        pre_install(f'Microsoft.NETCore.App.Runtime.AOT.linux-x64.Cross.android-{arch}', '9.0.13')
        pre_install(f'Microsoft.NETCore.App.Runtime.Mono.android-{arch}', '9.0.13')

    for arch in ['x86', 'x64', 'arm', 'arm64']:
        pre_install(f'Microsoft.NETCore.App.Runtime.AOT.linux-x64.Cross.android-{arch}', '10.0.3')

except Exception:
    traceback.print_exc()
    sys.exit(1)
