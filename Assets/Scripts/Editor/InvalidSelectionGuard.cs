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

            for (var index = 0; index < selectedObjects.Length; index++)
            {
                if (selectedObjects[index] != null)
                {
                    continue;
                }

                Selection.objects = Array.Empty<UnityEngine.Object>();
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                return;
            }
        }
    }
}
#endif
