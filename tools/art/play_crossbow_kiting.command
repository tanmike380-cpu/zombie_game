#!/bin/zsh
# Play the isolated 400-versus-2000 fixture. No main game scene is changed.
set -eu
project_root="$(cd "$(dirname "$0")/../.." && pwd)"
review_app="$project_root/Builds/TripoArcher/TripoCrossbow.app"
if [[ ! -d "$review_app" ]]; then
    print 'Build first in Unity: Tools > Zombie Game > Characters > Build Tripo Crossbow Battle'
    exit 1
fi
open -a "$review_app" --args -tripoKiting -tripoPaused -screen-width 1440 -screen-height 900 -screen-fullscreen 0
