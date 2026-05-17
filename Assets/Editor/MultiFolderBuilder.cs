using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class MultiFolderBuilder {
    [MenuItem("Build/Build To Multiple Folders")]
    public static void PerformMultiBuild() {
        string[] scenes = GetEnabledScenes();

        // 2. 빌드를 저장할 대상 폴더 경로들을 배열로 지정합니다.
        string[] targetFolders = new string[] {
            "C:/Users/tetep/OneDrive/Desktop/Extraction/ClientBuild/Client1",
            "C:/Users/tetep/OneDrive/Desktop/Extraction/ClientBuild/Client2"
        };

        string executableName = "MyGame.exe";

        foreach (string folder in targetFolders) {
            if (!Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            }

            string fullBuildPath = Path.Combine(folder, executableName);

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = scenes;
            buildPlayerOptions.locationPathName = fullBuildPath;

            buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
            buildPlayerOptions.options = BuildOptions.None;

            Debug.Log($"빌드 시작: {fullBuildPath}");

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) {
                Debug.Log($"<color=green>빌드 성공!</color> 저장 위치: {folder}");
            }
            else if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Failed) {
                Debug.LogError($"<color=red>빌드 실패!</color> 위치: {folder}");
            }
        }

        Debug.Log("모든 다중 폴더 빌드 작업이 완료되었습니다.");
    }

    private static string[] GetEnabledScenes() {
        List<string> scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes) {
            if (scene.enabled) {
                scenes.Add(scene.path);
            }
        }
        return scenes.ToArray();
    }
}