#!/usr/bin/env bash
# Resolve which FSharp.Analyzers.SDK version an analyzer package was built against,
# by reading the pin out of the package's own source repository at the exact commit
# the package was published from.
#
# Usage: sdk-from-source.sh <package-id> [version ...]      (no version = all)
set -uo pipefail

id_lower=$(echo "$1" | tr '[:upper:]' '[:lower:]'); shift

if [ "$#" -gt 0 ]; then versions=("$@"); else
  mapfile -t versions < <(curl -s "https://api.nuget.org/v3-flatcontainer/$id_lower/index.json" | jq -r '.versions[]')
fi

# Candidate files that may carry the pin, cheapest first.
CANDIDATES=(Directory.Packages.props Directory.Build.props)

printf '%-10s %-9s %-9s %s\n' VERSION SDK COMMIT SOURCE

for v in "${versions[@]}"; do
  nuspec=$(curl -sL "https://api.nuget.org/v3-flatcontainer/$id_lower/$v/$id_lower.nuspec")
  url=$(grep -o '<repository[^>]*url="[^"]*"' <<<"$nuspec" | grep -o 'url="[^"]*"' | cut -d'"' -f2)
  commit=$(grep -o '<repository[^>]*commit="[^"]*"' <<<"$nuspec" | grep -o 'commit="[^"]*"' | cut -d'"' -f2)

  if [ -z "$commit" ] || [ -z "$url" ]; then
    printf '%-10s %-9s %-9s %s\n' "$v" "?" "-" "no repository metadata in nuspec"
    continue
  fi

  slug=$(sed -E 's#^.*github\.com/##; s#\.git$##' <<<"$url")

  sdk=""; found=""
  for f in "${CANDIDATES[@]}"; do
    body=$(gh api "repos/$slug/contents/$f?ref=$commit" --jq '.content' 2>/dev/null | base64 -d 2>/dev/null)
    [ -z "$body" ] && continue
    sdk=$(grep -oE '"FSharp\.Analyzers\.SDK"[^/]*Version="\[?([0-9]+\.[0-9]+\.[0-9]+)' <<<"$body" \
          | grep -oE '[0-9]+\.[0-9]+\.[0-9]+$' | head -1)
    [ -n "$sdk" ] && { found="$f"; break; }
  done

  # Fall back to any fsproj if the props files did not carry it.
  if [ -z "$sdk" ]; then
    for f in $(gh api "repos/$slug/git/trees/$commit?recursive=1" --jq '.tree[]|select(.path|test("\\.fsproj$"))|.path' 2>/dev/null); do
      body=$(gh api "repos/$slug/contents/$f?ref=$commit" --jq '.content' 2>/dev/null | base64 -d 2>/dev/null)
      sdk=$(grep -oE '"FSharp\.Analyzers\.SDK"[^/]*Version="\[?([0-9]+\.[0-9]+\.[0-9]+)' <<<"$body" \
            | grep -oE '[0-9]+\.[0-9]+\.[0-9]+$' | head -1)
      [ -n "$sdk" ] && { found="$f"; break; }
    done
  fi

  printf '%-10s %-9s %-9s %s\n' "$v" "${sdk:-?}" "${commit:0:8}" "$slug/${found:-not found}"
done
