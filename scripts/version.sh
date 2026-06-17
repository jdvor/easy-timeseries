#!/usr/bin/env bash

semver=''
branch=''
incr=''
suffix=''
create_tag=''
can_push_tag='True'

while [[ "$#" -gt 0 ]]; do
    case $1 in
        -v|--semantic-version) semver="$2"; shift ;;
        -b|--branch) branch="$2"; shift ;;
        -i|--build-increment) incr="$2"; shift ;;
        -t|--create-tag) create_tag='True' ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

safe_name () {
    echo "$1" | tr / - | tr "[:upper:]" "[:lower:]" | sed s/[^a-z0-9\-]//g
}

get_semver_from_git_tags () {
    tag=$(git tag 2> /dev/null | grep -P '^([0-9]+\.[0-9]+\.[0-9]+)(-([a-z0-9\-]+))?$' | sort -V | tail -1)
    if [ -n "$tag" ]; then
        echo "$tag" | cut -d- -f1
    fi
}

fatal_error () {
    echo -e "[FAILURE] $1"
    exit ${2:-1}
}

or_die () {
    if [ $? -ne 0 ]; then
        echo -e "[FAILURE] $1"
        exit ${2:-1}
    fi
}

if [ -z "$semver" ]; then
    semver=$(get_semver_from_git_tags)
    if [ -z "$semver" ]; then
        fatal_error 'semantic version was not provided and could not be determined from git tags'
    fi
    can_push_tag=''
fi

if [ -z "$branch" ]; then
    branch=$(git rev-parse --abbrev-ref HEAD 2> /dev/null)
fi

if [ -n "$branch" ] && [ "$branch" != 'main' ] && [ "$branch" != 'master' ]; then
    suffix=$(safe_name "$branch")
    if [ -n "$incr" ]; then
        suffix="pre-$incr-$suffix"
    fi
fi

echo "version_prefix=$semver"
echo "version_suffix=$suffix"
[ -n "$suffix" ] && version="$semver-$suffix" || version=$semver
echo "full_version=$version"

if [ -n "$create_tag" ]; then
    if [ -z "$can_push_tag" ]; then
        fatal_error 'existing semantic version cannot be pushed again as new tag'
    fi
    git tag $version &> /dev/null
    git push origin $version &> /dev/null
    or_die "pushing new version tag $version"
fi
