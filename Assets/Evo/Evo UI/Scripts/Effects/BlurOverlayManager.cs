using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Manages shared camera captures for multiple BlurOverlay instances.
    /// Automatically created when needed - no manual setup required.
    /// </summary>
    public class BlurOverlayManager : MonoBehaviour
    {
        static BlurOverlayManager instance;
        public static BlurOverlayManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new("[Evo UI - Blur Manager]");
                    instance = go.AddComponent<BlurOverlayManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        class CameraCapture
        {
            public Camera camera;
            public RenderTexture texture;
            public HashSet<BlurOverlay> subscribers = new();
            public int updateInterval;
            public int frameCounter;
        }

        readonly Dictionary<Camera, CameraCapture> captures = new();

        void OnDestroy()
        {
            foreach (var capture in captures.Values)
            {
                if (capture.texture != null)
                {
                    capture.texture.Release();
                    Destroy(capture.texture);
                }
            }
            captures.Clear();
        }

        void Update()
        {
            foreach (var capture in captures.Values)
            {
                // Skip if no subscribers or update interval is 0 (manual updates only)
                if (capture.subscribers.Count == 0 || capture.updateInterval == 0)
                    continue;

                capture.frameCounter++;
                if (capture.frameCounter >= capture.updateInterval)
                {
                    capture.frameCounter = 0;
                    CaptureCamera(capture);

                    // Notify all subscribers to re-blur
                    foreach (var overlay in capture.subscribers)
                    {
                        if (overlay != null && overlay.isActiveAndEnabled)
                        {
                            overlay.OnSharedCaptureUpdated();
                        }
                    }
                }
            }
        }

        public void RegisterOverlay(BlurOverlay overlay, Camera camera, int updateInterval)
        {
            if (camera == null) { return; }
            if (!captures.TryGetValue(camera, out CameraCapture capture))
            {
                capture = new CameraCapture
                {
                    camera = camera,
                    updateInterval = updateInterval
                };
                captures[camera] = capture;
            }

            capture.subscribers.Add(overlay);

            // Use the highest update interval (lowest frequency) among all subscribers
            // This ensures we don't update more often than necessary
            capture.updateInterval = Mathf.Max(capture.updateInterval, updateInterval);

            // Initial capture
            EnsureCaptureTexture(capture);
            CaptureCamera(capture);
        }

        public void UnregisterOverlay(BlurOverlay overlay, Camera camera)
        {
            if (camera == null || !captures.TryGetValue(camera, out CameraCapture capture))
                return;

            capture.subscribers.Remove(overlay);

            // Cleanup if no more subscribers
            if (capture.subscribers.Count == 0)
            {
                if (capture.texture != null)
                {
                    capture.texture.Release();
                    Destroy(capture.texture);
                }
                captures.Remove(camera);
            }
        }

        public RenderTexture GetCaptureTexture(Camera camera, int baseDownsample)
        {
            if (!captures.TryGetValue(camera, out CameraCapture capture))
                return null;

            EnsureCaptureTexture(capture, baseDownsample);
            return capture.texture;
        }

        public void ManualCapture(Camera camera)
        {
            if (!captures.TryGetValue(camera, out CameraCapture capture))
                return;

            CaptureCamera(capture);

            // Notify subscribers
            foreach (var overlay in capture.subscribers)
            {
                if (overlay != null && overlay.isActiveAndEnabled)
                {
                    overlay.OnSharedCaptureUpdated();
                }
            }
        }

        void EnsureCaptureTexture(CameraCapture capture, int baseDownsample = 2)
        {
            if (capture.camera == null)
                return;

            int w = Mathf.Max(2, capture.camera.pixelWidth / Mathf.Max(1, baseDownsample));
            int h = Mathf.Max(2, capture.camera.pixelHeight / Mathf.Max(1, baseDownsample));

            if (capture.texture == null || capture.texture.width != w || capture.texture.height != h)
            {
                if (capture.texture != null)
                {
                    capture.texture.Release();
                    Destroy(capture.texture);
                }

                capture.texture = new RenderTexture(w, h, 0, RenderTextureFormat.Default)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false
                };
            }
        }

        void CaptureCamera(CameraCapture capture)
        {
            if (capture.camera == null || capture.texture == null)
                return;

            RenderTexture prevRT = capture.camera.targetTexture;
            capture.camera.targetTexture = capture.texture;
            capture.camera.Render();
            capture.camera.targetTexture = prevRT;
        }
    }
}