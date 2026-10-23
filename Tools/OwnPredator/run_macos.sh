#!/bin/bash
set -euo pipefail
predator_blender="/Applications/Blender.app/Contents/MacOS/Blender"
if [[ ! -x "$predator_blender" ]]; then
  predator_blender="$(command -v blender || true)"
fi
if [[ -z "$predator_blender" || ! -x "$predator_blender" ]]; then
  echo "Blender не найден. Открой Blender и используй команду Python Console из инструкции." >&2
  exit 1
fi
predator_source_url="${1:-https://raw.githubusercontent.com/alpachino09/horrorsilenceplace/codex/original-predator/Tools/OwnPredator/create_predator.py}"
predator_script="${TMPDIR:-/tmp}/SilencePredator_Generator_$$.py"
curl --fail --location --proto '=https' --proto-redir '=https' "$predator_source_url" --output "$predator_script"
"$predator_blender" --python "$predator_script"
