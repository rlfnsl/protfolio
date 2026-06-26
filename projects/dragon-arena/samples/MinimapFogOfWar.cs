// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\Dragon-Arena\Assets\Scripts\InGame\Minimap\MinimapFogOfWar.cs
// Lines: full file

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapFogOfWar : MonoBehaviour
{
    public MinimapManager Manager;
    public RawImage FogImage;

    public int TextureSize = 512;
    public float UpdateInterval = 0.1f;

    public float RevealRadiusWorld = 12f;
    public float SoftEdgeWorld = 4f;

    public float FogAlpha = 0.85f;
    public float ReFogSeconds = 0.5f;

    readonly Dictionary<Transform, float> revealers = new();
    Texture2D fogTex;
    Color32[] pixels;
    float[] fog01;
    float nextTime;

    int TEX_SIZE;

    void Awake()
    {
        TEX_SIZE = Mathf.Max(32, TextureSize);

        fogTex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.ARGB32, false, true);
        fogTex.wrapMode = TextureWrapMode.Clamp;
        fogTex.filterMode = FilterMode.Bilinear;

        pixels = new Color32[TEX_SIZE * TEX_SIZE];
        fog01 = new float[TEX_SIZE * TEX_SIZE];

        for (int i = 0; i < fog01.Length; i++)
            fog01[i] = 1f;

        ApplyToImage();
        PushPixels();
    }

    void OnEnable()
    {
        ApplyToImage();
    }

    void ApplyToImage()
    {
        if (FogImage == null)
            return;

        FogImage.texture = fogTex;
        FogImage.color = Color.white;
    }

    public void ClearRevealers()
    {
        revealers.Clear();
    }

    public void RegisterRevealer(Transform tr, float radiusWorld)
    {
        if (tr == null)
            return;

        radiusWorld = Mathf.Max(0.1f, radiusWorld);
        revealers[tr] = radiusWorld;
    }

    public void UnregisterRevealer(Transform tr)
    {
        if (tr == null)
            return;

        if (revealers.ContainsKey(tr))
            revealers.Remove(tr);
    }

    void LateUpdate()
    {
        if (Manager == null || FogImage == null)
            return;

        if (Time.unscaledTime < nextTime)
            return;

        nextTime = Time.unscaledTime + Mathf.Max(0.01f, UpdateInterval);

        StepReFog(Time.unscaledDeltaTime);
        StepReveal();

        PushPixels();
    }

    void StepReFog(float dt)
    {
        float _sec = Mathf.Max(0.01f, ReFogSeconds);
        float _t = Mathf.Clamp01(dt / _sec);

        for (int i = 0; i < fog01.Length; i++)
        {
            float _v = fog01[i];
            _v = Mathf.Lerp(_v, 1f, _t);
            fog01[i] = _v;
        }
    }

    void StepReveal()
    {
        CleanupRevealers();

        Camera _cam = Manager.MinimapCamera;
        Transform _camTr = _cam.transform;

        float _halfH = _cam.orthographicSize;
        float _aspect = GetAspect(_cam);
        float _halfW = _halfH * _aspect;

        Vector3 _camPos = _camTr.position;
        Vector3 _camRight = new Vector3(_camTr.right.x, 0f, _camTr.right.z).normalized;
        Vector3 _camUp = new Vector3(_camTr.up.x, 0f, _camTr.up.z).normalized;

        if (_camRight.sqrMagnitude < 1e-6f) _camRight = Vector3.right;
        if (_camUp.sqrMagnitude < 1e-6f) _camUp = Vector3.forward;

        foreach (var kv in revealers)
        {
            Transform _tr = kv.Key;
            float _radiusWorld = kv.Value;

            Vector3 _pos = _tr.position;
            if (!WorldToUv01(_pos, _camPos, _camRight, _camUp, _halfW, _halfH, out Vector2 _uv))
                continue;

            int _cx = Mathf.RoundToInt(_uv.x * (TEX_SIZE - 1));
            int _cy = Mathf.RoundToInt(_uv.y * (TEX_SIZE - 1));

            float _unitsPerPixelX = (_halfW * 2f) / TEX_SIZE;
            float _unitsPerPixelY = (_halfH * 2f) / TEX_SIZE;
            float _unitsPerPixel = Mathf.Max(0.0001f, (_unitsPerPixelX + _unitsPerPixelY) * 0.5f);

            float _rPix = _radiusWorld / _unitsPerPixel;
            float _softPix = SoftEdgeWorld / _unitsPerPixel;

            RevealCircle(_cx, _cy, _rPix, _softPix);
        }
    }

    void RevealCircle(int cx, int cy, float rPix, float softPix)
    {
        int _minX = Mathf.Max(0, Mathf.FloorToInt(cx - rPix - softPix - 1f));
        int _maxX = Mathf.Min(TEX_SIZE - 1, Mathf.CeilToInt(cx + rPix + softPix + 1f));
        int _minY = Mathf.Max(0, Mathf.FloorToInt(cy - rPix - softPix - 1f));
        int _maxY = Mathf.Min(TEX_SIZE - 1, Mathf.CeilToInt(cy + rPix + softPix + 1f));

        float _r0 = Mathf.Max(0.01f, rPix);
        float _r1 = Mathf.Max(_r0, rPix + Mathf.Max(0.01f, softPix));

        float _r0Sq = _r0 * _r0;
        float _r1Sq = _r1 * _r1;

        for (int y = _minY; y <= _maxY; y++)
        {
            int _row = y * TEX_SIZE;
            float _dy = y - cy;

            for (int x = _minX; x <= _maxX; x++)
            {
                float _dx = x - cx;
                float _dSq = _dx * _dx + _dy * _dy;

                if (_dSq >= _r1Sq)
                    continue;

                int _idx = _row + x;

                float _targetFog;
                if (_dSq <= _r0Sq)
                {
                    _targetFog = 0f;
                }
                else
                {
                    float _d = Mathf.Sqrt(_dSq);
                    float _t = Mathf.InverseLerp(_r0, _r1, _d);
                    _targetFog = Mathf.Clamp01(_t);
                }

                if (_targetFog < fog01[_idx])
                    fog01[_idx] = _targetFog;
            }
        }
    }

    void PushPixels()
    {
        byte _baseA = (byte)Mathf.Clamp(Mathf.RoundToInt(FogAlpha * 255f), 0, 255);

        for (int i = 0; i < fog01.Length; i++)
        {
            byte _a = (byte)Mathf.Clamp(Mathf.RoundToInt(fog01[i] * _baseA), 0, 255);
            pixels[i] = new Color32(0, 0, 0, _a);
        }

        fogTex.SetPixels32(pixels);
        fogTex.Apply(false, false);
    }

    float GetAspect(Camera cam)
    {
        if (cam == null)
            return 1f;

        if (cam.targetTexture != null)
        {
            float _w = Mathf.Max(1f, cam.targetTexture.width);
            float _h = Mathf.Max(1f, cam.targetTexture.height);
            return _w / _h;
        }

        return Mathf.Max(0.1f, cam.aspect);
    }

    bool WorldToUv01(
        Vector3 worldPos,
        Vector3 camPos,
        Vector3 camRight,
        Vector3 camUpAsForward,
        float halfW,
        float halfH,
        out Vector2 uv01)
    {
        Vector3 _d = worldPos - camPos;
        float _x = Vector3.Dot(_d, camRight);
        float _z = Vector3.Dot(_d, camUpAsForward);

        float _u = (_x / Mathf.Max(0.0001f, halfW) + 1f) * 0.5f;
        float _v = (_z / Mathf.Max(0.0001f, halfH) + 1f) * 0.5f;

        uv01 = new Vector2(_u, _v);

        if (_u < 0f || _u > 1f || _v < 0f || _v > 1f)
            return false;

        return true;
    }

    public bool IsRevealed(Vector3 worldPos, float threshold = 0.6f)
    {
        if (Manager == null || Manager.MinimapCamera == null)
            return true;

        Camera _cam = Manager.MinimapCamera;
        Transform _camTr = _cam.transform;

        float _halfH = _cam.orthographicSize;
        float _aspect = GetAspect(_cam);
        float _halfW = _halfH * _aspect;

        Vector3 _camPos = _camTr.position;
        Vector3 _camRight = new Vector3(_camTr.right.x, 0f, _camTr.right.z).normalized;
        Vector3 _camUp = new Vector3(_camTr.up.x, 0f, _camTr.up.z).normalized;

        if (_camRight.sqrMagnitude < 1e-6f) _camRight = Vector3.right;
        if (_camUp.sqrMagnitude < 1e-6f) _camUp = Vector3.forward;

        if (!WorldToUv01(worldPos, _camPos, _camRight, _camUp, _halfW, _halfH, out Vector2 _uv))
            return false;

        int _x = Mathf.Clamp(Mathf.RoundToInt(_uv.x * (TEX_SIZE - 1)), 0, TEX_SIZE - 1);
        int _y = Mathf.Clamp(Mathf.RoundToInt(_uv.y * (TEX_SIZE - 1)), 0, TEX_SIZE - 1);
        int _idx = _y * TEX_SIZE + _x;

        return fog01[_idx] <= threshold;
    }

    void CleanupRevealers()
    {
        List<Transform> _remove = null;

        foreach (var kv in revealers)
        {
            if (kv.Key != null)
                continue;

            _remove ??= new List<Transform>(8);
            _remove.Add(kv.Key);
        }

        if (_remove == null)
            return;

        for (int i = 0; i < _remove.Count; i++)
            revealers.Remove(_remove[i]);
    }
}
