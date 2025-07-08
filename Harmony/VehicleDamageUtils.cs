using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static void DamageTargets(ScanResults targets, EntityVehicle bobcat)
    {
      Vector3 forwardDir = bobcat.GetLookVector().normalized;
      foreach (var target in targets.Blocks)
      {
        Vector3i blockPos = target.Item1;
        BlockValue blockVal = target.Item2;
        string blockClassName = blockVal.Block.GetBlockName();

        if (BobcatConfig.BlockNamesToIgnoreList.Contains(blockClassName)) return;

        // Trader block protection
        if (BobcatConfig.AllowTraderBlockDestruction == false && GameManager.Instance.World.IsWithinTraderPlacingProtection(blockPos)) return;

        int blockDamage = GetBlockDamageByConfig(blockVal.Block);
        blockVal.Block.DamageBlock(
            GameManager.Instance.World,
            -1,
            blockPos,
            blockVal,
            blockDamage,
            GameManager.Instance.myEntityPlayerLocal.entityId,
            null,
            true
        );

        if (BobcatConfig.EnableDustParticles)
        {
          string nextName = BobcatParticleManager.GetNextDust();
          ParticleEffect pe = new ParticleEffect(nextName, blockPos, Quaternion.identity, 1f, Color.white);
          GameManager.Instance.SpawnParticleEffectServer(pe, GameManager.Instance.myEntityPlayerLocal.entityId, true, true);
        }

        GameManager.Instance.PlaySoundAtPositionServer(blockPos, "shovel_stone_swinglight", AudioRolloffMode.Linear, 20);
      }

      if (VehicleStatic.CurrentMode != VehicleStatic.BobcatMode.Tunneling) return; // Skip entity damage if drill not attached

      foreach (var target in targets.EntityIds)
      {
        Entity entity = GameManager.Instance.World.GetEntity(target);
        if (entity == null || BobcatConfig.EntityNamesToIgnoreList.Contains(entity.EntityClass.entityClassName)) continue;

        EntityAlive entityAlive = entity as EntityAlive;
        if (entityAlive == null) continue;

        int entityDamageToApply = GetEntityDamageByConfig(entityAlive);
        DamageSource dmgSource = new DamageSource(EnumDamageSource.External, EnumDamageTypes.Slashing);
        entity.DamageEntity(dmgSource, entityDamageToApply, true, 1);

        if (BobcatConfig.EnableBloodParticles)
        {
          string nextName = BobcatParticleManager.GetNextBlood();
          ParticleEffect pe = new ParticleEffect(nextName, entity.position, Quaternion.identity, 1f, Color.red);
          GameManager.Instance.SpawnParticleEffectServer(pe, GameManager.Instance.myEntityPlayerLocal.entityId);
        }

        if (BobcatConfig.EnableZombieRagdoll)
        {
          RagdollEnemy(target, bobcat);
        }

        GameManager.Instance.SpawnParticleEffectServer(new ParticleEffect("blood_vehicle", entity.position, Quaternion.identity, 1f, Color.red), GameManager.Instance.myEntityPlayerLocal.entityId);
        GameManager.Instance.PlaySoundAtPositionServer(entity.position, "metalslashorganic", AudioRolloffMode.Linear, 20);
      }
    }
    public static ScanResults GetTargets(Vector3 targetCenter, Vector3 right, Vector3 forward, EntityVehicle vehicle)
    {
      ScanResults results = new ScanResults();
      List<Entity> nearbyEntities = new List<Entity>();

      List<string> mods = GetModifierNames(vehicle);
      targetCenter -= forward.normalized; // Move targeting back by 1 block
      int width = GetHWDFromMods(mods)[1];
      int height = GetHWDFromMods(mods)[0];
      int depth = GetHWDFromMods(mods)[2];
      int halfWidth = width / 2;
      float heightOffset = 1f;
      float widthOffset = (width % 2 == 0) ? 0.5f : 0f;

      for (int d = 0; d < depth; d++)
      {
        for (int i = -halfWidth; i <= halfWidth - (width % 2 == 0 ? 1 : 0); i++)
        {
          for (int j = 0; j < height; j++)
          {
            Vector3 offsetPos = targetCenter - right * widthOffset + forward * d + right * i + Vector3.up * j;

            Vector3i blockPos = new Vector3i(
                Mathf.FloorToInt(offsetPos.x),
                Mathf.FloorToInt(offsetPos.y + heightOffset),
                Mathf.FloorToInt(offsetPos.z)
            );

            BlockValue blockVal = GameManager.Instance.World.GetBlock(blockPos);
            if (!blockVal.isair) results.Blocks.Add(Tuple.Create(blockPos, blockVal));

            nearbyEntities.Clear();
            GameManager.Instance.World.GetEntitiesAround(EntityFlags.Zombie, blockPos.ToVector3(), 1f, nearbyEntities);

            results.EntityIds.AddRange(
                nearbyEntities
                    .OfType<EntityAlive>()
                    .Where(e => e.IsAlive() && (e is EntityZombie || e is EntityAnimal))
                    .Select(e => e.entityId)
            );
          }
        }
      }

      return results;
    }
    public static void RagdollEnemy(int enemyId, EntityVehicle vehicle)
    {
      Entity entity = GameManager.Instance.World.GetEntity(enemyId);
      if (entity is EntityAlive alive && alive.IsAlive())
      {
        Vector3 forceDir = alive.position - vehicle.position;
        forceDir.Normalize();
        forceDir += Vector3.up * 1.25f; // Add vertical lift

        float forceMagnitude = 80f;
        Vector3 force = forceDir * forceMagnitude;

        alive.emodel.DoRagdoll(1.5f, EnumBodyPartHit.Torso, force, alive.position, false);
      }
    }
    public class ScanResults
    {
      public List<Tuple<Vector3i, BlockValue>> Blocks { get; set; } = new List<Tuple<Vector3i, BlockValue>>();
      public List<int> EntityIds { get; set; } = new List<int> { };
    }
  }
}
