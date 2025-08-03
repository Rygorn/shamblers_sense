using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Shamblers_Sense
{
    [HarmonyPatch(typeof(AIDirectorChunkEventComponent))]
    [HarmonyPatch("CheckToSpawn")]
    [HarmonyPatch(new[] { typeof(AIDirectorChunkData) })]
    public class Patch_AIDirector_CheckToSpawn
    {
        private static AIDirectorChunkEventComponent chunkEventComponent;

        private static readonly float[] thresholds = { 30f, 50f, 70f, 90f };

        private static long lastHighHeatSpawnTime = 0;
        private static System.Random rng = new System.Random();

        static bool Prefix(AIDirector __instance, AIDirectorChunkData _chunkData)
        {
            if (!GameStats.GetBool(EnumGameStats.ZombieHordeMeter) || !GameStats.GetBool(EnumGameStats.IsSpawnEnemies))
                return false;

            // Cache component for scout spawns
            //if (chunkEventComponent == null)
            //{
            //    chunkEventComponent = __instance.GetComponent<AIDirectorChunkEventComponent>();
            //    if (chunkEventComponent == null)
            //    {
            //        Debug.LogWarning("[Shamblers_Sense] Could not find AIDirectorChunkEventComponent.");
            //        return true;
            //    }
            //}

            var aiDirector = GameManager.Instance.World.aiDirector;
            var field = typeof(AIDirector).GetField("chunkEventComponent", BindingFlags.NonPublic | BindingFlags.Instance);
            var chunkEventComponent = field?.GetValue(aiDirector) as AIDirectorChunkEventComponent;



            if (chunkEventComponent != null)
            {
                Log.Warning("ChunkEventComponent found via reflection.");
            }
            else
            {
                Log.Warning("ChunkEventComponent is NULL.");
            }

            float activity = _chunkData.ActivityLevel;
            float lowestThreshold = thresholds[0];

            if (activity < lowestThreshold)
            {
                ShamblersSenseTracker.Reset();
            }

            foreach (float threshold in thresholds)
            {
                if (activity >= threshold && ShamblersSenseTracker.CanSpawn(threshold))
                {
                    var chunkEvent = FindBestEvent(_chunkData);
                    if (chunkEvent != null)
                    {
                        //chunkEventComponent.StartCooldownOnNeighbors(chunkEvent.Position);
                        chunkEventComponent.SpawnScouts(chunkEvent.Position.ToVector3());
                        ShamblersSenseTracker.RecordSpawn(threshold);
                        return false;
                    }
                }
            }

            // NEW: Trigger high-heat zombie group spawns
            ulong rawWorldTime = GameManager.Instance.World.worldTime;
            long worldTime = rawWorldTime <= long.MaxValue ? (long)rawWorldTime : long.MaxValue;

            if (worldTime - lastHighHeatSpawnTime >= 300) // ~every 5 in-game minutes
            {
                lastHighHeatSpawnTime = worldTime;
                SpawnGroupsNearHighHeatChunks(__instance);
            }

            return false;
        }

        private static AIDirectorChunkEvent FindBestEvent(AIDirectorChunkData chunkData)
        {
            AIDirectorChunkEvent bestEvent = null;
            if (chunkData.events.Count > 0)
            {
                bestEvent = chunkData.events[0];
                for (int i = 1; i < chunkData.events.Count; i++)
                {
                    if (chunkData.events[i].Value > bestEvent.Value)
                        bestEvent = chunkData.events[i];
                }
            }
            return bestEvent;
        }

        private static void SpawnGroupsNearHighHeatChunks(AIDirector director)
        {
            try
            {
                var field = typeof(AIDirector).GetField("chunkEventComponent", BindingFlags.NonPublic | BindingFlags.Instance);
                var chunkEventComponent = field?.GetValue(director) as AIDirectorChunkEventComponent;
                if (chunkEventComponent == null)
                {
                    Debug.LogWarning("[Shamblers_Sense] Could not find AIDirectorChunkEventComponent for high heat spawns.");
                    return;
                }

                var playerChunkCoords = GameManager.Instance.World.Players.list
                    .Select(p => World.toChunkXZ(p.GetPosition()))
                    .ToList();

                var hottestChunks = chunkEventComponent.activeChunks
                    .OrderByDescending(kvp => kvp.Value.ActivityLevel)
                    .Take(10)
                    .Select(kvp =>
                    {
                        var bestEvent = FindBestEvent(kvp.Value);
                        return bestEvent != null ? bestEvent.Position : new Vector3i(0, 0, 0);
                    })
                    .ToList();

                var playerChunkCoords3D = playerChunkCoords.Select(v2 => new Vector3i(v2.x, 0, v2.y)).ToList();

                Debug.Log($"Spawning zombie groups near {hottestChunks.Count} hottest chunks.");

                foreach (var chunk in hottestChunks)
                {
                    Debug.Log($"Spawning groups near chunk at {chunk}");
                    for (int i = 0; i < 3; i++) // Up to 3 groups per chunk
                    {
                        Vector3? spawnPos = FindValidSpawnPosition(chunk, 20, 5, playerChunkCoords3D);
                        if (spawnPos.HasValue)
                        {
                            Debug.Log($"Spawning zombie group at {spawnPos.Value}");
                            SpawnZombieGroup(spawnPos.Value, 4 + rng.Next(3)); // 4–6 zombies per group
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Shamblers_Sense] Exception in SpawnGroupsNearHighHeatChunks: " + ex);
            }
        }

        private static Vector3? FindValidSpawnPosition(Vector3i centerChunk, int maxRadiusChunks, int minDistanceChunks, List<Vector3i> playerChunks)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int dx = rng.Next(-maxRadiusChunks, maxRadiusChunks + 1);
                int dz = rng.Next(-maxRadiusChunks, maxRadiusChunks + 1);

                if (Mathf.Abs(dx) < minDistanceChunks && Mathf.Abs(dz) < minDistanceChunks)
                    continue;

                Vector3i targetChunk = new Vector3i(centerChunk.x + dx, 0, centerChunk.z + dz);

                bool tooCloseToPlayer = playerChunks.Any(p =>
                    Mathf.Abs(p.x - targetChunk.x) < minDistanceChunks &&
                    Mathf.Abs(p.z - targetChunk.z) < minDistanceChunks);

                if (tooCloseToPlayer)
                    continue;

                Vector3 pos = new Vector3(targetChunk.x * 16 + 8, 0, targetChunk.z * 16 + 8);
                pos.y = GameManager.Instance.World.GetHeight((int)pos.x, (int)pos.z) + 1f;

                return pos;
            }

            return null;
        }

        private static void SpawnZombieGroup(Vector3 center, int count)
        {
            Debug.Log($"Spawning zombie group at {center} with {count} zombies.");
            for (int i = 0; i < count; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float radius = 5f + (float)rng.NextDouble() * 10f;

                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                Vector3 spawnPos = center + offset;
                spawnPos.y = GameManager.Instance.World.GetHeight((int)spawnPos.x, (int)spawnPos.z) + 1f;

                Entity zombie = EntityFactory.CreateEntity(EntityClass.FromString("zombieBoe"), spawnPos); // Replace with your own types
                if (zombie != null)
                    zombie.SetPosition(spawnPos);
                    GameManager.Instance.World.SpawnEntityInWorld(zombie);
            }
        }
    }


    [HarmonyPatch(typeof(AIScoutHordeSpawner), "spawnHordeNear")]
    public class Patch_ScoutHordeSpawner_Suppress
    {
        static bool Prefix(World world, AIScoutHordeSpawner.ZombieCommand command, Vector3 target)
        {
            Debug.Log($"Screamer (Scout) scream triggered at position {target}");

            // Convert target world position to chunk coordinates (Vector2i)
            Vector2i chunkPos = World.toChunkXZ(new Vector2i((int)target.x, (int)target.z));

            // Get the AI director's chunk event component
            var chunkEventComponent = world.GetAIDirector().GetComponent<AIDirectorChunkEventComponent>();

            // Create the chunk key for the dictionary lookup
            long chunkKey = WorldChunkCache.MakeChunkKey(chunkPos.x, chunkPos.y);

            if (chunkEventComponent.activeChunks.TryGetValue(chunkKey, out var chunkData))
            {
                Debug.Log($"Current heat before adding: {chunkData.activityLevel}");
                // Increase the heat/activity level of the chunk
                chunkData.activityLevel += 2f; // adjust heat value as needed
                Debug.Log($"Heat added, new heat: {chunkData.activityLevel}");
            }
            else
            {
                // Create a new chunk data with initial heat if none exists
                chunkData = new AIDirectorChunkData();
                chunkData.activityLevel = 2f;
                chunkEventComponent.activeChunks[chunkKey] = chunkData;
                Debug.Log($"Heat added to new chunk data: {chunkData.activityLevel}");
            }

            // Suppress the actual horde spawn
            Debug.Log("Screamer horde suppressed, no horde spawned.");
            return false;
        }
    }


    public static class ShamblersSenseTracker
    {
        private static float lastTriggeredThreshold = 0f;
        private static int currentSpawnCount = 0;
        private static int maxSpawnsThisThreshold = 1;

        public static void Reset()
        {
            lastTriggeredThreshold = 0f;
            currentSpawnCount = 0;
            maxSpawnsThisThreshold = 1;
            Debug.Log("[Shamblers_Sense] Spawn tracker reset.");
        }

        public static bool CanSpawn(float currentThreshold)
        {
            if (currentThreshold > lastTriggeredThreshold)
            {
                lastTriggeredThreshold = currentThreshold;
                currentSpawnCount = 0;
                maxSpawnsThisThreshold = UnityEngine.Random.Range(1, 3);
                Debug.Log($"[Shamblers_Sense] New threshold {currentThreshold} reached, max spawns set to {maxSpawnsThisThreshold}");
                return true;
            }
            else if (currentThreshold == lastTriggeredThreshold)
            {
                return currentSpawnCount < maxSpawnsThisThreshold;
            }
            return false;
        }

        public static void RecordSpawn(float currentThreshold)
        {
            if (currentThreshold == lastTriggeredThreshold)
            {
                currentSpawnCount++;
                Debug.Log($"[Shamblers_Sense] Recorded spawn {currentSpawnCount}/{maxSpawnsThisThreshold} at threshold {currentThreshold}");
            }
        }
    }
}
