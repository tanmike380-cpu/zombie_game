"""Reopen the saved master and verify protected originals and consolidated clips."""
import hashlib
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0,str(Path(__file__).parent))
from consolidate_runtime_actions import ROOT,MASTER,GROUP,get_signature
from rectify_gate_tower import GALLERY,PLAYABLE,verify_joint_rays


def main():
    if not bpy.app.background:
        raise RuntimeError("Background verification only")
    action_report=json.loads((ROOT/"Builds/ArtReview/RuntimeActionConsolidation/audit.json").read_text())
    tower_report=json.loads((ROOT/"Builds/ArtReview/SolidDefenseTowers/verification.json").read_text())
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/action_report["backup"]))
    protected={obj.name:get_signature(obj) for obj in bpy.context.scene.objects}
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    for name,signature in protected.items():
        if get_signature(bpy.data.objects[name])!=signature:
            raise AssertionError("Saved master changed an existing object: "+name)
    for record in action_report["records"]:
        if bpy.data.collections.get(record["collection"]) is None:
            raise AssertionError("Missing reference collection: "+record["collection"])
        for name in record["all_actions"]:
            if bpy.data.actions.get(name) is None:
                raise AssertionError("Missing action: "+name)
    rays=sum(verify_joint_rays(collection) for collection in bpy.data.collections[GALLERY].children
             if any("tower_start_cell" in obj for obj in collection.objects))
    report={"master_blend":str(MASTER.relative_to(ROOT)),
            "master_sha256":hashlib.sha256(MASTER.read_bytes()).hexdigest(),
            "protected_objects_verified":len(protected),"original_user_objects":len(bpy.data.collections["Collection"].all_objects),
            "scene_objects":len(bpy.context.scene.objects),"reference_collection":GROUP,
            "reference_groups":len(action_report["records"]),"actions_in_master":len(bpy.data.actions),
            "rig_clips":sum(len(record["clips"]) for record in action_report["records"]),
            "archived_rig_clips":sum(clip["archived"] for record in action_report["records"] for clip in record["clips"]),
            "solid_tower_collection":"防御塔 · 实心方正石墩","playable_collection":PLAYABLE,
            "playable_meshes":sum(obj.type=="MESH" for obj in bpy.data.collections[PLAYABLE].objects),
            "sealed_join_rays":rays,"arrow_tower_status":"Missing authored source; no replacement invented",
            "tower_verification":tower_report,"animation_references":action_report["records"],
            "animation_backup":action_report["backup"]}
    (ROOT/"art/master_art_handoff.json").write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print("MASTER REOPEN QA PASS",json.dumps({key:value for key,value in report.items() if key not in {"animation_references","tower_verification"}},ensure_ascii=False))


if __name__=="__main__":
    main()
