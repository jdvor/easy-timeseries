#!/usr/bin/env bash

set -e
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
docs_dir="$(dirname "$script_dir")/docs"

cargo install mdbook
cargo install mdbook-mermaid
cargo install mdbook-toc
cargo install mdbook-variables

mkdir -p "$docs_dir/assets/vendor"

(
    export RUST_LOG=warn
    cd "$docs_dir"
    mdbook-mermaid install .
    mv mermaid* assets/vendor
    mdbook-admonish install --css-dir assets/vendor .
)