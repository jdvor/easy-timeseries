#!/usr/bin/env bash

# Rewrites the committed golden format fixtures under tests/data/golden/<version>/ from the definitions in
# tests/Easy.TimeSeries.Tests/GoldenFixtures.cs.
#
# A changed fixture is an on-disk FORMAT CHANGE. Run this only when such a change is intended, and review the
# resulting diff before committing - the whole point of the fixtures is to make that change visible.
#
# Usage: ./scripts/regen-golden.sh

set -e

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
solution_root="$(dirname "$script_dir")"
test_project="$solution_root/tests/Easy.TimeSeries.Tests/Easy.TimeSeries.Tests.csproj"

note () {
    echo -e "[NOTE] $1" >&2
}

note 'regenerating golden fixtures...'
DOTNET_NOLOGO=1 ETS_REGEN_GOLDEN=1 dotnet test -c Release \
    --project "$test_project" \
    -- --filter-class 'Easy.TimeSeries.Tests.GoldenRegenerationTests'

note 'done; review the diff before committing:'
git -C "$solution_root" status --short -- tests/data/golden
