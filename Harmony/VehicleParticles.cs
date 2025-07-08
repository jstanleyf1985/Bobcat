using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static class BobcatParticleManager
    {
      private static readonly string[] DustVariants = new[] { "p_BobcatDust1", "p_BobcatDust2", "p_BobcatDust3", "p_BobcatDust4", "p_BobcatDust5", "p_BobcatDust6", "p_BobcatDust7", "p_BobcatDust8", "p_BobcatDust9", "p_BobcatDust10", "p_BobcatDust11", "p_BobcatDust12", "p_BobcatDust13", "p_BobcatDust14", "p_BobcatDust15", "p_BobcatDust16", "p_BobcatDust17", "p_BobcatDust18", "p_BobcatDust19", "p_BobcatDust20" };
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
    public static void SetAmbientDrillParticles(EntityVehicle vehicle, bool enable)
    {
      bool drillParticles = (VehicleStatic.transformLookup.TryGetValue("DrillParticles", out var drillParticlesT));
      drillParticlesT.gameObject.SetActive(enable);
    }
    public static void StartParticleIfAvailable(Transform transform)
    {
      if (transform == null) return;

      var ps = transform.GetComponent<ParticleSystem>();
      if (ps != null && !ps.isPlaying) ps.Play();
    }
    public static void StopParticleIfAvailable(string key)
    {
      if (VehicleStatic.transformLookup.TryGetValue(key, out var transform))
      {
        ParticleSystem ps = transform.GetComponent<ParticleSystem>();

        if (ps != null && ps.isPlaying) ps.Stop();
        transform.gameObject.SetActive(false);
      }
    }
    public static void UpdateTurboParticle(ParticleSystem particle, EntityVehicle vehicle, bool hasSuperCharger, ParticleSystem.MinMaxCurve curve)
    {
      if (particle == null) return;

      var main = particle.main;
      var emission = particle.emission;

      if (hasSuperCharger) emission.rateOverTime = vehicle.speedForward * 50;
      else emission.rateOverTime = vehicle.speedForward * 100;

      main.startSize = curve;
      if (!particle.isPlaying) particle.Play();
    }
  }

}
