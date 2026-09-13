#!/usr/bin/env bash

semantic_version='1.0.0'
version_suffix=''
push=''

while [[ "$#" -gt 0 ]]; do
    case $1 in
        -v|--semantic-version) semantic_version="$2"; shift ;;
        -s|--version-suffix) version_suffix="$2"; shift ;;
        -p|--push) push="$2"; shift ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

pack_dir='./artifacts/pack'

rm -rf "$pack_dir"
dotnet pack timeseries.sln \
    -p:RunAnalyzers=false -p:AnalysisMode=None -clp:NoSummary --nologo -v minimal \
    -c Release \
    -o "$pack_dir" \
    -p:VersionPrefix="$semantic_version" \
    -p:VersionSuffix="$version_suffix"

readarray -d '' packages < <(find "$pack_dir" -type f -name "*.nupkg" -print0)
if (( ${#packages[@]} == 0 )); then
    echo "No packages created."
    exit 0
fi
readarray -t packages < <(printf '%s\n' "${packages[@]}" | sort)
ls -Alh "$pack_dir"

if [ -z "$push" ]; then
    exit 0
fi

# Pushed one at a time on purpose. A single `dotnet nuget push` over a glob aborts on the first
# rejected package and silently leaves the rest unpublished, which yields a half-released version.
# Here every package is attempted, and the script fails at the end if any of them did not make it.
failed=()
for pkg in "${packages[@]}"; do
    name=$(basename "$pkg")
    echo "--- pushing $name ---"
    if ! dotnet nuget push "$pkg" \
        --source 'https://api.nuget.org/v3/index.json' \
        --api-key "$push" \
        --skip-duplicate
    then
        echo "[FAILURE] push failed: $name" >&2
        failed+=("$name")
    fi
done

if (( ${#failed[@]} > 0 )); then
    echo "[FAILURE] ${#failed[@]} of ${#packages[@]} package(s) failed to push: ${failed[*]}" >&2
    exit 1
fi

echo "[NOTE] all ${#packages[@]} package(s) pushed" >&2
