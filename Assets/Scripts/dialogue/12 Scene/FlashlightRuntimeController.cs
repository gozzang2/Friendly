using System.Collections;
using TMPro;
using UnityEngine;

public class FlashlightRuntimeController : MonoBehaviour
{
    private static FlashlightRuntimeController _instance;

    [Header("Light Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Q;
    [SerializeField] private float range = 18f;
    [SerializeField] private float spotAngle = 70f;
    [SerializeField] private float intensity = 3f;

    private Light _flashlight;
    private Transform _cameraTransform;
    private bool _hasFlashlight;
    private bool _isOn = true;

    public static FlashlightRuntimeController Instance
    {
        get
        {
            if (_instance != null) return _instance;

            var existing = FindFirstObjectByType<FlashlightRuntimeController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return _instance;
            }

            var root = GameObject.Find("[PersistentRoot]") ?? GameObject.Find("PersistentRoot");
            GameObject host;

            if (root != null)
            {
                host = new GameObject("FlashlightRuntimeController");
                host.transform.SetParent(root.transform);
            }
            else
            {
                host = new GameObject("FlashlightRuntimeController");
                DontDestroyOnLoad(host);
            }

            _instance = host.AddComponent<FlashlightRuntimeController>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void Update()
    {
        if (!_hasFlashlight) return;

        if (_cameraTransform == null)
            BindRuntimeCamera();

        if (_flashlight == null)
            CreateLight();

        if (Input.GetKeyDown(toggleKey))
            ToggleFlashlight();

        if (_cameraTransform != null && _flashlight != null)
        {
            _flashlight.transform.position = _cameraTransform.position;
            _flashlight.transform.rotation = _cameraTransform.rotation;
        }
    }

    public void AcquireFlashlight()
    {
        if (_hasFlashlight)   return;
        if(_isOn) _isOn = false;   

        _hasFlashlight = true;
        _isOn = true;

        BindRuntimeCamera();
        CreateLight();

        if (_flashlight != null)
            _flashlight.enabled = true;

        ShowFlashlightUI();
    }

    private void ToggleFlashlight()
    {
        _isOn = !_isOn;

        if (_flashlight != null)
            _flashlight.enabled = _isOn;
    }

    private void BindRuntimeCamera()
    {
        var playerController = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (playerController != null && playerController.cameraTransform != null)
        {
            _cameraTransform = playerController.cameraTransform;
            return;
        }

        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var cam in cameras)
        {
            if (cam == null) continue;
            if (!cam.gameObject.scene.isLoaded) continue;
            if (!cam.isActiveAndEnabled) continue;

            _cameraTransform = cam.transform;
            return;
        }

        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;
    }

    private void CreateLight()
    {
        if (_flashlight != null) return;
        if (_cameraTransform == null) return;

        var lightObj = new GameObject("Runtime Flashlight Light");
        lightObj.transform.SetParent(transform);

        _flashlight = lightObj.AddComponent<Light>();
        _flashlight.type = LightType.Spot;
        _flashlight.range = range;
        _flashlight.spotAngle = spotAngle;
        _flashlight.intensity = intensity;
        _flashlight.shadows = LightShadows.Soft;

        lightObj.transform.position = _cameraTransform.position;
        lightObj.transform.rotation = _cameraTransform.rotation;
    }

    private void ShowFlashlightUI()
    {
        var ui = FindFlashlightUIText();

        if (ui == null)
        {
            Debug.LogWarning("[FlashlightRuntimeController] Flashlight UI text not found.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(ShowFlashlightUICoroutine(ui));
    }

    private TMP_Text FindFlashlightUIText()
    {
        var texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var text in texts)
        {
            if (text != null && text.gameObject.name == "Flashlight UI")
                return text;
        }

        return null;
    }

    private IEnumerator ShowFlashlightUICoroutine(TMP_Text ui)
    {
        ui.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        ui.gameObject.SetActive(false);
    }
}