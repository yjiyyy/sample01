using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 로비 배경(바닥·뒤 벽·안개·빛)을 세트 단위로 바꿉니다.
/// 뒤 벽에 사진을 넣으면 필터를 씌운 뒤 천천히 흐르고, 다음 장으로 겹쳐 바뀝니다.
/// 플레이 중 키보드 1 / 2 / 3으로 조명 세트를 테스트할 수 있습니다.
/// </summary>
public class LobbyEnvironment : MonoBehaviour
{
    public const float BackdropWidth = 16f;
    public const float BackdropHeight = 6f;
    public const float BackdropZ = 7f;

    [System.Serializable]
    public class LightingPreset
    {
        public string displayName = "Preset";
        public Color cameraBackground = new Color(0.62f, 0.64f, 0.68f);
        public Color ambient = new Color(0.42f, 0.43f, 0.45f);
        public Color fog = new Color(0.62f, 0.64f, 0.68f);
        public float fogStart = 11f;
        public float fogEnd = 28f;
        public Color lightColor = new Color(1f, 0.98f, 0.94f);
        public float lightIntensity = 1.1f;
        [Tooltip("세트 전환 때 각도는 바꾸지 않습니다. 각도는 위의 주광 각도를 씁니다.")]
        public Vector3 lightEuler = new Vector3(48f, -20f, 0f);
        [Tooltip("이 세트의 바닥 색. 조명 색과 섞지 않고 이 색을 그대로 씁니다.")]
        public Color floorColor = new Color(0.22f, 0.23f, 0.25f);
        public Color backdropColor = new Color(0.52f, 0.54f, 0.58f);
    }

    [Header("현재 세트")]
    [Tooltip("0 회색, 1 빨강, 2 어두운. 플레이 중 1/2/3 키와 같습니다.")]
    [SerializeField] private int currentPreset;

    [SerializeField] private LightingPreset[] presets = CreateDefaultPresets();

    [Header("분위기 전환")]
    [Tooltip("다른 세트로 넘어가는 시간(초). 색과 밝기만 바뀌고 그림자 방향은 그대로입니다.")]
    [SerializeField] private float moodTransitionDuration = 2f;

    [Tooltip("주광 각도. 세트와 상관없이 고정입니다.")]
    [SerializeField] private Vector3 studioLightEuler = new Vector3(48f, -20f, 0f);

