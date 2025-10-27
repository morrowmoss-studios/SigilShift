using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // works if the New Input System is installed
#endif

// Tiny contract so this compiles even if you haven't written the manager yet.
public interface ISlidingPuzzle
{
    void TrySlideTile(RuneTile tile);
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class RuneTile : MonoBehaviour
{
    [HideInInspector] public ISlidingPuzzle manager;   // auto-found in Awake (parent), or set by your loader
    [HideInInspector] public Vector2Int correctPos;    // where this tile belongs
    [HideInInspector] public Vector2Int currentPos;    // where this tile is now

    [SerializeField] float slideTime = 0.12f;          // seconds per slide
    bool sliding;
    Vector3 startPos, endPos;
    float t;
    Action _onComplete;

    SpriteRenderer _sr;
    BoxCollider2D _col;

    // ---- Tap SFX ----
    [Header("SFX (Tap)")]
    public AudioClip tapClip;                           // assign in Inspector
    [Range(0f,1f)] public float tapVolume = 0.6f;
    public Vector2 tapPitchJitter = new(0.98f, 1.02f);
    AudioSource _audio;
    int _lastTapFrame = -9999;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _col = GetComponent<BoxCollider2D>();
        if (_col != null) _col.isTrigger = false;

        // Try to auto-wire a manager from any parent that implements ISlidingPuzzle
        if (manager == null)
        {
            manager = GetComponentInParent<ISlidingPuzzle>();
        }

        // use parent's AudioSource if present, else add local
        _audio = GetComponentInParent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 0f; // 2D
    }

    public void SetSlideTime(float seconds) => slideTime = Mathf.Max(0.01f, seconds);

    public void SetLabel(int id) => gameObject.name = $"Tile_{id}";

    /// <summary>Place instantly or start a smooth slide to pos.</summary>
    public void SetWorldPos(Vector3 pos, bool instant)
    {
        if (instant)
        {
            transform.position = pos;
            sliding = false;
            return;
        }
        startPos = transform.position;
        endPos   = pos;
        t = 0f;
        sliding = true;
    }

    /// <summary>Animate to a target position and invoke onComplete at the end.</summary>
    public void SlideTo(Vector3 target, Action onComplete = null)
    {
        startPos = transform.position;
        endPos   = target;
        t = 0f;
        sliding = true;
        _onComplete = onComplete;
    }

    void Update()
    {
        // slide animation
        if (sliding)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, slideTime);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = Vector3.Lerp(startPos, endPos, k);
            if (t >= 1f)
            {
                sliding = false;
                transform.position = endPos;
                _onComplete?.Invoke();
            }
        }

        // click-to-move (both input systems)
        bool clicked = false;
        Vector2 screenPos = default;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            clicked = true;
            screenPos = Mouse.current.position.ReadValue();
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            clicked = true;
            screenPos = Input.mousePosition;
        }
#endif

        if (clicked)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                Vector2 p2 = new Vector2(wp.x, wp.y);
                var hit = Physics2D.OverlapPoint(p2);

                if (hit != null && hit.gameObject == gameObject)
                {
                    PlayTap();                        // <- play immediately on touch/click
                    manager?.TrySlideTile(this);
                }
            }
        }
    }

    // Legacy fallback so clicks still work even if input defines are weird
    void OnMouseDown()
    {
        PlayTap();                                    // frame-guard prevents double fire
        manager?.TrySlideTile(this);
    }

    // ---- SFX helper ----
    void PlayTap()
    {
        if (!tapClip) return;
        if (Time.frameCount == _lastTapFrame) return;
        _lastTapFrame = Time.frameCount;

        _audio.pitch = UnityEngine.Random.Range(tapPitchJitter.x, tapPitchJitter.y);
        _audio.PlayOneShot(tapClip, tapVolume);
        _audio.pitch = 1f;
    }

    // -----------------------
    // Rotation support
    // -----------------------
    [HideInInspector] public int rotationSteps;  // 0..(quarterTurns-1)
    int _maxSteps = 4;

    public void InitRotationSystem(int quarterTurns)
    {
        _maxSteps = Mathf.Max(1, quarterTurns);
        rotationSteps = 0;
        ApplyRotationVisual();
    }

    public void RotateOnce()
    {
        if (_maxSteps <= 1) return;
        rotationSteps = (rotationSteps + 1) % _maxSteps;
        ApplyRotationVisual();
    }

    public int GetMaxSteps() => _maxSteps;

    void ApplyRotationVisual()
    {
        float angle = (360f / Mathf.Max(1, _maxSteps)) * rotationSteps;
        transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }
}
