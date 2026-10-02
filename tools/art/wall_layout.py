"""Preview-only straight-wall layout in tile coordinates; no combat or economy numbers."""
import math
from dataclasses import asdict, dataclass


@dataclass(frozen=True)
class WallPlan:
    requested_length: float
    length: float
    depth: float
    valid: bool
    reason: str
    spans: tuple

    def to_dict(self):
        return asdict(self)


def plan_wall(requested_length, *, depth=2., minimum_length=2.,
              map_end=math.inf, obstacles=(), safety_gap=.05):
    """Plan along local +X; obstacle rectangles use wall-local tile coordinates.

    Footprints cover the entire strip including both end piers. Stop at the first
    obstacle intersecting the strip, never skip over a blocked gap to place more wall.
    This art preview does not implement Unity terrain queries, build costs or health.
    """
    if not math.isfinite(requested_length) or requested_length <= 0:
        raise ValueError("Requested wall length must be finite and positive")
    if depth <= 0 or minimum_length <= 0 or safety_gap < 0 or math.isnan(map_end):
        raise ValueError("Invalid wall footprint settings")
    length = min(requested_length, map_end)
    reason = "requested endpoint" if length == requested_length else "map edge"
    for x_min, y_min, x_max, y_max in obstacles:
        if x_min > x_max or y_min > y_max:
            raise ValueError("Obstacle bounds must be ordered")
        if y_max <= -depth/2 or y_min >= depth/2 or x_max <= 0:
            continue
        stop = max(0., x_min-safety_gap)
        if stop < length:
            length, reason = stop, "obstacle clearance"
    length = max(0., length)
    if length < minimum_length-1e-6:
        return WallPlan(requested_length, length, depth, False, "insufficient space for two intact end piers", ())
    # Logical damage/construction spans are independent of decorative mesh repeats.
    count = max(1, math.ceil((length-1e-6)/2))
    spans = tuple((index*2., min(2., length-index*2.)) for index in range(count))
    return WallPlan(requested_length, length, depth, True, reason, spans)


def verify_layout():
    """Cover fractional endpoints, strip-wide obstacle checks and too-short remainders."""
    for length in (2., 3., 6., 7.3):
        plan = plan_wall(length)
        assert plan.valid and abs(sum(width for _, width in plan.spans)-length) < 1e-6
    edge = plan_wall(8, map_end=7.3)
    assert edge.valid and edge.length == 7.3
    forest = plan_wall(8, obstacles=((5.7, .75, 8, 2),))
    assert forest.valid and abs(forest.length-5.65) < 1e-6
    assert plan_wall(8, obstacles=((5, 1.01, 8, 2),)).length == 8
    assert not plan_wall(8, obstacles=((1.9, -.5, 3, .5),)).valid
    assert not plan_wall(8, obstacles=((-1, -.1, .5, .1),)).valid
    assert not plan_wall(8, map_end=1.9).valid
    assert plan_wall(8, obstacles=((6, -.5, 7, .5), (4, -.5, 5, .5))).length == 3.95
    return {"passed": True, "cases": 11, "scope": "art footprint preview, not Unity gameplay"}
