using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static Dictionary<EntityVehicle, DamageState> BobcatDamageStates = new Dictionary<EntityVehicle, DamageState>();
    public static Dictionary<EntityVehicle, List<Transform>> DamagedBobcats = new Dictionary<EntityVehicle, List<Transform>>();
    public static void HandleEnterVehicle(EntityVehicle vehicle)
    {
      if (!GetIsBobcatVehicle(vehicle)) return;

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
      UpdateWheelTraction(vehicle);

      // UI
      SetVehicleStatusWindow(true, false);
      SetVehicleActivateWindow(true);

      // Light Indicators
      UpdateVehicleLights(vehicle);
    }
    public static void HandleExitVehicle(EntityVehicle vehicle)
    {
      if (!GetIsBobcatVehicle(vehicle)) return;

      StopParticleIfAvailable("PedalDown");
      StopParticleIfAvailable("PedalDownSuperCharger1");
      StopParticleIfAvailable("PedalDownSuperCharger2");
      List<string> modifierNames = GetModifierNames(vehicle);

      // Set mode back to none
      VehicleStatic.CurrentMode = VehicleStatic.BobcatMode.None;

      // Reset wheel stiffness when not in operating mode
      UpdateWheelTraction(vehicle);
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
      SetVehicleStatusWindow(false, false);
      SetVehicleActivateWindow(false);
      VehicleStatic.isCurrentModeActive = false;

      // Light Indicators
      UpdateVehicleLights(vehicle);

    }
    public static IEnumerator OnPlayerLoggedIn()
    {
      while (!VehicleStatic.isPlayerLoggedIn)
      {
        var player = GameManager.Instance?.myEntityPlayerLocal;

        // Wait until player and UI are valid
        if (player != null && player.entityId != -1)
        {
          var views = player?.PlayerUI?.xui?.xuiViewList;
          bool hasViews = views != null && views.Count > 0;

          if (hasViews)
          {
            // Optional delay to ensure views are fully populated
            yield return new WaitForSeconds(2.0f);

            VehicleStatic.Views = views;
            VehicleStatic.WindowBobcatStatus = views.Find(x => x.id == "VehicleModeStatusWindow");
            VehicleStatic.WindowBobcatActivate = views.Find(x => x.id == "VehicleModeActiveWindow");

            VehicleStatic.Labels = views.FindAll(x => x is XUiV_Label);
            VehicleStatic.Sprites = views.FindAll(x => x is XUiV_Sprite);

            VehicleStatic.bobcatStatusSprite = (XUiV_Sprite)VehicleStatic.Sprites.Find(x => x.id == "bobcatStatusBG");
            VehicleStatic.bobcatStatusLabel = (XUiV_Label)VehicleStatic.Labels.Find(x => x.id == "bobcatStatusLabel");

            VehicleStatic.bobcatActivateSprite = (XUiV_Sprite)VehicleStatic.Sprites.Find(x => x.id == "bobcatActiveBG");
            VehicleStatic.bobcatActivateLabel = (XUiV_Label)VehicleStatic.Labels.Find(x => x.id == "bobcatActiveLabel");

            // Set default/fallback label values
            if (VehicleStatic.bobcatActivateLabel != null)
            {
              VehicleStatic.bobcatActivateLabel.SetTextImmediately("Inactive");
              Log.Out("Bobcat Mod: Set activate label to 'Inactive'");
            }

            if (VehicleStatic.bobcatStatusLabel != null)
            {
              VehicleStatic.bobcatStatusLabel.SetTextImmediately("Stopped");
              Log.Out("Bobcat Mod: Set status label to 'Stopped'");
            }

            // Only mark as logged in if everything was found
            if (VehicleStatic.bobcatActivateLabel != null && VehicleStatic.bobcatStatusLabel != null)
            {
              VehicleStatic.isPlayerLoggedIn = true;

              // Hide windows by default
              VehicleStatic.WindowBobcatStatus?.OnClose();
              VehicleStatic.WindowBobcatActivate?.OnClose();
              SetVehicleStatusWindow(false, false);
              SetVehicleActivateWindow(false);

              Log.Warning("Bobcat Mod: Player UI initialized successfully.");
              yield break;
            }
            else
            {
              Log.Warning("Bobcat Mod: Labels not fully initialized, retrying...");
            }
          }
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
      public static string[] transformNames = { "Exhaust", "ExhaustSuperCharger", "HeadLampsOn", "HeadLampsOff", "DrillOn", "DrillOff", "Plow3", "Plow5", "PedalDown", "pedalDownSuperCharger1", "pedalDownSuperCharger2", "Damaged", "HeavilyDamaged", "Audio", "DrillParticles", "SandParticles", "BobcatBody", "BobcatLightActive", "BobcatLightInactive", "BobcatLightLandscaping", "BobcatLightLeveling", "BobcatLightFilling", "BobcatLightTunneling", "BobcatLightSmoothing", "BobcatLightNone" };
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
      public static int curBlockHeight = 0;

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
        XUiV_Window bobcatActivateWindow = (XUiV_Window)VehicleStatic.WindowBobcatActivate;
        bobcatStatusWindow.TargetAlpha = 0f;
        bobcatActivateWindow.TargetAlpha = 0f;
        bobcatStatusWindow.ForceHide = true;
        bobcatActivateWindow.ForceHide = true;
        bobcatStatusWindow.ForceVisible(0);
        bobcatActivateWindow.ForceVisible(1);
        bobcatStatusWindow.UpdateData();
        bobcatActivateWindow.UpdateData();
      }


      // Max velocity is used as a reference and should not change once set
      private static float[] _maxVelocity;
      public static bool _isMaxVelocitySet = false;

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
      public static bool AllowTraderBlockDestruction { get; private set; }
      public static bool EnableUI { get; private set; }
      public static bool EnableStatusLights { get; private set; }

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

      // Translation Settings
      public static string Inactive { get; private set; }
      public static string Active { get; private set; }
      public static string None { get; private set; }
      public static string Landscaping {  get; private set; }
      public static string Leveling { get; private set; }
      public static string Tunneling { get; private set; }
      public static string Filling { get; private set; }
      public static string Smoothing {  get; private set; }

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
        AllowTraderBlockDestruction = bool.Parse(settings["AllowTraderBlockDestruction"].Trim());
        EnableUI = bool.Parse(settings["EnableUI"].Trim());
        EnableStatusLights = bool.Parse(settings["EnableStatusLights"].Trim());

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

        // Translation Settings
        Inactive = settings["Inactive"].Trim();
        Active = settings["Active"].Trim();
        None = settings["None"].Trim();
        Landscaping = settings["Landscaping"].Trim();
        Leveling = settings["Leveling"].Trim();
        Tunneling = settings["Tunneling"].Trim();
        Filling = settings["Filling"].Trim();
        Smoothing = settings["Smoothing"].Trim();

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
  }
}
