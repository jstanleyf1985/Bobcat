using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static int[] GetHWDFromMods(List<string> mods)
    {
      int[] smallBucketHWD = new int[3] { BobcatConfig.SmallBucketHeight, BobcatConfig.SmallBucketWidth, BobcatConfig.SmallBucketDepth };
      int[] largeBucketHWD = new int[3] { BobcatConfig.LargeBucketHeight, BobcatConfig.LargeBucketWidth, BobcatConfig.LargeBucketDepth };
      int[] drillHWD = new int[3] { BobcatConfig.DrillHeight, BobcatConfig.DrillWidth, BobcatConfig.DrillDepth };
      if (mods.Contains("modVehicleBucket5")) return largeBucketHWD;
      if (mods.Contains("modVehicleBucket3")) return smallBucketHWD;
      if (mods.Contains("modVehicleDrill")) return drillHWD;

      return new int[3] { 1, 2, 2 };
    }
    public static bool GetIsBobcatVehicle(EntityVehicle vehicle)
    {
      if (vehicle == null || vehicle.EntityClass == null || string.IsNullOrEmpty(vehicle.EntityClass.entityClassName)) return false;
      if (vehicle.EntityClass.entityClassName != "vehicleBobcat") return false;
      return true;
    }
    public static Color HexToColor(string hex)
    {
      if (string.IsNullOrEmpty(hex))
        return Color.white;

      hex = hex.Replace("#", "");

      if (hex.Length == 6) // RGB
      {
        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        return new Color32(r, g, b, 255);
      }
      else if (hex.Length == 8) // RGBA
      {
        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        byte a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
        return new Color32(r, g, b, a);
      }

      Debug.LogWarning("Invalid hex color format: " + hex);
      return Color.white;
    }
  }
}
