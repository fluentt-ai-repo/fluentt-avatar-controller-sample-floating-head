using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    /// <summary>
    /// Tool to remap animation clip paths for blendshapes
    /// Useful when avatar hierarchy differs from animation clip hierarchy
    /// </summary>
    public class AnimationClipPathRemapper : EditorWindow
    {
        private List<Object> selectedAssets = new List<Object>(); // Can be AnimationClip or FBX/Model
        private string oldPath = "Face";
        private string newPath = "head_grp/face";
        private string outputFolder = "Assets/ProcessedAnimations";
        private Vector2 scrollPosition;

        [MenuItem("FluentT/Tools/Animation Clip Path Remapper")]
        public static void ShowWindow()
        {
            GetWindow<AnimationClipPathRemapper>("Animation Path Remapper");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Animation Clip Path Remapper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool remaps animation curve paths in AnimationClips.\n" +
                "• Select FBX files or AnimationClip assets\n" +
                "• FBX clips will be extracted as new assets (original FBX untouched)\n" +
                "• New clips will be saved to the output folder\n\n" +
                "Example:\n" +
                "• Animation has: Face\n" +
                "• Avatar has: head_grp/face\n" +
                "• Change 'Old Path' to 'Face' and 'New Path' to 'head_grp/face'",
                MessageType.Info);

            EditorGUILayout.Space();

            // Path settings
            EditorGUILayout.LabelField("Path Remapping", EditorStyles.boldLabel);
            oldPath = EditorGUILayout.TextField("Old Path", oldPath);
            newPath = EditorGUILayout.TextField("New Path", newPath);

            EditorGUILayout.Space();

            // Output folder
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    // Convert absolute path to relative Assets path
                    if (path.StartsWith(Application.dataPath))
                    {
                        outputFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Assets list
            EditorGUILayout.LabelField("Assets to Process (FBX or AnimationClips)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Selected Assets"))
            {
                AddSelectedAssets();
            }
            if (GUILayout.Button("Clear List"))
            {
                selectedAssets.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Scroll view for assets
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            for (int i = 0; i < selectedAssets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                selectedAssets[i] = EditorGUILayout.ObjectField(selectedAssets[i], typeof(Object), false);

                // Show asset type
                string assetType = "";
                if (selectedAssets[i] is AnimationClip)
                    assetType = " [Clip]";
                else if (selectedAssets[i] != null && AssetDatabase.GetAssetPath(selectedAssets[i]).EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    assetType = " [FBX]";

                EditorGUILayout.LabelField(assetType, GUILayout.Width(50));

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    selectedAssets.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Action buttons
            GUI.enabled = selectedAssets.Count > 0 && !string.IsNullOrEmpty(oldPath) && !string.IsNullOrEmpty(newPath);

            if (GUILayout.Button("Preview Changes", GUILayout.Height(30)))
            {
                PreviewChanges();
            }

            EditorGUILayout.Space();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Extract & Apply Path Remapping", GUILayout.Height(40)))
            {
                var clipCount = CountAllClips();
                if (EditorUtility.DisplayDialog("Confirm Path Remapping",
                    $"This will extract and remap paths in approximately {clipCount} animation clip(s).\n\n" +
                    $"Old Path: '{oldPath}'\n" +
                    $"New Path: '{newPath}'\n" +
                    $"Output: '{outputFolder}'\n\n" +
                    "FBX files will NOT be modified. New clips will be created.",
                    "Apply", "Cancel"))
                {
                    ApplyPathRemapping();
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void AddSelectedAssets()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj == null) continue;

                string assetPath = AssetDatabase.GetAssetPath(obj);

                // Check if it's an AnimationClip or FBX
                if (obj is AnimationClip || assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!selectedAssets.Contains(obj))
                    {
                        selectedAssets.Add(obj);
                    }
                }
            }
        }

        private int CountAllClips()
        {
            int count = 0;
            foreach (var asset in selectedAssets)
            {
                if (asset == null) continue;

                if (asset is AnimationClip)
                {
                    count++;
                }
                else
                {
                    string assetPath = AssetDatabase.GetAssetPath(asset);
                    if (assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var clips = GetAnimationClipsFromFBX(assetPath);
                        count += clips.Count;
                    }
                }
            }
            return count;
        }

        private List<AnimationClip> GetAnimationClipsFromFBX(string fbxPath)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            Object[] objects = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

            foreach (var obj in objects)
            {
                if (obj is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        private void PreviewChanges()
        {
            Debug.Log($"=== Animation Clip Path Remapping Preview ===");
            Debug.Log($"Old Path: '{oldPath}' → New Path: '{newPath}'");
            Debug.Log($"Output Folder: '{outputFolder}'\n");

            int totalClips = 0;
            int totalCurves = 0;
            int matchingCurves = 0;

            foreach (var asset in selectedAssets)
            {
                if (asset == null) continue;

                List<AnimationClip> clips = new List<AnimationClip>();
                string sourceInfo = "";

                if (asset is AnimationClip clip)
                {
                    clips.Add(clip);
                    sourceInfo = "(standalone clip)";
                }
                else
                {
                    string assetPath = AssetDatabase.GetAssetPath(asset);
                    if (assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        clips = GetAnimationClipsFromFBX(assetPath);
                        sourceInfo = $"(from FBX: {System.IO.Path.GetFileName(assetPath)})";
                    }
                }

                foreach (var c in clips)
                {
                    if (c == null) continue;

                    var bindings = AnimationUtility.GetCurveBindings(c);
                    int clipMatchingCurves = 0;

                    foreach (var binding in bindings)
                    {
                        totalCurves++;
                        if (binding.path == oldPath)
                        {
                            matchingCurves++;
                            clipMatchingCurves++;
                        }
                    }

                    if (clipMatchingCurves > 0)
                    {
                        Debug.Log($"• {c.name}: {clipMatchingCurves} curves will be remapped {sourceInfo}");
                        totalClips++;
                    }
                }
            }

            Debug.Log($"\nTotal: {totalClips} clips, {matchingCurves}/{totalCurves} curves will be remapped");
        }

        private void ApplyPathRemapping()
        {
            // Ensure output folder exists
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                string[] folders = outputFolder.Split('/');
                string currentPath = folders[0]; // "Assets"
                for (int i = 1; i < folders.Length; i++)
                {
                    string newPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = newPath;
                }
            }

            int processedClips = 0;
            int totalRemappedCurves = 0;
            List<string> createdAssets = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var asset in selectedAssets)
                {
                    if (asset == null) continue;

                    List<AnimationClip> clips = new List<AnimationClip>();
                    string fbxName = "";

                    if (asset is AnimationClip clip)
                    {
                        clips.Add(clip);
                        // For standalone clips, use the clip's asset name
                        string assetPath = AssetDatabase.GetAssetPath(clip);
                        fbxName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                    }
                    else
                    {
                        string assetPath = AssetDatabase.GetAssetPath(asset);
                        if (assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                        {
                            clips = GetAnimationClipsFromFBX(assetPath);
                            fbxName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                        }
                    }

                    foreach (var originalClip in clips)
                    {
                        if (originalClip == null) continue;

                        // Create a copy of the clip
                        AnimationClip newClip = Object.Instantiate(originalClip);
                        newClip.name = originalClip.name;

                        // Remap paths in the new clip
                        int remappedCurves = RemapClipPaths(newClip, oldPath, newPath);

                        if (remappedCurves > 0)
                        {
                            // Generate output file name: fbxName_clipName.anim
                            string outputFileName = $"{fbxName}_{newClip.name}.anim";
                            string outputPath = $"{outputFolder}/{outputFileName}";

                            // Handle duplicate names
                            int counter = 1;
                            while (AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath) != null)
                            {
                                outputFileName = $"{fbxName}_{newClip.name}_{counter}.anim";
                                outputPath = $"{outputFolder}/{outputFileName}";
                                counter++;
                            }

                            // Save as new asset
                            AssetDatabase.CreateAsset(newClip, outputPath);
                            createdAssets.Add(outputPath);

                            processedClips++;
                            totalRemappedCurves += remappedCurves;

                            Debug.Log($"Created: {outputPath} ({remappedCurves} curves remapped)");
                        }
                        else
                        {
                            // No curves needed remapping, but still save it
                            string outputFileName = $"{fbxName}_{newClip.name}.anim";
                            string outputPath = $"{outputFolder}/{outputFileName}";

                            int counter = 1;
                            while (AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath) != null)
                            {
                                outputFileName = $"{fbxName}_{newClip.name}_{counter}.anim";
                                outputPath = $"{outputFolder}/{outputFileName}";
                                counter++;
                            }

                            AssetDatabase.CreateAsset(newClip, outputPath);
                            createdAssets.Add(outputPath);
                            Debug.Log($"Created: {outputPath} (no path remapping needed)");
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"=== Path Remapping Complete ===");
            Debug.Log($"Processed {processedClips} clips");
            Debug.Log($"Remapped {totalRemappedCurves} curves");
            Debug.Log($"Created {createdAssets.Count} new animation assets");

            EditorUtility.DisplayDialog("Path Remapping Complete",
                $"Successfully processed {processedClips} animation clip(s).\n" +
                $"Remapped {totalRemappedCurves} curve(s).\n" +
                $"Created {createdAssets.Count} new animation assets in:\n{outputFolder}",
                "OK");

            // Select the output folder
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(outputFolder);
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
        }

        private int RemapClipPaths(AnimationClip clip, string oldPath, string newPath)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int remappedCount = 0;

            foreach (var binding in bindings)
            {
                if (binding.path == oldPath)
                {
                    // Get the curve
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

                    // Create new binding with updated path
                    EditorCurveBinding newBinding = new EditorCurveBinding
                    {
                        path = newPath,
                        type = binding.type,
                        propertyName = binding.propertyName
                    };

                    // Set curve with new path
                    AnimationUtility.SetEditorCurve(clip, newBinding, curve);

                    // Remove old curve
                    AnimationUtility.SetEditorCurve(clip, binding, null);

                    remappedCount++;
                }
            }

            return remappedCount;
        }
    }
}
