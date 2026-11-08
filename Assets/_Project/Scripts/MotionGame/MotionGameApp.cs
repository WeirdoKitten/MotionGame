using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
namespace FamilyAdventure.Presentation
{
    // Entry point of the standalone Motion Game project. In the full Family Adventure game the mission
    // menu opens from the egg house; here it opens as soon as the MotionGame scene loads. Other scenes
    // (such as the temporary scenes the test runner makes) are left alone.
    public static class MotionGameApp
    {
        public const string SceneName = "MotionGame";

        // Registered before the first scene loads, so the first scene and any later load of the
        // MotionGame scene both open the menu. Pressing Play in the Unity 6 editor reports the first scene
        // with an internal mode value (not Single), so only additive loads are skipped.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        static void Loaded(Scene scene, LoadSceneMode mode) { if (scene.name == SceneName && mode != LoadSceneMode.Additive) Launch(); }

        static void Launch()
        {
            Application.targetFrameRate = 60;
            // Phones and tablets play in landscape only; the screen stays on while playing.
            Screen.autorotateToPortrait = false; Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true;
            if (Application.isMobilePlatform) { Screen.orientation = ScreenOrientation.AutoRotation; Screen.sleepTimeout = SleepTimeout.NeverSleep; }
            if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            MotionMissionMenu.Open(Quit);
        }

        // The close button on the mission menu ends the app (or play mode in the editor).
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
