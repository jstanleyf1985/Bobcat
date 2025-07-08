using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static void HandleTurbo(EntityVehicle vehicle, PlayerActionsVehicle actions)
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
    public static IEnumerator HandleTurboWhileHeld(EntityVehicle vehicle)
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
  }

}
