using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public interface ISlidingPuzzle
{
    void TrySlideTile(RuneTile tile);
    void NotifyTileRotated(RuneTile tile);
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class RuneTile : MonoBehaviour
{
    [HideInInspector] public ISlidingPuzzle manager;
    [HideInInspector] public Vector2Int correctPos;
    [HideInInspector] public Vector2Int currentPos;

    [SerializeField] float slideTime = 0.12f;
    bool sliding;
    Vector3 startPos, endPos;
    float t;
    Action _onComplete;

    SpriteRenderer _sr;
    BoxCollider2D _col;

    // ---- Tap SFX ----
    [Header("SFX (Tap)")]
    public AudioClip tapClip;
    [Range(0f, 1f)] public float tapVolume = 0.6f;
    public Vector2 tapPitchJitter = new(0.98f, 1.02f);
    AudioSource _audio;
    int _lastTapFrame = -9999;

    // Guard so clicks don’t double-fire from multiple input paths
    int _lastClickFrame = -9999;

    // -----------------------
    // Explicit rotation (desktop + mobile)
    // -----------------------
    [Header("Explicit Rotate Input")]
    [Tooltip("Hold duration (seconds) to count as a long-press rotate.")]
    [SerializeField] float longPressSeconds = 0.35f;
    [Tooltip("Allow right-click / Shift+Left / long-press to rotate even if adjacent to blank.")]
    [SerializeField] bool allowExplicitRotate = true;

    bool _pointerDown;
    float _downAt;
    bool _longPressTriggered;

    // -----------------------
    // Rotation support
    // -----------------------
    [HideInInspector] public int rotationSteps;  // 0..(_maxSteps-1)
    int _maxSteps = 4;
    public int MaxRotationSteps => _maxSteps;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _col = GetComponent<BoxCollider2D>();
        if (_col != null) _col.isTrigger = false;

        if (manager == null)
            manager = GetComponentInParent<ISlidingPuzzle>();

        _audio = GetComponentInParent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 0f; // 2D
    }

    public void SetSlideTime(float seconds) => slideTime = Mathf.Max(0.01f, seconds);
    public void SetLabel(int id) => gameObject.name = $"Tile_{id}";

    public void SetWorldPos(Vector3 pos, bool instant)
    {
        if (instant)
        {
            transform.position = pos;
            sliding = false;
            return;
        }
        startPos = transform.position;
        endPos = pos;
        t = 0f;
        sliding = true;
    }

    public void SlideTo(Vector3 target, Action onComplete = null)
    {
        startPos = transform.position;
        endPos = target;
        t = 0f;
        sliding = true;
        _onComplete = onComplete;
    }

    void Update()
    {
        // ---------- slide animation ----------
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

        // ---------- UNIFIED POINTER (Input System + legacy fallback) ----------
        bool pointerDownThis = false;
        bool pointerUpThis = false;
        bool pointerHeld = false;
        Vector2 pointerPos = Vector2.zero;

        // --- New Input System first ---
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            pointerPos = touch.position.ReadValue();

            if (touch.press.wasPressedThisFrame) pointerDownThis = true;
            if (touch.press.isPressed)          pointerHeld = true;
            if (touch.press.wasReleasedThisFrame) pointerUpThis = true;
        }
        else if (Mouse.current != null)
        {
            pointerPos = Mouse.current.position.ReadValue();
            pointerDownThis = Mouse.current.leftButton.wasPressedThisFrame;
            pointerHeld = Mouse.current.leftButton.isPressed;
            pointerUpThis = Mouse.current.leftButton.wasReleasedThisFrame;
        }
#endif

        // --- Legacy Input fallback (for Editor / iOS "Both" etc.) ---
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pointerDownThis && !pointerHeld && !pointerUpThis)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                pointerPos = touch.position;
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        pointerDownThis = true;
                        pointerHeld = true;
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        pointerHeld = true;
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        pointerUpThis = true;
                        break;
                }
            }
            else
            {
                pointerPos = Input.mousePosition;
                pointerDownThis = Input.GetMouseButtonDown(0);
                pointerHeld = Input.GetMouseButton(0);
                pointerUpThis = Input.GetMouseButtonUp(0);
            }
        }
