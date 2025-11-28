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
    [HideInInspector] public ISlidingPuzzle manager;   // set by loader or auto-found
    [HideInInspector] public Vector2Int correctPos;
    [HideInInspector] public Vector2Int currentPos;

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

    // ---- Click guard (prevents double firing from multiple input paths) ----
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

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _col = GetComponent<BoxCollider2D>();
        if (_col != null) _col.isTrigger = false;

        // Try to auto-wire a manager from any parent that implements ISlidingPuzzle
        if (manager == null) manager = GetComponentInParent<ISlidingPuzzle>();

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

        // ---------- INPUT (unified) ----------
// We handle:
//  - Right-click = rotate
//  - Shift + Left-click = rotate
//  - Long-press = rotate
//  - Simple tap = slide (existing behavior)

#if ENABLE_INPUT_SYSTEM
        // We support BOTH mouse (Editor / desktop) and touch (phone)
        var mouse       = Mouse.current;
        var kb          = Keyboard.current;
        var touchscreen = Touchscreen.current;

        bool hasMouse   = mouse != null;
        bool hasTouch   = touchscreen != null;

        bool leftDownThis  = false;
        bool leftUpThis    = false;
        bool leftHeld      = false;
        bool rightDownThis = false;
        bool shift         = false;
        Vector2 pointerPos = Vector2.zero;

        if (hasMouse)
        {
            // Normal mouse controls (Editor, Mac/PC build)
            pointerPos    = mouse.position.ReadValue();
            leftDownThis  = mouse.leftButton.wasPressedThisFrame;
            leftUpThis    = mouse.leftButton.wasReleasedThisFrame;
            leftHeld      = mouse.leftButton.isPressed;
            rightDownThis = mouse.rightButton.wasPressedThisFrame;
            shift = (kb != null) &&
                    ((kb.leftShiftKey?.isPressed ?? false) ||
                     (kb.rightShiftKey?.isPressed ?? false));
        }
        else if (hasTouch)
        {
            // Treat primary touch as a "left mouse button"
            var touch      = touchscreen.primaryTouch;
            pointerPos     = touch.position.ReadValue();
            leftDownThis   = touch.press.wasPressedThisFrame;
            leftUpThis     = touch.press.wasReleasedThisFrame;
            leftHeld       = touch.press.isPressed;
            rightDownThis  = false;   // no right-click on touch
            shift          = false;   // no shift on touch
        }

        // --- explicit rotate on right-click or Shift+Left (only if over this tile) ---
        if (allowExplicitRotate && hasMouse) // only makes sense with real mouse
        {
            if (rightDownThis && PointerOverSelf(pointerPos))
            {
                TryExplicitRotate();
                _longPressTriggered = true;
            }
            else if (leftDownThis && shift && PointerOverSelf(pointerPos))
            {
                TryExplicitRotate();
                _longPressTriggered = true;
            }
        }

        // --- long-press detection (only if the press started on this tile) ---
        if (leftDownThis && (hasMouse || hasTouch))
        {
            _pointerDown        = PointerOverSelf(pointerPos);
            _downAt             = Time.time;
            _longPressTriggered = false;
        }

        if (_pointerDown && !_longPressTriggered && leftHeld && allowExplicitRotate && (hasMouse || hasTouch))
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

        if (leftUpThis && (hasMouse || hasTouch))
        {
            if (!_longPressTriggered && PointerOverSelf(pointerPos))
            {
                // treat as slide-tap
                TryHandleClickAtScreenPos(pointerPos);
            }
            _pointerDown = false;
        }

#else
        // -------- Legacy Input System --------
        bool leftDownThis  = Input.GetMouseButtonDown(0);
        bool leftUpThis    = Input.GetMouseButtonUp(0);
        bool leftHeld      = Input.GetMouseButton(0);
        bool rightDownThis = Input.GetMouseButtonDown(1);
        bool shift         = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Vector2 mpos       = Input.mousePosition;

        // explicit rotate: right click or Shift+Left — only if over this tile
        if (allowExplicitRotate)
        {
            if (rightDownThis && PointerOverSelf(mpos))
            {
                TryExplicitRotate();
                _longPressTriggered = true;
            }
            else if (leftDownThis && shift && PointerOverSelf(mpos))
            {
                TryExplicitRotate();
                _longPressTriggered = true;
            }
        }

        // long-press, only if press began on this tile
        if (leftDownThis)
        {
            _pointerDown        = PointerOverSelf(mpos);
            _downAt             = Time.time;
            _longPressTriggered = false;
        }

        if (_pointerDown && !_longPressTriggered && leftHeld && allowExplicitRotate)
        {
            if (Time.time - _downAt >= longPressSeconds)
            {
                if (PointerOverSelf(mpos))
                {
                    TryExplicitRotate();
                    _longPressTriggered = true;
                }
            }
        }

        if (leftUpThis)
        {
            if (!_longPressTriggered && PointerOverSelf(mpos))
            {
                TryHandleClickAtScreenPos(mpos);
            }
            _pointerDown = false;
        }
#endif

    }

    // We keep this method but make it a no-op to avoid double-firing; Update handles clicks.
    void OnMouseDown() { /* handled centrally in Update */ }

    // ---- Single guarded click handler (the only place that triggers a move) ----
    void TryHandleClickAtScreenPos(Vector2 screenPos)
    {
        // one-frame debounce so multiple input paths can't double-trigger
        if (Time.frameCount == _lastClickFrame) return;
        _lastClickFrame = Time.frameCount;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        Vector2 p2 = new Vector2(wp.x, wp.y);
        var hit = Physics2D.OverlapPoint(p2);

        if (hit != null && hit.gameObject == gameObject)
        {
            PlayTap();                 // SFX has its own frame guard
            manager?.TrySlideTile(this);
        }
    }

    // ---- SFX helper ----
    void PlayTap()
    {
        if (!tapClip) return;
        if (Time.frameCount == _lastTapFrame) return;  // prevent double SFX in same frame
        _lastTapFrame = Time.frameCount;

        _audio.pitch = UnityEngine.Random.Range(tapPitchJitter.x, tapPitchJitter.y);
        _audio.PlayOneShot(tapClip, tapVolume);
        _audio.pitch = 1f;
    }

    // -----------------------
    // Rotation support
    // -----------------------
    [HideInInspector] public int rotationSteps;  // 0..(_maxSteps-1)
    int _maxSteps = 4;

    public int MaxRotationSteps => _maxSteps;

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

    // set an exact rotation step from outside (used by randomizer)
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

    // ---- core explicit-rotate path (bypasses adjacency) ----
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
    }


    // Lightweight check so we don’t need to reference concrete manager type
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
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        var hit = Physics2D.OverlapPoint(new Vector2(wp.x, wp.y));
        return hit != null && hit.gameObject == gameObject;
    }

}
