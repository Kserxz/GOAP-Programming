namespace CleanUp.EditorTools
{
    using UnityEngine;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class UnusedAssetsCleaner : EditorWindow
    {
        private enum Langue { Français, English }
        private Langue langueActuelle;

        private List<string> unusedAssets = new();
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private List<string> scenesToAnalyze = new();

        private static readonly string[] extensionsToIgnore = { ".cs", ".dll", ".meta", ".unitypackage" };
        private static readonly string[] tagsToExclude = { "_DoNotDelete", "Keep" };

        [MenuItem("Tools/Asset Cleaner")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnusedAssetsCleaner>(true);
            try
            {
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UnusedAssetsCleaner/Icons/UnusedAssetsCleanerLogo.png");
                window.titleContent = icon != null ? new GUIContent("Asset Cleaner", icon) : new GUIContent("Asset Cleaner");
            }
            catch
            {
                window.titleContent = new GUIContent("Asset Cleaner");
            }
            window.Show();
        }

        private void OnEnable()
        {
            CleanerUISettings.Load();

            langueActuelle = (Langue)EditorPrefs.GetInt("Cleaner_Langue", (int)(Application.systemLanguage == SystemLanguage.French ? Langue.Français : Langue.English));
            searchFilter = EditorPrefs.GetString("Cleaner_Filter", "");

            scenesToAnalyze = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/Scenes"))
                .ToList();
        }

        private void OnDisable()
        {
            CleanerUISettings.Save();

            EditorPrefs.SetInt("Cleaner_Langue", (int)langueActuelle);
            EditorPrefs.SetString("Cleaner_Filter", searchFilter);
            unusedAssets?.Clear();
        }

        private void OnDestroy()
        {
            unusedAssets = null;
            searchFilter = null;
            scrollPosition = Vector2.zero;
        }

        private void OnGUI()
        {
            CleanerUISettings.DrawBackground(new Rect(0, 0, position.width, position.height));

            langueActuelle = (Langue)EditorGUILayout.EnumPopup(GetLabel("Langue / Language"), langueActuelle);
            GUILayout.Label(GetLabel("Nettoyage d'assets inutilisés", "Unused Assets Cleaner"), CleanerUISettings.GetLabelStyle());

            Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UnusedAssetsCleaner/Icons/UnusedAssetsCleanerLogo.png");
            if (logo != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(logo, GUILayout.Width(64), GUILayout.Height(64));
                GUILayout.BeginVertical();
                GUILayout.Label(GetLabel("Optimisez votre projet Unity", "Optimize your Unity project"), CleanerUISettings.GetLabelStyle());
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button(GetLabel("Analyser les scènes", "Analyze scenes")))
                RechercherAssetsInutilisés();

            if (unusedAssets.Count > 0)
            {
                if (GUILayout.Button(GetLabel("Créer un rapport", "Create Report")))
                    CreerRapportTexte();

                searchFilter = EditorGUILayout.TextField(GetLabel("Filtrer par nom :", "Filter by name:"), searchFilter);
                GUILayout.Label($"{GetLabel("Fichiers inutilisés :", "Unused assets:")} {unusedAssets.Count}", CleanerUISettings.GetLabelStyle());

                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                List<string> assetsToRemove = new();

                foreach (var path in unusedAssets.ToList())
                {
                    if (!string.IsNullOrEmpty(searchFilter) && !path.ToLower().Contains(searchFilter.ToLower()))
                        continue;

                    GUILayout.BeginHorizontal();

                    GUIStyle yellowText = new(CleanerUISettings.GetLabelStyle());
                    yellowText.normal.textColor = Color.yellow;
                    GUILayout.Label(path, yellowText, GUILayout.MaxWidth(position.width - 160));

                    if (GUILayout.Button(GetLabel("Mettre à la corbeille", "Send to Trash"), GUILayout.Width(140)))
                    {
                        if (EditorUtility.DisplayDialog(
                            GetLabel("Confirmation"),
                            GetLabel($"Déplacer '{path}' dans la corbeille et créer un package de sauvegarde ?", $"Move '{path}' to trash and create backup package?"),
                            GetLabel("Oui", "Yes"), GetLabel("Non", "No")))
                        {
                            ArchiverEtSupprimerComplet(path);
                            assetsToRemove.Add(path);
                        }
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();

                foreach (var p in assetsToRemove)
                    unusedAssets.Remove(p);

                Repaint();
            }
            else
            {
                GUILayout.Label(GetLabel("Aucun asset inutilisé trouvé ou pas encore analysé.", "No unused asset found or not yet analyzed."), CleanerUISettings.GetLabelStyle());
            }

            GUILayout.Space(15);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(GetLabel("Apparence de la fenêtre", "Window Appearance"), EditorStyles.boldLabel);
            CleanerUISettings.DrawCustomizationGUI();
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();

            Texture2D mailIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UnusedAssetsCleaner/Icons/Gatineau.png");
            GUILayout.Label(mailIcon != null ? (GUIContent)new GUIContent(mailIcon) : new GUIContent("📧"), GUILayout.Width(64));

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void RechercherAssetsInutilisés()
        {
            unusedAssets.Clear();
            HashSet<string> usedPaths = new();

            foreach (var scenePath in scenesToAnalyze)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"{GetLabel("Scène non trouvée", "Scene not found")} : {scenePath}");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                foreach (var obj in scene.GetRootGameObjects())
                {
                    foreach (var dep in EditorUtility.CollectDependencies(new Object[] { obj }))
                    {
                        string path = AssetDatabase.GetAssetPath(dep);
                        if (!string.IsNullOrEmpty(path))
                            usedPaths.Add(path);
                    }
                }
                EditorSceneManager.CloseScene(scene, true);
            }

            var allAssetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && !Directory.Exists(p))
                .Where(p => !extensionsToIgnore.Any(ext => p.EndsWith(ext)))
                .Where(p => !tagsToExclude.Any(tag => p.Contains(tag)))
                .Where(p => !p.StartsWith("Assets/_Trash/"))
                .ToList();

            unusedAssets = allAssetPaths.Where(p => !usedPaths.Contains(p)).ToList();
        }

        private void ArchiverEtSupprimerComplet(string assetPath)
        {
            string backupFolder = "Assets/_Trash";
            string exportPath = $"{backupFolder}/{Path.GetFileNameWithoutExtension(assetPath)}_backup.unitypackage";

            if (!AssetDatabase.IsValidFolder(backupFolder))
                AssetDatabase.CreateFolder("Assets", "_Trash");

            AssetDatabase.ExportPackage(assetPath, exportPath, ExportPackageOptions.IncludeDependencies);

            if (!AssetDatabase.DeleteAsset(assetPath))
                Debug.LogError($"{GetLabel("Échec de la suppression de l'asset", "Failed to delete asset")} : {assetPath}");
            else
            {
                unusedAssets.Remove(assetPath);
                Repaint();
            }

            AssetDatabase.Refresh();
            Debug.Log($"{GetLabel("Sauvegarde créée :", "Backup created:")} {exportPath}");
        }

        private void CreerRapportTexte()
        {
            string rapportPath = "Assets/_Trash/UnusedAssetsReport.txt";
            Directory.CreateDirectory("Assets/_Trash");

            try
            {
                File.WriteAllLines(rapportPath, unusedAssets);
                AssetDatabase.Refresh();
                Debug.Log(GetLabel("Rapport généré :", "Report created:") + " " + rapportPath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{GetLabel("Erreur lors de la génération du rapport", "Error creating report")} : {e.Message}");
            }
        }

        private string GetLabel(string fr, string en = null)
        {
            return langueActuelle == Langue.Français ? fr : (en ?? fr);
        }
    }
}
