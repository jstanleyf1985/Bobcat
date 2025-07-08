using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static bool GetConfigEnabledForMode(VehicleStatic.BobcatMode mode)
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
    public static void HandleHornToggle(EntityVehicle vehicle, PlayerActionsVehicle actions)
    {
      if (actions?.HonkHorn == null) return;

      bool hornPressed = actions.HonkHorn.IsPressed;

      if (hornPressed && !VehicleStatic.wasHornPressedLastFrame)
      {
        // Toggle mode activation
        if (VehicleStatic.CurrentMode != VehicleStatic.BobcatMode.None) VehicleStatic.isCurrentModeActive = !VehicleStatic.isCurrentModeActive;
        if (VehicleStatic.CurrentMode == VehicleStatic.BobcatMode.None) GameManager.Instance.PlaySoundAtPositionServer(vehicle.position, "Bobcat_horn", AudioRolloffMode.Linear, 20);
        SetVehicleActivateWindow(true);

        // Set leveling/filling reference height on activated
        if (VehicleStatic.isCurrentModeActive) VehicleStatic.vehicleLevelingModeHeight = GetLevelingReferencePosition(vehicle).y;
      }

      VehicleStatic.wasHornPressedLastFrame = hornPressed;
    }
    public static void HandleModeChange(EntityVehicle vehicle, PlayerActionsVehicle actions)
    {
      if (actions?.Hop == null) return;

      bool isHopPressed = actions.Hop.IsPressed;
      if (isHopPressed && !VehicleStatic.wasHopPressedLastFrame) GameManager.Instance.StartCoroutine(HandleModeChangeOnce(vehicle));

      VehicleStatic.wasHopPressedLastFrame = isHopPressed;

    }
    public static IEnumerator HandleModeChangeOnce(EntityVehicle vehicle)
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
        SetVehicleStatusWindow(true, false);
        SetVehicleActivateWindow(true);

        GameManager.Instance.StopCoroutine(RunVehicleMode(VehicleStatic.CurrentMode, vehicle));
        GameManager.Instance.StartCoroutine(RunVehicleMode(VehicleStatic.CurrentMode, vehicle));

      }

      yield break;
    }
    public static bool IsBobcatModeEnabled(VehicleStatic.BobcatMode mode)
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
    private static void SetVehicleModeSpeed(EntityVehicle vehicle)
    {
      float[] velocities = VehicleStatic.MaxVelocity;
      switch (VehicleStatic.CurrentMode)
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
        VehicleStatic.curBlockHeight = densityPos.y;

        int clrIdx = GetClrIdxFromBlockPos(densityPos);

        Vector3i levelTarget = new Vector3i(targetPos.x, VehicleStatic.vehicleLevelingModeHeight, targetPos.z);

        SetVehicleStatusWindow(true, VehicleStatic.vehicleLevelingModeHeight != VehicleStatic.curBlockHeight);
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

        SetVehicleStatusWindow(true, VehicleStatic.vehicleLevelingModeHeight != VehicleStatic.curBlockHeight);
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
  }
}
