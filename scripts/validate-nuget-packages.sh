#!/usr/bin/env bash

pack_dir='./artifacts/validate'
keep=''

while [[ "$#" -gt 0 ]]; do
    case $1 in
        -o|--output-dir) pack_dir="$2"; shift ;;
        -k|--keep) keep='True' ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

fatal_error () {
    echo -e "[FAILURE] $1" >&2
    exit ${2:-1}
}

note () {
    echo -e "[NOTE] $1" >&2
}

or_die () {
    if [ $? -ne 0 ]; then
        echo -e "[FAILURE] $1" >&2
        exit ${2:-1}
    fi
}

if ! command -v dotnet-validate > /dev/null 2>&1; then
    note 'dotnet-validate not found; installing (it only ships prerelease versions)...'
    dotnet tool install --global dotnet-validate --prerelease
    or_die 'failed to install dotnet-validate'
fi

# Packed with a throwaway version - this only validates package health (dependencies, symbols,
# deterministic build, etc.), not the real release version. Real versioning happens in pack.sh
# after the tag is created.
rm -rf "$pack_dir"
note "packing solution into $pack_dir for validation..."
dotnet pack timeseries.sln \
    -p:RunAnalyzers=false -p:AnalysisMode=None -clp:NoSummary --nologo -v minimal \
    -c Release \
    -o "$pack_dir" \
    -p:VersionPrefix=0.0.1 \
    -p:VersionSuffix=validate \
    -p:ContinuousIntegrationBuild=true
or_die 'dotnet pack failed'

readarray -d '' packages < <(find "$pack_dir" -type f -name '*.nupkg' -print0)
if (( ${#packages[@]} == 0 )); then
    fatal_error 'no packages found to validate'
fi
note "validating ${#packages[@]} package(s)..."

failed=0
for pkg in "${packages[@]}"; do
    echo "--- $(basename "$pkg") ---"
    dotnet-validate package local "$pkg" || failed=1
    echo
done

if [ -z "$keep" ]; then
    rm -rf "$pack_dir"
fi

if [ "$failed" -ne 0 ]; then
    fatal_error 'one or more packages failed validation'
fi

note 'all packages passed validation'
