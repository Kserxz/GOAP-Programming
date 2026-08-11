using UnityEngine;
using UnityEditor;

namespace CleanUp.EditorTools
{
    public static class CleanerUISettings
    {
        private const string PREF_TEXT_SIZE = "Cleaner_TextSize";
        private const string PREF_TEXT_COLOR = "Cleaner_TextColor";
        private const string PREF_BG_COLOR = "Cleaner_BackgroundColor";

        public static float TextSize { get; private set; } = 14f;
        public static Color TextColor { get; private set; } = Color.white;
        public static Color BackgroundColor { get; private set; } = new(0.2f, 0.2f, 0.2f);

        public static void Load()
        {
            TextSize = EditorPrefs.GetFloat(PREF_TEXT_SIZE, 14f);

            Color temp;
            if (ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(PREF_TEXT_COLOR, "FFFFFF"), out temp))
                TextColor = temp;

            if (ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(PREF_BG_COLOR, "333333"), out temp))
                BackgroundColor = temp;
        }

        public static void Save()
        {
            EditorPrefs.SetFloat(PREF_TEXT_SIZE, TextSize);
            EditorPrefs.SetString(PREF_TEXT_COLOR, ColorUtility.ToHtmlStringRGBA(TextColor));
            EditorPrefs.SetString(PREF_BG_COLOR, ColorUtility.ToHtmlStringRGBA(BackgroundColor));
        }

        public static void DrawCustomizationGUI()
        {
            GUILayout.Label("🎨 Personnalisation visuelle", EditorStyles.boldLabel);

            TextSize = EditorGUILayout.Slider("Taille du texte", TextSize, 10f, 30f);
            TextColor = EditorGUILayout.ColorField("Couleur du texte", TextColor);
            BackgroundColor = EditorGUILayout.ColorField("Couleur de la fenêtre", BackgroundColor);
        }

        public static GUIStyle GetLabelStyle()
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = (int)TextSize,
                normal = { textColor = TextColor }
            };
        }

        public static void DrawBackground(Rect position)
        {
            EditorGUI.DrawRect(position, BackgroundColor);
        }
    }
}
