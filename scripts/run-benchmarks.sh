#!/usr/bin/env bash

# Runs the size comparison and/or micro-benchmarks from Easy.TimeSeries.Benchmarks and stores the
# resulting markdown reports under performance-results/<date>-<short-sha>/. See docs/benchmarking.md.
#
# Usage: ./scripts/run-benchmarks.sh [-o|--output-dir <dir>] [-f|--filter <pattern>] [-s|--size-only] [-b|--bench-only]
#                                    [-F|--full]

output_dir=''
filter='*'
size_only=''
bench_only=''
mode_flag='--quick'

while [[ "$#" -gt 0 ]]; do
    case $1 in
        -o|--output-dir) output_dir="$2"; shift ;;
        -f|--filter) filter="$2"; shift ;;
        -s|--size-only) size_only='True' ;;
        -b|--bench-only) bench_only='True' ;;
        -F|--full) mode_flag='--full' ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
solution_root=$(dirname "$script_dir")
benchmarks_project="$solution_root/src/Easy.TimeSeries.Benchmarks"

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

if [ -n "$size_only" ] && [ -n "$bench_only" ]; then
    fatal_error '--size-only and --bench-only are mutually exclusive'
fi

do_size='True'
do_bench='True'
[ -n "$size_only" ] && do_bench=''
[ -n "$bench_only" ] && do_size=''

if [ -z "$output_dir" ]; then
    output_dir="$solution_root/performance-results"
fi

short_sha=$(git -C "$solution_root" rev-parse --short=7 HEAD 2>/dev/null)
[ -z "$short_sha" ] && fatal_error 'unable to determine git commit SHA'

run_date=$(date -u +%F)
run_id="${run_date}-${short_sha}"
run_dir="$output_dir/$run_id"
mkdir -p "$run_dir"

branch=$(git -C "$solution_root" rev-parse --abbrev-ref HEAD 2>/dev/null)
if [ -z "$branch" ] || [ "$branch" = 'HEAD' ]; then
    branch="${GITHUB_REF_NAME:-unknown}"
fi
dotnet_version=$(dotnet --version)
os_info=$(uname -sr)

# Prepends a short "who/what/when" header to an already-written report so it stays readable on its own,
# without relying on the caller also knowing which folder it came from.
prepend_header () {
    local file="$1"
    local title="$2"
    local tmp
    tmp=$(mktemp)
    {
        echo "# $title - $run_date"
        echo ''
        echo "- Commit: \`$short_sha\`"
        echo "- Branch: \`$branch\`"
        echo "- .NET SDK: \`$dotnet_version\`"
        echo "- OS: \`$os_info\`"
        echo ''
        echo '---'
        echo ''
        cat "$file"
    } > "$tmp"
    mv "$tmp" "$file"
}

run_size_comparison () {
    note 'running size comparison...'
    dotnet run -c Release --project "$benchmarks_project" -- size -o "$run_dir/size-report.md"
    or_die 'size comparison failed'
    prepend_header "$run_dir/size-report.md" 'Size Comparison'
    note "size report written to $run_dir/size-report.md"
}

run_microbenchmarks () {
    note "running micro-benchmarks (filter: $filter, mode: ${mode_flag#--})..."
    dotnet run -c Release --project "$benchmarks_project" -- --filter "$filter" "$mode_flag"
    or_die 'micro-benchmark run failed'

    local artifacts_dir="$solution_root/artifacts"
    local latest_dir
    latest_dir=$(ls -dt "$artifacts_dir"/*/ 2>/dev/null | head -1)
    [ -z "$latest_dir" ] && fatal_error 'no benchmark artifacts directory found after run'

    local md_file
    md_file=$(find "$latest_dir" -maxdepth 1 -name '*.md' | head -1)
    if [ -z "$md_file" ]; then
        fatal_error "no benchmark results produced in $latest_dir - check that --filter '$filter' matched at least one benchmark"
    fi

    cp "$md_file" "$run_dir/microbenchmarks.md"
    note "micro-benchmark report written to $run_dir/microbenchmarks.md"
}

[ -n "$do_size" ] && run_size_comparison
[ -n "$do_bench" ] && run_microbenchmarks

note "done; results in $run_dir"
