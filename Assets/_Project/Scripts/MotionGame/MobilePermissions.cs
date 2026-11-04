using System.Collections;
using UnityEngine;
namespace FamilyAdventure.Presentation
{
    // Camera and microphone permission. On Android the runtime permission dialog is used; elsewhere
    // Unity's user authorization (iOS, desktop).
    public static class MobilePermissions
    {
        public static bool HasCamera
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
#else
                return UnityEngine.Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
            }
        }
        public static bool HasMicrophone
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
                return UnityEngine.Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
            }
        }
        public static IEnumerator RequestCamera()
        {
            if (HasCamera) yield break;
#if UNITY_ANDROID && !UNITY_EDITOR
            yield return Ask(UnityEngine.Android.Permission.Camera);
#else
            yield return UnityEngine.Application.RequestUserAuthorization(UserAuthorization.WebCam);
#endif
        }
        public static IEnumerator RequestMicrophone()
        {
            if (HasMicrophone) yield break;
#if UNITY_ANDROID && !UNITY_EDITOR
            yield return Ask(UnityEngine.Android.Permission.Microphone);
#else
            yield return UnityEngine.Application.RequestUserAuthorization(UserAuthorization.Microphone);
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        static IEnumerator Ask(string permission)
        {
            bool answered = false;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered = true;
            callbacks.PermissionDenied += _ => answered = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => answered = true;
            UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);
            float until = Time.realtimeSinceStartup + 30;
            while (!answered && Time.realtimeSinceStartup < until) yield return null;
        }
#endif
    }
}
