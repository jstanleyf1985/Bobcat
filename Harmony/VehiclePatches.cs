using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    [HarmonyPatch(typeof(GameManager), "Update")]
    public static class Patch_VehicleIntercept
    {

      public static void Postfix()
      {
        var player = GameManager.Instance?.myEntityPlayerLocal;
        if (player == null) return;

        if (player.AttachedToEntity is EntityVehicle currentVehicle)
        {
          if (!GetIsBobcatVehicle(currentVehicle)) return;
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
        if (!GetIsBobcatVehicle(vehicle)) return;

        vehicle.SetBagModified();
        vehicle.bag.onBackpackChanged();
        vehicle.UpdateInteractionUI();

        UpdateVehicleVisuals(vehicle);
      }
    }

    [HarmonyPatch(typeof(EntityVehicle), "PostInit")]
    public class Patch_EntityVehicle_PostInit
    {
      static void Postfix(EntityVehicle __instance)
      {
        if (!GetIsBobcatVehicle(__instance)) return;
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
          if (!GetIsBobcatVehicle(vehicle)) return;

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
                  vehicle.bag.AddItem(stack);
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
        if (!GetIsBobcatVehicle(vehicle)) return;
        if (!BobcatConfig.HarvestableBlockList.Contains(_blockValue.Block.blockName)) return;

        Vector3 pos = World.blockToTransformPos(_blockPos) + new Vector3(0f, 0.5f, 0f);
        string surfaceCategory = _blockValue.Block.GetMaterialForSide(_blockValue, BlockFace.Top)?.SurfaceCategory ?? "stone";

        if (BobcatConfig.EnableDustParticles)
        {
          string nextName = BobcatParticleManager.GetNextDust();
          ParticleEffect pe = new ParticleEffect(nextName, pos, Quaternion.identity, 1f, Color.white);
          GameManager.Instance.SpawnParticleEffectServer(pe, GameManager.Instance.myEntityPlayerLocal.entityId);
        }
      }
    }
  }
}
