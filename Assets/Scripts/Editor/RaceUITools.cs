#if UNITY_EDITOR
using KartGame.Core;
using KartGame.Kart;
using KartGame.PowerUps;
using KartGame.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KartGame.EditorTools
{
    /*
     * Script: RaceUITools.cs
     * Purpose: Creates the complete race HUD hierarchy in the scene.
     * Attach To: Do not attach. This is an editor-only utility script.
     * Required Components: None.
     * Dependencies: RaceUI, ResultsScreen, RaceManager, LapManager, PositionManager.
     * Inspector Setup: Use the Tools/Kart Racing/UI menu in the Unity Editor.
     */
    public static class RaceUITools
    {
        private const string ClockIconPath = "Assets/Prefabs/UI/SM_Icon_Clock_01.prefab";
        private const string PauseIconPath = "Assets/Prefabs/UI/SM_Icon_Pause_01.prefab";

        [MenuItem("Tools/Kart Racing/UI/Create Race HUD")]
        public static void CreateRaceHudMenu()
        {
            var raceManager = Object.FindFirstObjectByType<RaceManager>();
            var lapManager = Object.FindFirstObjectByType<LapManager>();
            var positionManager = Object.FindFirstObjectByType<PositionManager>();
            var playerInput = Object.FindFirstObjectByType<PlayerKartInput>();

            var raceUi = Object.FindFirstObjectByType<RaceUI>();
            if (raceUi == null)
            {
                var hudRoot = new GameObject("RaceUI");
                Undo.RegisterCreatedObjectUndo(hudRoot, "Create Race HUD");
                raceUi = Undo.AddComponent<RaceUI>(hudRoot);
            }

            BuildHierarchy(raceUi);
            ConfigureRaceUi(raceUi, raceManager, lapManager, positionManager, playerInput);
            EnsureResultsScreen(raceUi);

            Selection.activeGameObject = raceUi.gameObject;
            EditorGUIUtility.PingObject(raceUi.gameObject);
            EditorUtility.SetDirty(raceUi);
            EditorUtility.SetDirty(raceUi.gameObject);

            EditorUtility.DisplayDialog(
                "Race UI",
                "RaceUI creado con canvas, paneles, textos e iconos de la HUD.",
                "OK");
        }

        private static void EnsureResultsScreen(RaceUI raceUi)
        {
            if (raceUi == null || Object.FindFirstObjectByType<ResultsScreen>() != null)
            {
                return;
            }

            Undo.AddComponent<ResultsScreen>(raceUi.gameObject);
        }

        private static void BuildHierarchy(RaceUI raceUi)
        {
            if (raceUi == null)
            {
                return;
            }

            var root = raceUi.gameObject.transform;
            var existingCanvas = FindChild(root, "RaceCanvas");
            if (existingCanvas != null)
            {
                Undo.DestroyObjectImmediate(existingCanvas.gameObject);
            }

            var canvasGo = CreateGameObject("RaceCanvas", root);
            var canvas = Undo.AddComponent<Canvas>(canvasGo);
            var scaler = Undo.AddComponent<CanvasScaler>(canvasGo);
            Undo.AddComponent<GraphicRaycaster>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var hudRoot = CreateStretchPanel(canvasGo.transform, "HUDRoot", new Color(0f, 0f, 0f, 0f));
            hudRoot.transform.SetAsFirstSibling();

            CreateTopLeftBlock(hudRoot.transform);
            CreateTopCenterBlock(hudRoot.transform);
            CreateTopRightBlock(hudRoot.transform);
            CreateCenterBlocks(hudRoot.transform);
            CreateBottomBlocks(hudRoot.transform);
            CreatePauseOverlay(canvasGo.transform);
        }

        private static void CreateTopLeftBlock(Transform parent)
        {
            var panel = CreateAnchoredPanel(parent, "TopLeftPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(350f, 132f), new Vector2(0f, 1f));
            CreateText(panel.transform, "CoinsText", "Coins: 0", new Vector2(14f, -18f), new Vector2(322f, 36f), 34, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            CreateText(panel.transform, "PowerUpPointsText", "PowerUps: 0", new Vector2(14f, -66f), new Vector2(322f, 36f), 28, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
        }

        private static void CreateTopCenterBlock(Transform parent)
        {
            var panel = CreateAnchoredPanel(parent, "TopCenterPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(420f, 88f), new Vector2(0.5f, 1f));
            CreateRawImage(panel.transform, "TimerIcon", new Vector2(28f, -42f), new Vector2(52f, 52f));
            CreateText(panel.transform, "TimerText", "0:00.00", new Vector2(92f, -10f), new Vector2(280f, 42f), 44, FontStyle.Normal, TextAnchor.UpperCenter, Color.white);
        }

        private static void CreateTopRightBlock(Transform parent)
        {
            var panel = CreateAnchoredPanel(parent, "TopRightPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(360f, 132f), new Vector2(1f, 1f));
            CreateText(panel.transform, "PositionText", "1/1\nPrimero", new Vector2(-14f, -18f), new Vector2(332f, 42f), 34, FontStyle.Bold, TextAnchor.UpperRight, Color.white);
            CreateText(panel.transform, "LapText", "Lap 1/1\nLeft 0", new Vector2(-14f, -66f), new Vector2(332f, 42f), 28, FontStyle.Bold, TextAnchor.UpperRight, Color.white);
        }

        private static void CreateCenterBlocks(Transform parent)
        {
            var countdownPanel = CreateAnchoredPanel(parent, "CountdownPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(420f, 200f), new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0f));
            CreateText(countdownPanel.transform, "CountdownText", "", new Vector2(0f, 0f), new Vector2(420f, 200f), 120, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, centeredInParent: true, outline: true);

            var finishPanel = CreateAnchoredPanel(parent, "FinishPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(860f, 160f), new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0f));
            CreateText(finishPanel.transform, "FinishText", "", new Vector2(0f, 0f), new Vector2(860f, 160f), 72, FontStyle.Bold, TextAnchor.MiddleCenter, Color.yellow, centeredInParent: true, outline: true);
        }

        private static void CreateBottomBlocks(Transform parent)
        {
            var spectatePanel = CreateAnchoredPanel(parent, "SpectatePanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(720f, 52f), new Vector2(0.5f, 0f), new Color(0f, 0f, 0f, 0f));
            CreateText(spectatePanel.transform, "SpectateText", "", new Vector2(0f, 0f), new Vector2(720f, 52f), 30, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f), centeredInParent: true, outline: true);

            var pauseHintPanel = CreateAnchoredPanel(parent, "PauseHintPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(520f, 36f), new Vector2(0.5f, 0f), new Color(0f, 0f, 0f, 0f));
            CreateText(pauseHintPanel.transform, "PauseHint", "ESC - Pausar / Reanudar", new Vector2(0f, 0f), new Vector2(520f, 36f), 20, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.75f), centeredInParent: true);
        }

        private static void CreatePauseOverlay(Transform parent)
        {
            var overlay = CreateStretchPanel(parent, "PauseOverlay", new Color(0f, 0f, 0f, 0.72f));
            overlay.transform.SetAsLastSibling();
            CreateRawImage(overlay.transform, "PauseIcon", new Vector2(0f, 0f), new Vector2(200f, 200f));
            overlay.SetActive(false);
        }

        private static void ConfigureRaceUi(
            RaceUI raceUi,
            RaceManager raceManager,
            LapManager lapManager,
            PositionManager positionManager,
            PlayerKartInput playerInput)
        {
            if (raceUi == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(raceUi);
            serializedObject.FindProperty("preferExistingUiHierarchy").boolValue = true;
            serializedObject.FindProperty("raceManager").objectReferenceValue = raceManager;
            serializedObject.FindProperty("lapManager").objectReferenceValue = lapManager;
            serializedObject.FindProperty("positionManager").objectReferenceValue = positionManager;
            serializedObject.FindProperty("playerInput").objectReferenceValue = playerInput;
            if (playerInput != null)
            {
                serializedObject.FindProperty("playerTracker").objectReferenceValue = playerInput.GetComponent<CheckpointTracker>() ?? playerInput.GetComponentInParent<CheckpointTracker>();
                serializedObject.FindProperty("playerKartController").objectReferenceValue = playerInput.GetComponent<KartController>() ?? playerInput.GetComponentInParent<KartController>();
                serializedObject.FindProperty("playerPowerUpController").objectReferenceValue = playerInput.GetComponent<KartPowerUpController>() ?? playerInput.GetComponentInParent<KartPowerUpController>();
            }
            serializedObject.FindProperty("clockIconPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ClockIconPath);
            serializedObject.FindProperty("pauseIconPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PauseIconPath);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateStretchPanel(Transform parent, string name, Color color)
        {
            var go = CreateGameObject(name, parent);
            var image = Undo.AddComponent<Image>(go);
            image.color = color;
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject CreateAnchoredPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Vector2? pivot = null, Color? color = null)
        {
            var go = CreateGameObject(name, parent);
            var image = Undo.AddComponent<Image>(go);
            image.color = color ?? new Color(0f, 0f, 0f, 0.36f);
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            return go;
        }

        private static Text CreateText(Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color, bool centeredInParent = false, bool outline = false)
        {
            var go = CreateGameObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            if (centeredInParent)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
            }
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var textComponent = Undo.AddComponent<Text>(go);
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.alignment = alignment;
            textComponent.color = color;
            textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.raycastTarget = false;

            if (outline)
            {
                var outlineComponent = Undo.AddComponent<Outline>(go);
                outlineComponent.effectColor = new Color(0f, 0f, 0f, 0.8f);
                outlineComponent.effectDistance = new Vector2(3f, -3f);
            }

            return textComponent;
        }

        private static RawImage CreateRawImage(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = CreateGameObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var rawImage = Undo.AddComponent<RawImage>(go);
            rawImage.raycastTarget = false;
            return rawImage;
        }

        private static GameObject CreateGameObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
#endif
