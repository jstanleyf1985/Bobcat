using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bobcat
{
  public partial class BobcatVehicle
  {
    public static void UpdateWheelTraction(EntityVehicle vehicle)
    {
      Transform physicsTransform = vehicle.PhysicsTransform;
      WheelCollider[] wheels = physicsTransform.GetComponentsInChildren<WheelCollider>();
      bool hasTractionControl = GetModifierNames(vehicle).Contains("modTractionControl");
      bool disableTractionControl = (!BobcatConfig.EnableTractionControl || !hasTractionControl);

      foreach (var wheel in wheels)
      {
        wheel.mass = disableTractionControl ? 40 : 1000;

        WheelFrictionCurve forwardFriction = wheel.forwardFriction;
        forwardFriction.stiffness = disableTractionControl ? 1.3f : 5f;
        forwardFriction.extremumSlip = disableTractionControl ? 0.4f : 2f;
        forwardFriction.extremumValue = disableTractionControl ? 1f : 2f;
        forwardFriction.asymptoteSlip = disableTractionControl ? 0.8f : 2f;
        forwardFriction.asymptoteValue = disableTractionControl ? 0.5f : 2f;
        wheel.forwardFriction = forwardFriction;

        WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
        sidewaysFriction.stiffness = disableTractionControl ? 1.3f : 5f;
        sidewaysFriction.extremumSlip = disableTractionControl ? 0.2f : 2f;
        sidewaysFriction.extremumValue = disableTractionControl ? 2f : 2f;
        sidewaysFriction.asymptoteSlip = disableTractionControl ? 0.75f : 2f;
        sidewaysFriction.asymptoteValue = disableTractionControl ? 0.75f : 2f;
        wheel.sidewaysFriction = sidewaysFriction;
      }
    }
  }
}
