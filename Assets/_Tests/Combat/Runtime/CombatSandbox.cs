using System;
using ZombieGame.Balance;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame.CombatTests
{
    public sealed class CombatSandbox : MonoBehaviour
    {
        public static float ATTACK_RANGE => UnitBalance.human.attack_range;
        public static float HUMAN_SIGHT => UnitBalance.config.human_sight;
        public static float NOISE_RADIUS => UnitBalance.human_noise(UnitBalance.human);
        public const int FRIENDLY_COUNT = 8;
        public const int RUNNER_COUNT = 36;
        public const int EXPLODER_COUNT = 6;
        public const int ZOMBIE_COUNT = RUNNER_COUNT + EXPLODER_COUNT;
        public static float BLAST_RADIUS => UnitBalance.exploder.explosion_radius;
        public static float BLAST_DAMAGE => UnitBalance.exploder.damage;
        public Material material_template;
        public bool running_checks;
        public readonly List<CombatActor> actors = new List<CombatActor>();
        public string notice = "Select blue Shenji, then right-click to move. A + left-click to attack.";
        public int shots, hits, bites, heard, kills, blasts;
        public int blast_visuals_created;
        private NavMeshData nav_data;
        private NavMeshDataInstance nav_instance;
        private GameObject world;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Arrow> arrows = new List<Arrow>();
        private readonly List<Pulse> pulses = new List<Pulse>();
        private Material blue, red, purple, stone, green, yellow, dark;
        private sealed class Arrow { public Transform visual; public CombatActor victim, shooter; }
        private sealed class Pulse { public Vector3 origin; public float time, radius; public LineRenderer ring; }
        private ZombieGame.Noise.NoiseTimeline noise;

        private void Start()
        {
            Application.runInBackground = true;
            create_materials();
            reset_battle();
            gameObject.AddComponent<RtsInput>().game = this;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-combatSmoke") >= 0)
            {
                running_checks = true;
                gameObject.AddComponent<CombatSmokeTest>().game = this;
            }
        }

        private Material make_material(Color color)
        {
            var material = new Material(material_template);
            material.SetColor("_Color", color);
            materials.Add(material);
            return material;
        }

        private void create_materials()
        {
            if (material_template == null) throw new InvalidOperationException("Combat material missing");
            blue = make_material(new Color(.15f, .6f, 1));
            red = make_material(new Color(.75f, .15f, .09f));
            purple = make_material(new Color(.95f, .15f, .85f));
            stone = make_material(new Color(.30f, .34f, .4f));
            green = make_material(new Color(.15f, 1, .4f));
            yellow = make_material(new Color(1, .8f, .15f));
            dark = make_material(new Color(.10f, .14f, .16f));
        }

        public void reset_battle()
        {
            if (world != null) { world.SetActive(false); Destroy(world); }
            if (nav_instance.valid) nav_instance.Remove();
            if (nav_data != null) Destroy(nav_data);
            actors.Clear(); arrows.Clear(); pulses.Clear();
            shots = hits = bites = heard = kills = blasts = blast_visuals_created = 0;
            world = new GameObject("Combat Test World");
            var sources = new List<NavMeshBuildSource>();
            create_box("Ground", new Vector3(0, -.5f, 0), new Vector3(32, 1, 24), dark, false);
            sources.Add(box_source(new Vector3(0, -.5f, 0), new Vector3(32, 1, 24), 0));
            foreach (float z in new[] { -5f, 5f })
            {
                float height = z < 0 ? .6f : 2;
                create_box(z < 0 ? "Low Wall" : "High Wall", new Vector3(0, height / 2, z), new Vector3(.7f, height, 5), stone, true);
                sources.Add(box_source(new Vector3(0, height / 2, z), new Vector3(.7f, height, 5), 1));
            }
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .3f; settings.agentHeight = 1.4f; settings.agentClimb = .2f;
            settings.overrideVoxelSize = true; settings.voxelSize = .1f;
            nav_data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Vector3.zero, new Vector3(34, 8, 26)), Vector3.zero, Quaternion.identity);
            if (nav_data == null) throw new InvalidOperationException("Combat NavMesh bake failed");
            nav_instance = NavMesh.AddNavMeshData(nav_data);
            for (int i = 0; i < FRIENDLY_COUNT; i++) spawn_actor(new Vector3(-8 - i / 4 * 1.5f, 0, -3 + i % 4 * 2), true);
            for (int i = 0; i < RUNNER_COUNT; i++) spawn_actor(new Vector3(3 + i % 6 * 1.7f, 0, -4.25f + i / 6 * 1.7f), false);
            for (int i = 0; i < EXPLODER_COUNT; i++) spawn_actor(new Vector3(4, 0, -5 + i * 2), false, true);
            noise = new ZombieGame.Noise.NoiseTimeline(actors.Count, NOISE_RADIUS);
            Physics.SyncTransforms();
            notice = "Purple = EXPLODER. Focus fire or spread out! Magenta warning ring = blast radius 2.";
        }

        private static NavMeshBuildSource box_source(Vector3 center, Vector3 size, int area) =>
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.Translate(center), size = size, area = area };

        private GameObject create_box(string name, Vector3 position, Vector3 scale, Material material, bool wall)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name; box.transform.SetParent(world.transform);
            box.transform.position = position; box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (wall) box.layer = 8;
            else Destroy(box.GetComponent<Collider>());
            return box;
        }

        private void spawn_actor(Vector3 position, bool friendly, bool exploder = false)
        {
            var root = new GameObject(friendly ? "Shenji" : exploder ? "Exploder Zombie" : "Runner Zombie");
            root.transform.SetParent(world.transform); root.transform.position = position;
            var actor = root.AddComponent<CombatActor>();
            actor.exploder = exploder;
            actor.friendly = friendly; actor.max_health = actor.health = actor.stats.health;
            actor.agent = root.AddComponent<NavMeshAgent>();
            actor.agent.radius = .3f; actor.agent.height = 1.4f;
            actor.agent.speed = actor.stats.move_speed;
            actor.agent.acceleration = actor.stats.acceleration; actor.agent.updateRotation = false;
            actor.agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform, false); body.transform.localPosition = Vector3.up * .7f;
            body.transform.localScale = new Vector3(.6f, .7f, .6f);
            body.GetComponent<Renderer>().sharedMaterial = friendly ? blue : exploder ? purple : red;
            actor.selection_ring = create_ring(root.transform, .55f, green);
            actor.selection_ring.enabled = false;
            if (friendly)
            {
                var bow = create_box("Simple Musket", position, new Vector3(.12f, .12f, .85f), yellow, false);
                bow.transform.SetParent(root.transform, false); bow.transform.localPosition = new Vector3(.4f, .8f, .2f);
            }
            actors.Add(actor);
        }

        public LineRenderer create_ring(Transform parent, float radius, Material material)
        {
            var ring = new GameObject("Ring").AddComponent<LineRenderer>();
            ring.transform.SetParent(parent, false); ring.useWorldSpace = false;
            ring.sharedMaterial = material; ring.loop = true; ring.widthMultiplier = .045f; ring.positionCount = 64;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, .08f, Mathf.Sin(angle) * radius));
            }
            return ring;
        }

        public bool can_see(Vector3 from, Vector3 to) => !Physics.Linecast(from + Vector3.up, to + Vector3.up, 1 << 8);
        public static float distance(CombatActor a, CombatActor b) => Vector3.Distance(a.transform.position, b.transform.position);

        public bool issue_order(CombatActor actor, CombatOrder order, Vector3 point, CombatActor target = null)
        {
            if (!actor.friendly || !actor.alive) return false;
            if (order != CombatOrder.Stop)
            {
                if (!NavMesh.SamplePosition(point, out var hit, .6f, NavMesh.AllAreas)) return false;
                point = hit.position;
            }
            actor.halt(); actor.order = order; actor.target = target;
            actor.destination = point; actor.patrol_start = actor.transform.position; actor.patrol_end = point;
            if (order == CombatOrder.Move || order == CombatOrder.Patrol || order == CombatOrder.AttackMove)
                actor.walk_to(point, .15f);
            return true;
        }

        private void Update()
        {
            foreach (var actor in actors)
            {
                if (!actor.alive) continue;
                actor.selection_ring.enabled = actor.friendly && actor.selected;
                if (actor.friendly) update_archer(actor); else update_zombie(actor);
            }
            update_arrows(); update_noise();
        }

        private CombatActor nearest_enemy(CombatActor actor, float radius)
        {
            CombatActor best = null;
            foreach (var other in actors)
            {
                if (!other.alive || other.friendly == actor.friendly) continue;
                float length = distance(actor, other);
                if (length <= radius && can_see(actor.transform.position, other.transform.position)) { radius = length; best = other; }
            }
            return best;
        }

        private void update_archer(CombatActor actor)
        {
            if (actor.order == CombatOrder.Move) { follow_route(actor); return; }
            if (actor.order == CombatOrder.AttackTarget && (actor.target == null || !actor.target.alive))
            { actor.order = CombatOrder.Stop; actor.target = null; actor.halt(); return; }
            if (actor.target == null || !actor.target.alive || actor.order != CombatOrder.AttackTarget)
                actor.target = nearest_enemy(actor, actor.order == CombatOrder.Stop ? ATTACK_RANGE : HUMAN_SIGHT);
            if (actor.target != null && actor.target.alive)
            {
                bool visible = can_see(actor.transform.position, actor.target.transform.position);
                if (distance(actor, actor.target) <= ATTACK_RANGE && visible)
                {
                    actor.halt(); face_target(actor, actor.target.transform.position);
                    if (Time.time >= actor.next_attack) fire_arrow(actor, actor.target);
                    return;
                }
                if (actor.order != CombatOrder.Stop)
                { actor.walk_to(actor.target.transform.position, visible ? ATTACK_RANGE - .5f : .2f); return; }
            }
            if (actor.order == CombatOrder.AttackTarget) { actor.order = CombatOrder.Stop; actor.target = null; }
            follow_route(actor);
        }

        private void follow_route(CombatActor actor)
        {
            if (actor.order == CombatOrder.Stop) { actor.halt(); return; }
            if (Vector3.Distance(actor.transform.position, actor.destination) < .5f)
            {
                if (actor.order == CombatOrder.Patrol)
                    actor.destination = Vector3.Distance(actor.destination, actor.patrol_end) < .1f ? actor.patrol_start : actor.patrol_end;
                else { actor.order = CombatOrder.Stop; actor.halt(); return; }
            }
            actor.walk_to(actor.destination, .15f);
        }

        private void update_zombie(CombatActor actor)
        {
            if (actor.detonate_at >= 0)
            {
                actor.halt();
                if (Time.time >= actor.detonate_at) apply_damage(actor, actor.health);
                return;
            }
            var visible = nearest_enemy(actor, UnitBalance.config.zombie_sight);
            if (visible != null) { actor.target = visible; actor.has_memory = true; actor.memory_position = visible.transform.position; }
            else actor.target = null;
            actor.agent.autoBraking = actor.target == null;
            if (actor.exploder && actor.target != null && distance(actor, actor.target) < actor.stats.attack_range)
            {
                actor.halt(); actor.detonate_at = Time.time + actor.stats.fuse_seconds;
                actor.blast_ring = create_ring(actor.transform, BLAST_RADIUS, purple);
                actor.blast_ring.widthMultiplier = .12f;
                return;
            }
            if (actor.target != null && distance(actor, actor.target) < actor.stats.attack_range)
            {
                actor.halt(); face_target(actor, actor.target.transform.position);
                if (Time.time >= actor.next_attack)
                { actor.next_attack = Time.time + actor.stats.attack_interval; bites++; apply_damage(actor.target, actor.stats.damage); }
            }
            else if (actor.has_memory)
            {
                if (Vector3.Distance(actor.transform.position, actor.memory_position) < .5f) { actor.has_memory = false; actor.halt(); }
                else actor.walk_to(actor.memory_position, .25f);
            }
        }

        private void detonate_actor(CombatActor actor)
        {
            Vector3 origin = actor.transform.position;
            blasts++;
            foreach (var victim in actors)
            {
                if (!victim.friendly || !victim.alive) continue;
                Vector3 offset = victim.transform.position - origin; offset.y = 0;
                if (offset.sqrMagnitude <= BLAST_RADIUS * BLAST_RADIUS && can_see(origin, victim.transform.position))
                    apply_damage(victim, BLAST_DAMAGE);
            }
            var flash = create_ring(world.transform, 1, purple);
            flash.transform.position = origin;
            flash.gameObject.AddComponent<BlastVisual>().initialize(flash, BLAST_RADIUS);
            blast_visuals_created++;
            // Zombie explosions are visual/damage events only: never emit attraction noise.
        }

        private void face_target(CombatActor actor, Vector3 target)
        {
            Vector3 direction = target - actor.transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) actor.transform.rotation = Quaternion.LookRotation(direction);
        }

        private void fire_arrow(CombatActor actor, CombatActor victim)
        {
            actor.next_attack = Time.time + actor.stats.attack_interval; shots++;
            var arrow = create_box("Musket Tracer", actor.transform.position + Vector3.up, new Vector3(.07f, .07f, .65f), yellow, false);
            arrows.Add(new Arrow { visual = arrow.transform, victim = victim, shooter = actor });
            emit_noise(actor.transform.position, NOISE_RADIUS);
        }

        public void emit_noise(Vector3 origin, float radius)
        {
            var holder = new GameObject("Noise Pulse"); holder.transform.SetParent(world.transform); holder.transform.position = origin;
            var ring = create_ring(holder.transform, 1, yellow);
            pulses.Add(new Pulse { origin = origin, radius = radius, time = Time.time, ring = ring });
            var signal = noise.create_signal(origin, radius, Time.time);
            for (int i = 0; i < actors.Count; i++)
                if (!actors[i].friendly && actors[i].alive) noise.queue_listener(i, signal, actors[i].transform.position);
        }

        private void update_noise()
        {
            noise.advance(Time.time);
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.alive || actor.friendly || !noise.try_hear(i, out var signal) || !actor.sound_memory.accept(signal)) continue;
                heard++;
                if (actor.target == null)
                {
                    actor.has_memory = true; actor.memory_position = signal.origin;
                    actor.next_repath = 0;
                }
            }
            for (int p = pulses.Count - 1; p >= 0; p--)
            {
                var pulse = pulses[p]; float age = Time.time - pulse.time;
                pulse.ring.transform.localScale = Vector3.one * Mathf.Min(pulse.radius, age * UnitBalance.config.noise_propagation_speed);
                if (age > pulse.radius / UnitBalance.config.noise_propagation_speed + UnitBalance.config.noise_pulse_duration) { Destroy(pulse.ring.transform.parent.gameObject); pulses.RemoveAt(p); }
            }
        }

        private void update_arrows()
        {
            for (int i = arrows.Count - 1; i >= 0; i--)
            {
                var arrow = arrows[i];
                if (arrow.victim == null || !arrow.victim.alive) { Destroy(arrow.visual.gameObject); arrows.RemoveAt(i); continue; }
                Vector3 goal = arrow.victim.transform.position + Vector3.up;
                Vector3 previous = arrow.visual.position;
                Vector3 next = Vector3.MoveTowards(previous, goal, UnitBalance.human.projectile_speed * Time.deltaTime);
                arrow.visual.position = next;
                if ((goal - previous).sqrMagnitude > .001f) arrow.visual.rotation = Quaternion.LookRotation(goal - previous);
                bool blocked = Physics.Linecast(previous, next, 1 << 8);
                if (!blocked && Vector3.Distance(next, goal) > .15f) continue;
                if (!blocked)
                {
                    hits++; apply_damage(arrow.victim, UnitBalance.human.damage);
                    if (arrow.victim.alive && arrow.shooter != null)
                    { arrow.victim.has_memory = true; arrow.victim.memory_position = arrow.shooter.transform.position; }
                }
                Destroy(arrow.visual.gameObject); arrows.RemoveAt(i);
            }
        }

        public void apply_damage(CombatActor actor, float damage)
        {
            if (!actor.alive) return;
            actor.health = Mathf.Max(0, actor.health - damage);
            if (actor.alive) return;
            // All lethal causes use the same explosion, including remote gunfire.
            if (actor.exploder) detonate_actor(actor);
            actor.halt(); actor.agent.enabled = false; actor.selected = false; actor.selection_ring.enabled = false;
            if (actor.blast_ring != null) Destroy(actor.blast_ring.gameObject);
            foreach (var collider in actor.GetComponentsInChildren<Collider>()) collider.enabled = false;
            actor.transform.localScale = new Vector3(1, .15f, 1);
            if (!actor.friendly) kills++;
        }

        private void OnDestroy()
        {
            if (nav_instance.valid) nav_instance.Remove();
            if (nav_data != null) Destroy(nav_data);
            if (world != null) Destroy(world);
            foreach (var material in materials) Destroy(material);
        }
    }
}
