using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LCThirdPerson.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class CursorPatch
    {
        internal static Sprite CreateCrosshairSprite()
        {
            string filename = @"BepinEx\plugins\LCThirdPerson\crosshair.png";
            var crosshairData = System.IO.File.ReadAllBytes(filename);

            var tex = new Texture2D(2, 2);
            tex.LoadImage(crosshairData);
            tex.filterMode = FilterMode.Point;

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(tex.width / 2, tex.height / 2)
            );
        }

        [HarmonyPostfix]
        [HarmonyPatch("LateUpdate")]
        private static void PatchUpdate(
            ref PlayerControllerB __instance,
            ref bool ___isCameraDisabled
        ) {
            if (!__instance.isPlayerControlled || ___isCameraDisabled || !ThirdPersonPlugin.Instance.Enabled)
            {
                return;
            }

            if (!ThirdPersonPlugin.Instance.ShowCursor.Value || __instance.inTerminalMenu || __instance.cursorIcon.enabled)
            {
                return;
            }

            __instance.cursorIcon.enabled = true;
            __instance.cursorIcon.sprite = ThirdPersonPlugin.CrosshairSprite;
        }
    }
}
