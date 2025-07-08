namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static void SetVehicleStatusWindow(bool show, bool showRedSprite)
    {
      string label = "None";
      string spriteName = "BobcatStatusBGNone";
      switch (VehicleStatic.CurrentMode)
      {
        case VehicleStatic.BobcatMode.None:
          label = "None";
          spriteName = "BobcatStatusBGNone";
          break;
        case VehicleStatic.BobcatMode.LandscapingHigh:
          label = "Landscape";
          spriteName = "BobcatStatusBGLandscapingHigh";
          break;
        case VehicleStatic.BobcatMode.Leveling:
          label = "Leveling";
          spriteName = showRedSprite ? "BobcatStatusBGLevelingRed" : "BobcatStatusBGLeveling";
          break;
        case VehicleStatic.BobcatMode.Filling:
          label = "Fill";
          spriteName = showRedSprite ? "BobcatStatusBGFillingRed" : "BobcatStatusBGFilling";
          break;
        case VehicleStatic.BobcatMode.Tunneling:
          label = "Tunneling";
          spriteName = "BobcatStatusBGTunneling";
          break;
        case VehicleStatic.BobcatMode.Smoothing:
          label = "Smoothing";
          spriteName = "BobcatStatusBGSmoothing";
          break;
        default:
          label = "None";
          spriteName = "BobcatStatusBGNone";
          break;
      }

      XUiV_Window bobcatStatusWindow = (XUiV_Window)VehicleStatic.WindowBobcatStatus;
      VehicleStatic.bobcatStatusLabel.SetTextImmediately(label);
      VehicleStatic.bobcatStatusSprite.SetSpriteImmediately(spriteName);
      bobcatStatusWindow.TargetAlpha = show ? 1 : 0;
      bobcatStatusWindow.ForceHide = show;
      bobcatStatusWindow.ForceVisible(show ? 1 : 0);
      bobcatStatusWindow.UpdateData();
    }
    public static void SetVehicleActivateWindow(bool show)
    {
      string label = VehicleStatic.isCurrentModeActive ? "Active" : "Inactive";
      string spriteName = VehicleStatic.isCurrentModeActive ? "BobcatActiveBG" : "BobcatInactiveBG";

      XUiV_Window bobcatActivateWindow = (XUiV_Window)VehicleStatic.WindowBobcatActivate;
      VehicleStatic.bobcatActivateLabel.SetTextImmediately(label);
      VehicleStatic.bobcatActivateSprite.SetSpriteImmediately(spriteName);
      bobcatActivateWindow.TargetAlpha = show ? 1 : 0;
      bobcatActivateWindow.ForceHide = show;
      bobcatActivateWindow.ForceVisible(show ? 1 : 0);
      bobcatActivateWindow.UpdateData();
    }
  }
}