    [Header("연결")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Light keyLight;
    [SerializeField] private Renderer floorRenderer;
    [SerializeField] private Renderer backdropRenderer;
    [SerializeField] private Renderer mistRenderer;

    [Header("뒤 벽 사진")]
    [Tooltip("아무 사진이나 넣으면 셰이더가 로비 분위기로 바꿔 보여 줍니다. 비어 있으면 단색 벽만 보입니다.")]
    [SerializeField] private Texture[] slides;

    [Tooltip("다음 사진으로 바꾸기 전까지 보여주는 시간(초). 흐르기는 멈추지 않습니다.")]
    [SerializeField] private float holdDuration = 8f;

    [Tooltip("다음 장으로 겹쳐 바뀌는 시간(초).")]
    [SerializeField] private float fadeDuration = 2f;

    [Tooltip("사진이 왼쪽으로 흐르는 속도. 클수록 빠릅니다.")]
    [SerializeField] private float scrollSpeed = 0.05f;

    [Tooltip("1이면 세로에 맞추고, 클수록 조금 확대됩니다. 가로는 타일처럼 이어집니다.")]
    [SerializeField] [Range(1f, 2f)] private float zoom = 1.1f;

    [Tooltip("사진이 벽에 비치는 세기. 낮을수록 유령처럼 옅습니다.")]
    [SerializeField] [Range(0f, 1f)] private float imageOpacity = 0.28f;

    [Tooltip("0이면 흑백, 1이면 원본 색에 가깝습니다.")]
    [SerializeField] [Range(0f, 1f)] private float saturation = 0.12f;

    [Tooltip("1보다 작으면 대비가 낮아져 사진이 벽에 더 잘 녹아듭니다.")]
    [SerializeField] [Range(0.2f, 1.5f)] private float contrast = 0.7f;

    [Header("벽·바닥 경계")]
    [Tooltip("벽 아래쪽을 뿌옇게 만드는 높이. 너무 크면 경계에 띠가 생깁니다.")]
    [SerializeField] [Range(0.05f, 0.8f)] private float horizonHeight = 0.22f;

    [SerializeField] [Range(0f, 1f)] private float horizonStrength = 0.28f;

    [Tooltip("바닥이 벽 쪽으로 갈수록 벽 색에 섞이는 세기. 낮을수록 띠가 덜 보입니다.")]
    [SerializeField] [Range(0f, 1f)] private float mistAlpha = 0.48f;

    [Tooltip("이 Z부터 바닥이 벽 색으로 바뀌기 시작합니다. 캐릭터보다 뒤에 두는 게 좋습니다.")]
    [SerializeField] private float mistFadeStartZ = -2.4f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int TexAId = Shader.PropertyToID("_TexA");
    private static readonly int TexBId = Shader.PropertyToID("_TexB");
    private static readonly int TexAAspectId = Shader.PropertyToID("_TexAAspect");
    private static readonly int TexBAspectId = Shader.PropertyToID("_TexBAspect");
    private static readonly int WallAspectId = Shader.PropertyToID("_WallAspect");
    private static readonly int ScrollAId = Shader.PropertyToID("_ScrollA");
    private static readonly int ScrollBId = Shader.PropertyToID("_ScrollB");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int ZoomId = Shader.PropertyToID("_Zoom");
    private static readonly int ImageOpacityId = Shader.PropertyToID("_ImageOpacity");
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
    private static readonly int HorizonId = Shader.PropertyToID("_Horizon");
    private static readonly int HorizonStrengthId = Shader.PropertyToID("_HorizonStrength");
    private static readonly int FadeStartZId = Shader.PropertyToID("_FadeStartZ");
    private static readonly int FadeEndZId = Shader.PropertyToID("_FadeEndZ");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock _floorBlock;
    private MaterialPropertyBlock _backdropBlock;
    private MaterialPropertyBlock _mistBlock;
    private Texture _whiteTex;

    private int _indexA;
    private int _indexB;
    private float _scrollA;
    private float _scrollB;
    private float _fade;
    private float _holdTimer;
    private bool _fading;

    private readonly LightingPreset _live = new LightingPreset();
    private readonly LightingPreset _moodFrom = new LightingPreset();
    private readonly LightingPreset _moodTo = new LightingPreset();
    private float _moodTime;
    private float _moodDuration;
    private bool _moodRunning;

    public int PresetCount => presets != null ? presets.Length : 0;

    public static LightingPreset[] CreateDefaultPresets()
    {
        return new[]
        {
            new LightingPreset
            {
                displayName = "회색 스튜디오",
                cameraBackground = new Color(0.62f, 0.64f, 0.68f),
                ambient = new Color(0.50f, 0.50f, 0.52f),
                fog = new Color(0.62f, 0.64f, 0.68f),
                fogStart = 11f,
                fogEnd = 28f,
                lightColor = new Color(1f, 0.98f, 0.94f),
                lightIntensity = 1.15f,
                lightEuler = new Vector3(48f, -20f, 0f),
                floorColor = new Color(0.32f, 0.33f, 0.36f),
                backdropColor = new Color(0.54f, 0.56f, 0.60f)
            },
            new LightingPreset
            {
                displayName = "빨간 분위기",
                cameraBackground = new Color(0.48f, 0.06f, 0.07f),
                ambient = new Color(0.40f, 0.16f, 0.14f),
                fog = new Color(0.42f, 0.05f, 0.06f),
                fogStart = 10f,
                fogEnd = 26f,
                lightColor = new Color(1f, 0.72f, 0.66f),
                lightIntensity = 1.25f,
                lightEuler = new Vector3(48f, -20f, 0f),
                floorColor = new Color(0.30f, 0.12f, 0.10f),
                backdropColor = new Color(0.50f, 0.07f, 0.08f)
            },
            new LightingPreset
            {
                displayName = "어두운 분위기",
                cameraBackground = new Color(0.10f, 0.11f, 0.14f),
                ambient = new Color(0.16f, 0.17f, 0.22f),
                fog = new Color(0.10f, 0.11f, 0.14f),
                fogStart = 10f,
                fogEnd = 24f,
                lightColor = new Color(0.82f, 0.86f, 1f),
                lightIntensity = 0.85f,
                lightEuler = new Vector3(48f, -20f, 0f),
                floorColor = new Color(0.14f, 0.15f, 0.18f),
                backdropColor = new Color(0.13f, 0.14f, 0.17f)
            }
        };
    }

    public static void ApplyBackdropTransform(Transform backdrop)
    {
        if (backdrop == null)
            return;

        backdrop.position = new Vector3(0f, BackdropHeight * 0.5f, BackdropZ);
        backdrop.rotation = Quaternion.identity;
        backdrop.localScale = new Vector3(BackdropWidth, BackdropHeight, 1f);
    }

    public static void ApplyMistTransform(Transform mist)
    {
        if (mist == null)
            return;

        // 바닥에 눕혀서, 캐릭터 쪽은 어둡고 벽 쪽으로 천천히 벽 색이 섞이게 합니다.
        float startZ = -2.6f;
        float endZ = BackdropZ + 0.4f;
        float length = endZ - startZ;
        mist.position = new Vector3(0f, 0.03f, (startZ + endZ) * 0.5f);
        mist.rotation = Quaternion.Euler(90f, 0f, 0f);
        mist.localScale = new Vector3(BackdropWidth + 6f, length, 1f);
    }

    public static Mesh GetQuadMesh()
    {
        var mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        if (mesh != null)
            return mesh;

        var backdrop = GameObject.Find("LobbyBackdrop");
        if (backdrop != null)
        {
            var filter = backdrop.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                return filter.sharedMesh;
        }

        return null;
    }

    public static void EnsureQuadMesh(GameObject go)
    {
        if (go == null)
            return;

        var filter = go.GetComponent<MeshFilter>();
        if (filter == null)
            filter = go.AddComponent<MeshFilter>();

        if (filter.sharedMesh == null)
            filter.sharedMesh = GetQuadMesh();
    }

    private void Reset()
    {
        presets = CreateDefaultPresets();
        currentPreset = 0;
        AutoFindRefs();
    }

    private void OnEnable()
    {
        AutoFindRefs();
        ResetSlideshow();
        SnapToPreset(currentPreset, log: false);
        PushBackdropPlayback();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        HandlePresetHotkeys();
        TickMood(Time.deltaTime);
        TickSlideshow(Time.deltaTime);
        PushBackdropPlayback();
    }

    private void HandlePresetHotkeys()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            TransitionToPreset(0);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            TransitionToPreset(1);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            TransitionToPreset(2);
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            TransitionToPreset(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            TransitionToPreset(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            TransitionToPreset(2);
#endif
    }

    /// <summary>
    /// 지정한 세트로 색과 밝기를 서서히 바꿉니다. 그림자 방향은 그대로입니다.
    /// </summary>
    public void TransitionToPreset(int index)
    {
        if (presets == null || presets.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, presets.Length - 1);
        currentPreset = index;

        if (!Application.isPlaying)
        {
            SnapToPreset(index);
            return;
        }

        CopyPreset(_live, _moodFrom);
        CopyPreset(presets[index], _moodTo);
        _moodTime = 0f;
        _moodDuration = Mathf.Max(0.05f, moodTransitionDuration);
        _moodRunning = true;
    }

    /// <summary>
    /// 에디터 버튼용. 플레이 중이면 서서히 바꾸고, 아니면 바로 적용합니다.
    /// </summary>
    public void ApplyPreset(int index, bool log = true)
    {
        if (Application.isPlaying)
        {
            TransitionToPreset(index);
            if (log)
                Debug.Log($"[LobbyEnvironment] 분위기 전환: {presets[Mathf.Clamp(index, 0, presets.Length - 1)].displayName}");
            return;
        }

        SnapToPreset(index, log);
    }

    private void SnapToPreset(int index, bool log = true)
    {
        if (presets == null || presets.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, presets.Length - 1);
        currentPreset = index;
        CopyPreset(presets[index], _live);
        _moodRunning = false;
        ApplyMoodVisuals(_live);

        if (log)
            Debug.Log($"[LobbyEnvironment] 조명 세트: {presets[index].displayName}  (키보드 1/2/3으로 전환)");

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void TickMood(float dt)
    {
        if (!_moodRunning)
            return;

        _moodTime += dt;
        float t = Mathf.Clamp01(_moodTime / _moodDuration);
        t = t * t * (3f - 2f * t);
        LerpPreset(_moodFrom, _moodTo, t, _live);
        ApplyMoodVisuals(_live);

        if (t >= 1f)
            _moodRunning = false;
    }

    private void ApplyMoodVisuals(LightingPreset preset)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = preset.ambient;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = preset.fog;
        RenderSettings.fogStartDistance = preset.fogStart;
        RenderSettings.fogEndDistance = preset.fogEnd;

        if (targetCamera != null)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = preset.cameraBackground;
        }

        if (keyLight != null)
        {
            keyLight.color = preset.lightColor;
            keyLight.intensity = preset.lightIntensity;
            keyLight.transform.rotation = Quaternion.Euler(studioLightEuler);
        }

        ApplyFloor(preset);
        ApplyMistColor(preset);
    }

    private void AutoFindRefs()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (keyLight == null)
        {
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
                keyLight = lightGo.GetComponent<Light>();
        }
        if (floorRenderer == null)
        {
            var floor = GameObject.Find("LobbyFloor");
            if (floor != null)
                floorRenderer = floor.GetComponent<Renderer>();
        }
        if (backdropRenderer == null)
        {
            var backdrop = GameObject.Find("LobbyBackdrop");
            if (backdrop != null)
                backdropRenderer = backdrop.GetComponent<Renderer>();
        }

        EnsureHorizonMist();
    }

