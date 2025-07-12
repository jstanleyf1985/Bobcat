using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
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
    public static List<string> GetModifierNames(EntityVehicle vehicle)
    {
      if (vehicle == null || vehicle.EntityClass == null || vehicle.EntityClass.entityClassName != "vehicleBobcat")
        return new List<string>();

      if (GameManager.Instance?.myEntityPlayerLocal != null)
      {
        VehicleStatic.actions = GameManager.Instance.myEntityPlayerLocal.playerInput?.VehicleActions;
      }

      ItemValue[] mods = vehicle.GetVehicle()?.itemValue?.Modifications;
      if (mods == null) return new List<string>();

      return mods
          .Where(x => x != null && x.ItemClass != null)
          .Select(x => x.ItemClass.GetItemName())
          .ToList();
    }
    public static List<string> GetCosModifierNames(EntityVehicle vehicle)
    {
      if (vehicle == null || vehicle.EntityClass == null || vehicle.EntityClass.entityClassName != "vehicleBobcat")
        return new List<string>();

      if (GameManager.Instance?.myEntityPlayerLocal != null)
      {
        VehicleStatic.actions = GameManager.Instance.myEntityPlayerLocal.playerInput?.VehicleActions;
      }

      ItemValue[] mods = vehicle.GetVehicle()?.itemValue?.CosmeticMods;
      if (mods == null) return new List<string>();

      return mods
          .Where(x => x != null && x.ItemClass != null)
          .Select(x => x.ItemClass.GetItemName())
          .ToList();
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
  }
}
