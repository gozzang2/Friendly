using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DebugConsole : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject consoleRoot;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text logText;

    [Header("Refs")]
    [SerializeField] private dialog dialogSystem;

    [Header("Optional Refs")]
    [SerializeField] private PlayerInput playerInput;

    private bool isOpen = false;
    private bool _sceneJumpRequested = false;
    private readonly List<string> logs = new();

    private void Awake()
    {
        if (consoleRoot) consoleRoot.SetActive(false);

        Application.logMessageReceived += HandleLog;
        SceneManager.sceneLoaded += OnSceneLoaded;

        RebindPlayerInputIfNeeded();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.LeftShift))
        {
            ToggleConsole();
        }

        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            SubmitCommand();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseConsole();
        }
    }

    private void ToggleConsole()
    {
        if (isOpen) CloseConsole();
        else OpenConsole();
    }

    private void OpenConsole()
    {
        isOpen = true;

        if (consoleRoot)
            consoleRoot.SetActive(true);

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RebindPlayerInputIfNeeded();
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");

        if (inputField)
        {
            inputField.text = "";
            inputField.ActivateInputField();
            inputField.Select();
        }
    }

    private void CloseConsole()
    {
        isOpen = false;

        if (inputField != null)
            inputField.DeactivateInputField();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (consoleRoot)
            consoleRoot.SetActive(false);

        RestoreGameplayState();
    }

    private void RestoreGameplayState()
    {
        Time.timeScale = 1f;

        bool gameplay = IsGameplayScene();

        if (gameplay)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        RebindPlayerInputIfNeeded();

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap(gameplay ? "Player" : "UI");
    }

    private bool IsGameplayScene()
    {
        var n = SceneManager.GetActiveScene().name;
        return n != "TitleScene" && n != "BootstrapScene";
    }

    private void RebindPlayerInputIfNeeded()
    {
        if (playerInput != null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerInput = player.GetComponent<PlayerInput>();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindPlayerInputIfNeeded();

        if (_sceneJumpRequested)
        {
            _sceneJumpRequested = false;
            RestoreGameplayState();
        }
    }

    private void SubmitCommand()
    {
        if (inputField == null) return;

        string cmd = inputField.text.Trim();
        inputField.text = "";

        ExecuteCommand(cmd);
    }

    private void ExecuteCommand(string cmd)
    {
        if (string.IsNullOrEmpty(cmd)) return;

        Debug.Log($"[CMD] {cmd}");

        switch (cmd.ToLower())
        {
            case "/nextnodeadmin":
                if (dialogSystem == null)
                {
                    Debug.LogError("[CMD] dialogSystem not assigned");
                    return;
                }
                CloseConsole();
                dialogSystem.SkipToNextNode();
                break;

            case "/nextsceneadmin":
                if (dialogSystem == null)
                {
                    Debug.LogError("[CMD] dialogSystem not assigned");
                    return;
                }

                PrepareDebugSpawnOverrideForNextScene();

                _sceneJumpRequested = true;
                CloseConsole();
                dialogSystem.SkipToNextScene();
                break;

            case "/showlog":
                ShowLogs();
                break;

            case "/close":
                CloseConsole();
                break;

            case "/goscenecctvadmin":
                {
                    CloseConsole();

                    SceneLoader.nextSpawnID = "CCTV_Ent";

                    dialogSystem.GotoScene(
                        "S07_CCTV_ROOM"
                    );

                    break;
                }

            default:
                Debug.LogWarning($"[CMD] Unknown command: {cmd}");
                break;
        }
    }


    private void PrepareDebugSpawnOverrideForNextScene()
    {
        if (dialogSystem == null || dialogSystem.data == null || dialogSystem.data.scenes == null)
        {
            SceneLoader.nextSpawnID = null;
            return;
        }

        var currentScene = dialogSystem.data.scenes.FindIndex(s => s.id == dialogSystem.CurrentStorySceneId);
        if (currentScene < 0 || currentScene + 1 >= dialogSystem.data.scenes.Count)
        {
            SceneLoader.nextSpawnID = null;
            return;
        }

        var nextStoryScene = dialogSystem.data.scenes[currentScene + 1];
        if (nextStoryScene == null)
        {
            SceneLoader.nextSpawnID = null;
            return;
        }

        string nextUnitySceneName = dialogSystem.GetUnitySceneNameForStoryScene(nextStoryScene.id);

        switch (nextUnitySceneName)
        {
            case "10_F1_Main":
                SceneLoader.nextSpawnID = "Hospital_Ent";
                break;

            case "2F_Hall":
                SceneLoader.nextSpawnID = "2F_Ent";
                break;

            case "OssuaryIndoorScene":
                SceneLoader.nextSpawnID = "Ossuary_Ent";
                break;

            default:
                SceneLoader.nextSpawnID = null;
                break;
        }

        Debug.Log($"[DebugConsole] next unity scene = {nextUnitySceneName}, nextSpawnID = {SceneLoader.nextSpawnID}");
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        logs.Add(logString);

        if (logs.Count > 100)
            logs.RemoveAt(0);
    }

    private void ShowLogs()
    {
        if (logText == null)
        {
            Debug.LogWarning("[CMD] logText UI not assigned");
            return;
        }

        logText.text = string.Join("\n", logs);
    }
}