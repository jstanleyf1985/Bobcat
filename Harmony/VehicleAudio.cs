using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static void DisableDrillAudio(EntityVehicle vehicle)
    {
      bool audio = VehicleStatic.transformLookup.TryGetValue("Audio", out var audioT);
      Transform rockDrillT = audioT.Find("RockDrilling");
      AudioSource drillAudio = rockDrillT.GetComponent<AudioSource>();
      if (drillAudio != null) drillAudio.Stop();
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
    public static IEnumerator FadeInAudio(AudioSource audio, float secondsToFade, float targetVolume)
    {
      if (audio == null) yield break;

      audio.volume = 0f;
      audio.Play();
      VehicleStatic.vehicleModeAudioSources.Add(audio);

      float time = 0f;
      while (time < secondsToFade)
      {
        // Bail out if audio was destroyed or became null
        if (audio == null || audio.gameObject == null)
          yield break;

        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / secondsToFade);
        audio.volume = Mathf.Lerp(0f, targetVolume, t);
        yield return null;
      }

      if (audio != null)
        audio.volume = BobcatConfig.DrillAudioVolume;
    }
    public static IEnumerator FadeOutAudio(AudioSource audio, float secondsToFade, float startVolume)
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
    public static void ResetDrillAudioAnim(EntityVehicle vehicle)
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
    public static void UpdateDrillAudioAnimBySpeed(EntityVehicle vehicle)
    {
      UpdateDrillAudioPitch(vehicle);
      UpdateDrillAnimationSpeed(vehicle);
    }
    public static void UpdateDrillAudioPitch(EntityVehicle vehicle)
    {
      bool drillAudio = VehicleStatic.transformLookup.TryGetValue("Audio", out var drillAudioT);
      AudioSource drillAudioComponent = drillAudioT.Find("RockDrilling").GetComponentInChildren<AudioSource>();
      if (drillAudioComponent.isPlaying) drillAudioComponent.pitch = Mathf.Clamp(vehicle.GetVehicle().CurrentForwardVelocity * 0.4f, 0.5f, 2f);
    }
  }
}
