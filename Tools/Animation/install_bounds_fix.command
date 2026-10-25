#!/bin/bash
# Packaged by build_update.py with exact hashes of the two shipped source files.
# macOS /bin/bash 3.2 compatible. No downloads, sudo, .meta or scene changes.
set -eu
patch_root="$(cd "$(dirname "$0")" && pwd -P)"
files=(
  "LocalPackages/com.silence.multiplayer/Editor/Visual/CharacterReworkWorkshop.cs"
  "Assets/_Silence/Scripts/Core/SkinBoundsModel.cs"
)
hashes=("@WORKSHOP_SHA256@" "@BOUNDS_SHA256@")
backup=""
started=0

hash_matches() {
  [ -f "$1" ] || return 1
  local actual
  actual="$(shasum -a 256 "$1")" || return 1
  [ "${actual%% *}" = "$2" ]
}

on_exit() {
  local code=$?
  trap - EXIT
  if [ "$code" -ne 0 ] && [ "$started" -eq 1 ]; then
    local i
    for i in "${!files[@]}"; do
      cp "$backup/${files[$i]}" "$project/${files[$i]}" || printf 'Не удалось восстановить %s; копия: %s\n' "${files[$i]}" "$backup" >&2
    done
    printf 'Установка остановлена; прежний код восстановлен из %s\n' "$backup" >&2
  fi
  exit "$code"
}
trap on_exit EXIT

for i in "${!files[@]}"; do
  if ! hash_matches "$patch_root/${files[$i]}" "${hashes[$i]}"; then
    printf 'Повреждена или не полностью распакована папка обновления: %s\n' "${files[$i]}" >&2
    exit 1
  fi
done

if [ "$#" -gt 1 ]; then
  printf 'Использование: bash Install_Bounds_Fix.command [папка Unity-проекта]\n' >&2
  exit 1
fi
if [ "$#" -eq 1 ]; then
  project="$1"
else
  printf 'Сначала закрой Unity. Выбери папку СВОЕГО Unity-проекта.\n'
  project="$(osascript -e 'POSIX path of (choose folder with prompt "Выбери Unity-проект: внутри Assets, Packages и ProjectSettings. Сначала закрой Unity.")')"
fi
if [ -z "$project" ] || [ ! -d "$project" ]; then
  printf 'Папка Unity-проекта не выбрана.\n' >&2
  exit 1
fi
project="$(cd "$project" && pwd -P)"
if [ "$project" = "$patch_root" ] || [ ! -f "$project/ProjectSettings/ProjectVersion.txt" ] || [ ! -d "$project/Assets" ]; then
  printf 'Это не Unity-проект. Выбери папку с Assets и ProjectSettings, а не распакованное обновление.\n' >&2
  exit 1
fi
for i in "${!files[@]}"; do
  if [ ! -f "$project/${files[$i]}" ]; then
    printf 'В этом проекте не найден ожидаемый файл: %s\n' "${files[$i]}" >&2
    exit 1
  fi
done

backup="$project/Library/Silence/SourceBackups/BoundsFix-0.4.9-$(date -u '+%Y%m%d-%H%M%S')-$$"
# Back up both originals before the first write. Existing .meta stay untouched.
for i in "${!files[@]}"; do
  mkdir -p "$(dirname "$backup/${files[$i]}")"
  cp "$project/${files[$i]}" "$backup/${files[$i]}"
done
started=1
for i in "${!files[@]}"; do
  cp "$patch_root/${files[$i]}" "$project/${files[$i]}"
  if ! hash_matches "$project/${files[$i]}" "${hashes[$i]}"; then
    printf 'Не прошла проверка записанного файла: %s\n' "${files[$i]}" >&2
    exit 1
  fi
done
{
  printf 'SILENCE Bounds Fix 0.4.9\nProject: %s\nBackup: %s\n' "$project" "$backup"
  for i in "${!files[@]}"; do
    printf '%s  %s\n' "${hashes[$i]}" "${files[$i]}"
  done
} > "$project/Library/Silence/BoundsFixInstalled.txt"
started=0
printf '\nУстановлена проверка bounds 0.4.9. Оба файла сверены по SHA-256.\nПроект: %s\nРезервная копия кода: %s\n' "$project" "$backup"
printf 'Открой ЭТОТ проект в Unity и дождись компиляции.\nВ SILENCE → 27 должна появиться надпись «Версия проверки: 0.4.9».\nНажми только «Проверить ссылки камеры и персонажа». Ещё одну копию мира создавать не нужно.\n'
