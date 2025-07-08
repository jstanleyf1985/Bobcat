using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static int[] GetHWDFromMods(List<string> mods)
    {
      if (mods.Contains("modVehicleBucket5")) return new int[3] { 2, 5, 3 };
      if (mods.Contains("modVehicleBucket3")) return new int[3] { 2, 3, 3 };
      if (mods.Contains("modVehicleDrill")) return new int[3] { 3, 3, 4 };

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
