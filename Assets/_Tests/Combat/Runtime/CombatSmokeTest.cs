using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ZombieGame.CombatTests
{
    public sealed class CombatSmokeTest : MonoBehaviour
    {
        public CombatSandbox game;
        private IEnumerator Start()
        {
            yield return null;
            require(CombatSandbox.NOISE_RADIUS == CombatSandbox.ATTACK_RANGE * 2, "Noise/range ratio");
            require(game.can_see(new Vector3(-2, 0, -5), new Vector3(2, 0, -5)), "Low wall blocked shots");
            require(!game.can_see(new Vector3(-2, 0, 5), new Vector3(2, 0, 5)), "High wall leaked shots");
            var archer = game.actors[0];
            var zombie = game.actors[CombatSandbox.FRIENDLY_COUNT];
            require(game.actors.Count == CombatSandbox.FRIENDLY_COUNT + CombatSandbox.ZOMBIE_COUNT, "Population count");
            require(Mathf.Approximately(archer.agent.speed, 3.5f) && Mathf.Approximately(zombie.agent.speed, 4.5f), "Shared speed multiplier");
            require(!game.issue_order(zombie, CombatOrder.Move, Vector3.zero), "Zombie accepted player order");
            require(RtsInput.selection_rect(new Vector2(5, 5), Vector2.zero).Contains(new Vector2(2, 2)), "Reverse box selection");
            game.issue_order(archer, CombatOrder.Move, new Vector3(-5, 0, -2));
            yield return new WaitForSeconds(2);
            require(archer.transform.position.x > -6, "Move failed");
            require(game.shots == 0, "Move auto-fired");
            game.issue_order(archer, CombatOrder.Move, new Vector3(-10, 0, -2));
            require(!archer.agent.pathPending && archer.agent.hasPath, "Explicit move queued path");
            yield return new WaitForSeconds(.1f);
            require(archer.agent.velocity.x < 0, "Reverse command response exceeded 100ms");
            game.issue_order(archer, CombatOrder.Patrol, new Vector3(-9, 0, -2));
            yield return new WaitForSeconds(5);
            require(archer.order == CombatOrder.Patrol, "Patrol did not persist");
            game.issue_order(archer, CombatOrder.Stop, Vector3.zero);
            Vector3 stopped = archer.transform.position;
            yield return new WaitForSeconds(.5f);
            require(Vector3.Distance(stopped, archer.transform.position) < .1f, "Stop moved");
            // Distant listener is outside weapon and sight range, but inside gun noise.
            game.reset_battle();
            archer = game.actors[0]; zombie = game.actors[CombatSandbox.FRIENDLY_COUNT];
            foreach (var actor in game.actors) if (actor.friendly) actor.next_attack = Time.time + 10;
            Vector3 source = new Vector3(-4, 0, 5);
            zombie.agent.Warp(new Vector3(5, 0, 5));
            zombie.agent.speed = 0; // Hold listener until pulse expires to isolate source memory.
            var outside = game.actors[CombatSandbox.FRIENDLY_COUNT + 1]; outside.agent.Warp(new Vector3(11, 0, 5));
            require(Vector3.Distance(source, zombie.transform.position) > CombatSandbox.ATTACK_RANGE, "Listener inside attack range");
            require(!game.can_see(source, zombie.transform.position), "Noise case not behind high wall");
            game.emit_noise(source, CombatSandbox.NOISE_RADIUS);
            yield return new WaitForSeconds(3.6f);
            require(zombie.has_memory && zombie.target == null, "Noise beyond range/through wall failed");
            require(!outside.has_memory, "Outside noise radius reacted");
            game.reset_battle();
            archer = game.actors[0]; zombie = game.actors[CombatSandbox.FRIENDLY_COUNT];
            foreach (var actor in game.actors)
                if (actor.friendly) game.issue_order(actor, CombatOrder.AttackMove, new Vector3(8, 0, 0));
            yield return new WaitForSeconds(12);
            require(game.shots > 0 && game.hits > 0 && game.kills > 0, "Gun/damage/death cycle failed");
            string combat_counts = $"shots={game.shots} hits={game.hits} kills={game.kills}";
            game.reset_battle();
            yield return null; // Register fresh agents before constructing the melee fixture.
            archer = game.actors[0]; zombie = game.actors[CombatSandbox.FRIENDLY_COUNT];
            foreach (var actor in game.actors) if (actor.friendly) actor.next_attack = Time.time + 10;
            require(zombie.agent.Warp(archer.transform.position + Vector3.right * .7f), "Melee fixture warp failed");
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.5f);
            require(game.bites > 0 && archer.health < archer.max_health,
                $"Zombie melee failed: bites={game.bites} hp={archer.health} distance={CombatSandbox.distance(archer, zombie)} target={zombie.target}");
            game.apply_damage(archer, 1000);
            require(!archer.alive && !archer.agent.enabled, "Friendly death failed");
            int melee_count = game.bites;
            yield return verify_explosions();
            string message = $"PASS 8 Shenji/36 Runner/6 Exploder, move/no auto-fire, reverse within 100ms, patrol, stop drift <0.1, low/high wall LOS, noise 2x range, distant wall listener/outside rejection, noise memory, AI order rejection, gun/hits/kills, melee, death, AOE inside/outside, contact/fuse-kill/remote-death explosion visuals, no duplicate blast, silent explosions leave idle zombies unaware. {combat_counts} bites={melee_count}";
            Debug.Log("[CombatSmoke] " + message);
            Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, "CombatSandbox"));
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "CombatSandbox/smoke.txt"), message);
            game.reset_battle();
            game.running_checks = false;
        }

        private IEnumerator verify_explosions()
        {
            game.reset_battle();
            yield return null;
            foreach (var actor in game.actors) if (actor.friendly) actor.next_attack = Time.time + 10;
            var exploder = game.actors[CombatSandbox.FRIENDLY_COUNT + CombatSandbox.RUNNER_COUNT];
            var near = game.actors[0]; var second = game.actors[1]; var outside = game.actors[2];
            require(exploder.exploder && Mathf.Approximately(exploder.agent.speed, 4.25f), "Exploder type/speed");
            require(exploder.agent.Warp(new Vector3(-8, 0, 0)), "Exploder fixture warp");
            require(near.agent.Warp(new Vector3(-7.2f, 0, 0)), "Near fixture warp");
            require(second.agent.Warp(new Vector3(-8, 0, 1.2f)), "Second fixture warp");
            require(outside.agent.Warp(new Vector3(-5.5f, 0, 0)), "Outside fixture warp");
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.2f);
            require(game.blasts == 1 && !exploder.alive,
                $"Contact explosion / self death: blasts={game.blasts} alive={exploder.alive} fuse={exploder.detonate_at} time={Time.time} distance={CombatSandbox.distance(near, exploder)}");
            require(near.health == near.max_health - CombatSandbox.BLAST_DAMAGE &&
                second.health == second.max_health - CombatSandbox.BLAST_DAMAGE, "Cluster AOE damage");
            require(outside.health == outside.max_health, "AOE damaged outside radius");
            require(game.heard == 0, "Contact explosion emitted attraction noise");

            game.reset_battle();
            yield return null;
            foreach (var actor in game.actors) if (actor.friendly) actor.next_attack = Time.time + 10;
            exploder = game.actors[CombatSandbox.FRIENDLY_COUNT + CombatSandbox.RUNNER_COUNT];
            require(exploder.agent.Warp(game.actors[0].transform.position + Vector3.right * .7f), "Cancel-fuse fixture warp");
            yield return new WaitForSeconds(.15f);
            require(exploder.detonate_at >= 0, "Contact failed to arm fuse");
            game.apply_damage(exploder, 1000);
            yield return new WaitForSeconds(.8f);
            require(game.blasts == 1 && game.blast_visuals_created == 1, "Killed armed exploder did not explode once");

            game.reset_battle();
            yield return null;
            exploder = game.actors[CombatSandbox.FRIENDLY_COUNT + CombatSandbox.RUNNER_COUNT];
            require(exploder.detonate_at < 0, "Remote exploder unexpectedly armed");
            float friendly_health = game.actors[0].health;
            game.apply_damage(exploder, 1000);
            require(game.blasts == 1 && game.blast_visuals_created == 1, "Remote death missing explosion visual");
            require(FindObjectsByType<BlastVisual>(FindObjectsSortMode.None).Length > 0, "No live remote explosion effect");
            require(game.actors[0].health == friendly_health, "Remote blast damaged distant friendly");
            game.apply_damage(exploder, 1000);
            require(game.blasts == 1, "Dead exploder detonated twice");
            yield return new WaitForSeconds(1);
            require(FindObjectsByType<BlastVisual>(FindObjectsSortMode.None).Length == 0, "Explosion effect leaked");
            require(game.heard == 0, "Remote explosion emitted attraction noise");
            foreach (var actor in game.actors)
                if (!actor.friendly && actor.alive)
                    require(!actor.has_memory && actor.target == null, "Silent explosion attracted idle zombie");
        }

        private void require(bool condition, string message)
        {
            if (condition) return;
            game.running_checks = false;
            game.notice = "AUTO CHECK FAILED: " + message;
            throw new InvalidOperationException("Combat smoke: " + message);
        }
    }
}
