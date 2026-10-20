using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace SmartXR.Input
{
    /// <summary>Ob gerade eine echte Brille ein Bild bekommt.</summary>
    public static class XrPresence
    {
        private static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        public static bool HeadsetActive
        {
            get
            {
                SubsystemManager.GetSubsystems(displays);
                foreach (var d in displays)
                    if (d.running) return true;
                return false;
            }
        }
    }
}
