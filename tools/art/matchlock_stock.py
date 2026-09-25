"""Authorized wood-only refit: full fore-end and a dropped shoulder stock."""
import bpy
from mathutils import Vector
from human_hands import GUN_AXIS, GUN_ORIGIN

STOCK_NAME = 'veteran_matchlock_carved_stock'


def refit_stock(preset):
    """Preserve receiver/grip landmarks; extend only forward wood and lower the butt."""
    settings = preset.get('stock_refit')
    if not settings:
        return
    stock = bpy.data.objects[STOCK_NAME]
    up = Vector((-GUN_AXIS.z, 0, GUN_AXIS.x))
    inverse = stock.matrix_world.inverted()
    for vertex in stock.data.vertices:
        point = stock.matrix_world @ vertex.co
        along = (point-GUN_ORIGIN).dot(GUN_AXIS)
        forward = max(0, (along-.40)/(1.06-.40))
        drop = max(0, min(1, (.24-along)/.14))
        drop = drop*drop*(3-2*drop)
        point += GUN_AXIS*(forward*(settings['foreend_end_m']-1.06))
        point -= up*(drop*settings['butt_drop_m'])
        vertex.co = inverse @ point
    stock.data.update()


def get_stock_coordinates():
    return [tuple(vertex.co) for vertex in bpy.data.objects[STOCK_NAME].data.vertices]


def verify_weapon_fit(name, preset):
    """Validate full wood length, separation of grips, and shoulder contact region."""
    stock = bpy.data.objects[STOCK_NAME]
    axis = Vector(preset['gun_axis']).normalized()
    origin = Vector(preset['gun_origin'])
    points = [stock.matrix_world @ vertex.co for vertex in stock.data.vertices]
    length = max((point-origin).dot(axis) for point in points)
    if abs(length-preset['stock_refit']['foreend_end_m']) > .002 or 1.5-length > .02:
        raise ValueError(f'{name}: fore-end no longer reaches the muzzle')
    gap = preset['grip_distance_m']['support']-preset['grip_distance_m']['trigger']
    if gap < .26:
        raise ValueError(f'{name}: supporting hand is still too close to the trigger hand')
    report = {'foreend_m': round(length, 4), 'grip_separation_m': round(gap, 4)}
    if name == 'aim':
        butt = [point for point in points if (point-origin).dot(axis) < .006]
        center = sum(butt, Vector())/len(butt)
        shoulder = bpy.data.objects['Equipment | editable holding pose rig'].pose.bones['upper.-1'].head
        distance = (center-shoulder).length
        if distance > .115:
            raise ValueError(f'Aiming butt is outside the shoulder contact region: {distance:.3f}m')
        report['butt_to_right_shoulder_center_m'] = round(distance, 4)
    return report
