#!/usr/bin/env bash
# heic2png.sh – převod HEIC/HEIF na PNG
# Použití: ./heic2png.sh [složka] [výstupní_složka]
#   ./heic2png.sh                  # aktuální složka, PNG vedle originálů
#   ./heic2png.sh ~/Fotky out/     # výstup do složky out/

set -euo pipefail

SRC="${1:-.}"
DST="${2:-$SRC}"

# Zjištění dostupného nástroje
if command -v magick >/dev/null 2>&1; then
    convert_cmd() { magick "$1" "$2"; }
elif command -v convert >/dev/null 2>&1; then
    convert_cmd() { convert "$1" "$2"; }
elif command -v heif-convert >/dev/null 2>&1; then
    convert_cmd() { heif-convert "$1" "$2" >/dev/null; }
else
    echo "Chybí nástroj. Nainstaluj: sudo pacman -S imagemagick libheif" >&2
    exit 1
fi

mkdir -p "$DST"

shopt -s nullglob nocaseglob
files=("$SRC"/*.heic "$SRC"/*.heif)

if [ ${#files[@]} -eq 0 ]; then
    echo "Žádné HEIC/HEIF soubory v '$SRC'." >&2
    exit 1
fi

ok=0; fail=0
for f in "${files[@]}"; do
    out="$DST/$(basename "${f%.*}").png"
    if convert_cmd "$f" "$out"; then
        echo "✔ $(basename "$f") → $(basename "$out")"
        ok=$((ok + 1))
    else
        echo "✘ $(basename "$f")" >&2
        fail=$((fail + 1))
    fi
done

echo "Hotovo: $ok OK, $fail chyb."

