"""Preview-only straight-wall layout in tile coordinates; no combat or economy numbers."""
import math
from dataclasses import asdict, dataclass


@dataclass(frozen=True)
class WallPlan:
    requested_length: int
    length: int
    depth: int
    valid: bool
    reason: str
    spans: tuple

    def to_dict(self):
        return asdict(self)


def require_integer(value, field):
    """Validate authored cell coordinates; mouse snapping belongs to future runtime input."""
    if isinstance(value, bool) or not math.isfinite(value) or value != int(value):
        raise ValueError(f"{field} must be a finite integer cell coordinate: {value}")
    return int(value)


def plan_wall(requested_length, *, map_end=None, obstacles=()):
    """Plan along local +X; obstacle rectangles use wall-local tile coordinates.

    Footprints cover the entire strip including both end piers. Stop at the first
    obstacle intersecting the strip, never skip over a blocked gap to place more wall.
    This art preview does not implement Unity terrain queries, build costs or health.
    """
    requested_length = require_integer(requested_length, "requested_length")
    if requested_length <= 0:
        raise ValueError("Requested wall length must be positive")
    depth, minimum_length = 2, 2
    length = requested_length if map_end is None else min(requested_length, require_integer(map_end, "map_end"))
    reason = "requested endpoint" if length == requested_length else "map edge"
    for x_min, y_min, x_max, y_max in obstacles:
        x_min, y_min, x_max, y_max = (require_integer(value, "obstacle bound")
                                    for value in (x_min, y_min, x_max, y_max))
        if x_min > x_max or y_min > y_max:
            raise ValueError("Obstacle bounds must be ordered")
        if y_max <= -depth/2 or y_min >= depth/2 or x_max <= 0:
            continue
        stop = max(0, x_min)
        if stop < length:
            length, reason = stop, "obstacle clearance"
    length = max(0, length)
    if length < minimum_length:
        return WallPlan(requested_length, length, depth, False, "insufficient space for two intact end piers", ())
    # Odd runs may end in a one-cell internal span, never a standalone 2x1 wall.
    spans = tuple((start, min(2, length-start)) for start in range(0, length, 2))
    return WallPlan(requested_length, length, depth, True, reason, spans)


def verify_layout():
    """Cover every integer length through 256, odd runs, cell obstacles and invalid input."""
    cases = 0
    for length in range(2, 257):
        plan = plan_wall(length)
        assert plan.valid and sum(width for _, width in plan.spans) == length
        assert plan.length == length and all(isinstance(width, int) for _, width in plan.spans)
        cases += 1
    checks = [plan_wall(8, map_end=7).length == 7,
              plan_wall(8, obstacles=((6, 0, 7, 1),)).length == 6,
              plan_wall(8, obstacles=((5, 1, 6, 2),)).length == 8,
              not plan_wall(8, obstacles=((1, 0, 2, 1),)).valid,
              not plan_wall(8, obstacles=((-1, 0, 1, 1),)).valid,
              not plan_wall(8, map_end=1).valid,
              not plan_wall(1).valid,
              plan_wall(8, obstacles=((6, 0, 7, 1), (4, -1, 5, 0))).length == 4]
    assert all(checks)
    cases += len(checks)
    for request, kwargs in ((7.3, {}), (8, {"map_end": 7.3}),
                            (8, {"obstacles": ((5.7, 0, 6, 1),)}), (float("nan"), {})):
        try:
            plan_wall(request, **kwargs)
        except ValueError:
            cases += 1
        else:
            raise AssertionError("Non-integer placement was accepted")
    return {"passed": True, "cases": cases, "scope": "art footprint preview, not Unity gameplay"}


def plan_tower_slots(wall, slots):
    """Replace integer 2x2 middle cells, reserving each end pier's occupied grid cell."""
    placements = []
    if not wall.valid:
        return {"valid": False, "reason": "wall is not placeable", "slots": []}
    for kind, start in slots:
        start = require_integer(start, "tower start cell")
        if kind not in ("cannon", "arrow", "flame"):
            raise ValueError(f"Unsupported wall-mounted tower type: {kind}")
        if start < 1 or start+2 > wall.length-1:
            return {"valid": False, "reason": "end-pier cell is protected", "slots": []}
        if any(start < previous["end"] and start+2 > previous["start"] for previous in placements):
            return {"valid": False, "reason": "tower footprint already occupied", "slots": []}
        placements.append({"kind": kind, "start": start, "end": start+2, "depth": 2})
    return {"valid": True, "reason": "replace wall body; retain end piers",
            "slots": sorted(placements,key=lambda entry: entry["start"])}


def verify_tower_slots():
    """All three tower types share the same integer replacement and endpoint rules."""
    cases = 0
    for kind in ("cannon", "arrow", "flame"):
        for length in range(2,33):
            for start in range(-1,length+1):
                result = plan_tower_slots(plan_wall(length),((kind,start),))
                assert result["valid"] == (1 <= start and start+2 <= length-1)
                cases += 1
    assert plan_tower_slots(plan_wall(8),(("arrow",1),("flame",3),("cannon",5)))["valid"]
    assert not plan_tower_slots(plan_wall(8),(("arrow",2),("flame",3)))["valid"]
    assert not plan_tower_slots(plan_wall(8),(("cannon",2),("cannon",2)))["valid"]
    try:
        plan_tower_slots(plan_wall(8),(("cannon",2.5),))
    except ValueError:
        pass
    else:
        raise AssertionError("Fractional tower placement accepted")
    return {"passed": True, "cases": cases+4, "scope": "Blender integer occupancy preview, not Unity construction"}
