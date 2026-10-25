#!/bin/bash
# Offline source update generated from the code-only delivery manifest.
set -eu
patch_root="$(cd "$(dirname "$0")" && pwd -P)"
files=(
@FILE_LIST@
)
hashes=(
@HASH_LIST@
)
backup=""
started=0
existed=()
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
      if [ "${existed[$i]}" -eq 1 ]; then
        cp "$backup/${files[$i]}" "$project/${files[$i]}" || printf 'Не восстановлен %s; резервная копия: %s\n' "${files[$i]}" "$backup" >&2
      else
        rm -f "$project/${files[$i]}"
      fi
    done
    printf 'Обновление остановлено; прежние исходники восстановлены.\n' >&2
  fi
  exit "$code"
}
trap on_exit EXIT
for i in "${!files[@]}"; do
  if ! hash_matches "$patch_root/${files[$i]}" "${hashes[$i]}"; then
    printf 'Неполное или повреждённое обновление: %s\n' "${files[$i]}" >&2
    exit 1
  fi
done
if [ "$#" -gt 1 ]; then printf 'Использование: bash Install_Update.command [папка Unity-проекта]\n' >&2;exit 1;fi
if [ "$#" -eq 1 ]; then project="$1";else
  printf 'Сначала закрой Unity. Выбери папку своего проекта с Assets и ProjectSettings.\n'
  project="$(osascript -e 'POSIX path of (choose folder with prompt "Выбери свой Unity-проект с Assets, Packages и ProjectSettings. Сначала закрой Unity.")')"
fi
if [ -z "$project" ] || [ ! -d "$project" ]; then printf 'Проект не выбран.\n' >&2;exit 1;fi
project="$(cd "$project" && pwd -P)"
if [ "$project" = "$patch_root" ] || [ ! -f "$project/ProjectSettings/ProjectVersion.txt" ] || [ ! -d "$project/Assets" ] || [ ! -f "$project/LocalPackages/com.silence.multiplayer/package.json" ]; then
  printf 'Выбери существующий Unity-проект SILENCE, а не папку обновления.\n' >&2;exit 1
fi
backup="$project/Library/Silence/SourceBackups/Update-@VERSION@-$(date -u '+%Y%m%d-%H%M%S')-$$"
mkdir -p "$backup"
for i in "${!files[@]}"; do
  if [ -f "$project/${files[$i]}" ]; then
    existed[$i]=1
    mkdir -p "$(dirname "$backup/${files[$i]}")"
    cp "$project/${files[$i]}" "$backup/${files[$i]}"
  else
    existed[$i]=0
    printf '%s\n' "${files[$i]}" >> "$backup/new-files.txt"
  fi
done
started=1
for i in "${!files[@]}"; do
  mkdir -p "$(dirname "$project/${files[$i]}")"
  cp "$patch_root/${files[$i]}" "$project/${files[$i]}"
  if ! hash_matches "$project/${files[$i]}" "${hashes[$i]}";then printf 'Ошибка записи: %s\n' "${files[$i]}" >&2;exit 1;fi
done
printf 'SILENCE @VERSION@\nProject: %s\nBackup: %s\nFiles: %s\n' "$project" "$backup" "${#files[@]}" > "$project/Library/Silence/UpdateInstalled.txt"
started=0
printf '\nУстановлено SILENCE @VERSION@: %s проверенных файлов.\nПроект: %s\nBackup: %s\n' "${#files[@]}" "$project" "$backup"
printf 'Импортированные ассеты и .meta сохранены. Открой этот проект в Unity, дождись компиляции.\nДля исправления рук: Play в нынешнем мире; создавать новую копию не нужно.\nПодключение новых ассетов при необходимости: SILENCE → 28 Imported Textures and Human Motions.\n'
