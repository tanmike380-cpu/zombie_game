"""Blender review-only passage policy in grid cells, not a Unity NavMesh replacement."""
from dataclasses import dataclass


@dataclass(frozen=True)
class GatePassage:
    """Two-cell square with a north/south friendly-only centre corridor."""
    half_extent: float = 1.0
    passage_half_width: float = .36

    def can_cross(self, faction, x, radius):
        """Require the entire actor to fit the central lane; enemies never qualify."""
        return faction == "friendly" and radius > 0 and abs(x)+radius <= self.passage_half_width

    def advance(self, faction, start, finish, radius):
        """Sweep one demonstration move against a padded square; never tunnel through."""
        if self.can_cross(faction, start[0], radius) and start[0] == finish[0]:
            return finish
        extent = self.half_extent+radius
        enter, leave = 0., 1.
        for origin, target in zip(start, finish):
            delta = target-origin
            if abs(delta) < 1e-9:
                if abs(origin) >= extent:
                    return finish
                continue
            first, last = sorted(((-extent-origin)/delta, (extent-origin)/delta))
            enter, leave = max(enter, first), min(leave, last)
            if enter > leave:
                return finish
        if leave < 0 or enter > 1:
            return finish
        fraction = max(0., enter-1e-5)
        return tuple(a+(b-a)*fraction for a, b in zip(start, finish))


def verify_passage():
    """Exercise direction, faction, width and fast crossing without game balance changes."""
    gate = GatePassage()
    checks = [gate.advance("friendly", (0,-2), (0,2), .14) == (0,2),
              gate.advance("friendly", (0,2), (0,-2), .14) == (0,-2),
              gate.advance("zombie", (0,-2), (0,2), .14)[1] < -1,
              gate.advance("zombie", (0,2), (0,-2), .14)[1] > 1,
              gate.advance("friendly", (-2,0), (2,0), .14)[0] < -1,
              gate.advance("friendly", (.6,-2), (.6,2), .14)[1] < -1,
              not gate.can_cross("friendly", 0, .4),
              gate.advance("zombie", (2,-2), (2,2), .14) == (2,2),
              not gate.can_cross("zombie", 0, .01)]
    assert all(checks), "Gate passage fixture failed"
    return {"passed": True, "cases": len(checks), "scope": "Blender geometric fixture, not Unity pathfinding"}
