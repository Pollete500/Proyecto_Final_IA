#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace KartGame.EditorTools
{
    [InitializeOnLoad]
    public static class InvalidSelectionGuard
    {
        static InvalidSelectionGuard()
        {
            EditorApplication.update += ClearInvalidSelection;
            EditorApplication.playModeStateChanged += _ => ClearInvalidSelection();
        }

        private static void ClearInvalidSelection()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return;
            }

            var hasInvalidActiveObject = Selection.activeObject == null;
            var hasInvalidActiveGameObject = Selection.activeGameObject == null && Selection.activeTransform != null;

            for (var index = 0; index < selectedObjects.Length; index++)
            {
                if (selectedObjects[index] != null)
                {
                    continue;
                }

                ClearSelectionAndRebuild();
                return;
            }

            if (hasInvalidActiveObject || hasInvalidActiveGameObject)
            {
                ClearSelectionAndRebuild();
                return;
            }
        }

        private static void ClearSelectionAndRebuild()
        {
            Selection.activeObject = null;
            Selection.objects = Array.Empty<UnityEngine.Object>();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            SceneView.RepaintAll();
        }
    }
}
#endif
