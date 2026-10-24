#!/bin/bash
set -euo pipefail
task_root="$(cd "$(dirname "$0")/../.." && pwd)"
task_blender="${SILENCE_BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}"
if [ ! -x "$task_blender" ]; then
  echo 'Blender not found. Set SILENCE_BLENDER to its executable.' >&2
  exit 1
fi
task_stamp="$(date -u +%Y%m%d-%H%M%S)"
task_log="$task_root/artifacts/HoodedGirl/$task_stamp"
mkdir -p "$task_log"
export SILENCE_REPOSITORY="$task_root"
if [ -d "$task_root/ArtSource/HoodedGirl" ]; then
  cp -R "$task_root/ArtSource/HoodedGirl" "$task_log/SourceBackup"
fi
if [ -d "$task_root/Assets/Characters/HoodedGirl/Models" ]; then
  cp -R "$task_root/Assets/Characters/HoodedGirl/Models" "$task_log/ModelsBackup"
fi
"$task_blender" --background --factory-startup --python-exit-code 1 --python-expr 'import bpy; assert bpy.app.version >= (5, 2, 0), "Use Blender 5.2 or newer"' > "$task_log/version.log" 2>&1
for task_script in build_character dress_character rig_character animate_character refine_surface export_character verify_character render_review; do
  echo "SILENCE: $task_script"
  if ! "$task_blender" --background --factory-startup --python-exit-code 1 --python "$task_root/Tools/HoodedGirl/$task_script.py" > "$task_log/$task_script.log" 2>&1; then
    tail -n 8 "$task_log/$task_script.log" >&2
    exit 1
  fi
done
echo "Saved: ArtSource/HoodedGirl/HoodedGirl.blend. Logs: $task_log"
