#!/usr/bin/env bash
# Downloads the NVIDIA CUDA redistributable runtime libraries (cudart + NVRTC) used by the
# DotCompute.Backends.CUDA.Natives.* NuGet packages (GH #187), verifies their SHA256 against
# the pinned NVIDIA redist manifests, and extracts ONLY the runtime libraries into
#   artifacts/cuda-natives/<cu12|cu13>/<win-x64|linux-x64>/
#
# Redistribution of cudart and NVRTC is expressly permitted by the NVIDIA CUDA Toolkit EULA
# (Attachment A); see src/Backends/DotCompute.Backends.CUDA.Natives/LICENSE-NVIDIA-CUDA.txt.
#
# Pinned versions (from https://developer.download.nvidia.com/compute/cuda/redist/):
#   CU13: redistrib_13.0.2.json  -> cuda_cudart 13.0.96, cuda_nvrtc 13.0.88
#   CU12: redistrib_12.9.1.json  -> cuda_cudart 12.9.79, cuda_nvrtc 12.9.86
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$REPO_ROOT/artifacts/cuda-natives"
CACHE="${CUDA_NATIVES_CACHE:-$REPO_ROOT/artifacts/cuda-natives-cache}"
BASE=https://developer.download.nvidia.com/compute/cuda/redist
mkdir -p "$OUT" "$CACHE"

# name|relative_path|sha256
ARCHIVES=(
  "cuda_cudart-windows-x86_64-13.0.96-archive.zip|cuda_cudart/windows-x86_64|a2ed875f9997aa24904fb70cc9db3acd9308433cde99bc8e63ec1271c9da31b4"
  "cuda_cudart-linux-x86_64-13.0.96-archive.tar.xz|cuda_cudart/linux-x86_64|25b8071951baba827be1580b841d363464f6ee6c39f48d33a81646f90cc95ed1"
  "cuda_nvrtc-windows-x86_64-13.0.88-archive.zip|cuda_nvrtc/windows-x86_64|8c50a52467826167e0dbe99936140c52d62272bfc5849fe2d6587d050c8c5d29"
  "cuda_nvrtc-linux-x86_64-13.0.88-archive.tar.xz|cuda_nvrtc/linux-x86_64|00038aac08e1dba6f1933237dbfb217ac6452ae24fab970edcac808f103ca64b"
  "cuda_cudart-windows-x86_64-12.9.79-archive.zip|cuda_cudart/windows-x86_64|179e9c43b0735ffe67207b3da556eb5a0c50f3047961882b7657d3b822d34ef8"
  "cuda_cudart-linux-x86_64-12.9.79-archive.tar.xz|cuda_cudart/linux-x86_64|1f6ad42d4f530b24bfa35894ccf6b7209d2354f59101fd62ec4a6192a184ce99"
  "cuda_nvrtc-windows-x86_64-12.9.86-archive.zip|cuda_nvrtc/windows-x86_64|1aa0644fa53c8ca34cdc73db17bcc73530557bdd3f582c7bfdbd7916c8b48f65"
  "cuda_nvrtc-linux-x86_64-12.9.86-archive.tar.xz|cuda_nvrtc/linux-x86_64|82913658363892dbc0f2638b070476234476e06e084fed60db861cb7e161a6af"
)

echo "==> Downloading + verifying archives into $CACHE"
for entry in "${ARCHIVES[@]}"; do
  IFS='|' read -r name path sha <<<"$entry"
  f="$CACHE/$name"
  if [ ! -f "$f" ]; then
    echo "    downloading $name"
    curl -sSf -o "$f" "$BASE/$path/$name"
  fi
  actual=$(sha256sum "$f" | cut -d' ' -f1)
  if [ "$actual" != "$sha" ]; then
    echo "ERROR: SHA256 mismatch for $name" >&2
    echo "  expected $sha" >&2
    echo "  actual   $actual" >&2
    exit 1
  fi
  echo "    OK $name"
done

extract_windows() { # zip, dest, dll patterns...
  local zip="$1" dest="$2"; shift 2
  mkdir -p "$dest"
  local tmp; tmp=$(mktemp -d)
  unzip -qo "$zip" -d "$tmp"
  for pat in "$@"; do
    find "$tmp" -type f -name "$pat" -exec cp -v {} "$dest/" \;
  done
  rm -rf "$tmp"
}

extract_linux() { # tar.xz, dest, sonames... (resolve symlinks IN-ARCHIVE, write real bytes)
  # Python instead of `tar -x`: the archives contain symlinks (libcudart.so.13 -> .13.0.96),
  # which tar cannot create on Windows runners — and the package must ship the real file under
  # its SONAME name anyway (NuGet packages cannot contain symlinks).
  local tarball="$1" dest="$2"; shift 2
  mkdir -p "$dest"
  python3 - "$tarball" "$dest" "$@" <<'PY'
import os, sys, tarfile
tarball, dest, *sonames = sys.argv[1:]
with tarfile.open(tarball, "r:xz") as tf:
    members = {m.name: m for m in tf.getmembers()}
    def resolve(soname):
        for m in members.values():
            if os.path.basename(m.name) == soname and "/stubs/" not in m.name:
                while m.issym():
                    target = os.path.normpath(os.path.join(os.path.dirname(m.name), m.linkname))
                    m = members[target]
                return m
        raise SystemExit(f"ERROR: {soname} not found in {tarball}")
    for soname in sonames:
        member = resolve(soname)
        with tf.extractfile(member) as src, open(os.path.join(dest, soname), "wb") as out:
            out.write(src.read())
        print(f"extracted {member.name} -> {dest}/{soname}")
PY
}

echo "==> CU13 win-x64"
extract_windows "$CACHE/cuda_cudart-windows-x86_64-13.0.96-archive.zip" "$OUT/cu13/win-x64" "cudart64_13.dll"
extract_windows "$CACHE/cuda_nvrtc-windows-x86_64-13.0.88-archive.zip" "$OUT/cu13/win-x64" "nvrtc64_130_0.dll" "nvrtc-builtins64_130.dll"

echo "==> CU13 linux-x64"
extract_linux "$CACHE/cuda_cudart-linux-x86_64-13.0.96-archive.tar.xz" "$OUT/cu13/linux-x64" "libcudart.so.13"
extract_linux "$CACHE/cuda_nvrtc-linux-x86_64-13.0.88-archive.tar.xz" "$OUT/cu13/linux-x64" "libnvrtc.so.13" "libnvrtc-builtins.so.13.0"

echo "==> CU12 win-x64"
extract_windows "$CACHE/cuda_cudart-windows-x86_64-12.9.79-archive.zip" "$OUT/cu12/win-x64" "cudart64_12.dll"
extract_windows "$CACHE/cuda_nvrtc-windows-x86_64-12.9.86-archive.zip" "$OUT/cu12/win-x64" "nvrtc64_120_0.dll" "nvrtc-builtins64_129.dll"

echo "==> CU12 linux-x64"
extract_linux "$CACHE/cuda_cudart-linux-x86_64-12.9.79-archive.tar.xz" "$OUT/cu12/linux-x64" "libcudart.so.12"
extract_linux "$CACHE/cuda_nvrtc-linux-x86_64-12.9.86-archive.tar.xz" "$OUT/cu12/linux-x64" "libnvrtc.so.12" "libnvrtc-builtins.so.12.9"

echo "==> Result"
find "$OUT" -type f | sort
