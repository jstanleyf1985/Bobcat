using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static void ActivateDrillOff(EntityVehicle vehicle)
    {
      bool drillOn = VehicleStatic.transformLookup.TryGetValue("DrillOn", out var drillOnT);
      bool drillOff = VehicleStatic.transformLookup.TryGetValue("DrillOff", out var drillOffT);

      List<string> mods = GetModifierNames(vehicle);
      if (mods.Contains("modVehicleDrill"))
      {
        EnableDisableTransforms(new List<Transform>() { drillOffT }, new List<Transform>() { drillOnT });
        DisableDrillAudio(vehicle);
      }
      else
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
    public static IEnumerator BobcatUpdateVisualsCoroutine()
    {
      yield return new WaitUntil(() =>
        GameManager.Instance != null &&
        GameManager.Instance.World != null &&
        VehicleManager.Instance != null &&
        VehicleManager.Instance.GetVehicles() != null &&
        VehicleManager.Instance.GetVehicles().Count > 0
       );

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
    public static void DisableLightChangeEffect(EntityVehicle vehicle)
    {
      List<string> modifierNames = GetModifierNames(vehicle);
      if (modifierNames.Contains("modVehicleOffRoadHeadlights"))
      {
        if (VehicleStatic.transformLookup.TryGetValue("HeadLampsOn", out var hlOn)) hlOn.gameObject.SetActive(false);
        if (VehicleStatic.transformLookup.TryGetValue("HeadLampsOff", out var hlOff)) hlOff.gameObject.SetActive(true);
      }
    }
    public static void DisableTransforms(List<Transform> disableT)
    {
      if (disableT == null)
      {
        Log.Warning("DisableTransforms called with null list.");
        return;
      }

      foreach (Transform t in disableT)
      {
        if (t != null)
        {
          t.gameObject?.SetActive(false);
        }
        else
        {
          Log.Warning("DisableTransforms found null Transform in list.");
        }
      }
    }
    public static void EnableDisableTransforms(List<Transform> enableT, List<Transform> disableT)
    {
      foreach (Transform t in enableT) t.gameObject.SetActive(true);
      foreach (Transform t in disableT) t.gameObject.SetActive(false);
    }
    public static void EnableLightChangeEffect(EntityVehicle vehicle)
    {
      List<string> modifierNames = GetModifierNames(vehicle);
      if (modifierNames.Contains("modVehicleOffRoadHeadlights"))
      {
        if (VehicleStatic.transformLookup.TryGetValue("HeadLampsOn", out var hlOn)) hlOn.gameObject.SetActive(true);
        if (VehicleStatic.transformLookup.TryGetValue("HeadLampsOff", out var hlOff)) hlOff.gameObject.SetActive(false);
      }
    }
    public static void HandleLightChange(EntityVehicle vehicle)
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
    public static IEnumerator HandleLightChangeOnce(EntityVehicle vehicle)
    {
      EnableLightChangeEffect(vehicle);
      while (true)
      {
        yield return null; // just hold state while lights remain on
      }
    }
    private static void PopulateVehicleTransformHandles(EntityVehicle vehicle)
    {
      VehicleStatic.transformLookup = null;
      VehicleStatic.transformLookup = VehicleStatic.transformLookup = vehicle.gameObject.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
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
    public static IEnumerator RunGlobalBobcat()
    {
      while (true)
      {
        if (ConnectionManager.Instance.IsSinglePlayer || ConnectionManager.Instance.IsServer) RefreshDamagedBobcats();
        yield return new WaitForSeconds(1f);
      }
    }
    public static void SetDyeColor(EntityVehicle vehicle, Transform part, string hex)
    {
      Renderer renderer = part.GetComponent<Renderer>();
      Material mat = renderer.material;
      mat.color = HexToColor(hex);
    }
    public static void UpdateDrillAnimationSpeed(EntityVehicle vehicle)
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
          new ItemValue()
        };

        vehicle.GetVehicle().SetItemValueMods(newVehItemVal);
        vehicle.GetVehicle().SetColors();
      }

      // Has a dye
      if (cosModsInstalled.Count > 0)
      {
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
              vehicle.GetVehicle().SetColors();
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
    public static IEnumerator UpdateVehicleVisualsDelayed(EntityVehicle vehicle)
    {
      yield return new WaitForSeconds(0.5f);
      if (vehicle != null) UpdateVehicleVisuals(vehicle);

      yield break;
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
  }
}
