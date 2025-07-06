using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;
using RaycastPathing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;

namespace Bobcat
{
  public class Vehicle
  {
    public enum DamageState
    {
      None,
      Damaged,
      HeavilyDamaged
    }

    public static Dictionary<EntityVehicle, DamageState> BobcatDamageStates = new Dictionary<EntityVehicle, DamageState>();
    public static Dictionary<EntityVehicle, List<Transform>> DamagedBobcats = new Dictionary<EntityVehicle, List<Transform>>();

    [HarmonyPatch(typeof(GameManager), "Update")]
    public static class Patch_VehicleIntercept
    {

      public static void Postfix()
      {
        var player = GameManager.Instance?.myEntityPlayerLocal;
        if (player == null) return;

        if (player.AttachedToEntity is EntityVehicle currentVehicle)
        {
          // Only run this once when entering a new vehicle
          if (VehicleStatic.lastAttachedVehicle != currentVehicle)
          {
            VehicleStatic.lastAttachedVehicle = currentVehicle;

            HandleEnterVehicle(currentVehicle);
          }


          HandleModeChange(currentVehicle, VehicleStatic.actions);
          HandleHornToggle(currentVehicle, VehicleStatic.actions);
          HandleLightChange(currentVehicle);
          HandleTurbo(currentVehicle, VehicleStatic.actions);
        }
        else
        {
          // Player is not attached to a vehicle anymore
          if (VehicleStatic.lastAttachedVehicle != null) HandleExitVehicle(VehicleStatic.lastAttachedVehicle);

          VehicleStatic.lastAttachedVehicle = null;
          VehicleStatic.wasHopPressedLastFrame = false;
          VehicleStatic.wasHornPressedLastFrame = false;

          VehicleStatic.CurrentMode = VehicleStatic.BobcatMode.None;
        }
      }
    }

    [HarmonyPatch(typeof(XUiC_VehicleWindowGroup), "OnClose")]
    public class Patch_VehicleUI_Close
    {
      static void Prefix(XUiC_VehicleWindowGroup __instance)
      {
        EntityVehicle vehicle = __instance?.CurrentVehicleEntity;
        if (vehicle == null) return;

        UpdateVehicleVisuals(vehicle);
      }
    }

    [HarmonyPatch(typeof(EntityVehicle), "PostInit")]
    public class Patch_EntityVehicle_PostInit
    {
      static void Postfix(EntityVehicle __instance)
      {
        GameManager.Instance.StartCoroutine(UpdateVehicleVisualsDelayed(__instance));
      }
    }

    [HarmonyPatch(typeof(Block), nameof(Block.OnBlockDestroyedBy))]
    public class Patch_Block_OnBlockDestroyedBy
    {
      static void Postfix(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, int _entityId, bool _bUseHarvestTool)
      {
        EntityPlayerLocal player = GameManager.Instance.World.GetEntity(_entityId) as EntityPlayerLocal;
        if (player != null && player.AttachedToEntity is EntityVehicle vehicle)
        {
          // Only allow harvestable blocks from the block list to drop items
          if (!BobcatConfig.HarvestableBlockList.Contains(_blockValue.Block.blockName)) return;

          List<ItemStack> drops = new List<ItemStack>();
          if (_blockValue.Block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out var harvestDrops))
          {
            foreach (var drop in harvestDrops) 
            {
              if (UnityEngine.Random.value <= drop.prob)
              {
                int baseAmount = UnityEngine.Random.Range(drop.minCount, drop.maxCount + 1);
                int configAmount = GetBlockHarvestAmountByConfig(_blockValue.Block, baseAmount);
                ItemClass itemClass = ItemClass.GetItemClass(drop.name, true);
                if (itemClass != null)
                {
                  ItemValue itemValue = new ItemValue(itemClass.Id, false);
                  ItemStack stack = new ItemStack(itemValue, Mathf.Clamp(configAmount, 1, _blockValue.Block.Stacknumber));
                  ((EntityVehicle)player.AttachedToEntity).bag.AddItem(stack);
                }
              }
            }
          }
        }
      }
    }

    [HarmonyPatch(typeof(Block), nameof(Block.SpawnDestroyParticleEffect))]
    public class Patch_Block_SpawnDestroyParticleEffect
    {
      public static void Postfix(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, float _lightValue, Color _color, int _entityIdThatCaused)
      {
        EntityPlayerLocal player = GameManager.Instance.World.GetEntity(_entityIdThatCaused) as EntityPlayerLocal;
        if (player == null || !(player.AttachedToEntity is EntityVehicle vehicle)) return;

        if (!BobcatConfig.HarvestableBlockList.Contains(_blockValue.Block.blockName)) return;

        Vector3 pos = World.blockToTransformPos(_blockPos) + new Vector3(0f, 0.5f, 0f);
        string surfaceCategory = _blockValue.Block.GetMaterialForSide(_blockValue, BlockFace.Top)?.SurfaceCategory ?? "stone";

        if(BobcatConfig.EnableDustParticles)
        {
          string nextName = BobcatParticleManager.GetNextDust();
          ParticleEffect pe = new ParticleEffect(nextName, pos, Quaternion.identity, 1f, Color.white);
          GameManager.Instance.SpawnParticleEffectServer(pe, GameManager.Instance.myEntityPlayerLocal.entityId);
        }
      }
    }
    public static class BobcatParticleManager
    {
      private static readonly string[] DustVariants = new[] {"p_BobcatDust1", "p_BobcatDust2", "p_BobcatDust3", "p_BobcatDust4", "p_BobcatDust5","p_BobcatDust6", "p_BobcatDust7", "p_BobcatDust8", "p_BobcatDust9", "p_BobcatDust10","p_BobcatDust11", "p_BobcatDust12", "p_BobcatDust13", "p_BobcatDust14", "p_BobcatDust15","p_BobcatDust16", "p_BobcatDust17", "p_BobcatDust18", "p_BobcatDust19", "p_BobcatDust20"};
      private static readonly string[] BloodVariants = new[] { "p_BloodMist1", "p_BloodMist2", "p_BloodMist3", "p_BloodMist4", "p_BloodMist5", "p_BloodMist6", "p_BloodMist7", "p_BloodMist8", "p_BloodMist9", "p_BloodMist10", "p_BloodMist11", "p_BloodMist12", "p_BloodMist13", "p_BloodMist14", "p_BloodMist15", "p_BloodMist16", "p_BloodMist17", "p_BloodMist18", "p_BloodMist19", "p_BloodMist20" };
      private static int dustIndex = 0;
      private static int bloodIndex = 0;

      public static string GetNextDust()
      {
        string name = DustVariants[dustIndex];
        dustIndex = (dustIndex + 1) % DustVariants.Length;
        return name;
      }

      public static string GetNextBlood()
      {
        string name = BloodVariants[bloodIndex];
        bloodIndex = (bloodIndex + 1) % BloodVariants.Length;
        return name;
      }
    }

    // Modes
    public static void RunLandscapingHighMode(EntityVehicle vehicle)
    {
      // Flatten forward and right vectors to remove pitch/roll effect
      Vector3 forward = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up).normalized;
      Vector3 right = Vector3.ProjectOnPlane(vehicle.transform.right, Vector3.up).normalized;

      // Center point 1 blocks in front of vehicle
      Vector3 targetCenter = vehicle.position + forward + Vector3.down * 0.5f;

