using Epic.OnlineServices.Presence;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static void SetVehicleStatusWindow(bool show, bool showRedSprite)
    {
      if (!BobcatConfig.EnableUI) return;

      var player = GameManager.Instance?.myEntityPlayerLocal;
      var entity = player?.AttachedToEntity;

      if (!(entity is EntityVehicle vehicle) || vehicle.EntityClass == null || vehicle.EntityClass.entityClassName != "vehicleBobcat")
      {
        // Player is not in a Bobcat vehicle — hide and clear UI
        VehicleStatic.bobcatStatusLabel?.SetTextImmediately("");
        VehicleStatic.bobcatStatusSprite?.SetSpriteImmediately("");

        if (VehicleStatic.WindowBobcatStatus is XUiV_Window window)
        {
          window.TargetAlpha = 0f;
          window.ForceHide = true;
          window.ForceVisible(0);
          window.UpdateData();
        }

        // Turn off all mode lights just in case
        SetVehicleStatusModeLight(null, false, BobcatConfig.None);
        return;
      }

      // Determine current label and sprite based on mode
      string label = BobcatConfig.None;
      string spriteName = "BobcatStatusBGNone";
      string modeString = BobcatConfig.None;

      switch (VehicleStatic.CurrentMode)
      {
        case VehicleStatic.BobcatMode.LandscapingHigh:
          label = BobcatConfig.Landscaping;
          spriteName = "BobcatStatusBGLandscapingHigh";
          modeString = BobcatConfig.Landscaping;
          break;
        case VehicleStatic.BobcatMode.Leveling:
          label = BobcatConfig.Leveling;
          spriteName = showRedSprite ? "BobcatStatusBGLevelingRed" : "BobcatStatusBGLeveling";
          modeString = BobcatConfig.Leveling;
          break;
        case VehicleStatic.BobcatMode.Filling:
          label = BobcatConfig.Filling;
          spriteName = showRedSprite ? "BobcatStatusBGFillingRed" : "BobcatStatusBGFilling";
          modeString = BobcatConfig.Filling;
          break;
        case VehicleStatic.BobcatMode.Tunneling:
          label = BobcatConfig.Tunneling;
          spriteName = "BobcatStatusBGTunneling";
          modeString = BobcatConfig.Tunneling;
          break;
        case VehicleStatic.BobcatMode.Smoothing:
          label = BobcatConfig.Smoothing;
          spriteName = "BobcatStatusBGSmoothing";
          modeString = BobcatConfig.Smoothing;
          break;
        default:
          modeString = BobcatConfig.None;
          break;
      }

      // Update UI label and sprite
      VehicleStatic.bobcatStatusLabel?.SetTextImmediately(label);
      VehicleStatic.bobcatStatusSprite?.SetSpriteImmediately(spriteName);

      if (VehicleStatic.WindowBobcatStatus is XUiV_Window bobcatStatusWindow)
      {
        bobcatStatusWindow.TargetAlpha = show ? 1f : 0f;
        bobcatStatusWindow.ForceHide = !show;
        bobcatStatusWindow.ForceVisible(show ? 1 : 0);
        bobcatStatusWindow.UpdateData();
      }

      // 🔌 Set status mode lights here
      SetVehicleStatusModeLight(vehicle, show, modeString);
    }
    public static void SetVehicleActivateWindow(bool show)
    {
      if (!BobcatConfig.EnableUI) return;

      var player = GameManager.Instance?.myEntityPlayerLocal;
      if (player == null) return;

      var entity = player.AttachedToEntity;
      if (!(entity is EntityVehicle vehicle) || vehicle.EntityClass == null || vehicle.EntityClass.entityClassName != "vehicleBobcat")
      {
        // Not in a Bobcat vehicle — hide and clear UI
        VehicleStatic.bobcatActivateLabel?.SetTextImmediately("");
        VehicleStatic.bobcatActivateSprite?.SetSpriteImmediately("");

        if (VehicleStatic.WindowBobcatActivate is XUiV_Window window && VehicleStatic.WindowBobcatStatus is XUiV_Window windowS)
        {
          window.TargetAlpha = 0f;
          window.ForceHide = true;
          window.ForceVisible(0);
          window.UpdateData();
        }

        return;
      }

      // Valid Bobcat vehicle — update UI
      string label = VehicleStatic.isCurrentModeActive ? BobcatConfig.Active : BobcatConfig.Inactive;
      string spriteName = VehicleStatic.isCurrentModeActive ? "BobcatActiveBG" : "BobcatInactiveBG";

      VehicleStatic.bobcatActivateLabel?.SetTextImmediately(label);
      VehicleStatic.bobcatActivateSprite?.SetSpriteImmediately(spriteName);

      if (VehicleStatic.WindowBobcatActivate is XUiV_Window bobcatActivateWindow)
      {
        bobcatActivateWindow.TargetAlpha = show ? 1f : 0f;
        bobcatActivateWindow.ForceHide = !show;
        bobcatActivateWindow.ForceVisible(show ? 1 : 0);
        bobcatActivateWindow.UpdateData();
      }
    }
    public static void SetVehicleStatusPowerLights(EntityVehicle vehicle, bool enabled)
    {
      if (!BobcatConfig.EnableStatusLights) return;

      if (VehicleStatic.transformLookup.TryGetValue("BobcatLightActive", out var lightActive))
        lightActive.gameObject.SetActive(enabled);

      if (VehicleStatic.transformLookup.TryGetValue("BobcatLightInactive", out var lightInactive))
        lightInactive.gameObject.SetActive(!enabled);
    }
    public static void SetVehicleStatusModeLight(EntityVehicle vehicle, bool enabled, string status)
    {
      if (!BobcatConfig.EnableStatusLights) return;

      var statusLightMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        { BobcatConfig.Landscaping, "BobcatLightLandscaping" },
        { BobcatConfig.Leveling,    "BobcatLightLeveling" },
        { BobcatConfig.Tunneling,   "BobcatLightTunneling" },
        { BobcatConfig.Filling,     "BobcatLightFilling" },
        { BobcatConfig.Smoothing,   "BobcatLightSmoothing" },
        { BobcatConfig.None,        "BobcatLightNone" }
      };

      // Turn off all mode lights first
      foreach (var key in statusLightMap.Values)
      {
        if (VehicleStatic.transformLookup.TryGetValue(key, out var light))
          light.gameObject.SetActive(false);
      }

      // Enable the target light if valid
      if (statusLightMap.TryGetValue(status ?? BobcatConfig.None, out var targetLightName) &&
          VehicleStatic.transformLookup.TryGetValue(targetLightName, out var targetLight))
      {
        targetLight.gameObject.SetActive(enabled);
      }
    }
    public static void UpdateVehicleLights(EntityVehicle vehicle)
    {
      // If vehicle is null or not a Bobcat, turn off everything
      if (vehicle == null || vehicle.EntityClass == null || vehicle.EntityClass.entityClassName != "vehicleBobcat")
      {
        SetVehicleStatusPowerLights(null, false);
        SetVehicleStatusModeLight(null, false, BobcatConfig.None);
        return;
      }

      // Turn on/off active light based on current activity state
      SetVehicleStatusPowerLights(vehicle, VehicleStatic.isCurrentModeActive);

      // Update the correct mode light based on current mode
      string mode = BobcatConfig.None;
      switch (VehicleStatic.CurrentMode)
      {
        case VehicleStatic.BobcatMode.LandscapingHigh: mode = BobcatConfig.Landscaping; break;
        case VehicleStatic.BobcatMode.Leveling: mode = BobcatConfig.Leveling; break;
        case VehicleStatic.BobcatMode.Tunneling: mode = BobcatConfig.Tunneling; break;
        case VehicleStatic.BobcatMode.Filling: mode = BobcatConfig.Filling; break;
        case VehicleStatic.BobcatMode.Smoothing: mode = BobcatConfig.Smoothing; break;
        case VehicleStatic.BobcatMode.None:
        default: mode = BobcatConfig.None; break;
      }

      SetVehicleStatusModeLight(vehicle, true, mode);
    }
  }
}
