#!/usr/bin/env bash

dotnet_version=''
target_framework=''

while [[ "$#" -gt 0 ]]; do
    case $1 in
        -v|--dotnet-version) dotnet_version="$2"; shift ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
solution_root=$(dirname "$script_dir")

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

# Reads the SDK version (e.g. "10.0.300") from global.json at the solution root.
get_dotnet_version_from_global_json () {
    local file="$solution_root/global.json"
    if [ -f "$file" ]; then
        sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$file" | head -1
    fi
}

# Converts an SDK version (e.g. "10.0.300") to a target framework moniker (e.g. "net10.0").
to_target_framework () {
    echo "net$(echo "$1" | cut -d'.' -f1,2)"
}

# Finds the first Directory.Packages.props, ignoring build artifacts.
find_packages_props () {
    find "$solution_root" -name 'Directory.Packages.props' -not -path '*/obj/*' | head -1
}

# Extracts sorted, unique package names from <PackageVersion Include="..." /> entries.
get_package_names () {
    grep '<PackageVersion' "$1" | sed -n 's/.*Include="\([^"]*\)".*/\1/p' | sort -u
}

if [ -z "$dotnet_version" ]; then
    dotnet_version=$(get_dotnet_version_from_global_json)
    if [ -z "$dotnet_version" ]; then
        fatal_error 'dotnet version was not provided and could not be determined from global.json'
    fi
    note "dotnet version not provided; using $dotnet_version from global.json"
fi
target_framework=$(to_target_framework "$dotnet_version")

props_file=$(find_packages_props)
if [ -z "$props_file" ]; then
    fatal_error 'Directory.Packages.props not found'
fi
note "using central package manifest: $props_file"

props_dir=$(dirname "$props_file")

package_names=$(get_package_names "$props_file")
if [ -z "$package_names" ]; then
    fatal_error "no PackageVersion entries found in $props_file"
fi

# Build <PackageReference> items (no Version - CPM provides them).
refs=''
while IFS= read -r pkg; do
    refs+="    <PackageReference Include=\"$pkg\" />"$'\n'
done <<< "$package_names"

# Write a temp .csproj next to Directory.Packages.props so CPM is picked up.
tmp_csproj="$props_dir/check-vulnerabilities-tmp.csproj"

cat > "$tmp_csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>${target_framework}</TargetFramework>
    <!-- Suppress restore artifacts from the flat-project scan approach:
         NU1605 = package downgrade (CPM pins vs transitive requires higher)
         NU1510 = unnecessary package (pruning noise) -->
    <NoWarn>NU1605;NU1510</NoWarn>
  </PropertyGroup>
  <ItemGroup>
${refs}  </ItemGroup>
</Project>
EOF

trap 'rm -f "$tmp_csproj"' EXIT

note "scanning $(echo "$package_names" | wc -l | tr -d ' ') packages for vulnerabilities..."

output=$(dotnet list "$tmp_csproj" package --vulnerable --include-transitive 2>&1) || true
echo "$output"

if echo "$output" | grep -iqwE '(High|Critical)'; then
    fatal_error 'critical or high severity vulnerabilities detected'
fi