      ScanResults targets = GetTargets(targetCenter, right, forward, vehicle);
      DamageTargets(targets, vehicle);
    }
    public static void RunTunnelingMode(EntityVehicle vehicle)
    {
      // Flatten forward and right vectors to remove pitch/roll effect
      Vector3 forward = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up).normalized;
      Vector3 right = Vector3.ProjectOnPlane(vehicle.transform.right, Vector3.up).normalized;

      // Center point 1 blocks in front of vehicle
      Vector3 targetCenter = vehicle.position + forward;

      var targets = GetTargets(targetCenter, right, forward, vehicle);
      DamageTargets(targets, vehicle);
    }
    public static void RunLevelingMode(EntityVehicle vehicle)
    {
      Vector3 forward = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up).normalized;
      Vector3 right = Vector3.ProjectOnPlane(vehicle.transform.right, Vector3.up).normalized;
      Vector3 centerPos = vehicle.GetCenterPosition();
      Vector3 offsetPos = centerPos + forward * 2f; // 2 blocks forward

      // Build a transform matrix based on vehicle orientation
      Matrix4x4 vehicleMatrix = Matrix4x4.TRS(centerPos, Quaternion.LookRotation(forward, Vector3.up), Vector3.one);
      List<Vector3Int> offsets = GetFlatteningOffsets(vehicle, GetModifierNames(vehicle));
      

      foreach (Vector3Int offset in offsets)
      {
        Vector3 worldPos = vehicleMatrix.MultiplyPoint3x4(offset);
        Vector3i basePos = new Vector3i(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y), Mathf.RoundToInt(worldPos.z));

        // Raycast downward to find first solid block
        Vector3i targetPos = RaycastToGround(basePos, 256);
        if (targetPos == Vector3i.zero) continue; // Ground not found

        Vector3i densityPos = new Vector3i(targetPos.x, targetPos.y - 0.5f, targetPos.z);

        int clrIdx = GetClrIdxFromBlockPos(densityPos);

        Vector3i levelTarget = new Vector3i(targetPos.x, VehicleStatic.vehicleLevelingModeHeight, targetPos.z);
        FlattenBlockDensityArea(GameManager.Instance.World, clrIdx, levelTarget, BobcatConfig.LevelingModeFillHeight, vehicle);
      }
    }
    public static void RunFillMode(EntityVehicle vehicle)
    {
      Vector3 forward = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up).normalized;
      Vector3 right = Vector3.ProjectOnPlane(vehicle.transform.right, Vector3.up).normalized;
      Vector3 centerPos = vehicle.GetCenterPosition();
      Vector3 offsetPos = centerPos + forward * 2f; // 2 blocks forward

      // Build a transform matrix based on vehicle orientation
      Matrix4x4 vehicleMatrix = Matrix4x4.TRS(centerPos, Quaternion.LookRotation(forward, Vector3.up), Vector3.one);
      List<Vector3Int> offsets = GetFlatteningOffsets(vehicle, GetModifierNames(vehicle));


      foreach (Vector3Int offset in offsets)
      {
        Vector3 worldPos = vehicleMatrix.MultiplyPoint3x4(offset);
        Vector3i basePos = new Vector3i(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y), Mathf.RoundToInt(worldPos.z));


        // Raycast downward to find first solid block
        Vector3i raycastStart = new Vector3i(basePos.x, basePos.y + 5, basePos.z);
        Vector3i targetPos = RaycastToGround(raycastStart, 256);
        if (targetPos == Vector3i.zero) continue; // Ground not found

        Vector3i densityPos = new Vector3i(targetPos.x, targetPos.y - 0.5f, targetPos.z);

        int clrIdx = GetClrIdxFromBlockPos(densityPos);

        Vector3i levelTarget = new Vector3i(targetPos.x, VehicleStatic.vehicleLevelingModeHeight, targetPos.z);
        FillTerrain(vehicle, levelTarget, 1, BobcatConfig.LevelingModeFillHeight, clrIdx);
      }
    }
    public static void RunSmoothingMode(EntityVehicle vehicle)
    {
      World world = GameManager.Instance.World;
      if (world == null || vehicle == null) return;

      // Only apply if vehicle is level
      if (Mathf.Abs(Vector3.Dot(vehicle.transform.up, Vector3.up)) < 0.98f) return;

      Vector3 forward = vehicle.transform.forward.normalized;
      Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
      Vector3 centerWorld = vehicle.GetPosition() + forward * 3f;
      Vector3i centerBlock = Vector3i.FromVector3Rounded(centerWorld);
      int vehicleY = Mathf.RoundToInt(vehicle.GetPosition().y);

      List<BlockChangeInfo> changes = new List<BlockChangeInfo>();
      List<string> mods = GetModifierNames(vehicle);
      int[] bucketSizeToDX = new int[2] { -1, 1 };
      if (mods.Contains("modVehicleBucket5")) { bucketSizeToDX[0] = -2; bucketSizeToDX[1] = 2; }

      for (int dz = 0; dz < BobcatConfig.TerrainBlocksToSmooth; dz++)
      {
        for (int dx = bucketSizeToDX[0]; dx <= bucketSizeToDX[1]; dx++)
        {
          Vector3 offset = dz * forward + dx * right;
          Vector3 samplePos = centerWorld + offset;
          Vector3i pos = Vector3i.FromVector3Rounded(samplePos);
          pos.y = world.GetHeight(pos.x, pos.z);

          // Only affect blocks at the same height
          if (pos.y != vehicleY) continue;

          BlockValue blockVal = world.GetBlock(pos);
          if (blockVal.isair || blockVal.isWater) continue;

          Chunk chunk = world.GetChunkFromWorldPos(pos) as Chunk;
          if (chunk == null) continue;

          int clrIdx = GetClrIdxFromBlockPos(pos);

          sbyte startDensity = 60;
          sbyte endDensity = -60;
          float t = dz / (float)(BobcatConfig.TerrainBlocksToSmooth - 1);
          sbyte targetDensity = (sbyte)Mathf.RoundToInt(Mathf.Lerp(startDensity, endDensity, t));

          changes.Add(new BlockChangeInfo(clrIdx, pos, targetDensity, true));
        }
      }

      if (changes.Count > 0) world.SetBlocksRPC(changes);
    }
    private static List<Vector3Int> GetFlatteningOffsets(EntityVehicle vehicle, List<string> mods)
    {
      int width = 2;
      int depth = 2;

      if (mods.Contains("modVehicleBucket5")) { width = 5; depth = 3; }
      else if (mods.Contains("modVehicleBucket3")) { width = 3; depth = 3; }

      List<Vector3Int> offsets = new List<Vector3Int>();

      for (int x = -width / 2; x <= width / 2; x++)
      {
        for (int z = 0; z < depth; z++)
        {
          offsets.Add(new Vector3Int(x, -1, z)); // 1 block below the vehicle
        }
      }

      return offsets;
    }
    public static void FillTerrain_Old(EntityVehicle vehicle, Vector3i centerPos, int radius, sbyte density, int clrIdx)
    {
      World world = GameManager.Instance.World;
      bool inventoryEmpty = true;

      Vector3 fwd = vehicle.transform.forward;
      float pitch = Vector3.Angle(Vector3.ProjectOnPlane(fwd, Vector3.up), fwd); // in degrees

      Vector3 right = vehicle.transform.right;
      float roll = Vector3.Angle(Vector3.ProjectOnPlane(right, Vector3.up), right); // in degrees

      bool lowTilt = pitch < 3f && roll < 3f;

      List<string> mods = GetModifierNames(vehicle);
      if (mods.Count == 0) return;
      if (mods.Contains("modVehicleBucket5")) radius = 2;
      for (int dx = -radius; dx <= radius; dx++)
      {
        for (int dy = -radius; dy <= radius; dy++)
        {
          for (int dz = -radius; dz <= radius; dz++)
          {
            Vector3i pos = centerPos + new Vector3i(dx, dy, dz);
            BlockValue current = world.GetBlock(pos);

            if ((current.isair || current.isWater))
            {
              ItemStack[] vehicleInventory = vehicle?.bag?.items;
              if (vehicleInventory == null || vehicleInventory.Length == 0) continue;

              Dictionary<int, ItemStack> fillBlockStack = GetNextTerrainResource(vehicle, vehicleInventory);
              inventoryEmpty = (fillBlockStack == null || fillBlockStack.Count == 0);

              if (fillBlockStack.Count == 0) continue;
              KeyValuePair<int,ItemStack> itemStack = fillBlockStack.First();
              GameManager.Instance.World.SetBlockRPC(clrIdx, pos, itemStack.Value.itemValue.ToBlockValue());

              int newCount = itemStack.Value.count - 1;
              if (newCount <= 0) vehicleInventory[itemStack.Key] = ItemStack.Empty.Clone();
              else vehicleInventory[itemStack.Key] = new ItemStack(itemStack.Value.itemValue.Clone(), newCount);

              vehicle.bag.SetSlot(itemStack.Key, vehicleInventory[itemStack.Key]);

              // Play particles
              if (BobcatConfig.EnableDustParticles)
              {
                Vector3 worldParticlePos = pos.ToVector3() + Vector3.up * 0.25f;
                string nextName = BobcatParticleManager.GetNextDust();
                ParticleEffect pe = new ParticleEffect(nextName, worldParticlePos, Quaternion.identity, 1f, Color.white);
                GameManager.Instance.SpawnParticleEffectServer(pe, GameManager.Instance.myEntityPlayerLocal.entityId, true, true);
              }
            }
          }
        }
      }
    }
    public static void FillTerrain(EntityVehicle vehicle, Vector3i centerPos, int radius, sbyte density, int clrIdx)
    {
      World world = GameManager.Instance.World;
      bool inventoryEmpty = true;

      Vector3 fwd = vehicle.transform.forward;
      float pitch = Vector3.Angle(Vector3.ProjectOnPlane(fwd, Vector3.up), fwd);

      Vector3 right = vehicle.transform.right;
      float roll = Vector3.Angle(Vector3.ProjectOnPlane(right, Vector3.up), right);

      bool lowTilt = pitch < 3f && roll < 3f;

      List<string> mods = GetModifierNames(vehicle);
      if (mods.Count == 0) return;
      if (mods.Contains("modVehicleBucket5")) radius = 2;

      // Collect candidate positions
      List<Vector3i> positionsToFill = new List<Vector3i>();

      for (int dx = -radius; dx <= radius; dx++)
      {
        for (int dy = -radius; dy <= radius; dy++)
        {
          for (int dz = -radius; dz <= radius; dz++)
          {
            Vector3i pos = centerPos + new Vector3i(dx, dy, dz);
            BlockValue current = world.GetBlock(pos);

            if (current.isair || current.isWater)
            {
              positionsToFill.Add(pos);
            }
          }
        }
      }

      // Sort lowest to highest Y
      positionsToFill.Sort((a, b) => a.y.CompareTo(b.y));

      foreach (var pos in positionsToFill)
      {
        Vector3i below = new Vector3i(pos.x, pos.y - 1, pos.z);
        BlockValue belowBlock = world.GetBlock(below);

        // Only place block if there is support underneath
        if (belowBlock.isair || belowBlock.isWater) continue;

        ItemStack[] vehicleInventory = vehicle?.bag?.items;
        if (vehicleInventory == null || vehicleInventory.Length == 0) continue;

        Dictionary<int, ItemStack> fillBlockStack = GetNextTerrainResource(vehicle, vehicleInventory);
        if (fillBlockStack == null || fillBlockStack.Count == 0) continue;

        KeyValuePair<int, ItemStack> itemStack = fillBlockStack.First();
        BlockValue blockToPlace = itemStack.Value.itemValue.ToBlockValue();
        world.SetBlockRPC(clrIdx, pos, blockToPlace);

        int newCount = itemStack.Value.count - 1;
        if (newCount <= 0)
          vehicleInventory[itemStack.Key] = ItemStack.Empty.Clone();
        else
          vehicleInventory[itemStack.Key] = new ItemStack(itemStack.Value.itemValue.Clone(), newCount);

        vehicle.bag.SetSlot(itemStack.Key, vehicleInventory[itemStack.Key]);

        // Play particles
        if (BobcatConfig.EnableDustParticles)
        {
          Vector3 worldParticlePos = pos.ToVector3() + Vector3.up * 0.25f;
          string nextName = BobcatParticleManager.GetNextDust();
          ParticleEffect pe = new ParticleEffect(nextName, worldParticlePos, Quaternion.identity, 1f, Color.white);
          GameManager.Instance.SpawnParticleEffectServer(pe, GameManager.Instance.myEntityPlayerLocal.entityId, true, true);
        }
      }
    }
    public static void FlattenBlockDensityArea(World world, int clrIdx, Vector3i centerPos, sbyte targetDensity, EntityVehicle vehicle)
    {
      if (world == null) return;

      List<BlockChangeInfo> changes = new List<BlockChangeInfo>();
      Vector3 fwd = vehicle.transform.forward;
      float pitch = Vector3.Angle(Vector3.ProjectOnPlane(fwd, Vector3.up), fwd); // in degrees

      Vector3 right = vehicle.transform.right;
      float roll = Vector3.Angle(Vector3.ProjectOnPlane(right, Vector3.up), right); // in degrees

      bool lowTilt = pitch < 3f && roll < 3f;
      if (!lowTilt) return;

      List<string> mods = GetModifierNames(vehicle);
      if(mods.Count == 0) return;

      int leftWidth = -1;
      int rightWidth = 1;

      if (mods.Contains("modVehicleBucket5")) leftWidth = -2; rightWidth = 2;

      for (int dx = leftWidth; dx <= rightWidth; dx++)
      {
        for (int dz = -1; dz <= 1; dz++)
        {
          for (int dy = -1; dy <= 1; dy++) // 1-block vertical range
          {
            Vector3i pos = centerPos + new Vector3i(dx, dy, dz);
            BlockValue current = world.GetBlock(pos);
            if (current.isair || current.isWater) continue;

              current.hasdecal = false;
              current.decaltex = 0;
              current.decalface = BlockFace.None;

            changes.Add(new BlockChangeInfo(clrIdx, pos, current, targetDensity));
          }
        }
      }

      if (changes.Count > 0)
      {
        world.SetBlocksRPC(changes);
      }
    }

    public static Dictionary<int, ItemStack> GetNextTerrainResource(EntityVehicle vehicle, ItemStack[] vehicleInventory)
    {
      Dictionary<int, ItemStack> itemStack = new Dictionary<int, ItemStack>();

      for (int i = 0; i < vehicleInventory.Length; i++)
      {
        ItemStack item = vehicleInventory[i];
        string itemName = item?.itemValue?.ItemClass?.GetItemName();
        int itemCount = item?.count ?? 0;

        if (itemName == null || itemCount < 1) continue;

        if (BobcatConfig.FillModeValidTerrainItems.Contains(itemName))
        {
          itemStack.Add(i, item);
          break;
        }
      }

      return itemStack;
    }
    private static Vector3i RaycastToGround(Vector3i start, int maxDepth)
    {
      for (int y = 0; y < maxDepth; y++)
      {
        Vector3i pos = new Vector3i(start.x, start.y - y, start.z);
        BlockValue blockVal = GameManager.Instance.World.GetBlock(pos);

        if (!blockVal.isair)
        {
          return pos;
        }
      }
      return Vector3i.zero; // Ground not found
    }
    private static Vector3i GetLevelingReferencePosition(EntityVehicle vehicle)
    {
      Vector3 center = vehicle.GetCenterPosition();
      Vector3 checkPos = center + Vector3.down * 2.5f; // Aim below

      // Floor Y
      Vector3i pos = new Vector3i(
          Mathf.RoundToInt(checkPos.x),
          Mathf.FloorToInt(checkPos.y),
          Mathf.RoundToInt(checkPos.z)
      );

      return pos;
    }
    public static void DamageTargets(ScanResults targets, EntityVehicle bobcat)
    {
      Vector3 forwardDir = bobcat.GetLookVector().normalized;
      foreach (var target in targets.Blocks)
      {
        Vector3i blockPos = target.Item1;
        BlockValue blockVal = target.Item2;
        string blockClassName = blockVal.Block.GetBlockName();

        if (BobcatConfig.BlockNamesToIgnoreList.Contains(blockClassName)) return;

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

        GameManager.Instance.SpawnParticleEffectServer(new ParticleEffect("blood_vehicle", entity.position, Quaternion.identity, 1f, Color.red),GameManager.Instance.myEntityPlayerLocal.entityId);
        GameManager.Instance.PlaySoundAtPositionServer(entity.position, "metalslashorganic", AudioRolloffMode.Linear, 20);
      }
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

        alive.emodel.DoRagdoll(1.5f,EnumBodyPartHit.Torso,force,alive.position,false);
      }
    }
    private static int GetClrIdxFromBlockPos(Vector3i blockPos)
    {
      var clusters = GameManager.Instance.World.ChunkClusters;
      for (int i = 0; i < clusters.Count; i++)
      {
        if (clusters[i]?.GetChunk(blockPos.x >> 4, blockPos.z >> 4) != null)
          return i;
      }
      return 0;
    }
    private static int GetChunkKeyFromBlockPos(Vector3i pos)
    {
      int chunkX = pos.x >> 4;
      int chunkZ = pos.z >> 4;
      return (chunkZ << 16) | (chunkX & 0xFFFF);
    }
    public static void UpdateVehicleVisuals(EntityVehicle vehicle)
    {
      VehicleStatic.transformLookup = null;
      VehicleStatic.transformLookup = VehicleStatic.transformLookup = vehicle.gameObject.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());

      List<string> modsInstalled = GetModifierNames(vehicle);
      List<string> cosModsInstalled = GetCosModifierNames(vehicle);
      bool pedalDown = (VehicleStatic.transformLookup.TryGetValue("PedalDown", out var pedalDownT));
      bool pedalDownSuperCharger1 = (VehicleStatic.transformLookup.TryGetValue("PedalDownSuperCharger1", out var pedalDownSuperCharger1T));
      bool pedalDownSuperCharger2 = (VehicleStatic.transformLookup.TryGetValue("PedalDownSuperCharger2", out var pedalDownSuperCharger2T));
      Transform bobcatBodyT = vehicle.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BobcatBody");
      bool headLampsOn = (VehicleStatic.transformLookup.TryGetValue("HeadLampsOn", out var hlOnT));
      bool headLampsOff = (VehicleStatic.transformLookup.TryGetValue("HeadLampsOff", out var hlOffT));
      bool gasCans = (VehicleStatic.transformLookup.TryGetValue("GasCans", out var gasCansT));
      bool bucket3 = (VehicleStatic.transformLookup.TryGetValue("Plow3", out var bucket3T));
      bool bucket5 = (VehicleStatic.transformLookup.TryGetValue("Plow5", out var bucket5T));
      bool armor = (VehicleStatic.transformLookup.TryGetValue("Armor", out var armorT));
      List<Transform> bucketTs = new List<Transform> { bucket3T, bucket5T };
      List<string> bucketNames = new List<string> { "modVehicleBucket3", "modVehicleBucket5" };
      bool drillOff = (VehicleStatic.transformLookup.TryGetValue("DrillOff", out var drillOffT));
      bool drillOn = (VehicleStatic.transformLookup.TryGetValue("DrillOn", out var drillOnT));
      bool bucketInstalled = bucketNames.Any(mod => modsInstalled.Contains(mod));
      if (!bucketInstalled) DisableTransforms(bucketTs);

      if (bobcatBodyT == null) return;

      // Does not have a dye
      if (cosModsInstalled.Count == 0)
      {
        SetDyeColor(vehicle, bobcatBodyT, "#FFFFFF");

        ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
        newVehItemVal.CosmeticMods = new ItemValue[]
        {
                new ItemValue(ItemClass.GetItem("modDyeWhite").type, true)
        };

        vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
      }

      // Has a dye
      if (cosModsInstalled.Count > 0)
      {
        foreach (var mod in cosModsInstalled) Log.Warning(mod);
        string cosmodInstalled = cosModsInstalled[0];

        switch (cosmodInstalled)
        {
          case "modDyeOrange":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#FFA500");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyeOrange").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          case "modDyeBrown":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#964B00");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyeBrown").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          case "modDyeRed":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#FF0000");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyeRed").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          case "modDyeYellow":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#FFFF00");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyeYellow").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          case "modDyeGreen":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#008000");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyeGreen").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          case "modDyeBlue":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#0000FF");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyeBlue").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          case "modDyePurple":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#5D3FD3");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyePurple").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          case "modDyeBlack":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#111111");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyeBlack").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          case "modDyePink":
            {
              SetDyeColor(vehicle, bobcatBodyT, "#FFC0CB");

              ItemValue newVehItemVal = vehicle.GetVehicle().itemValue.Clone();
              newVehItemVal.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyePink").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
              break;
            }

          default:
            {
              SetDyeColor(vehicle, bobcatBodyT, "#FFFFFF");

              ItemValue newVehItemValWhite = vehicle.GetVehicle().itemValue.Clone();
              newVehItemValWhite.CosmeticMods = new ItemValue[]
              {
                new ItemValue(ItemClass.GetItem("modDyeWhite").type, true)
              };

              vehicle.GetVehicle().SetItemValueMods(newVehItemValWhite);
              break;
            }
        }
      }

      // Supercharger
      if (modsInstalled.Contains("modVehicleSuperCharger"))
      {
        pedalDownT.gameObject.SetActive(false);
        pedalDownSuperCharger1T.gameObject.SetActive(true);
        pedalDownSuperCharger2T.gameObject.SetActive(true);
      }
      else
      {
        pedalDownT.gameObject.SetActive(true);
        pedalDownSuperCharger1T.gameObject.SetActive(false);
        pedalDownSuperCharger2T.gameObject.SetActive(false);
      }

      // Offroad headlights
      if (modsInstalled.Contains("modVehicleOffRoadHeadlights"))
      {
        hlOnT.gameObject.SetActive(true);
        hlOffT.gameObject.SetActive(true);
      }
      else
      {
        hlOnT.gameObject.SetActive(false);
        hlOffT.gameObject.SetActive(false);
      }

      // Fuel Tank
      if (modsInstalled.Contains("modVehicleReserveFuelTank")) gasCansT.gameObject.SetActive(true);
      else if (gasCans) gasCansT.gameObject.SetActive(false);

      // Armor
      if (modsInstalled.Contains("modVehicleArmor")) armorT.gameObject.SetActive(true);
      else if (armor) armorT.gameObject.SetActive(false);

      // Buckets
      if (modsInstalled.Contains("modVehicleBucket3")) EnableDisableTransforms(new List<Transform> { bucket3T }, new List<Transform> { bucket5T });
      if (modsInstalled.Contains("modVehicleBucket5")) EnableDisableTransforms(new List<Transform> { bucket5T }, new List<Transform> { bucket3T });
      if (!bucketInstalled) DisableTransforms(bucketTs);

      // Drill
      if (modsInstalled.Contains("modVehicleDrill")) SetAmbientDrillParticles(vehicle, BobcatConfig.EnableDrillParticles);
      if (modsInstalled.Contains("modVehicleDrill") && vehicle.IsDriven()) ActivateDrillOn(vehicle);
      if (modsInstalled.Contains("modVehicleDrill") && !vehicle.IsDriven()) ActivateDrillOff(vehicle);
      if (!modsInstalled.Contains("modVehicleDrill")) DisableTransforms(new List<Transform> { drillOnT, drillOffT });
      if (!modsInstalled.Contains("modVehicleDrill")) DisableDrillAudio(vehicle);
    }

    public static void SetDyeColor(EntityVehicle vehicle, Transform part, string hex)
    {
      Renderer renderer = part.GetComponent<Renderer>();
      Material mat = renderer.material;
      mat.color = HexToColor(hex);
    }
    public static Color HexToColor(string hex)
    {
      if (string.IsNullOrEmpty(hex))
        return Color.white;

      hex = hex.Replace("#", "");

      if (hex.Length == 6) // RGB
      {
        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        return new Color32(r, g, b, 255);
      }
      else if (hex.Length == 8) // RGBA
      {
        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        byte a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
        return new Color32(r, g, b, a);
      }

      Debug.LogWarning("Invalid hex color format: " + hex);
      return Color.white;
    }
    public static int GetEntityDamageByConfig(EntityAlive entity)
    {
      string entityClassName = entity.EntityClass.entityClassName;
      ProgressionValue miner69erProgression = GameManager.Instance.myEntityPlayerLocal?.Progression?.GetProgressionValue("perkMiner69r");
      int perkDamageBonus = miner69erProgression != null ? miner69erProgression.Level : 1;
      float[] progressionToMultiplier = new float[6] { 1.0f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f };

      int entityDamage = 0;

      switch (entityClassName)
      {
        case string name when name.Contains("feral"):
          entityDamage = Mathf.RoundToInt((BobcatConfig.EntityDamage * BobcatConfig.FeralZombieDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
          break;
        case string name when name.Contains("radiated"):
          entityDamage = Mathf.RoundToInt((BobcatConfig.EntityDamage * BobcatConfig.RadiatedZombieDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
          break;
        case string name when name.Contains("charged"):
          entityDamage = Mathf.RoundToInt((BobcatConfig.EntityDamage * BobcatConfig.ChargedZombieDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
          break;
        case string name when name.Contains("infernal"):
          entityDamage = Mathf.RoundToInt((BobcatConfig.EntityDamage * BobcatConfig.InfernalZombieDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
          break;
        case string name when name.Contains("animal"):
          entityDamage = Mathf.RoundToInt((BobcatConfig.EntityDamage * BobcatConfig.AnimalDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
          break;
        default:
          entityDamage = Mathf.RoundToInt((BobcatConfig.EntityDamage * BobcatConfig.ZombieDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
          break;
      }

      return entityDamage;
    }
    public static int GetBlockDamageByConfig(Block block)
    {
      string blockMaterial = block.blockMaterial.id;
      ProgressionValue miner69erProgression = GameManager.Instance.myEntityPlayerLocal?.Progression?.GetProgressionValue("perkMiner69r");
      int perkDamageBonus = miner69erProgression != null ? miner69erProgression.Level : 1;
      float[] progressionToMultiplier = new float[6] { 1.0f, 1.3f, 1.6f, 1.9f, 2.2f, 2.5f };

      if (string.IsNullOrEmpty(blockMaterial)) return BobcatConfig.TerrainDamage;

      string[] wood = new string[3] { "Mwood", "Mdrywall", "MwoodOld" };
      string[] concrete = new string[3] { "Mconcrete", "MconcretePolished", "Mtiles" };
      string[] metal = new string[6] { "Mmetal", "Msteel", "Mframe", "MscrapMetal", "Mpanel", "MmetalOld" };
      string[] cloth = new string[5] { "Mcloth", "Mcarpet", "Mpaper", "Mplastic", "MclothOld" };
      string[] glass = new string[1] { "Mglass" };
      string[] stone = new string[1] { "Mstone" };
      string[] ground = new string[8] { "Mground", "Msand", "Mgravel", "Msnow", "Mliquid", "Mlava", "Msoil", "Mbone" };
      string[] plant = new string[2] { "Mplant", "Mhay" };
      string[] flesh = new string[3] { "Morganic", "Mleaves", "Mtrash" };
      string[] other = new string[4] { "Muntagged", "Mtar", "Mpaver", "Mpaint" };


      if (wood.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.WoodDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
      if (concrete.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.ConcreteDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
      if (metal.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.MetalDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
      if (cloth.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.ClothDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
      if (glass.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.GlassDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
      if (stone.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.StoneDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
      if (ground.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.DirtDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
      if (plant.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.PlantDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);
      if (flesh.Contains(blockMaterial)) return Mathf.RoundToInt((BobcatConfig.TerrainDamage * BobcatConfig.FleshDamageMultiplier) * progressionToMultiplier[perkDamageBonus]);

      return BobcatConfig.TerrainDamage;
    }
    public static int GetBlockHarvestAmountByConfig(Block block, int baseAmount)
    {
      string blockMaterial = block.blockMaterial.id;
      ProgressionValue motherlodeProgression = GameManager.Instance.myEntityPlayerLocal?.Progression?.GetProgressionValue("perkMotherLode");
      int perkHarvestBonus = motherlodeProgression != null ? motherlodeProgression.Level : 1;
      float[] progressionToMultiplier = new float[6] { 1.0f, 1.2f, 1.4f, 1.6f, 1.8f, 2.0f };

      if (string.IsNullOrEmpty(blockMaterial)) return baseAmount;

      string[] wood = new string[3] { "Mwood", "Mdrywall", "MwoodOld" };
      string[] concrete = new string[3] { "Mconcrete", "MconcretePolished", "Mtiles" };
      string[] metal = new string[6] { "Mmetal", "Msteel", "Mframe", "MscrapMetal", "Mpanel", "MmetalOld" };
      string[] cloth = new string[5] { "Mcloth", "Mcarpet", "Mpaper", "Mplastic", "MclothOld" };
      string[] glass = new string[1] { "Mglass" };
      string[] stone = new string[1] { "Mstone" };
      string[] ground = new string[8] { "Mground", "Msand", "Mgravel", "Msnow", "Mliquid", "Mlava", "Msoil", "Mbone" };
      string[] plant = new string[2] { "Mplant", "Mhay" };
      string[] flesh = new string[3] { "Morganic", "Mleaves", "Mtrash" };
      string[] other = new string[4] { "Muntagged", "Mtar", "Mpaver", "Mpaint" };

      if (wood.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.WoodHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);
      if (concrete.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.ConcreteHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);
      if (metal.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.MetalHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);
      if (cloth.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.ClothHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);
      if (glass.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.GlassHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);
      if (stone.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.StoneHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);
      if (ground.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.DirtHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);
      if (plant.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.PlantHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);
      if (flesh.Contains(blockMaterial)) return Mathf.RoundToInt((baseAmount * BobcatConfig.FleshHarvestMultiplier) * progressionToMultiplier[perkHarvestBonus]);

      return baseAmount;
    }
    public static IEnumerator UpdateVehicleVisualsDelayed(EntityVehicle vehicle)
    {
      yield return new WaitForSeconds(0.5f);
      if (vehicle != null) UpdateVehicleVisuals(vehicle);

      yield break;
    }
    public static void EnableDisableTransforms(List<Transform> enableT, List<Transform> disableT)
    {
      foreach (Transform t in enableT) t.gameObject.SetActive(true);
      foreach (Transform t in disableT) t.gameObject.SetActive(false);
    }
    public static void DisableTransforms(List<Transform> disableT)
    {
      foreach (Transform t in disableT) t.gameObject?.SetActive(false);
    }
    public static void ActivateDrillOn(EntityVehicle vehicle)
    {
      bool drillOn = VehicleStatic.transformLookup.TryGetValue("DrillOn", out var drillOnT);
      bool drillOff = VehicleStatic.transformLookup.TryGetValue("DrillOff", out var drillOffT);
      bool audio = VehicleStatic.transformLookup.TryGetValue("Audio", out var audioT);

      // Only enable operating sounds and particles when in landscaping mode, else show inactive
      if (VehicleStatic.CurrentMode == VehicleStatic.BobcatMode.LandscapingHigh)
      {
        EnableDisableTransforms(new List<Transform> { drillOffT }, new List<Transform> { drillOnT });
        return;
      }

      EnableDisableTransforms(new List<Transform> { drillOnT }, new List<Transform> { drillOffT });

      // Get all Animator components in DrillOn and its children
      Animator[] animators = drillOnT.GetComponentsInChildren<Animator>(true);

      foreach (var animator in animators)
      {
        if (animator == null) continue;

        animator.enabled = true;

        string[] clipNames = new[] { "DrillSpin1", "DrillSpin2", "DrillSpin3" };
        foreach (var clipName in clipNames)
        {
          if (animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Any(c => c.name == clipName))
          {
            animator.Play(clipName, 0, 0f);
            break;
          }
        }

        // Run drilling audio
        EnableDrillAudio(vehicle);
      }
    }
    public static void ActivateDrillOff(EntityVehicle vehicle)
    {
      bool drillOn = VehicleStatic.transformLookup.TryGetValue("DrillOn", out var drillOnT);
      bool drillOff = VehicleStatic.transformLookup.TryGetValue("DrillOff", out var drillOffT);

      List<string> mods = GetModifierNames(vehicle);
      if (mods.Contains("modVehicleDrill"))
      {
        EnableDisableTransforms(new List<Transform>() { drillOffT }, new List<Transform>() { drillOnT });
        DisableDrillAudio(vehicle);
      } else
      {
        DisableTransforms(new List<Transform>() { drillOnT, drillOffT });
        DisableDrillAudio(vehicle);
      }

      // Get all Animator components in DrillOn and its children
      Animator[] animators = drillOnT.GetComponentsInChildren<Animator>(true);
      foreach (var animator in animators)
      {
        if (animator == null) continue;

        animator.enabled = true;

        // Start the appropriate clip by name
        string[] clipNames = new[] { "DrillSpin1", "DrillSpin2", "DrillSpin3" };
        foreach (var clipName in clipNames)
        {
          if (animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Any(c => c.name == clipName))
          {
            animator.StopPlayback();
            break;
          }
        }
      }
    }
    public static void EnableDrillAudio(EntityVehicle vehicle)
    {
      bool audio = VehicleStatic.transformLookup.TryGetValue("Audio", out var audioT);
      Transform rockDrillT = audioT.Find("RockDrilling");
      AudioSource drillAudio = rockDrillT.GetComponent<AudioSource>();
      if (drillAudio != null && !drillAudio.isPlaying)
      {
        drillAudio.volume = BobcatConfig.DrillAudioVolume;
        drillAudio.Play();
      }
    }
    public static void DisableDrillAudio(EntityVehicle vehicle)
    {
      bool audio = VehicleStatic.transformLookup.TryGetValue("Audio", out var audioT);
      Transform rockDrillT = audioT.Find("RockDrilling");
      AudioSource drillAudio = rockDrillT.GetComponent<AudioSource>();
      if (drillAudio != null) drillAudio.Stop();
    }
    private static void PopulateVehicleTransformHandles(EntityVehicle vehicle)
    {
      VehicleStatic.transformLookup = null;
      VehicleStatic.transformLookup = VehicleStatic.transformLookup = vehicle.gameObject.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
    }
    private static void HandleModeChange(EntityVehicle vehicle, PlayerActionsVehicle actions)
    {
      if (actions?.Hop == null) return;

      bool isHopPressed = actions.Hop.IsPressed;
      if (isHopPressed && !VehicleStatic.wasHopPressedLastFrame) GameManager.Instance.StartCoroutine(HandleModeChangeOnce(vehicle));

      VehicleStatic.wasHopPressedLastFrame = isHopPressed;
      
    }
    private static void HandleHornToggle(EntityVehicle vehicle, PlayerActionsVehicle actions)
    {
      if (actions?.HonkHorn == null) return;

      bool hornPressed = actions.HonkHorn.IsPressed;

      if (hornPressed && !VehicleStatic.wasHornPressedLastFrame)
      {
        // Toggle mode activation
        if(VehicleStatic.CurrentMode != VehicleStatic.BobcatMode.None) VehicleStatic.isCurrentModeActive = !VehicleStatic.isCurrentModeActive;
        if(VehicleStatic.CurrentMode == VehicleStatic.BobcatMode.None) GameManager.Instance.PlaySoundAtPositionServer(vehicle.position, "Bobcat_horn", AudioRolloffMode.Linear, 20);
        SetVehicleActivateWindow(true);

        // Set leveling/filling reference height on activated
        if (VehicleStatic.isCurrentModeActive) VehicleStatic.vehicleLevelingModeHeight = GetLevelingReferencePosition(vehicle).y;
      }

      VehicleStatic.wasHornPressedLastFrame = hornPressed;
    }
    private static IEnumerator HandleModeChangeOnce(EntityVehicle vehicle)
    {
      string entityName = EntityClass.list[vehicle.entityClass].entityClassName;
      if (entityName == "vehicleBobcat")
      {
        var modeValues = Enum.GetValues(typeof(VehicleStatic.BobcatMode)).Cast<VehicleStatic.BobcatMode>().ToList();
        int currentIndex = modeValues.IndexOf(VehicleStatic.CurrentMode);
        bool bobcatAudio = VehicleStatic.transformLookup.TryGetValue("Audio", out var bobcatAudioT);

        AudioSource fillAudioComponent = bobcatAudioT.Find("RockLeveling").GetComponentInChildren<AudioSource>();
        AudioSource modeChangeAudioComponent = bobcatAudioT.Find("ModeChange").GetComponentInChildren<AudioSource>();

        // Attempt to find the next valid mode
        for (int i = 1; i <= modeValues.Count; i++)
        {
          int nextIndex = (currentIndex + i) % modeValues.Count;
          var nextMode = modeValues[nextIndex];

          if (IsBobcatModeEnabled(nextMode))
          {
            VehicleStatic.CurrentMode = nextMode;
            modeChangeAudioComponent.volume = BobcatConfig.ModeChangeAudioVolume;
            modeChangeAudioComponent.Play();
            break;
          }
        }

        SetVehicleModeSpeed(vehicle);

        // Handle mode audio
        if (VehicleStatic.CurrentMode == VehicleStatic.BobcatMode.Filling) GameManager.Instance.StartCoroutine(FadeInAudio(fillAudioComponent, 2, BobcatConfig.LevelingModeAudioVolume));
        else GameManager.Instance.StartCoroutine(FadeOutAudio(fillAudioComponent, 2, 1f));

        // UI
        VehicleStatic.isCurrentModeActive = false;
        SetVehicleStatusWindow(true);
        SetVehicleActivateWindow(true);

        GameManager.Instance.StopCoroutine(RunVehicleMode(VehicleStatic.CurrentMode, vehicle));
        GameManager.Instance.StartCoroutine(RunVehicleMode(VehicleStatic.CurrentMode, vehicle));
        
      }

      yield break;
    }
    public static void SetTractionControl(EntityVehicle vehicle)
    {
      Transform physicsTransform = vehicle.PhysicsTransform;
      WheelCollider[] wheels = physicsTransform.GetComponentsInChildren<WheelCollider>();
      bool hasTractionControl = GetModifierNames(vehicle).Contains("modTractionControl");
      bool disableTractionControl = (!BobcatConfig.EnableTractionControl || !hasTractionControl);

      foreach (var wheel in wheels)
      {
        wheel.mass = disableTractionControl ? 40 : 1000;

        WheelFrictionCurve forwardFriction = wheel.forwardFriction;
        forwardFriction.stiffness = disableTractionControl ? 1.3f : 5f;
        forwardFriction.extremumSlip = disableTractionControl ? 0.4f : 2f;
        forwardFriction.extremumValue = disableTractionControl ? 1f : 2f;
        forwardFriction.asymptoteSlip = disableTractionControl ? 0.8f : 2f;
        forwardFriction.asymptoteValue = disableTractionControl ? 0.5f : 2f;
        wheel.forwardFriction = forwardFriction;

        WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
        sidewaysFriction.stiffness = disableTractionControl ? 1.3f : 5f;
        sidewaysFriction.extremumSlip = disableTractionControl ? 0.2f : 2f;
        sidewaysFriction.extremumValue = disableTractionControl ? 2f : 2f;
        sidewaysFriction.asymptoteSlip = disableTractionControl ? 0.75f : 2f;
        sidewaysFriction.asymptoteValue = disableTractionControl ? 0.75f : 2f;
        wheel.sidewaysFriction = sidewaysFriction;
      }
    }
    private static bool IsBobcatModeEnabled(VehicleStatic.BobcatMode mode)
    {
      var vehicle = GameManager.Instance.myEntityPlayerLocal?.AttachedToEntity as EntityVehicle;
      if (vehicle == null) return false;

      List<string> mods = GetModifierNames(vehicle);

      bool hasDrill = mods.Contains("modVehicleDrill");
      bool hasBucket = mods.Any(m => m.StartsWith("modVehicleBucket"));

      switch (mode)
      {
        case VehicleStatic.BobcatMode.Tunneling:
          return hasDrill && BobcatConfig.EnableTunnelingMode;
        case VehicleStatic.BobcatMode.LandscapingHigh:
        case VehicleStatic.BobcatMode.Leveling:
        case VehicleStatic.BobcatMode.Filling:
        case VehicleStatic.BobcatMode.Smoothing:
          return hasBucket && GetConfigEnabledForMode(mode);
        case VehicleStatic.BobcatMode.None:
          return true;
        default:
          return false;
      }
    }
    private static bool GetConfigEnabledForMode(VehicleStatic.BobcatMode mode)
    {
      switch (mode)
      {
        case VehicleStatic.BobcatMode.LandscapingHigh: return BobcatConfig.EnableLandscapingHighMode;
        case VehicleStatic.BobcatMode.Tunneling: return BobcatConfig.EnableTunnelingMode;
        case VehicleStatic.BobcatMode.Filling: return BobcatConfig.EnableFillMode;
        case VehicleStatic.BobcatMode.Leveling: return BobcatConfig.EnableLevelingMode;
        case VehicleStatic.BobcatMode.Smoothing: return BobcatConfig.EnableTerrainSmoothing;
        default: return false;
      }
    }
    private static IEnumerator FadeOutAudio(AudioSource audio, float secondsToFade, float startVolume)
    {
      if (audio == null || !audio.isPlaying) yield break;

      float time = 0f;

      while (time < secondsToFade)
      {
        if (audio == null) yield break;

        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / secondsToFade);
        audio.volume = Mathf.Lerp(startVolume, 0f, t);
        yield return null;
      }

      if (audio != null)
      {
        audio.volume = 0f;
        audio.Stop();
      }
      VehicleStatic.vehicleModeAudioSources?.Remove(audio);
    }
    private static IEnumerator FadeInAudio(AudioSource audio, float secondsToFade, float targetVolume)
    {
      if (audio == null) yield break;

      audio.volume = 0f;
      audio.Play();
      VehicleStatic.vehicleModeAudioSources.Add(audio);

      float time = 0f;
      while (time < secondsToFade)
      {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / secondsToFade);
        audio.volume = Mathf.Lerp(0f, targetVolume, t);
        yield return null;
      }

      audio.volume = BobcatConfig.DrillAudioVolume;
    }

    // Handle light on/off for offroad lights mod
    private static void HandleLightChange(EntityVehicle vehicle)
    {
      bool lightIsOn = vehicle.IsHeadlightOn;

      // Lights turned ON this frame
      if (lightIsOn && !VehicleStatic.lightWasOnLastFrame)
      {
        if (VehicleStatic.lightCoroutine == null) VehicleStatic.lightCoroutine = GameManager.Instance.StartCoroutine(HandleLightChangeOnce(vehicle));
      }

      // Lights turned OFF this frame
      else if (!lightIsOn && VehicleStatic.lightWasOnLastFrame)
      {
        if (VehicleStatic.lightCoroutine != null)
        {
          GameManager.Instance.StopCoroutine(VehicleStatic.lightCoroutine);
          DisableLightChangeEffect(vehicle);
          VehicleStatic.lightCoroutine = null;
        }
      }

      VehicleStatic.lightWasOnLastFrame = lightIsOn;
    }
    private static IEnumerator HandleLightChangeOnce(EntityVehicle vehicle)
    {
      EnableLightChangeEffect(vehicle);
      while (true)
      {
        yield return null; // just hold state while lights remain on
      }
    }
    private static void EnableLightChangeEffect(EntityVehicle vehicle)
    {
      List<string> modifierNames = GetModifierNames(vehicle);
      if (modifierNames.Contains("modVehicleOffRoadHeadlights"))
      {
        if (VehicleStatic.transformLookup.TryGetValue("HeadLampsOn", out var hlOn)) hlOn.gameObject.SetActive(true);
        if (VehicleStatic.transformLookup.TryGetValue("HeadLampsOff", out var hlOff)) hlOff.gameObject.SetActive(false);
      }
    }
    private static void DisableLightChangeEffect(EntityVehicle vehicle)
    {
      List<string> modifierNames = GetModifierNames(vehicle);
      if (modifierNames.Contains("modVehicleOffRoadHeadlights"))
      {
        if (VehicleStatic.transformLookup.TryGetValue("HeadLampsOn", out var hlOn)) hlOn.gameObject.SetActive(false);
        if (VehicleStatic.transformLookup.TryGetValue("HeadLampsOff", out var hlOff)) hlOff.gameObject.SetActive(true);
      }
    }

    // Handle turbo held for particles
    private static void HandleTurbo(EntityVehicle vehicle, PlayerActionsVehicle actions)
    {
      if (actions?.Turbo == null) return;

      bool isTurboHeld = actions.Turbo.IsPressed;

      if (isTurboHeld && VehicleStatic.turboCoroutine == null) VehicleStatic.turboCoroutine = GameManager.Instance.StartCoroutine(HandleTurboWhileHeld(vehicle));
      else if (!isTurboHeld && VehicleStatic.turboCoroutine != null)
      {
        GameManager.Instance.StopCoroutine(VehicleStatic.turboCoroutine);
        VehicleStatic.turboCoroutine = null;

        ResetDrillAudioAnim(vehicle);
      }
    }
    private static IEnumerator HandleTurboWhileHeld(EntityVehicle vehicle)
    {
      List<string> mods = GetModifierNames(vehicle);
      bool hasSuperCharger = mods.Contains("modVehicleSuperCharger");
      ParticleSystem exhaustTurbo = null;
      ParticleSystem exhaustSuperTurbo1 = null;
      ParticleSystem exhaustSuperTurbo2 = null;
      ParticleSystem.MinMaxCurve exhaustMinMaxSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
      ParticleSystem.MinMaxCurve superChargerExhaustMinMaxSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
      bool exhaustTurboTransformFound = VehicleStatic.transformLookup.TryGetValue("PedalDown", out var exhaustTurboTransform);
      bool exhaustSuperTurboTransform1Found = VehicleStatic.transformLookup.TryGetValue("PedalDownSuperCharger1", out var exhaustSuperTurboTransform1);
      bool exhaustSuperTurboTransform2Found = VehicleStatic.transformLookup.TryGetValue("PedalDownSuperCharger2", out var exhaustSuperTurboTransform2);
      bool[] foundStates = new bool[3] { exhaustTurboTransformFound, exhaustSuperTurboTransform1Found, exhaustSuperTurboTransform2Found };


      if (foundStates.All(state => state == true))
      {
        if (hasSuperCharger)
        {
          exhaustSuperTurboTransform1.gameObject.SetActive(true);
          exhaustSuperTurboTransform2.gameObject.SetActive(true);
          exhaustSuperTurbo1 = exhaustSuperTurboTransform1.GetComponent<ParticleSystem>();
          exhaustSuperTurbo2 = exhaustSuperTurboTransform2.GetComponent<ParticleSystem>();
          if (exhaustSuperTurbo1 != null && !exhaustSuperTurbo1.isPlaying) exhaustSuperTurbo1.Play();
          if (exhaustSuperTurbo2 != null && !exhaustSuperTurbo2.isPlaying) exhaustSuperTurbo2.Play();
        }
        else
        {
          exhaustTurboTransform.gameObject.SetActive(true);
          exhaustTurbo = exhaustTurboTransform.GetComponent<ParticleSystem>();
          if (exhaustTurbo != null && !exhaustTurbo.isPlaying) exhaustTurbo.Play();
        }
      }

      while (true)
      {
        if (exhaustTurbo != null) UpdateTurboParticle(exhaustTurbo, vehicle, hasSuperCharger, exhaustMinMaxSize);
        if (exhaustSuperTurbo1 != null) UpdateTurboParticle(exhaustSuperTurbo1, vehicle, hasSuperCharger, superChargerExhaustMinMaxSize);
        if (exhaustSuperTurbo2 != null) UpdateTurboParticle(exhaustSuperTurbo2, vehicle, hasSuperCharger, superChargerExhaustMinMaxSize);
        UpdateDrillAudioAnimBySpeed(vehicle);

        yield return new WaitForSeconds(0.1f);
      }
    }
    private static void UpdateTurboParticle(ParticleSystem particle, EntityVehicle vehicle, bool hasSuperCharger, ParticleSystem.MinMaxCurve curve)
    {
      if (particle == null) return;

      var main = particle.main;
      var emission = particle.emission;

      if (hasSuperCharger) emission.rateOverTime = vehicle.speedForward * 50;
      else emission.rateOverTime = vehicle.speedForward * 100;

      main.startSize = curve;
      if (!particle.isPlaying) particle.Play();
    }
    private static void UpdateDrillAudioPitch(EntityVehicle vehicle)
    {
      bool drillAudio = VehicleStatic.transformLookup.TryGetValue("Audio", out var drillAudioT);
      AudioSource drillAudioComponent = drillAudioT.Find("RockDrilling").GetComponentInChildren<AudioSource>();
      if (drillAudioComponent.isPlaying) drillAudioComponent.pitch = Mathf.Clamp(vehicle.GetVehicle().CurrentForwardVelocity * 0.4f, 0.5f, 2f);
    }
    private static void UpdateDrillAnimationSpeed(EntityVehicle vehicle)
    {
      bool drillOn = VehicleStatic.transformLookup.TryGetValue("DrillOn", out var drillOnT);
      Animator[] animators = drillOnT.GetComponentsInChildren<Animator>(true);
      foreach (var animator in animators)
      {
        if (animator == null) continue;

        animator.enabled = true;

        string[] clipNames = new[] { "DrillSpin1", "DrillSpin2", "DrillSpin3" };
        foreach (var clipName in clipNames)
        {
          if (animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Any(c => c.name == clipName))
          {
            animator.speed = Mathf.Clamp(vehicle.GetVehicle().CurrentForwardVelocity, 0.1f, 3);
            break;
          }
        }
      }
    }
    private static void UpdateDrillAudioAnimBySpeed(EntityVehicle vehicle)
    {
      UpdateDrillAudioPitch(vehicle);
      UpdateDrillAnimationSpeed(vehicle);
    }
    private static void ResetDrillAudioAnim(EntityVehicle vehicle)
    {
      // Reset drill pitch to default
      if (VehicleStatic.transformLookup.TryGetValue("Audio", out var audioT))
      {
        var drill = audioT.Find("RockDrilling");
        var audio = drill?.GetComponent<AudioSource>();
        if (audio != null) audio.pitch = 1f;
      }

      // Reset drill animator speed
      if (VehicleStatic.transformLookup.TryGetValue("DrillOn", out var drillT))
      {
        var animators = drillT.GetComponentsInChildren<Animator>(true);
        foreach (var animator in animators)
        {
          if (animator != null) animator.speed = 1f;
        }
      }
    }


    // Regular vehicle running handler for displaying mods based on equipped
    public static void HandleEnterVehicle(EntityVehicle vehicle)
    {
      PopulateVehicleTransformHandles(vehicle);
      List<string> modifierNames = GetModifierNames(vehicle);

      Transform exhaust = VehicleStatic.transformLookup["Exhaust"];
      Transform exhaustSuperCharger = VehicleStatic.transformLookup["ExhaustSuperCharger"];
      Transform pedalDown = VehicleStatic.transformLookup["PedalDown"];
      Transform pedalDownSuperCharger1 = VehicleStatic.transformLookup["PedalDownSuperCharger1"];
      Transform pedalDownSuperCharger2 = VehicleStatic.transformLookup["PedalDownSuperCharger2"];
      Transform gasCans = VehicleStatic.transformLookup["GasCans"];

      bool hasSuperCharger = modifierNames.Contains("modVehicleSuperCharger");
      bool hasOffRoadLights = modifierNames.Contains("modVehicleOffRoadHeadlights");
      bool hasGasCans = modifierNames.Contains("modVehicleReserveFuelTank");

      VehicleStatic.SetMaxVelocityOnce(vehicle);

      // Supercharger visual setup
      if (hasSuperCharger)
      {
        exhaust.gameObject.SetActive(false);
        exhaustSuperCharger.gameObject.SetActive(true);

        pedalDownSuperCharger1.gameObject.SetActive(true);
        pedalDownSuperCharger2.gameObject.SetActive(true);

        StartParticleIfAvailable(pedalDownSuperCharger1);
        StartParticleIfAvailable(pedalDownSuperCharger2);
      }
      else
      {
        exhaust.gameObject.SetActive(true);
        exhaustSuperCharger.gameObject.SetActive(false);

        pedalDown.gameObject.SetActive(true);
        StartParticleIfAvailable(pedalDown);
      }

      // Light setup
      if (hasOffRoadLights && vehicle.IsHeadlightOn)
      {
        VehicleStatic.transformLookup["HeadLampsOn"].gameObject.SetActive(true);
        VehicleStatic.transformLookup["HeadLampsOff"].gameObject.SetActive(false);
      }
      else if (hasOffRoadLights)
      {
        VehicleStatic.transformLookup["HeadLampsOn"].gameObject.SetActive(false);
        VehicleStatic.transformLookup["HeadLampsOff"].gameObject.SetActive(true);
      }
      else
      {
        VehicleStatic.transformLookup["HeadLampsOn"].gameObject.SetActive(false);
        VehicleStatic.transformLookup["HeadLampsOff"].gameObject.SetActive(false);
      }

      /// Fuel Tank
      if (hasGasCans) VehicleStatic.transformLookup["GasCans"].gameObject.SetActive(true);
      else VehicleStatic.transformLookup["GasCans"].gameObject.SetActive(false);

      // Drill
      if (modifierNames.Contains("modVehicleDrill") && vehicle.IsDriven()) ActivateDrillOn(vehicle);
      else ActivateDrillOff(vehicle);

      // Set falling block damage to 0 if armor mod is installed
      if (modifierNames.Contains("modVehicleArmor") && vehicle.IsDriven()) GameManager.Instance.myEntityPlayerLocal.Buffs.AddBuff("buffNoFallingBlockDamage");

      // Traction control
      SetTractionControl(vehicle);

      // UI
      SetVehicleStatusWindow(true);
      SetVehicleActivateWindow(true);
    }
    private static void StartParticleIfAvailable(Transform transform)
    {
      if (transform == null) return;
      var ps = transform.GetComponent<ParticleSystem>();
      if (ps != null && !ps.isPlaying)
      {
        ps.Play();
      }
    }
    private static void HandleExitVehicle(EntityVehicle vehicle)
    {
      StopParticleIfAvailable("PedalDown");
      StopParticleIfAvailable("PedalDownSuperCharger1");
      StopParticleIfAvailable("PedalDownSuperCharger2");
      List<string> modifierNames = GetModifierNames(vehicle);

      // Set mode back to none
      VehicleStatic.CurrentMode = VehicleStatic.BobcatMode.None;

      // Reset wheel stiffness when not in operating mode
      SetTractionControl(vehicle);
      if (VehicleStatic.turboCoroutine != null)
      {
        GameManager.Instance.StopCoroutine(VehicleStatic.turboCoroutine);
        VehicleStatic.turboCoroutine = null;
      }

      if (VehicleStatic.lightCoroutine != null)
      {
        GameManager.Instance.StopCoroutine(VehicleStatic.lightCoroutine);
        VehicleStatic.lightCoroutine = null;
      }

      ActivateDrillOff(vehicle);

      // Stop running audio
      if(VehicleStatic.vehicleModeAudioSources.Count > 0)
      {
        foreach(AudioSource audio in VehicleStatic.vehicleModeAudioSources) GameManager.Instance.StartCoroutine(FadeOutAudio(audio, 2, audio.volume));
      }

      // Restore falling block damage on exit vehicle
      GameManager.Instance.myEntityPlayerLocal.Buffs.RemoveBuff("buffNoFallingBlockDamage");

      // Update speed when exiting vehicle
      SetVehicleModeSpeed(vehicle);

      // UI
      SetVehicleStatusWindow(false);
      SetVehicleActivateWindow(false);
      VehicleStatic.isCurrentModeActive = false;

    }
    public static void SetVehicleStatusWindow(bool show)
    {
      string label = "None";
      string spriteName = "BobcatStatusBGNone";
      switch (VehicleStatic.CurrentMode)
      {
        case VehicleStatic.BobcatMode.None:
          label = "None";
          spriteName = "BobcatStatusBGNone";
          break;
        case VehicleStatic.BobcatMode.LandscapingHigh:
          label = "Landscape";
          spriteName = "BobcatStatusBGLandscapingHigh";
          break;
        case VehicleStatic.BobcatMode.Leveling:
          label = "Leveling";
          spriteName = "BobcatStatusBGLeveling";
          break;
        case VehicleStatic.BobcatMode.Filling:
          label = "Fill";
          spriteName = "BobcatStatusBGFilling";
          break;
        case VehicleStatic.BobcatMode.Tunneling:
          label = "Tunneling";
          spriteName = "BobcatStatusBGTunneling";
          break;
        case VehicleStatic.BobcatMode.Smoothing:
          label = "Smoothing";
          spriteName = "BobcatStatusBGSmoothing";
          break;
        default:
          label = "None";
          spriteName = "BobcatStatusBGNone";
          break;
      }

      XUiV_Window bobcatStatusWindow = (XUiV_Window)VehicleStatic.WindowBobcatStatus;
      VehicleStatic.bobcatStatusLabel.SetTextImmediately(label);
      VehicleStatic.bobcatStatusSprite.SetSpriteImmediately(spriteName);
      bobcatStatusWindow.TargetAlpha = show ? 1 : 0;
      bobcatStatusWindow.ForceHide = show;
      bobcatStatusWindow.ForceVisible(show ? 1 : 0);
      bobcatStatusWindow.UpdateData();
    }
    public static void SetVehicleActivateWindow(bool show)
    {
      string label = VehicleStatic.isCurrentModeActive ? "Active" : "Inactive";
      string spriteName = VehicleStatic.isCurrentModeActive ? "BobcatActiveBG" : "BobcatInactiveBG";

      XUiV_Window bobcatActivateWindow = (XUiV_Window)VehicleStatic.WindowBobcatActivate;
      VehicleStatic.bobcatActivateLabel.SetTextImmediately(label);
      VehicleStatic.bobcatActivateSprite.SetSpriteImmediately(spriteName);
      bobcatActivateWindow.TargetAlpha = show ? 1 : 0;
      bobcatActivateWindow.ForceHide = show;
      bobcatActivateWindow.ForceVisible(show ? 1 : 0);
      bobcatActivateWindow.UpdateData();
    }
    private static void StopParticleIfAvailable(string key)
    {
      if (VehicleStatic.transformLookup.TryGetValue(key, out var transform))
      {
        var ps = transform.GetComponent<ParticleSystem>();
        if (ps != null && ps.isPlaying)
        {
          ps.Stop();
        }
        transform.gameObject.SetActive(false);
      }
    }
    public static List<string> GetModifierNames(EntityVehicle vehicle)
    {
      VehicleStatic.actions = GameManager.Instance.myEntityPlayerLocal.playerInput?.VehicleActions;
      ItemValue[] mods = vehicle.GetVehicle()?.itemValue?.Modifications;
      List<string> modifierNames = new List<string>();
      if (mods != null)
      {
        modifierNames = mods
          .Where(x => x != null && x.ItemClass != null)
          .Select(x => x.ItemClass.GetItemName())
          .ToList();
      }

      return modifierNames;
    }
    public static List<string> GetCosModifierNames(EntityVehicle vehicle)
    {
      VehicleStatic.actions = GameManager.Instance.myEntityPlayerLocal.playerInput?.VehicleActions;
      ItemValue[] mods = vehicle.GetVehicle()?.itemValue?.CosmeticMods;
      List<string> modifierNames = new List<string>();
      if (mods != null)
      {
        modifierNames = mods
          .Where(x => x != null && x.ItemClass != null)
          .Select(x => x.ItemClass.GetItemName())
          .ToList();
      }

      return modifierNames;
    }
    public static IEnumerator RunVehicleMode(Enum vehicleMode, EntityVehicle vehicle)
    {
      while (VehicleStatic.CurrentMode != VehicleStatic.BobcatMode.None)
      {
        switch (VehicleStatic.CurrentMode)
        {
          case VehicleStatic.BobcatMode.LandscapingHigh:
            if (BobcatConfig.EnableLandscapingHighMode && VehicleStatic.isCurrentModeActive) RunLandscapingHighMode(vehicle);
            break;
          case VehicleStatic.BobcatMode.Tunneling:
            if (BobcatConfig.EnableTunnelingMode && VehicleStatic.isCurrentModeActive) RunTunnelingMode(vehicle);
            break;
          case VehicleStatic.BobcatMode.Filling:
            if (BobcatConfig.EnableFillMode && VehicleStatic.isCurrentModeActive) RunFillMode(vehicle);
            break;
          case VehicleStatic.BobcatMode.Leveling:
            if (BobcatConfig.EnableLevelingMode && VehicleStatic.isCurrentModeActive) RunLevelingMode(vehicle);
            break;
          case VehicleStatic.BobcatMode.Smoothing:
            if (BobcatConfig.EnableTerrainSmoothing && VehicleStatic.isCurrentModeActive) RunSmoothingMode(vehicle);
            break;
          default:
            break;
        }
        yield return new WaitForSeconds(BobcatConfig.SecondsPerAttack);
      }
    }
    public static IEnumerator RunGlobalBobcat()
    {
      while (true)
      {
        if (ConnectionManager.Instance.IsSinglePlayer || ConnectionManager.Instance.IsServer) RefreshDamagedBobcats();
        yield return new WaitForSeconds(1f);
      }
    }
    public static void RefreshDamagedBobcats()
    {
      VehicleStatic.vehicles = VehicleManager.Instance?.GetVehicles();
      if (VehicleStatic.vehicles == null) return;

      foreach (var entry in VehicleStatic.vehicles)
      {
        Entity entity = GameManager.Instance.World?.GetEntity(entry.id);
        EntityVehicle vehicle = entity as EntityVehicle;
        if (vehicle == null) continue;
        if (!EntityClass.list.TryGetValue(vehicle.entityClass, out var entityClass)) continue;
        if (entityClass.entityClassName != "vehicleBobcat") continue;

        float hp = vehicle.GetVehicle()?.GetHealthPercent() ?? 1f;

        DamageState newState = DamageState.None;
        if (hp <= 0.1f) newState = DamageState.HeavilyDamaged;
        else if (hp <= 0.25f) newState = DamageState.Damaged;

        BobcatDamageStates.TryGetValue(vehicle, out var previousState);

        if (newState == DamageState.None)
        {
          if (previousState != DamageState.None)
          {
            // Remove and deactivate
            if (DamagedBobcats.TryGetValue(vehicle, out var oldTransforms))
            {
              foreach (var t in oldTransforms) t.gameObject.SetActive(false);
              DamagedBobcats.Remove(vehicle);
            }
            BobcatDamageStates[vehicle] = DamageState.None;
          }
          continue;
        }

        // If state changed, update visuals
        if (newState != previousState)
        {
          // Disable old ones
          if (DamagedBobcats.TryGetValue(vehicle, out var oldTransforms))
          {
            foreach (var t in oldTransforms) t.gameObject.SetActive(false);
          }

          var newTransforms = vehicle.gameObject.GetComponentsInChildren<Transform>(true)
              .Where(t =>
                  (newState == DamageState.Damaged && t.name == "Damaged") ||
                  (newState == DamageState.HeavilyDamaged && (t.name == "HeavilyDamaged" || t.name == "Damaged")))
              .ToList();

          DamagedBobcats[vehicle] = newTransforms;
          BobcatDamageStates[vehicle] = newState;

          foreach (var t in newTransforms)
          {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
          }
        }
      }
    }
    public static void BobcatCleanup()
    {
      VehicleStatic.vehicles.Clear();
    }
    public static IEnumerator BobcatUpdateVisualsCoroutine()
    {
      yield return new WaitUntil(() => GameManager.Instance.World != null && VehicleManager.Instance.GetVehicles()?.Count > 0);

      while (true)
      {
        var vehicles = VehicleManager.Instance.GetVehicles();
        if (vehicles == null)
        {
          yield return new WaitForSeconds(1f);
          continue;
        }

        foreach (var entry in vehicles)
        {
          Entity entity = GameManager.Instance.World.GetEntity(entry.id);
          if (entity is EntityVehicle vehicle)
          {
            if (EntityClass.list.TryGetValue(vehicle.entityClass, out var entityClass) && entityClass.entityClassName == "vehicleBobcat")
            {
              UpdateVehicleVisuals(vehicle);
              yield break;
            }
          }
        }

        yield return new WaitForSeconds(1f);
      }
    }
    public static void SetAmbientDrillParticles(EntityVehicle vehicle, bool enable)
    {
      bool drillParticles = (VehicleStatic.transformLookup.TryGetValue("DrillParticles", out var drillParticlesT));
      drillParticlesT.gameObject.SetActive(enable);
    }
    public static Dictionary<EntityVehicle, List<Transform>> GetDamagedBobcats()
    {
      IEnumerable<EntityVehicle> bobcats = VehicleManager.Instance.GetVehicles()
          .Select(v => GameManager.Instance.World.GetEntity(v.id))
          .OfType<EntityVehicle>()
          .Where(v => EntityClass.list[v.entityClass].entityClassName == "vehicleBobcat");

      var result = bobcats.ToDictionary(
          vehicle => vehicle,
          vehicle =>
          {
            // Get all transforms in vehicle
            var transforms = vehicle.gameObject.GetComponentsInChildren<Transform>(true);
            var damagedVisuals = transforms
              .Where(t => t.name == "Damaged" || t.name == "HeavilyDamaged")
              .ToList();

            return damagedVisuals;
          });

      return result;
    }
    public static ScanResults GetTargets(Vector3 targetCenter, Vector3 right, Vector3 forward, EntityVehicle vehicle)
    {
      Vehicle.ScanResults results = new ScanResults();
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
    private static int[] GetHWDFromMods(List<string> mods)
    {
      if (mods.Contains("modVehicleBucket5")) return new int[3] { 2, 5, 3 };
      if (mods.Contains("modVehicleBucket3")) return new int[3] { 2, 3, 3 };
      if (mods.Contains("modVehicleDrill")) return new int[3] { 3, 3, 4 };
      
      return new int[3] { 1, 2, 2 };
    }
    private static void SetVehicleModeSpeed(EntityVehicle vehicle)
    {
      float[] velocities = VehicleStatic.MaxVelocity;
      switch(VehicleStatic.CurrentMode)
      {
        case VehicleStatic.BobcatMode.None:
          velocities = velocities.Select(v => v * BobcatConfig.NoneModeSpeedMultiplier).ToArray();
          break;
        case VehicleStatic.BobcatMode.LandscapingHigh:
          velocities = velocities.Select(v => v * BobcatConfig.LandscapingHighModeSpeedMultiplier).ToArray();
          break;
        case VehicleStatic.BobcatMode.Tunneling:
          velocities = velocities.Select(v => v * BobcatConfig.TunnelingModeSpeedMultiplier).ToArray();
          break;
        case VehicleStatic.BobcatMode.Leveling:
          velocities = velocities.Select(v => v * BobcatConfig.LevelingModeSpeedMultiplier).ToArray();
          break;
        case VehicleStatic.BobcatMode.Filling:
          velocities = velocities.Select(v => v * BobcatConfig.FillingModeSpeedMultiplier).ToArray();
          break;
        case VehicleStatic.BobcatMode.Smoothing:
          velocities = velocities.Select(v => v * BobcatConfig.SmoothingModeSpeedMultiplier).ToArray();
          break;
        default:
          break;
      }

      var bobcat = vehicle.GetVehicle();
          bobcat.VelocityMaxForward = velocities[0];
          bobcat.VelocityMaxBackward = velocities[1];
          bobcat.VelocityMaxTurboForward = velocities[2];
          bobcat.VelocityMaxTurboBackward = velocities[3];
    }
    public static IEnumerator OnPlayerLoggedIn()
    {
      while(!VehicleStatic.isPlayerLoggedIn)
      {
        if (GameManager.Instance?.myEntityPlayerLocal?.entityId != -1 && GameManager.Instance?.myEntityPlayerLocal?.PlayerUI?.xui?.xuiViewList != null)
        {
          VehicleStatic.isPlayerLoggedIn = true;

          VehicleStatic.Views = GameManager.Instance?.myEntityPlayerLocal?.PlayerUI?.xui?.xuiViewList;
          VehicleStatic.WindowBobcatStatus = VehicleStatic.Views.Find(x => x.id == "VehicleModeStatusWindow");
          VehicleStatic.WindowBobcatActivate = VehicleStatic.Views.Find(x => x.id == "VehicleModeActiveWindow");

          VehicleStatic.Labels = VehicleStatic.Views.FindAll(x => x is XUiV_Label);
          VehicleStatic.Sprites = VehicleStatic.Views.FindAll(x => x is XUiV_Sprite);

          VehicleStatic.bobcatStatusSprite = (XUiV_Sprite)VehicleStatic.Sprites.Find(x => x.id == "bobcatStatusBG");
          VehicleStatic.bobcatStatusLabel = (XUiV_Label)VehicleStatic.Labels.Find(x => x.id == "bobcatStatusLabel");

          VehicleStatic.bobcatActivateSprite = (XUiV_Sprite)VehicleStatic.Sprites.Find(x => x.id == "bobcatActiveBG");
          VehicleStatic.bobcatActivateLabel = (XUiV_Label)VehicleStatic.Labels.Find(x => x.id == "bobcatActiveLabel");

          // UI
          SetVehicleStatusWindow(false);
          SetVehicleActivateWindow(false);
        }

        yield return new WaitForSeconds(1f);
      }
    }
    public static class VehicleStatic
    {
      public enum BobcatMode
      {
        None,
        LandscapingHigh,
        Tunneling,
        Leveling,
        Filling,
        Smoothing
      };

      public static BobcatMode CurrentMode = BobcatMode.None;
      public static ItemValue[] mods;
      public static PlayerActionsVehicle actions;
      public static string[] transformNames = { "Exhaust", "ExhaustSuperCharger", "HeadLampsOn", "HeadLampsOff", "DrillOn", "DrillOff", "Plow3", "Plow5", "PedalDown", "pedalDownSuperCharger1", "pedalDownSuperCharger2", "Damaged", "HeavilyDamaged", "Audio", "DrillParticles", "SandParticles", "BobcatBody" };
      public static Dictionary<string, Transform> transformLookup;
      public static bool lightsOn = false;
      public static List<EntityCreationData> vehicles;
      public static List<EntityAlive> zombiesAndAnimals = new List<EntityAlive>();
      public static int vehicleLevelingModeHeight;
      public static List<AudioSource> vehicleModeAudioSources = new List<AudioSource>();
      public static XUiView WindowBobcatStatus;
      public static XUiView WindowBobcatActivate;
      public static bool isPlayerLoggedIn = false;
      public static List<XUiView> Views = GameManager.Instance?.myEntityPlayerLocal?.PlayerUI?.xui?.xuiViewList;
      public static List<XUiView> Labels;
      public static List<XUiView> Sprites;
      public static XUiV_Sprite bobcatStatusSprite;
      public static XUiV_Sprite bobcatActivateSprite;
      public static XUiV_Label bobcatStatusLabel;
      public static XUiV_Label bobcatActivateLabel;
      public static bool isCurrentModeActive = false;

      public static bool wasHopPressedLastFrame = false;
      public static bool wasHornPressedLastFrame = false;
      public static bool lightWasOnLastFrame = false;
      public static EntityVehicle lastAttachedVehicle = null;
      public static Coroutine turboCoroutine = null;
      public static Coroutine lightCoroutine = null;

      // Hides UI
      public static void HideWindowBobcatStatus()
      {
        XUiV_Window bobcatStatusWindow = (XUiV_Window)VehicleStatic.WindowBobcatStatus;
        bobcatStatusWindow.TargetAlpha = 0f;
        bobcatStatusWindow.ForceHide = true;
        bobcatStatusWindow.ForceVisible(0);
        bobcatStatusWindow.UpdateData();
      }


      // Max velocity is used as a reference and should not change once set
      private static float[] _maxVelocity;
      private static bool _isMaxVelocitySet = false;

      public static float[] MaxVelocity => _maxVelocity;

      public static void SetMaxVelocityOnce(EntityVehicle vehicle)
      {
        if (_isMaxVelocitySet || vehicle == null)
          return;

        _maxVelocity = new float[4]
        {
            vehicle.GetVehicle().VelocityMaxForward,
            vehicle.GetVehicle().VelocityMaxBackward,
            vehicle.GetVehicle().VelocityMaxTurboForward,
            vehicle.GetVehicle().VelocityMaxTurboBackward
        };

        _isMaxVelocitySet = true;
      }
    }
    public static class BobcatConfig
    {
      public static bool EnableLandscapingHighMode { get; private set; }
      public static bool EnableTunnelingMode { get; private set; }
      public static bool EnableLevelingMode { get; private set; }
      public static bool EnableTerrainSmoothing { get; private set; }
      public static bool EnableFillMode { get; private set; }
      public static bool EnableDrillParticles { get; private set; }
      public static bool EnableDustParticles { get; private set; }
      public static bool EnableBloodParticles { get; private set; }
      public static bool EnableZombieDamage { get; private set; }
      public static bool EnableZombieRagdoll { get; private set; }
      public static bool EnableTractionControl { get; private set; }

      public static int TerrainDamage { get; private set; }
      public static int EntityDamage { get; private set; }
      public static sbyte LevelingModeFillHeight { get; private set; }
      public static float SecondsPerAttack { get; private set; }
      public static float DrillAudioVolume { get; private set; }
      public static float ModeChangeAudioVolume { get; private set; }
      public static float LevelingModeAudioVolume { get; private set; }
      public static float NoneModeSpeedMultiplier { get; private set; }
      public static float LandscapingHighModeSpeedMultiplier { get; private set; }
      public static float TunnelingModeSpeedMultiplier { get; private set; }
      public static float LevelingModeSpeedMultiplier  { get; private set; }
      public static float FillingModeSpeedMultiplier { get; private set; }
      public static float SmoothingModeSpeedMultiplier { get; private set; }
      public static int TerrainBlocksToSmooth {  get; private set; }
      public static float TimeToWait { get; private set; }

      public static float WoodDamageMultiplier { get; private set; }
      public static float GlassDamageMultiplier { get; private set; }
      public static float ClothDamageMultiplier { get; private set; }
      public static float FleshDamageMultiplier { get; private set; }
      public static float StoneDamageMultiplier { get; private set; }
      public static float DirtDamageMultiplier { get; private set; }
      public static float ConcreteDamageMultiplier { get; private set; }
      public static float MetalDamageMultiplier { get; private set; }
      public static float PlantDamageMultiplier { get; private set; }

      public static float WoodHarvestMultiplier { get; private set; }
      public static float GlassHarvestMultiplier { get; private set; }
      public static float ClothHarvestMultiplier { get; private set; }
      public static float FleshHarvestMultiplier { get; private set; }
      public static float StoneHarvestMultiplier { get; private set; }
      public static float DirtHarvestMultiplier { get; private set; }
      public static float ConcreteHarvestMultiplier { get; private set; }
      public static float MetalHarvestMultiplier { get; private set; }
      public static float PlantHarvestMultiplier { get; private set; }

      public static float ZombieDamageMultiplier { get; private set; }
      public static float FeralZombieDamageMultiplier { get; private set; }
      public static float RadiatedZombieDamageMultiplier { get; private set; }
      public static float ChargedZombieDamageMultiplier { get; private set; }
      public static float InfernalZombieDamageMultiplier { get; private set; }
      public static float AnimalDamageMultiplier { get; private set; }

      public static List<string> HarvestableBlockList { get; private set; }
      public static List<string> FillModeValidTerrainItems { get; private set; }
      public static List<string> EntityNamesToIgnoreList { get; private set; }
      public static List<string> BlockNamesToIgnoreList { get; private set; }
      public static void Load(string path)
      {
        var doc = XDocument.Load(path);
        var settings = new Dictionary<string, string>();

        foreach (var setting in doc.Descendants("setting"))
        {
          var key = setting.Attribute("key")?.Value;
          var value = setting.Value;
          if (!string.IsNullOrEmpty(key))
          {
            settings[key] = value;
          }
        }

        EnableLandscapingHighMode = bool.Parse(settings["EnableLandscapingHighMode"].Trim());
        EnableTunnelingMode = bool.Parse(settings["EnableTunnelingMode"].Trim());
        EnableLevelingMode = bool.Parse(settings["EnableLevelingMode"].Trim());
        EnableFillMode = bool.Parse(settings["EnableFillMode"].Trim());
        EnableTerrainSmoothing = bool.Parse(settings["EnableTerrainSmoothing"].Trim());
        EnableDrillParticles = bool.Parse(settings["EnableDrillParticles"].Trim());
        EnableDustParticles = bool.Parse(settings["EnableDustParticles"].Trim());
        EnableBloodParticles = bool.Parse(settings["EnableBloodParticles"].Trim());
        EnableZombieDamage = bool.Parse(settings["EnableZombieDamage"].Trim());
        EnableZombieRagdoll = bool.Parse(settings["EnableZombieRagdoll"].Trim());
        EnableTractionControl = bool.Parse(settings["EnableTractionControl"].Trim());

        TerrainDamage = int.Parse(settings["TerrainDamage"].Trim());
        EntityDamage = int.Parse(settings["EntityDamage"].Trim());
        SecondsPerAttack = float.Parse(settings["SecondsPerAttack"].Trim());
        DrillAudioVolume = float.Parse(settings["DrillAudioVolume"].Trim());
        ModeChangeAudioVolume = float.Parse(settings["ModeChangeAudioVolume"].Trim());
        LevelingModeAudioVolume = float.Parse(settings["LevelingModeAudioVolume"].Trim());
        NoneModeSpeedMultiplier = float.Parse(settings["NoneModeSpeedMultiplier"].Trim());
        LandscapingHighModeSpeedMultiplier = float.Parse(settings["LandscapingHighModeSpeedMultiplier"].Trim());
        TunnelingModeSpeedMultiplier = float.Parse(settings["TunnelingModeSpeedMultiplier"].Trim());
        LevelingModeSpeedMultiplier = float.Parse(settings["LevelingModeSpeedMultiplier"].Trim());
        FillingModeSpeedMultiplier = float.Parse(settings["LevelingModeSpeedMultiplier"].Trim());
        SmoothingModeSpeedMultiplier = float.Parse(settings["SmoothingModeSpeedMultiplier"].Trim());
        LevelingModeFillHeight = sbyte.Parse(settings["LevelingModeFillHeight"].Trim());
        TerrainBlocksToSmooth = int.Parse(settings["TerrainBlocksToSmooth"].Trim());
        TimeToWait = float.Parse(settings["TimeToWait"].Trim());

        WoodDamageMultiplier = float.Parse(settings["WoodDamageMultiplier"].Trim());
        GlassDamageMultiplier = float.Parse(settings["GlassDamageMultiplier"].Trim());
        ClothDamageMultiplier = float.Parse(settings["ClothDamageMultiplier"].Trim());
        FleshDamageMultiplier = float.Parse(settings["FleshDamageMultiplier"].Trim());
        StoneDamageMultiplier = float.Parse(settings["StoneDamageMultiplier"].Trim());
        DirtDamageMultiplier = float.Parse(settings["DirtDamageMultiplier"].Trim());
        ConcreteDamageMultiplier = float.Parse(settings["ConcreteDamageMultiplier"].Trim());
        MetalDamageMultiplier = float.Parse(settings["MetalDamageMultiplier"].Trim());
        PlantDamageMultiplier = float.Parse(settings["PlantDamageMultiplier"].Trim());

        WoodHarvestMultiplier = float.Parse(settings["WoodHarvestMultiplier"].Trim());
        GlassHarvestMultiplier = float.Parse(settings["GlassHarvestMultiplier"].Trim());
        ClothHarvestMultiplier = float.Parse(settings["ClothHarvestMultiplier"].Trim());
        FleshHarvestMultiplier = float.Parse(settings["FleshHarvestMultiplier"].Trim());
        StoneHarvestMultiplier = float.Parse(settings["StoneHarvestMultiplier"].Trim());
        DirtHarvestMultiplier = float.Parse(settings["DirtHarvestMultiplier"].Trim());
        ConcreteHarvestMultiplier = float.Parse(settings["ConcreteHarvestMultiplier"].Trim());
        MetalHarvestMultiplier = float.Parse(settings["MetalHarvestMultiplier"].Trim());
        PlantHarvestMultiplier = float.Parse(settings["PlantHarvestMultiplier"].Trim());

        ZombieDamageMultiplier = float.Parse(settings["ZombieDamageMultiplier"].Trim());
        FeralZombieDamageMultiplier = float.Parse(settings["FeralZombieDamageMultiplier"].Trim());
        RadiatedZombieDamageMultiplier = float.Parse(settings["RadiatedZombieDamageMultiplier"].Trim());
        ChargedZombieDamageMultiplier = float.Parse(settings["ChargedZombieDamageMultiplier"].Trim());
        InfernalZombieDamageMultiplier = float.Parse(settings["InfernalZombieDamageMultiplier"].Trim());
        AnimalDamageMultiplier = float.Parse(settings["AnimalDamageMultiplier"].Trim());

        HarvestableBlockList = settings["HarvestableBlockList"]
          .Split(new[] { ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(str => str.Trim())
          .ToList();

        FillModeValidTerrainItems = settings["FillModeValidTerrainItems"]
          .Split(new[] { ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(str => str.Trim())
          .ToList();

        EntityNamesToIgnoreList = settings["EntityNamesToIgnore"]
          .Split(new[] { ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(str => str.Trim())
          .ToList();

        BlockNamesToIgnoreList = settings["BlockNamesToIgnore"]
          .Split(new[] { ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
          .Select(str => str.Trim())
          .ToList();
      }
    }
    public class ScanResults
    {
      public List<Tuple<Vector3i, BlockValue>> Blocks { get; set; } = new List<Tuple<Vector3i, BlockValue>>();
      public List<int> EntityIds { get; set; } = new List<int> {};
    }
  }
}
