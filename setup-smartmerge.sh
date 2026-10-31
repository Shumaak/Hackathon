#!/usr/bin/env bash
# Sets up Unity Smart Merge (UnityYAMLMerge) for this repo.
# Run once per machine, from anywhere inside the repo: ./setup-smartmerge.sh
# Works on Linux, macOS, and Windows (Git Bash).

set -e
cd "$(git rev-parse --show-toplevel)"

find_tool() {
    local candidates=()
    case "$(uname -s)" in
        Linux)
            candidates=("$HOME"/Unity/Hub/Editor/*/Editor/Data/Tools/UnityYAMLMerge) ;;
        Darwin)
            candidates=(/Applications/Unity/Hub/Editor/*/Unity.app/Contents/Tools/UnityYAMLMerge) ;;
        MINGW*|MSYS*|CYGWIN*)
            candidates=(/c/Program\ Files/Unity/Hub/Editor/*/Editor/Data/Tools/UnityYAMLMerge.exe) ;;
    esac
    # Pick the newest editor version that actually exists. Sort by version, not
    # alphabetically - plain globbing puts 6000.10 before 6000.7.
    local existing=()
    for c in "${candidates[@]}"; do
        [ -f "$c" ] && existing+=("$c")
    done
    [ ${#existing[@]} -eq 0 ] && return

    printf '%s\n' "${existing[@]}" | sort -V | tail -n 1
}

TOOL="$(find_tool)"

if [ -z "$TOOL" ]; then
    echo "Could not find UnityYAMLMerge automatically."
    echo "Find it in your Unity install (Editor/Data/Tools/UnityYAMLMerge) and run:"
    echo "  $0 /path/to/UnityYAMLMerge"
    [ -n "$1" ] && TOOL="$1"
    [ -z "$TOOL" ] && exit 1
fi

echo "Using: $TOOL"

git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'$TOOL' merge -h -p --force --fallback none %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
git config mergetool.unityyamlmerge.trustExitCode false
git config mergetool.unityyamlmerge.cmd "'$TOOL' merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""

# The git config above only says HOW to run SmartMerge. .gitattributes says WHICH files
# get it - without this half, git quietly falls back to a line-based text merge and
# scatters conflict markers through the YAML. Both halves are required.
MARKER="# --- Unity SmartMerge (managed by setup-smartmerge.sh) ---"

if grep -qF "$MARKER" .gitattributes 2>/dev/null; then
    echo ".gitattributes already has the SmartMerge block - left alone."
else
    [ -s .gitattributes ] && [ -n "$(tail -c 1 .gitattributes)" ] && echo "" >> .gitattributes

    {
        echo "$MARKER"
        echo "# Requires Asset Serialization = Force Text in Project Settings."
        for ext in unity prefab asset mat anim controller overrideController \
                   physicMaterial physicsMaterial2D playable mask terrainlayer \
                   mixer shadervariants preset spriteatlas guiskin fontsettings \
                   renderTexture flare brush meta; do
            printf '*.%-20s merge=unityyamlmerge eol=lf\n' "$ext"
        done
    } >> .gitattributes

    echo "Wrote the SmartMerge block to .gitattributes - commit it so the whole team gets it."
fi

echo
echo "Done. Scenes/prefabs will now smart-merge; on conflicts run: git mergetool --tool=unityyamlmerge"
echo "Note: .gitattributes is per-repo and belongs in the commit; the git config is per-machine."
