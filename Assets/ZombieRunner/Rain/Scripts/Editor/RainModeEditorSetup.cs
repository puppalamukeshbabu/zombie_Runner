using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieRunner.Audio;
using ZombieRunner.World;

namespace ZombieRunner.Rain.Editor
{
    /// <summary>
    /// Editor tooling for Rain Mode environment variant setup and testing.
    /// Provides 1-click verification, configuration, and toggling of Rain Mode.
    /// </summary>
    public static class RainModeEditorSetup
    {
        [MenuItem("ZombieRunner/Rain Mode/Apply Rain Mode to Gameplay Scene", priority = 100)]
        public static void ApplyRainModeToScene()
        {
            string gameplayScenePath = "Assets/ZombieRunner/Scenes/ZombieRunnerGameplay.unity";
            var currentScene = EditorSceneManager.GetActiveScene();

            if (currentScene.path != gameplayScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(gameplayScenePath);
                }
            }

            Debug.Log("[RainModeEditor] Configuring Rain Mode in Gameplay Scene...");

            // 1. EnvironmentThemeManager configuration
            EnvironmentThemeManager themeMgr = Object.FindAnyObjectByType<EnvironmentThemeManager>();
            if (themeMgr != null)
            {
                themeMgr.IsRainMode = true;
                EditorUtility.SetDirty(themeMgr);
                Debug.Log("[RainModeEditor] Set EnvironmentThemeManager.rainMode = true.");
            }

            // 2. AmbientAudioController configuration
            AmbientAudioController audioCtrl = Object.FindAnyObjectByType<AmbientAudioController>();
            if (audioCtrl != null)
            {
                audioCtrl.RainModeAudio = true;
                AudioClip rainClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/ZombieRunner/Audio/Clips/Ambience_Rain_Storm_Loop.wav");
                if (rainClip != null)
                {
                    SerializedObject so = new SerializedObject(audioCtrl);
                    SerializedProperty clipProp = so.FindProperty("rainAmbienceClip");
                    if (clipProp != null)
                    {
                        clipProp.objectReferenceValue = rainClip;
                        so.ApplyModifiedProperties();
                    }
                }
                EditorUtility.SetDirty(audioCtrl);
                Debug.Log("[RainModeEditor] Configured AmbientAudioController with Rain Ambience Clip.");
            }

            // 3. RainModeManager GameObject
            RainModeManager rainMgr = Object.FindAnyObjectByType<RainModeManager>();
            if (rainMgr == null)
            {
                GameObject rainObj = new GameObject("Rain Mode Environment Manager");
                rainMgr = rainObj.AddComponent<RainModeManager>();
                Undo.RegisterCreatedObjectUndo(rainObj, "Create Rain Mode Manager");
                Debug.Log("[RainModeEditor] Created 'Rain Mode Environment Manager' GameObject in scene.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[RainModeEditor] Rain Mode successfully configured and saved! Press Play to experience Rain Mode.");
        }

        [MenuItem("ZombieRunner/Rain Mode/Toggle Rain Mode (Enable or Disable)", priority = 101)]
        public static void ToggleRainMode()
        {
            EnvironmentThemeManager themeMgr = Object.FindAnyObjectByType<EnvironmentThemeManager>();
            AmbientAudioController audioCtrl = Object.FindAnyObjectByType<AmbientAudioController>();
            RainModeManager rainMgr = Object.FindAnyObjectByType<RainModeManager>();

            bool current = themeMgr != null ? themeMgr.IsRainMode : true;
            bool newState = !current;

            if (themeMgr != null)
            {
                themeMgr.IsRainMode = newState;
                EditorUtility.SetDirty(themeMgr);
            }

            if (audioCtrl != null)
            {
                audioCtrl.RainModeAudio = newState;
                EditorUtility.SetDirty(audioCtrl);
            }

            if (rainMgr != null)
            {
                rainMgr.gameObject.SetActive(newState);
                EditorUtility.SetDirty(rainMgr);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[RainModeEditor] Rain Mode toggled to: {(newState ? "ENABLED (Rain Variant)" : "DISABLED (Original Game)")}");
        }
    }
}
