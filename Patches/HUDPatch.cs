using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace LCThirdPerson.Patches
{
    [HarmonyPatch(typeof(HUDManager))]
    internal class HUDPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("UpdateScanNodes")]
        private static void UnderwaterPrepatch(PlayerControllerB playerScript)
        {
            if (ThirdPersonPlugin.Camera == null)
            {
                return;
            }

            playerScript.gameplayCamera.transform.position = ThirdPersonPlugin.Camera.transform.position;
            playerScript.gameplayCamera.transform.rotation = ThirdPersonPlugin.Camera.transform.rotation;
        }

        [HarmonyPostfix]
        [HarmonyPatch("UpdateScanNodes")]
        private static void UnderwaterPostpatch(PlayerControllerB playerScript)
        {
            if (ThirdPersonPlugin.OriginalTransform == null)
            {
                return;
            }

            playerScript.gameplayCamera.transform.position = ThirdPersonPlugin.OriginalTransform.transform.position;
            playerScript.gameplayCamera.transform.rotation = ThirdPersonPlugin.OriginalTransform.transform.rotation;
        }
    }
}