    private void EnsureHorizonMist()
    {
        GameObject existing = mistRenderer != null ? mistRenderer.gameObject : GameObject.Find("LobbyHorizonMist");
        if (existing == null)
        {
            existing = GameObject.CreatePrimitive(PrimitiveType.Quad);
            existing.name = "LobbyHorizonMist";
            var collider = existing.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }
        }

        EnsureQuadMesh(existing);
        ApplyMistTransform(existing.transform);
        mistRenderer = existing.GetComponent<Renderer>();
        if (mistRenderer == null)
            return;

        mistRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mistRenderer.receiveShadows = false;

        if (mistRenderer.sharedMaterial == null ||
            mistRenderer.sharedMaterial.shader == null ||
            mistRenderer.sharedMaterial.shader.name != "Custom/Lobby/HorizonMist")
        {
            var shader = Shader.Find("Custom/Lobby/HorizonMist");
            if (shader != null)
                mistRenderer.sharedMaterial = new Material(shader);
        }
    }

    private void ResetSlideshow()
    {
        _indexA = FirstValidIndex(0);
        _indexB = NextValidIndex(_indexA);
        _scrollA = 0f;
        _scrollB = 0f;
        _fade = 0f;
        _holdTimer = 0f;
        _fading = false;
    }

    private void TickSlideshow(float dt)
    {
        int count = CountValidSlides();
        if (count == 0)
            return;

        _scrollA += dt * Mathf.Max(0f, scrollSpeed);
        if (_scrollA > 1000f)
            _scrollA -= 1000f;
        _scrollB = _scrollA;

        if (count < 2)
        {
            _fading = false;
            _fade = 0f;
            return;
        }

        float hold = Mathf.Max(0.1f, holdDuration);
        float fade = Mathf.Max(0.05f, fadeDuration);

        if (!_fading)
        {
            _holdTimer += dt;
            if (_holdTimer >= hold)
            {
                _fading = true;
                _fade = 0f;
                _indexB = NextValidIndex(_indexA);
            }
        }
        else
        {
            _fade = Mathf.Clamp01(_fade + dt / fade);
            if (_fade >= 1f)
            {
                _indexA = _indexB;
                _holdTimer = 0f;
                _fade = 0f;
                _fading = false;
            }
        }
    }

    private void PushBackdropPlayback()
    {
        if (backdropRenderer == null)
            return;

        if (_backdropBlock == null)
            _backdropBlock = new MaterialPropertyBlock();

        var preset = _live;
        Texture texA = GetSlide(_indexA);
        Texture texB = GetSlide(_indexB);
        if (texA == null)
            texA = WhiteTex;
        if (texB == null)
            texB = texA;

        int validCount = CountValidSlides();
        float opacity = validCount > 0 ? imageOpacity : 0f;
        float wallAspect = 1f;
        if (backdropRenderer.transform.lossyScale.y > 0.0001f)
            wallAspect = backdropRenderer.transform.lossyScale.x / backdropRenderer.transform.lossyScale.y;

        backdropRenderer.GetPropertyBlock(_backdropBlock);
        _backdropBlock.SetColor(BaseColorId, preset.backdropColor);
        _backdropBlock.SetColor(ColorId, preset.backdropColor);
        _backdropBlock.SetColor(HorizonColorId, preset.fog);
        _backdropBlock.SetColor(TintColorId, Color.Lerp(preset.backdropColor, Color.white, 0.18f));
        _backdropBlock.SetTexture(TexAId, texA);
        _backdropBlock.SetTexture(TexBId, texB);
        _backdropBlock.SetFloat(TexAAspectId, AspectOf(texA));
        _backdropBlock.SetFloat(TexBAspectId, AspectOf(texB));
        _backdropBlock.SetFloat(WallAspectId, wallAspect);
        _backdropBlock.SetFloat(ScrollAId, _scrollA);
        _backdropBlock.SetFloat(ScrollBId, _scrollB);
        _backdropBlock.SetFloat(FadeId, _fade);
        _backdropBlock.SetFloat(ZoomId, zoom);
        _backdropBlock.SetFloat(ImageOpacityId, opacity);
        _backdropBlock.SetFloat(SaturationId, saturation);
        _backdropBlock.SetFloat(ContrastId, contrast);
        _backdropBlock.SetFloat(HorizonId, horizonHeight);
        _backdropBlock.SetFloat(HorizonStrengthId, horizonStrength);
        backdropRenderer.SetPropertyBlock(_backdropBlock);
    }

    private static void CopyPreset(LightingPreset from, LightingPreset to)
    {
        if (from == null || to == null)
            return;

        to.displayName = from.displayName;
        to.cameraBackground = from.cameraBackground;
        to.ambient = from.ambient;
        to.fog = from.fog;
        to.fogStart = from.fogStart;
        to.fogEnd = from.fogEnd;
        to.lightColor = from.lightColor;
        to.lightIntensity = from.lightIntensity;
        to.lightEuler = from.lightEuler;
        to.floorColor = from.floorColor;
        to.backdropColor = from.backdropColor;
    }

    private static void LerpPreset(LightingPreset from, LightingPreset to, float t, LightingPreset result)
    {
        result.displayName = t < 1f ? from.displayName : to.displayName;
        result.cameraBackground = Color.Lerp(from.cameraBackground, to.cameraBackground, t);
        result.ambient = Color.Lerp(from.ambient, to.ambient, t);
        result.fog = Color.Lerp(from.fog, to.fog, t);
        result.fogStart = Mathf.Lerp(from.fogStart, to.fogStart, t);
        result.fogEnd = Mathf.Lerp(from.fogEnd, to.fogEnd, t);
        result.lightColor = Color.Lerp(from.lightColor, to.lightColor, t);
        result.lightIntensity = Mathf.Lerp(from.lightIntensity, to.lightIntensity, t);
        result.floorColor = Color.Lerp(from.floorColor, to.floorColor, t);
        result.backdropColor = Color.Lerp(from.backdropColor, to.backdropColor, t);
    }

    private void ApplyMistColor(LightingPreset preset)
    {
        if (mistRenderer == null)
            return;

        if (_mistBlock == null)
            _mistBlock = new MaterialPropertyBlock();

        Color mist = Color.Lerp(preset.fog, preset.backdropColor, 0.4f);
        mist.a = mistAlpha;
        mistRenderer.GetPropertyBlock(_mistBlock);
        _mistBlock.SetColor(ColorId, mist);
        _mistBlock.SetColor(BaseColorId, mist);
        _mistBlock.SetFloat(FadeStartZId, mistFadeStartZ);
        _mistBlock.SetFloat(FadeEndZId, BackdropZ - 1.2f);
        mistRenderer.SetPropertyBlock(_mistBlock);
    }

    private void ApplyFloor(LightingPreset preset)
    {
        if (floorRenderer == null)
            return;

        if (_floorBlock == null)
            _floorBlock = new MaterialPropertyBlock();

        Color floor = preset.floorColor;
        Color fill = floor * 0.06f;
        fill.a = 1f;

        floorRenderer.GetPropertyBlock(_floorBlock);
        _floorBlock.SetColor(BaseColorId, floor);
        _floorBlock.SetColor(ColorId, floor);
        _floorBlock.SetColor(EmissionColorId, fill);
        floorRenderer.SetPropertyBlock(_floorBlock);
    }

    private Texture WhiteTex
    {
        get
        {
            if (_whiteTex == null)
                _whiteTex = Texture2D.whiteTexture;
            return _whiteTex;
        }
    }

    private static float AspectOf(Texture tex)
    {
        if (tex == null || tex.height <= 0)
            return 1f;
        return tex.width / (float)tex.height;
    }

    private Texture GetSlide(int index)
    {
        if (slides == null || index < 0 || index >= slides.Length)
            return null;
        return slides[index];
    }

    private int CountValidSlides()
    {
        if (slides == null)
            return 0;

        int count = 0;
        for (int i = 0; i < slides.Length; i++)
        {
            if (slides[i] != null)
                count++;
        }

        return count;
    }

    private int FirstValidIndex(int start)
    {
        if (slides == null || slides.Length == 0)
            return 0;

        for (int i = 0; i < slides.Length; i++)
        {
            int index = (start + i) % slides.Length;
            if (slides[index] != null)
                return index;
        }

        return 0;
    }

    private int NextValidIndex(int current)
    {
        if (slides == null || slides.Length == 0)
            return current;

        for (int i = 1; i <= slides.Length; i++)
        {
            int index = (current + i) % slides.Length;
            if (slides[index] != null)
                return index;
        }

        return current;
    }
}