#endif

        // ---------- modifiers (shift/right click) ----------
        bool rightClick = false;
        bool shift = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
                rightClick = true;
        }
        if (Keyboard.current != null)
        {
            shift = Keyboard.current.leftShiftKey.isPressed ||
                    Keyboard.current.rightShiftKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!rightClick && Input.GetMouseButtonDown(1))
            rightClick = true;
        if (!shift)
        {
            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
#endif

        // ----- explicit rotate via right-click or Shift+Click -----
        if (allowExplicitRotate)
        {
#if ENABLE_INPUT_SYSTEM
            if (rightClick && Mouse.current != null)
            {
                var mPos = Mouse.current.position.ReadValue();
                if (PointerOverSelf(mPos))
                {
                    TryExplicitRotate();
                    _longPressTriggered = true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            // Legacy right-click path (if still enabled)
            if (allowExplicitRotate && !rightClick && Input.GetMouseButtonDown(1))
            {
                if (PointerOverSelf(Input.mousePosition))
                {
                    TryExplicitRotate();
                    _longPressTriggered = true;
                }
            }
#endif

            if (pointerDownThis && shift && PointerOverSelf(pointerPos))
            {
                TryExplicitRotate();
                _longPressTriggered = true;
            }
        }

        // ----- long-press detection (mouse or touch) -----
        if (pointerDownThis)
        {
            _pointerDown = PointerOverSelf(pointerPos);
            _downAt = Time.time;
            _longPressTriggered = false;
        }

        if (_pointerDown && !_longPressTriggered && pointerHeld && allowExplicitRotate)
        {
            if (Time.time - _downAt >= longPressSeconds)
            {
                if (PointerOverSelf(pointerPos))
                {
                    TryExplicitRotate();
                    _longPressTriggered = true;
                }
            }
        }

        if (pointerUpThis)
        {
            if (!_longPressTriggered && PointerOverSelf(pointerPos))
            {
                TryHandleClickAtScreenPos(pointerPos);
            }
            _pointerDown = false;
        }
    }

    // We keep this so nothing else wired to OnMouseDown breaks, but it does nothing
    void OnMouseDown() { /* handled centrally in Update */ }

    // ---- Single guarded click handler ----
    void TryHandleClickAtScreenPos(Vector2 screenPos)
    {
        if (Time.frameCount == _lastClickFrame) return;
        _lastClickFrame = Time.frameCount;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 wp = cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z)
        );
        Vector2 p2 = new Vector2(wp.x, wp.y);
        var hit = Physics2D.OverlapPoint(p2);

        if (hit != null && hit.gameObject == gameObject)
        {
            PlayTap();
            manager?.TrySlideTile(this);
        }
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

    public void InitRotationSystem(int quarterTurns)
    {
        _maxSteps = Mathf.Max(1, quarterTurns);
        rotationSteps = 0;
        ApplyRotationVisual();
    }

    public void RotateOnce()
    {
        Debug.Log($"[RuneTile] RotateOnce CALLED on {name} | _maxSteps={_maxSteps} | beforeSteps={rotationSteps}");

        if (_maxSteps <= 1) return;
        rotationSteps = (rotationSteps + 1) % _maxSteps;
        ApplyRotationVisual();
    }

    public void SetRotationSteps(int steps)
    {
        if (_maxSteps <= 1)
        {
            rotationSteps = 0;
            ApplyRotationVisual();
            return;
        }
        rotationSteps = ((steps % _maxSteps) + _maxSteps) % _maxSteps;
        ApplyRotationVisual();
    }

    public void ApplyRotationVisual()
    {
        float angle = (360f / _maxSteps) * rotationSteps;
        transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    void TryExplicitRotate()
    {
        Debug.Log($"[RuneTile] TryExplicitRotate on {name} | allowExplicitRotate={allowExplicitRotate}");

        if (!allowExplicitRotate) return;
        if (manager == null)
        {
            Debug.Log($"[RuneTile] manager NULL on {name} -> NO rotate");
            return;
        }

        bool enabled = GetRotationEnabledFromManager();
        Debug.Log($"[RuneTile] manager says rotationEnabled={enabled} | MaxRotationSteps={MaxRotationSteps}");

        if (!enabled) return;
        if (MaxRotationSteps <= 1) return;

        Debug.Log($"[RuneTile] EXPLICIT ROTATE FIRED on {name}");
        RotateOnce();
        manager?.NotifyTileRotated(this);
    }

    bool GetRotationEnabledFromManager()
    {
        try
        {
            var m = manager as MonoBehaviour;
            if (!m) return false;
            var fi = m.GetType().GetField("rotationEnabled",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (fi != null && fi.FieldType == typeof(bool))
                return (bool)fi.GetValue(m);
        }
        catch { }
        return false;
    }

    bool PointerOverSelf(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (!cam) return false;

        Vector3 wp = cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z)
        );
        var hit = Physics2D.OverlapPoint(new Vector2(wp.x, wp.y));
        return hit != null && hit.gameObject == gameObject;
    }
}
