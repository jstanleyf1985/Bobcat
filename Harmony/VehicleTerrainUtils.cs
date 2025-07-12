using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static int GetClrIdxFromBlockPos(Vector3i blockPos)
    {
      var clusters = GameManager.Instance.World.ChunkClusters;
      for (int i = 0; i < clusters.Count; i++)
      {
        if (clusters[i]?.GetChunk(blockPos.x >> 4, blockPos.z >> 4) != null)
          return i;
      }
      return 0;
    }
    public static int GetChunkKeyFromBlockPos(Vector3i pos)
    {
      int chunkX = pos.x >> 4;
      int chunkZ = pos.z >> 4;
      return (chunkZ << 16) | (chunkX & 0xFFFF);
    }
    public static List<Vector3Int> GetFlatteningOffsets(EntityVehicle vehicle, List<string> mods)
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
    public static Vector3i GetLevelingReferencePosition(EntityVehicle vehicle)
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
    public static void FillTerrain(EntityVehicle vehicle, Vector3i centerPos, int radius, sbyte density, int clrIdx)
    {
      World world = GameManager.Instance.World;

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
        if (newCount <= 0) vehicleInventory[itemStack.Key] = ItemStack.Empty.Clone();
        else vehicleInventory[itemStack.Key] = new ItemStack(itemStack.Value.itemValue.Clone(), newCount);

        vehicle.bag.SetSlot(itemStack.Key, vehicleInventory[itemStack.Key]);

        vehicle.SetBagModified();
        vehicle.bag.onBackpackChanged();


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
      if (mods.Count == 0) return;

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
    public static Vector3i RaycastToGround(Vector3i start, int maxDepth)
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
  }
}
