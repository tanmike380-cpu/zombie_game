#!/bin/zsh
# Launch the original-model 400 crossbow versus 4000 exploder fixture.
set -eu
project_root="$(cd "$(dirname "$0")/../.." && pwd)"
review_app="$project_root/Builds/TripoArcher/TripoCrossbow.app"
if [[ ! -d "$review_app" ]]; then
    print 'Build first in Unity: Tools > Zombie Game > Characters > Build Tripo Crossbow Battle'
    exit 1
fi
open -a "$review_app" --args -tripoExploders -tripoPaused -screen-width 1440 -screen-height 900 -screen-fullscreen 0
