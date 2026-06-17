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
dotnet pack src/DotnetMarkdownReports.App/DotnetMarkdownReports.App.csproj \
    -p:RunAnalyzers=false -p:AnalysisMode=None -clp:NoSummary --nologo -v minimal \
    -c Release \
    -o "$pack_dir" \
    -p:VersionPrefix="$semantic_version" -p:VersionSuffix="$version_suffix"

readarray -d '' packages < <(find "$pack_dir" -type f -name "*.nupkg" -print0)
if (( ${#packages[@]} == 0 )); then
    echo "No packages created."
    exit 0
fi
ls -Alh "$pack_dir"

if [ -n "$push" ]; then
    dotnet nuget push "$pack_dir/*.nupkg" --source "https://api.nuget.org/v3/index.json" --api-key "$push" --skip-duplicate
fi
