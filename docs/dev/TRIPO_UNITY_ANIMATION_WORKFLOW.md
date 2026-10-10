# Tripo models → Unity weapon assembly and combat animation

Decision updated 2026-10-10. This is an approved production direction, not an animation test result.

## Responsibilities

- Tripo: character/weapon geometry, UV/material generation and initial character skinning. Keep useful existing locomotion clips, especially zombie idle/walk.
- Blender: preserve all accepted models in the registered master; repair geometry, UV, broken skin weights and necessary deformable weapon parts. Do not rebuild accepted bodies or rigs just to change the workflow.
- Unity: assemble independent approved weapons with character bones, retarget clips, prototype complex combat motions with AI, correct grip/aim through constraints, and synchronize projectile, sound and VFX events. The user's latest instruction allows this animation authoring in Unity rather than requiring every attack to be authored in Blender first. This is not permission to invent substitute models or distort source meshes.
- Preserve the original clips and document generated `.anim` files, Avatar, weapon Prefab, grip offsets and provenance in the repository. Unity-authored clips are explicit derivative assets, not an unrecorded competing model master.

## Verified tools and limits

Unity's official Animation Generator supports Text to Motion and Video to Motion and outputs clips assigned through an Animator Controller. The reviewed documentation is Assistant **2.20.0-pre.2**, a prerelease, not a package installed or tested in this project. [Official animation overview](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.20/manual/Animation/animation-overview.html)

Generation from reference video requires one person, full visible body/feet and a stable camera without cuts or zooms. Use a video we own or are authorized to process, not an assumed download right for a game clip. The generation screen can refine direction and looping, but quality still requires review. [Create animation](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.20/manual/Animation/animation-create.html)

The AI menu requires a supported editor, a Unity Cloud-linked project and the user's acceptance of AI terms. Installed editor is 6000.6.1f1; `Packages/manifest.json` currently has neither Animation Rigging nor the AI Assistant package. `com.unity.modules.ai` is not proof that the animation generator is active. No account setting, terms acceptance, package installation, credits or purchase was performed during this research. [AI menu requirements](https://docs.unity.com/en-us/engine/6000.6/manual/unity-ai/ai-menu-access)

Unity can attach a rigid weapon by a bone socket or parent constraint. Two Bone IK can place a hand at a target and use an elbow hint; these are rigging controls, **not** AI generation. They do not repair broken skin weights. [Parent constraint](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/constraints/MultiParentConstraint.html) · [Two Bone IK](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/constraints/TwoBoneIKConstraint.html)

## Proposed first acceptance sample

1. Use one approved T-pose archer and one independently generated bow. Verify Avatar mapping, proportions and shoulders before adding a weapon.
2. Attach bow to the left-hand socket. Set draw-hand and elbow targets. Do not make a weapon follow a hand that simultaneously follows that weapon in a constraint cycle.
3. Generate or reuse draw/aim/release/recover body motion. Review the string pulling toward the face rather than chest, foot contact and full cycle; AI body motion alone does not implement the bow.
4. Bow limbs/string require a separate deformable rig or suitable blend shapes. Keep an arrow as a separate renderable, hide the held arrow when released, and spawn the actual projectile on the authored release timing. Regeneration of the whole soldier is not required.
5. Crossbow: rigid stock plus separate string/bolt and reload controls. Matchlock: right-hand weapon socket, left-hand grip target, aim/fire/reload events. Repeating crossbow: moving lever/feeding parts. Do not reuse one attack clip indiscriminately for every weapon.
6. Test idle→attack, consecutive shots, movement interruptions, target rotation, empty ammo, three ranks and normal RTS camera distance. Reject first-shot twitch, broken hands, invisible repeat arrows and floating feet.

The hand/weapon correction setup is a design inference from Unity's constraints, not a claim that the AI automatically recognizes and solves every prop interaction. AI generation can be evaluated first; a reusable licensed clip or authored correction remains a fallback when it fails.

## Performance boundary

Reuse compatible Humanoid clips across ranks. Prefer baking accepted corrections where practical rather than running multiple full IK solvers on every one of 10,000 units. Use animation visibility/LOD policy while combat simulation continues off-screen. Validate actual frame times before choosing the bake/export path; no FPS improvement is claimed here. [Unity animation optimization](https://docs.unity.com/en-us/engine/6000.0/manual/animation-section/animation-mecanim/mecanim-peformanceand-optimization)

---

# 中文摘要

最新路线：Tripo 做人物／武器模型和初始骨骼蒙皮，保留可用的走路、待机；Unity 做独立武器装配、复杂攻击动作生成／重定向与握持修正。Blender 仍保存正式模型总文件并处理网格、UV、蒙皮和必要的弓弦／机构分件，不强制先做每一条攻击动作。

Unity 官方确有文字／视频生成动作的工具，但“生成人体动作”和“自动把弓、弓弦、箭、双手与伤害时机全部做对”不是同一件事。先做一个弓箭手样本，再复用到同兵种三级外观。当前只是路线与工具核实，尚未启用 Unity AI、消耗积分或完成射击样本。

人物、武器概念图独立；武器正／侧／背也分成三张，不拼图。AI 多视图不是机械施工图，模型生成后仍需检查武器结构、方向、握点和人物比例。现有8张武器单图保留为旧版参考；新版另存到概念目录，与人物共用总册入口。
