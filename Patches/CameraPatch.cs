using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace LCThirdPerson.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class CameraPatch
    {
        private static PlayerControllerB Instance;
        private static bool TriggerAwake;
        private static int OriginalCullingMask;
        private static UnityEngine.Rendering.ShadowCastingMode OriginalShadowCastingMode;

        private static readonly string[] IgnoreGameObjectPrefixes = new[]{
            "VolumeMain"
        };

        public static void OnEnable()
        {
            ThirdPersonPlugin.Log.LogInfo("Enabled");

            var visor = Instance.localVisor;
            var playerModel = Instance.thisPlayerModel;

            // Hide the visor
            visor.gameObject.SetActive(false);

            // Show the player model
            playerModel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Hide the player arms
            Instance.thisPlayerModelArms.enabled = false;

            // Increase the grab distance
            Instance.grabDistance = Math.Max(5f - ThirdPersonPlugin.Instance.Offset.Value.z, 5);

            // Set culling mask to see model's layer
            Instance.gameplayCamera.cullingMask = OriginalCullingMask | (1 << 23);
        }

        public static void OnDisable()
        {
            ThirdPersonPlugin.Log.LogInfo("Disabled");

            var visor = Instance.localVisor;
            var playerModel = Instance.thisPlayerModel;

            // Show the visor
            visor.gameObject.SetActive(true);

            // Hide the player model
            playerModel.shadowCastingMode = OriginalShadowCastingMode;

            // Show the arms
            Instance.thisPlayerModelArms.enabled = true;

            // Reset the grab distance
            Instance.grabDistance = 5f;

            // Hide the models' layer again
            Instance.gameplayCamera.cullingMask = OriginalCullingMask;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void Awake()
        {
            TriggerAwake = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        private static void PatchUpdate(ref PlayerControllerB __instance, ref bool ___isCameraDisabled, ref bool ___isPlayerControlled)
        {
            if (!___isPlayerControlled || ___isCameraDisabled)
            {
                return;
            }

            if (TriggerAwake)
            {
                Instance = __instance;
                OriginalCullingMask = Instance.gameplayCamera.cullingMask;
                OriginalShadowCastingMode = Instance.thisPlayerModel.shadowCastingMode;
                ThirdPersonPlugin.Instance.OnEnable.AddListener(OnEnable);
                ThirdPersonPlugin.Instance.OnDisable.AddListener(OnDisable);

                if (ThirdPersonPlugin.Instance.Enabled)
                {
                    OnEnable();
                }

                TriggerAwake = false;
            }

            if (Instance == null)
            {
                return;
            }

            if (ThirdPersonPlugin.OriginalTransform == null)
            {
                ThirdPersonPlugin.OriginalTransform = CopyTransform(Instance.gameplayCamera.transform, "LCThirdPerson_Original Camera Position");
                ThirdPersonPlugin.Camera = CopyTransform(Instance.gameplayCamera.transform, "LCThirdPerson_Camera Position");
            }

            // Set this for any method that needs patching inbetween the start of Update and the end of LateUpdate
            ThirdPersonPlugin.Camera.position = Instance.gameplayCamera.transform.position;
            ThirdPersonPlugin.Camera.rotation = Instance.gameplayCamera.transform.rotation;

            // Reset the camera before the PlayerController update method, so nothing gets too messed up
            Instance.gameplayCamera.transform.rotation = ThirdPersonPlugin.OriginalTransform.transform.rotation;
            Instance.gameplayCamera.transform.position = ThirdPersonPlugin.OriginalTransform.transform.position;

            // Don't check for toggle key if in terminal menu or typing in chat
            if (Instance.inTerminalMenu || Instance.isTypingChat)
            {
                return;
            }

            ThirdPersonPlugin.Instance.CheckEnable();
        }

        [HarmonyPostfix]
        [HarmonyPatch("LateUpdate")]
        private static void PatchCamera(ref bool ___isCameraDisabled, ref bool ___isPlayerControlled)
        {
            var originalTransform = ThirdPersonPlugin.OriginalTransform;

            if (!___isPlayerControlled || ___isCameraDisabled || originalTransform == null)
            {
                return;
            }

            var gameplayCamera = Instance.gameplayCamera;

            // Set the placeholder rotation to match the updated gameplayCamera rotation
            originalTransform.transform.rotation = gameplayCamera.transform.rotation;

            if (!ThirdPersonPlugin.Instance.Enabled || Instance.inTerminalMenu)
            {
                return;
            }

            var offset = originalTransform.transform.right * ThirdPersonPlugin.Instance.Offset.Value.x +
                originalTransform.transform.up * ThirdPersonPlugin.Instance.Offset.Value.y;
            var lineStart = originalTransform.transform.position;
            var lineEnd = originalTransform.transform.position + offset + originalTransform.transform.forward * ThirdPersonPlugin.Instance.Offset.Value.z;

            // Check for camera collisions
            if (Physics.Linecast(lineStart, lineEnd, out RaycastHit hit, StartOfRound.Instance.collidersAndRoomMask) && !IgnoreCollision(hit.transform.name))
            {
                offset += originalTransform.transform.forward * -Mathf.Max(hit.distance, 0);
            }
            else
            {
                offset += originalTransform.transform.forward * ThirdPersonPlugin.Instance.Offset.Value.z;
            }

            // Set the camera offset
            gameplayCamera.transform.position = originalTransform.transform.position + offset;

            // Don't fix interact ray if on a ladder
            if (Instance.isClimbingLadder)
            {
                return;
            }

            // Fix the interact ray 
            var methodInfo = typeof(PlayerControllerB).GetMethod(
                "SetHoverTipAndCurrentInteractTrigger",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            methodInfo.Invoke(Instance, new object[] { });
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetFaceUnderwaterFilters")]
        private static void UnderwaterPrepatch()
        {
            if (Instance == null || ThirdPersonPlugin.Camera == null)
            {
                return;
            }

            Instance.gameplayCamera.transform.position = ThirdPersonPlugin.Camera.transform.position;
            Instance.gameplayCamera.transform.rotation = ThirdPersonPlugin.Camera.transform.rotation;
        }

        [HarmonyPostfix]
        [HarmonyPatch("SetFaceUnderwaterFilters")]
        private static void UnderwaterPostpatch()
        {
            if (Instance == null || ThirdPersonPlugin.OriginalTransform == null)
            {
                return;
            }

            Instance.gameplayCamera.transform.position = ThirdPersonPlugin.OriginalTransform.transform.position;
            Instance.gameplayCamera.transform.rotation = ThirdPersonPlugin.OriginalTransform.transform.rotation;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.SpawnPlayerAnimation))]
        private static void SpawnPlayerPostpatch(ref bool ___isPlayerControlled)
        {
            if (Instance == null || !___isPlayerControlled)
            {
                return;
            }

            if (!ThirdPersonPlugin.Instance.Enabled)
            {
                return;
            }

            OnEnable();
        }

        private static Transform CopyTransform(Transform copyTransform, string gameObjectName)
        {
            var newTransform = new GameObject(gameObjectName).transform;
            newTransform.position = copyTransform.position;
            newTransform.rotation = copyTransform.rotation;
            newTransform.parent = copyTransform.parent;

            return newTransform;
        }

        private static bool IgnoreCollision(string name)
        {
            return IgnoreGameObjectPrefixes.Any(prefix => name.StartsWith(prefix));
        }
    }
}
