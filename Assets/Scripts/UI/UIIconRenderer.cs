using System.Collections.Generic;
using UnityEngine;

namespace KartGame.UI
{
    public static class UIIconRenderer
    {
        // Renders one 3D prefab to a RenderTexture.
        public static RenderTexture Render(GameObject prefab, int size = 64,
            float rotX = 20f, float rotY = -25f)
        {
            if (prefab == null) return null;

            var stagePos = new Vector3(0f, -9998f, 0f);

            var iconGo = Object.Instantiate(prefab);
            iconGo.hideFlags = HideFlags.HideAndDontSave;
            iconGo.transform.position = stagePos;
            iconGo.transform.rotation = Quaternion.Euler(rotX, rotY, 0f);

            var renderers = iconGo.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(stagePos, Vector3.one * 0.1f);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            var camGo = new GameObject("_IconCam");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(0.05f,
                Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.5f);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 40f;
            cam.transform.position = bounds.center + Vector3.back * 15f;
            cam.transform.LookAt(bounds.center);

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(iconGo);
            return rt;
        }

#if UNITY_EDITOR
        // Renders a word using the Polygon Icons letter prefabs, returned as a wide RenderTexture.
        public static RenderTexture RenderWord(string word, int charHeight = 128)
        {
            const string prefabBase = "Assets/Synty/PolygonIcons/Prefabs/SM_Icon_Text_";
            const float letterStep  = 1.1f;
            const float spaceWidth  = 0.55f;

            var stagePos = new Vector3(0f, -9990f, 0f);
            var instances = new List<GameObject>();
            float curX = 0f;

            foreach (char c in word.ToUpperInvariant())
            {
                if (c == ' ') { curX += spaceWidth; continue; }
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{prefabBase}{c}.prefab");
                if (prefab == null) { curX += letterStep; continue; }

                var go = Object.Instantiate(prefab);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.position = stagePos + new Vector3(curX, 0f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                instances.Add(go);
                curX += letterStep;
            }

            if (instances.Count == 0) return null;

            var bounds = new Bounds(instances[0].transform.position, Vector3.one * 0.01f);
            foreach (var go in instances)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);

            float extent = bounds.extents.y * 1.4f;
            int rtWidth = Mathf.Max(64,
                Mathf.RoundToInt(charHeight * bounds.size.x / (extent * 2f)));

            var camGo = new GameObject("_WordCam");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.orthographic = true;
            cam.orthographicSize = extent;
            cam.aspect = (float)rtWidth / charHeight;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 40f;
            cam.transform.position = bounds.center + Vector3.back * 15f;
            cam.transform.LookAt(bounds.center);

            var rt = new RenderTexture(rtWidth, charHeight, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            Object.DestroyImmediate(camGo);
            foreach (var go in instances) Object.DestroyImmediate(go);
            return rt;
        }
#endif
    }
}
