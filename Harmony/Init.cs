using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle : IModApi
  {
    public static AssetBundle bobcatBundle = null;
    public static void BobcatStart()
    //public static void BobcatStart(ref ModEvents.SGameStartDoneData _data)
    {
      BobcatConfig.Load(System.IO.Path.Combine(ModManager.GetMod("Bobcat")?.Path, "Configuration.xml"));

      GameManager.Instance.StartCoroutine(RunGlobalBobcat());
      GameManager.Instance.StartCoroutine(BobcatUpdateVisualsCoroutine());
      GameManager.Instance.StartCoroutine(OnPlayerLoggedIn());
      LoadBobcatParticles();
      
    }

    public static void BobcatShutdown()
    //public static void BobcatShutdown(ref ModEvents.SWorldShuttingDownData _data)
    {
      if (bobcatBundle != null)
      {
        bobcatBundle.Unload(unloadAllLoadedObjects: false);
        bobcatBundle = null;
      }

      VehicleStatic.vehicles.Clear();
    }

    public static void LoadBobcatParticles()
    {
      // Workaround to ensure particles aren't despawning prematurely as unity re-uses particles with the same name for some reason
      string[] dustParticleNames = new string[20] {"p_BobcatDust1","p_BobcatDust2","p_BobcatDust3","p_BobcatDust4","p_BobcatDust5","p_BobcatDust6","p_BobcatDust7","p_BobcatDust8","p_BobcatDust9","p_BobcatDust10","p_BobcatDust11","p_BobcatDust12","p_BobcatDust13","p_BobcatDust14","p_BobcatDust15","p_BobcatDust16","p_BobcatDust17","p_BobcatDust18","p_BobcatDust19","p_BobcatDust20"};
      string[] bloodParticleNames = new string[20] { "p_BloodMist1", "p_BloodMist2", "p_BloodMist3", "p_BloodMist4", "p_BloodMist5", "p_BloodMist6", "p_BloodMist7", "p_BloodMist8", "p_BloodMist9", "p_BloodMist10", "p_BloodMist11", "p_BloodMist12", "p_BloodMist13", "p_BloodMist14", "p_BloodMist15", "p_BloodMist16", "p_BloodMist17", "p_BloodMist18", "p_BloodMist19", "p_BloodMist20" };
      string[] dustParticlePaths = new string[20] {"assets/exports/vehicles/p_bobcatdust1.prefab","assets/exports/vehicles/p_bobcatdust2.prefab","assets/exports/vehicles/p_bobcatdust3.prefab","assets/exports/vehicles/p_bobcatdust4.prefab","assets/exports/vehicles/p_bobcatdust5.prefab","assets/exports/vehicles/p_bobcatdust6.prefab","assets/exports/vehicles/p_bobcatdust7.prefab","assets/exports/vehicles/p_bobcatdust8.prefab","assets/exports/vehicles/p_bobcatdust9.prefab","assets/exports/vehicles/p_bobcatdust10.prefab","assets/exports/vehicles/p_bobcatdust11.prefab","assets/exports/vehicles/p_bobcatdust12.prefab","assets/exports/vehicles/p_bobcatdust13.prefab","assets/exports/vehicles/p_bobcatdust14.prefab","assets/exports/vehicles/p_bobcatdust15.prefab","assets/exports/vehicles/p_bobcatdust16.prefab","assets/exports/vehicles/p_bobcatdust17.prefab","assets/exports/vehicles/p_bobcatdust18.prefab","assets/exports/vehicles/p_bobcatdust19.prefab","assets/exports/vehicles/p_bobcatdust20.prefab"};
      string[] bloodParticlePaths = new string[20] { "assets/exports/vehicles/p_bloodmist1.prefab", "assets/exports/vehicles/p_bloodmist2.prefab", "assets/exports/vehicles/p_bloodmist3.prefab", "assets/exports/vehicles/p_bloodmist4.prefab", "assets/exports/vehicles/p_bloodmist5.prefab", "assets/exports/vehicles/p_bloodmist6.prefab", "assets/exports/vehicles/p_bloodmist7.prefab", "assets/exports/vehicles/p_bloodmist8.prefab", "assets/exports/vehicles/p_bloodmist9.prefab", "assets/exports/vehicles/p_bloodmist10.prefab", "assets/exports/vehicles/p_bloodmist11.prefab", "assets/exports/vehicles/p_bloodmist12.prefab", "assets/exports/vehicles/p_bloodmist13.prefab", "assets/exports/vehicles/p_bloodmist14.prefab", "assets/exports/vehicles/p_bloodmist15.prefab", "assets/exports/vehicles/p_bloodmist16.prefab", "assets/exports/vehicles/p_bloodmist17.prefab", "assets/exports/vehicles/p_bloodmist18.prefab", "assets/exports/vehicles/p_bloodmist19.prefab", "assets/exports/vehicles/p_bloodmist20.prefab" };
      GameObject[] dustPrefabs = new GameObject[20];
      GameObject[] bloodPrefabs = new GameObject[20];
      int[] dustIds = new int[20];
      int[] bloodIds = new int[20];

      if (bobcatBundle == null)
      {
        string path = System.IO.Path.Combine(ModManager.GetMod("Bobcat")?.Path, "Resources", "BobcatParticles.unity3d");
        bobcatBundle = AssetBundle.LoadFromFile(path);
        if (bobcatBundle == null)
        {
          Log.Error("BobcatMod: Failed to load asset bundle from path: " + path);
          return;
        }
      }
      else
      {
        Log.Warning("BobcatMod: Asset bundle already loaded, skipping reload.");
      }

      for (int i = 0; i < dustParticlePaths.Length; i++) dustPrefabs[i] = bobcatBundle.LoadAsset<GameObject>(dustParticlePaths[i]);
      for (int i = 0; i < bloodParticlePaths.Length; i++) bloodPrefabs[i] = bobcatBundle.LoadAsset<GameObject>(bloodParticlePaths[i]);

      for (int i = 0; i < dustParticleNames.Length; i++) dustIds[i] = ParticleEffect.ToId(dustParticleNames[i]);
      for (int i = 0; i < bloodParticleNames.Length; i++) bloodIds[i] = ParticleEffect.ToId(bloodParticleNames[i]);

      for (int i = 0; i < dustIds.Length; i++) ParticleEffect.loadedTs[dustIds[i]] = dustPrefabs[i].transform;
      for (int i = 0; i < bloodIds.Length; i++) ParticleEffect.loadedTs[bloodIds[i]] = bloodPrefabs[i].transform;
    }

    public void InitMod(Mod _modInstance)
    {
      Log.Out(" Loading Patch: " + GetType());

      var harmony = new HarmonyLib.Harmony(GetType().ToString());
      harmony.PatchAll(Assembly.GetExecutingAssembly());

      ModEvents.GameStartDone.RegisterHandler(BobcatStart);
      ModEvents.WorldShuttingDown.RegisterHandler(BobcatShutdown);
    }
  }
}