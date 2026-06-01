using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using NavKeypad;    //Asset > Keypad > Scripts > KeypadModalController.cs
using nodedef;
using Newtonsoft.Json;

public class dialog : MonoBehaviour
{
    [Header("Load")]
    [SerializeField] private string jsonFileName = "SCRIPT_0209.json";  // Connect Script json
    [SerializeField] private string startSceneId;
    [SerializeField] private string startNodeId;    //empty : [0]

    [Header("Refs")]
    [SerializeField] private UIController ui; // Connect ui.ShowToast
    [SerializeField] private NavKeypad.Keypad keypad;
    [SerializeField] private NavKeypad.KeypadModalController modal;
    [SerializeField] private SafeModalController safeModal;

    //scene binding (optional, for external scene load requests)
    [Serializable]
    private class StoryUnitySceneBinding
    {
        public string storySceneId;
        public string unitySceneName;
    }

    [SerializeField] private List<StoryUnitySceneBinding> storyUnitySceneBindings = new();

    private bool _keypadOptionalFallbackRunning;

    //Public API for external callers (DebugConsole, etc.)
    public string CurrentStorySceneId => _currentScene != null ? _currentScene.id : null;

    public string GetUnitySceneNameForStoryScene(string storySceneId)
    {
        if (string.IsNullOrWhiteSpace(storySceneId))
            return null;

        return _unitySceneNameByStorySceneId.TryGetValue(storySceneId, out var unitySceneName)
            ? unitySceneName
            : null;
    }

    //Scene binding lookup
    private readonly Dictionary<string, string> _unitySceneNameByStorySceneId = new();
    private string _waitingStorySceneId;
    private string _waitingStoryStartNodeId;

    //State Allow nodes to check/modify these during flow
    private bool _interactionRequested;
    private string _interactionTarget;
    private bool _interactionWaiting;

    //Loaded Data
    public StoryData data { get; private set; }

    //Scene, node Lookup
    private readonly Dictionary<string, SceneDef> _scenesById = new();
    private readonly Dictionary<string, NodeDef> _nodesById = new();
    private readonly Dictionary<string, int> _nodeIndexById = new();
    private readonly Dictionary<string, string> _sceneResumeNodeById = new();
    private List<NodeDef> _currentNodes;
    private void Goto(string nextId) => _nextNodeId = nextId;
    private SceneDef _currentScene;

    //Item pickup flow control
    private bool _pickupWaiting;
    private readonly HashSet<string> _pendingPickupItemIds = new();
    private string _pickupResumeNodeId;

    //Flow control
    private string _nextNodeId;
    private string _pendingSceneId;
    private string _pendingSceneStartNodeId;

    //Special Interaction Item
    private const string KeypadTargetName = "Keypad";
    private const string SafeTargetName = "Locked Safe";

    //Current Progress Save/Load API (for rollback, etc.)
    public string CurrentSceneId => _currentScene?.id;
    public string CurrentNodeId => _nextNodeId;

    private void Awake()
    {
        if (jsonFileName != null) LoadFromStreamingAssets(jsonFileName);

        BuildStoryUnitySceneBindings();
        ResolveSceneRuntimeRefs();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (data == null) return;

        if (!string.IsNullOrEmpty(startSceneId))
        StartScene(startSceneId, startNodeId);
    }

    #region Loading
    public void LoadFromStreamingAssets(string fileName)
{
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, fileName);

        if (!System.IO.File.Exists(path))
        {
            Debug.LogError($"[dialog] JSON file not found: {path}");
            return;
        }

        try
        {
            string json = System.IO.File.ReadAllText(path);
            data = JsonConvert.DeserializeObject<StoryData>(json);

            if (data == null)
            {
                Debug.LogError("[dialog] Failed to deserialize JSON.");
                return;
            }

            BuildLookups();
            Debug.Log("[dialog] JSON Loaded Successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[dialog] JSON Load Exception: {e.Message}");
        }
    }

    private void BuildLookups()
    {
        _scenesById.Clear();

        if(data?.scenes == null)
        {
            Debug.LogError("StoryData.scenes is null");
            return;
        }

        foreach (var s in data.scenes)
        {
            if (string.IsNullOrEmpty(s.id)) continue;
            _scenesById[s.id] = s;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void BuildStoryUnitySceneBindings()
    {
        _unitySceneNameByStorySceneId.Clear();

        if (storyUnitySceneBindings == null) return;

        foreach (var b in storyUnitySceneBindings)
        {
            if (b == null) continue;
            if (string.IsNullOrWhiteSpace(b.storySceneId)) continue;
            if (string.IsNullOrWhiteSpace(b.unitySceneName)) continue;

            _unitySceneNameByStorySceneId[b.storySceneId] = b.unitySceneName;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 새 scene 오브젝트들 다시 탐색
        keypad = null;
        modal = null;
        safeModal = null;
        ResolveSceneRuntimeRefs();

        if (string.IsNullOrEmpty(_waitingStorySceneId))
            return;

        if (_unitySceneNameByStorySceneId.TryGetValue(_waitingStorySceneId, out var expectedUnitySceneName) &&
            string.Equals(scene.name, expectedUnitySceneName, StringComparison.Ordinal))
        {
            string nextStorySceneId = _waitingStorySceneId;
            string nextStoryStartNodeId = _waitingStoryStartNodeId;

            _waitingStorySceneId = null;
            _waitingStoryStartNodeId = null;

            StartScene(nextStorySceneId, nextStoryStartNodeId);
        }
    }

    #endregion

    #region scene entry
    public void StartScene(string sceneId, string nodeId = null)
    {
        if (!_scenesById.TryGetValue(sceneId, out var scene))
        {
            Debug.LogError($"Scene not found: {sceneId}");
            return;
        }

        if (ui != null)
            ui.HideDialogueImmediate();

        if (string.IsNullOrEmpty(nodeId) &&
            _sceneResumeNodeById.TryGetValue(sceneId, out var resumeNodeId) &&
            !string.IsNullOrEmpty(resumeNodeId))
        {
            nodeId = resumeNodeId;
        }

        StopAllCoroutines();
        StartCoroutine(RunScene(scene, nodeId));
    }

    private IEnumerator RunScene(SceneDef scene, string startNodeOverride)
    {
        _currentScene = scene;
        _currentNodes = scene.nodes ?? new List<NodeDef>();

        if (_currentNodes == null || _currentNodes.Count == 0)
        {
            Debug.LogError($"[dialog] Scene has no nodes: {scene.id}");
            yield break;
        }

        // build node lookup for this scene
        _nodesById.Clear();
        _nodeIndexById.Clear();

        for (int i = 0; i < _currentNodes.Count; i++)
        {
            var n = _currentNodes[i];

            if (n == null || string.IsNullOrEmpty(n.id))
            {
            Debug.LogWarning($"[dialog] Invalid node at index {i} in scene {scene.id}");
            continue;
            }

            _nodesById[n.id] = n;
            _nodeIndexById[n.id] = i;
        }

        // onEnter actions
        if (scene.onEnter != null && scene.onEnter.Count > 0)
            yield return RunCommands(scene.onEnter);

        // pick start node
        string nodeId = !string.IsNullOrEmpty(startNodeOverride)
            ? startNodeOverride
            : (_currentNodes.Count > 0 ? _currentNodes[0].id : null);

        yield return RunFlow(nodeId);
    }

    #endregion

    #region core flow loof
    private IEnumerator RunFlow(string nodeId)
    {
        while (!string.IsNullOrEmpty(nodeId))
        {
            if (!_nodesById.TryGetValue(nodeId, out var node) || node == null)
            {
                Debug.LogError($"Node not found in scene '{_currentScene?.id}': {nodeId}");
                yield break;
            }

            _nextNodeId = null;

            yield return RunNode(node);

            // 현재 scene의 resume 지점 저장
            if (_currentScene != null)
            {
                string resumeNode = _nextNodeId;

                if (string.IsNullOrEmpty(resumeNode) && string.IsNullOrEmpty(_pendingSceneId))
                    resumeNode = NextIdOrNull(node.id);

                if (!string.IsNullOrEmpty(resumeNode))
                    _sceneResumeNodeById[_currentScene.id] = resumeNode;
            }

            if (!string.IsNullOrEmpty(_pendingSceneId))
            {
                var nextSceneId = _pendingSceneId;
                var nextStartNode = _pendingSceneStartNodeId;

                _pendingSceneId = null;
                _pendingSceneStartNodeId = null;

                if (_scenesById.TryGetValue(nextSceneId, out var nextScene))
                {
                    yield return RunScene(nextScene, nextStartNode);
                    yield break;
                }
                else
                {
                    Debug.LogError($"Next scene not found: {nextSceneId}");
                    yield break;
                }
            }

            nodeId = _nextNodeId;
        }

        if (ui != null)
            ui.HideDialogueImmediate();
    }

    private void GotoNextInScene(string currentNodeId)
    {
        if(_nodeIndexById.TryGetValue(currentNodeId, out var idx))
        {
            int nextIdx = idx+1;
            if (nextIdx>=0 && nextIdx < _currentNodes.Count)
                _nextNodeId = _currentNodes[nextIdx].id;
            else
                _nextNodeId = null;
        }
        else
        {
            _nextNodeId = null;
        }
    }

    public void GotoScene(string sceneId, string startNodeId = null)
    {
        if (_unitySceneNameByStorySceneId.TryGetValue(sceneId, out var unitySceneName) &&
            !string.IsNullOrWhiteSpace(unitySceneName))
        {
            Debug.Log($"[dialog] Story scene '{sceneId}' -> Load Unity scene '{unitySceneName}'");

            _waitingStorySceneId = sceneId;
            _waitingStoryStartNodeId = startNodeId;

            _pendingSceneId = null;
            _pendingSceneStartNodeId = null;
            _nextNodeId = null;

            SceneManager.LoadScene(unitySceneName);
            return;
        }

        Debug.Log($"[dialog] Story scene '{sceneId}' has no Unity scene binding. Stay in current Unity scene.");

        _pendingSceneId = sceneId;
        _pendingSceneStartNodeId = startNodeId;
        _nextNodeId = null;
    }

    public bool IsWaitingForInteractionTarget(string target)
    {
        if (!_interactionWaiting) return false;

        string requestedTarget = string.IsNullOrWhiteSpace(target) ? null : Template(target);
        string waitingTarget = string.IsNullOrWhiteSpace(_interactionTarget) ? null : Template(_interactionTarget);

        return !string.IsNullOrEmpty(requestedTarget) &&
               !string.IsNullOrEmpty(waitingTarget) &&
               string.Equals(requestedTarget, waitingTarget, StringComparison.OrdinalIgnoreCase);
    }

    public void TriggerOptionalInteractionNow(string target)
    {
        if (_currentScene == null || _currentNodes == null) return;
        StartCoroutine(TriggerOptionalInteractionRoutine(target));
    }

    private IEnumerator TriggerOptionalInteractionRoutine(string target)
    {
        string resolvedTarget = Template(target);

        InteractionNode found = null;

        foreach (var node in _currentNodes)
        {
            if (node is InteractionNode interactionNode)
            {
                string nodeTarget = Template(interactionNode.target);
                if (string.Equals(nodeTarget, resolvedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    found = interactionNode;
                    break;
                }
            }
        }

        if (found == null)
        {
            Debug.LogWarning($"[dialog] Optional interaction target not found in current scene: {resolvedTarget}");
            yield break;
        }

        if (found.whenInteract != null && found.whenInteract.Count > 0)
            yield return RunCommands(found.whenInteract);
    }

    #endregion

    #region node dispatcher

    //helper for nodes that can trigger scene transition (e.g. uiObjective)
    private bool TryGotoScene(NodeDef node)
    {
        string ns = GetNextScene(node);
        if (!string.IsNullOrEmpty(ns))
        {
            GotoScene(ns, node.nextNode);
            return true;
        }

        return false;
    }

    private IEnumerator RunNode(NodeDef node)
    {
        switch (node.Type)
        {
            case NodeType.narration:
            {
                var n = (NarrationNode)node;
                yield return RunDialogueLike(null, Template(n.text));

                if (node.effects != null && node.effects.Count > 0)
                    yield return RunCommands(node.effects);

                if (TryGotoScene(node)) break;

                Goto(string.IsNullOrEmpty(node.next) ? NextIdOrNull(node.id) : node.next);
                break;
            }
            case NodeType.dialogue:
            {
                var d = (DialogueNode)node;
                yield return RunDialogueLike(Template(d.speaker), Template(d.text));

                if (node.effects != null && node.effects.Count > 0)
                    yield return RunCommands(node.effects);

                if (TryGotoScene(node)) break;

                Goto(string.IsNullOrEmpty(node.next) ? NextIdOrNull(node.id) : node.next);
                break;
            }
            case NodeType.pickup:
            {
                yield return WaitForPickupBlock(node.id);
                break;
            }
            case NodeType.interaction:
            {
                var it = (InteractionNode)node;

                if (it.optional)
                {
                    if (!string.IsNullOrEmpty(node.next)) Goto(node.next);
                    else GotoNextInScene(node.id);
                    break;
                }

                yield return RunInteraction(it);

                // whenInteract 안에서 dialogue next/goto 등이 발생하면 _nextNodeId가 설정될 것
                if (string.IsNullOrEmpty(_nextNodeId))
                {
                    if (!string.IsNullOrEmpty(node.next)) Goto(node.next);
                    else GotoNextInScene(node.id);
                }
                break;
            }
            case NodeType.action:
            {
                var a = (ActionNode)node;
                if (a.Do != null && a.Do.Count > 0)
                    yield return RunCommands(a.Do);

                if (node.effects != null && node.effects.Count > 0)
                    yield return RunCommands(node.effects);

                if (TryGotoScene(node)) break;

                if (!string.IsNullOrEmpty(node.next)) Goto(node.next);
                else GotoNextInScene(node.id);
                break;
            }
            case NodeType.choice:
            {
                var c = (ChoiceNode)node;
                yield return RunChoice(c);
                // RunChoice 내부에서 Goto를 결정
                break;
            }
            case NodeType.timeSkip:
            {
                var t = (TimeSkipNode)node;

                if (!string.IsNullOrEmpty(t.text))
                    yield return RunDialogueLike(null, Template(t.text));

                if (node.effects != null && node.effects.Count > 0)
                    yield return RunCommands(node.effects);

                if (t.seconds > 0f)
                    yield return new WaitForSeconds(t.seconds);

                if (TryGotoScene(node)) break;

                if (!string.IsNullOrEmpty(node.next)) Goto(node.next);
                else GotoNextInScene(node.id);
                break;
            }
            case NodeType.uiObjective:
            {
                var u = (UiObjectiveNode)node;

                if (ui != null)
                    ui.ShowObjective(Template(u.text));
                else
                    Debug.Log($"[Objective] {Template(u.text)}");

                // 추가
                if (node.effects != null && node.effects.Count > 0)
                    yield return RunCommands(node.effects);

                if (TryGotoScene(node))
                    break;

                Goto(string.IsNullOrEmpty(node.next)
                    ? NextIdOrNull(node.id)
                    : node.next);

                break;
            }
            case NodeType.ending:
            {
                var e = (EndingNode)node;
                // TODO: 엔딩 UI/씬 처리
                Debug.Log($"[ENDING] {e.endingId} {e.title}\n{e.text}");
                _nextNodeId = null;
                break;
            }
            default:
                Debug.LogError($"Unknown node type: {node.Type} ({node.id})");
                Goto(node.next);
                break;
        }
    }

    private string NextIdOrNull(string currentId)
    {
        if (_nodeIndexById.TryGetValue(currentId, out var idx))
        {
            int nextIdx = idx+1;
            return(nextIdx>= 0 && nextIdx < _currentNodes.Count) ? _currentNodes[nextIdx].id : null;
        }
        return null;
    }

    #endregion

    #region Nodehandlers

    private IEnumerator RunDialogueLike(string speaker, string text)
    {
        bool done = false;

        if(ui != null) ui.ShowDialogue(speaker, text, () => done = true);
        else { Debug.Log($"{speaker}: {text}"); done = true;}

        while (!done) yield return null;
    }

    private IEnumerator WaitForPickupBlock(string startNodeId)
    {
        BuildPickupBlock(startNodeId, out var requiredItemIds, out var resumeNodeId);

        _pendingPickupItemIds.Clear();
        foreach (var itemId in requiredItemIds)
        {
            if (!HasItemInStoryState(itemId))
                _pendingPickupItemIds.Add(itemId);
        }

        _pickupResumeNodeId = resumeNodeId;

        // 이미 다 주운 상태면 바로 다음으로
        if (_pendingPickupItemIds.Count == 0)
        {
            Goto(_pickupResumeNodeId);
            yield break;
        }

        _pickupWaiting = true;

        while (_pickupWaiting)
            yield return null;
    }

    public void PickupItemById_FromWorld(string itemId,
    bool showInspectLine)
    {
        if (itemId == null || string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("[dialog] PickupItemById_FromWorld: invalid node");
            return;
        }

        StartCoroutine(
            HandleWorldPickupRoutine(itemId, showInspectLine)
        );
    }

    private IEnumerator HandleWorldPickupRoutine(string itemId, bool showInspectLine)
    {
        // 이미 획득했으면 중복 방지
        if (HasItemInStoryState(itemId))
            yield break;

        yield return PickupItemRoutine(itemId, showInspectLine);

        if (_pickupWaiting && _pendingPickupItemIds.Contains(itemId))
        {
            _pendingPickupItemIds.Remove(itemId);

            if (_pendingPickupItemIds.Count == 0)
            {
                _pickupWaiting = false;
                Goto(_pickupResumeNodeId);
            }
        }
    }

    private IEnumerator PickupItemRoutine(string itemId, bool showInspectLine)
    {
        if (data == null || data.items == null)
        {
            Debug.LogError("Story data/items not loaded.");
            yield break;
        }

        if (!data.items.TryGetValue(itemId, out var item))
        {
            Debug.LogError($"Item not found: {itemId}");
            yield break;
        }


        // inventory add
        if (data.state.inventory == null) data.state.inventory = new List<string>();
        if (!data.state.inventory.Contains(itemId))
            data.state.inventory.Add(itemId);

        // flags/vars apply
        if (item.addFlags != null)
        {
            if (data.state.flags == null) data.state.flags = new Dictionary<string, bool>();
            foreach (var f in item.addFlags) data.state.flags[f] = true;
        }

        if (item.addVar != null)
        {
            if (data.state.vars == null) data.state.vars = new Dictionary<string, int>();
            foreach (var kv in item.addVar)
            {
                data.state.vars.TryGetValue(kv.Key, out int prev);
                data.state.vars[kv.Key] = prev + kv.Value;
            }
        }

        // randomly print inspect Line
        if (showInspectLine)
        {
            string line = PickRandomInspectLine(item);
            // and
            yield return RunDialogueLike(Template("{PC_NAME}"), Template(line));
        }

        // Toast
        if (itemId == "PHOTO_BABY" && ui != null)
        {
            //Toast UI 만들어진 상태면 toast 출력됨
            ui.ShowToast("Safe Passcode Unlocked: 11985", 1.8f);
            yield return new WaitForSeconds(1.8f);
        }

        Debug.Log($"Picked Up : {itemId}");
    }

    public void RequestInteraction(string target = null)
    {
        if (!_interactionWaiting)
        {
            Debug.LogWarning($"[dialog] Interaction requested while not waiting. target={target}");
            return;
        }

        string requestedTarget = string.IsNullOrWhiteSpace(target) ? null : Template(target);
        string waitingTarget = string.IsNullOrWhiteSpace(_interactionTarget) ? null : Template(_interactionTarget);

        if (string.IsNullOrEmpty(target) ||
            string.IsNullOrEmpty(waitingTarget) ||
            string.Equals(requestedTarget, waitingTarget, StringComparison.OrdinalIgnoreCase))
        {
            _interactionRequested = true;
        }
    }

    public bool IsFlagTrue(string flag)
    {
        if (data == null || data.state == null || data.state.flags == null) return false;
        if (string.IsNullOrEmpty(flag)) return false;

        data.state.flags.TryGetValue(flag, out bool value);
        return value;
    }

    private IEnumerator RunInteraction(InteractionNode node)
    {
        _interactionWaiting = true;
        _interactionRequested = false;
        _interactionTarget = Template(node.target);

        if (ui != null) ui.ShowInteractHint(true, _interactionTarget);

        while (!_interactionRequested)
            yield return null;

        _interactionWaiting = false;

        if (ui != null) ui.ShowInteractHint(false, _interactionTarget);

        if (node.whenInteract != null && node.whenInteract.Count > 0)
            yield return RunCommands(node.whenInteract);
    }

    private IEnumerator RunChoice(ChoiceNode node)
    {
        if (node.choices == null || node.choices.Count == 0)
        {
            Debug.LogWarning($"Choice node has no options: {node.id}");
            if (!string.IsNullOrEmpty(node.next)) Goto(node.next);
            else GotoNextInScene(node.id);
            yield break;
        }

        bool done = false;
        int picked = -1;

        if (ui != null && ui.SupportsChoiceUI)
        {
            var texts = new List<string>();
            foreach (var c in node.choices) texts.Add(Template(c.text));
            ui.ShowChoice(Template(node.prompt), texts, idx => { picked = idx; done = true; });
        }
        else
        {
            // fallback: automatically pick first choice
            Debug.Log($"[CHOICE] {node.prompt} (fallback picks #0)");
            picked = 0;
            done = true;
        }

        while (!done) yield return null;

        if (picked < 0 || picked >= node.choices.Count) picked = 0;

        var opt = node.choices[picked];

        // apply effects
        if (opt.effects != null && opt.effects.Count > 0)
            yield return RunCommands(opt.effects);

        // go next
        if (!string.IsNullOrEmpty(opt.nextScene))
            GotoScene(opt.nextScene, opt.nextNode);
        else if (!string.IsNullOrEmpty(opt.next))
            Goto(opt.next);
        else
            GotoNextInScene(node.id);
    }

    #endregion

    #region Command Runner (whenInteract / onEnter / do / effects)

    private IEnumerator RunCommands(List<Command> cmds)
    {
        foreach (var cmd in cmds)
        {
            if(cmd == null) continue;

            switch(cmd.type)
            {
                case "setFlags" :
                case "setFlag":
                {
                    if (data.state.flags == null) data.state.flags = new Dictionary<string, bool>();
                    data.state.flags[cmd.flag] = cmd.value ?? true;
                    SetFlag(cmd.flag, cmd.value);
                    break;
                }
                case "addVar":
                {
                    if (data.state.vars == null) data.state.vars = new Dictionary<string, int>();
                    data.state.vars.TryGetValue(cmd.var, out int prev);
                    data.state.vars[cmd.var] = prev + (cmd.delta ?? 0);
                    break;
                }
                case "narration":
                    yield return RunDialogueLike(null, Template(cmd.text));
                    break;
                case "dialogue":
                    yield return RunDialogueLike(Template(cmd.speaker), Template(cmd.text));
                    if (!string.IsNullOrEmpty(cmd.next))
                    {
                        Goto(cmd.next);
                        yield break; // 대사 액션이 next로 흐름을 끊는 경우
                    }
                    break;
                case "sound":
                    if (ui != null) ui.PlaySound(cmd.name);
                    else Debug.Log($"[SOUND] {cmd.name}");
                    break;
                case "video":
                    if (ui != null) yield return ui.PlayVideoAndWait(cmd.id);
                    else Debug.Log($"[VIDEO] {cmd.id} (no UI)");
                    break;
                case "light":
                    if (ui != null) ui.SetLight(cmd.mode, cmd.intensity ?? 1f);
                    break;
                case "vignette":
                    if (ui != null) ui.SetVignette(cmd.mode, cmd.strength ?? 0.5f);
                    break;
                case "branch":
                {
                    bool ok = EvalConditions(cmd.conditions);
                    var list = ok ? cmd.then : cmd.@else;
                    if (list != null && list.Count > 0)
                        yield return RunCommands(list);
                    break;
                }
                case "inputCode":
                    yield return RunInputCode(cmd);
                    break;
                case "goto":
                    Goto(cmd.next);
                    yield break;
                case "uiObjective":
                    {
                        if (ui != null) ui.ShowObjective(Template(cmd.text));
                        else Debug.Log($"[OBJECTIVE] {Template(cmd.text)}");
                        break;
                    }

                default:
                    Debug.LogWarning($"Unknown command type: {cmd.type}");
                    break;
            }
        }
    }

    private void SetFlag(string flag, bool? value)
    {
        if (string.IsNullOrEmpty(flag))
            return;

        if (data == null)
            return;

        if (data.state == null)
            return;

        if (data.state.flags == null)
            data.state.flags = new Dictionary<string, bool>();

        data.state.flags[flag] = value ?? true;

        Debug.Log($"[dialog] Flag Set: {flag} = {data.state.flags[flag]}");
    }

    private bool EvalConditions(List<Condition> conditions)
    {
        if (conditions == null || conditions.Count == 0) return true;
        if (data.state.flags == null) data.state.flags = new Dictionary<string, bool>();

        foreach (var c in conditions)
        {
            if (!string.IsNullOrEmpty(c.flag))
            {
                data.state.flags.TryGetValue(c.flag, out var has);
                if (!has) return false;
            }
            if (!string.IsNullOrEmpty(c.flagNot))
            {
                data.state.flags.TryGetValue(c.flagNot, out var has);
                if (has) return false;
            }
        }
        return true;
    }

    private void ResolveSceneRuntimeRefs()
    {
        // keypad
        if (keypad == null)
            keypad = FindFirstObjectByType<NavKeypad.Keypad>(FindObjectsInactive.Include);

        // keypad modal
        if (modal == null)
            modal = FindFirstObjectByType<NavKeypad.KeypadModalController>(FindObjectsInactive.Include);

        // safe modal
        if (safeModal == null)
            safeModal = FindFirstObjectByType<SafeModalController>(FindObjectsInactive.Include);

        Debug.Log($"[dialog] keypad={keypad}, keypadModal={modal}, safeModal={safeModal}");
    }

    private IEnumerator RunInputCode(Command cmd)
    {
        ResolveSceneRuntimeRefs();

        string currentTarget = string.IsNullOrWhiteSpace(_interactionTarget)
            ? string.Empty
            : Template(_interactionTarget);

        bool useSafeModal = string.Equals(
            currentTarget,
            SafeTargetName,
            StringComparison.OrdinalIgnoreCase
        );

        bool useKeypad = string.Equals(
            currentTarget,
            KeypadTargetName,
            StringComparison.OrdinalIgnoreCase
        );

        if (useSafeModal)
        {
            yield return RunSafeInputCode(cmd);
            yield break;
        }

        if (useKeypad)
        {
            yield return RunKeypadInputCode(cmd);
            yield break;
        }

        Debug.LogError($"[dialog] Unsupported inputCode target: '{currentTarget}'");

        if (!string.IsNullOrEmpty(cmd.onCancel))
            Goto(cmd.onCancel);
    }

    public void TriggerKeypadOptionalFallback()
    {
        StartCoroutine(TriggerKeypadOptionalFallbackRoutine());
    }

    private IEnumerator TriggerKeypadOptionalFallbackRoutine()
    {
        _keypadOptionalFallbackRunning = true;

        // 이미 keypad 메인 interaction을 기다리는 상황이면 fallback을 쓰지 않음
        if (IsWaitingForInteractionTarget(KeypadTargetName))
        {
            RequestInteraction(KeypadTargetName);
            _keypadOptionalFallbackRunning = false;
            yield break;
        }

        // S06_N3의 의미를 hard-coded로 실행
        yield return RunDialogueLike(Template("{PC_NAME}"), "Not now. I should look around first.");
        _keypadOptionalFallbackRunning = false;
    }

    private IEnumerator RunSafeInputCode(Command cmd)
    {
        if (safeModal == null)
        {
            Debug.LogError("[dialog] SafeModalController not assigned.");
            if (!string.IsNullOrEmpty(cmd.onCancel))
                Goto(cmd.onCancel);
            yield break;
        }

        bool done = false;
        string nextNode = null;

        safeModal.Open(
            cmd.expected,
            onSuccess: () =>
            {
                done = true;
                nextNode = cmd.onSuccess;
            },
            onFail: () =>
            {
                done = true;
                nextNode = cmd.onFail;
            },
            onCancel: () =>
            {
                done = true;
                nextNode = cmd.onCancel;
            }
        );

        while (!done)
            yield return null;

        if (!string.IsNullOrEmpty(nextNode))
            Goto(nextNode);
    }

    private IEnumerator RunKeypadInputCode(Command cmd)
    {
        if (keypad == null || modal == null)
        {
            Debug.LogError("[dialog] Keypad or KeypadModalController not assigned.");
            if (!string.IsNullOrEmpty(cmd.onCancel))
                Goto(cmd.onCancel);
            yield break;
        }

        if (!int.TryParse(cmd.expected, out var combo))
        {
            Debug.LogError($"[dialog] Invalid keypad expected code: {cmd.expected}");
            if (!string.IsNullOrEmpty(cmd.onCancel))
                Goto(cmd.onCancel);
            yield break;
        }

        keypad.SetCombo(combo);
        modal.Open();

        bool done = false;
        string nextNode = null;

        UnityAction granted = () =>
        {
            done = true;
            nextNode = cmd.onSuccess;
        };

        UnityAction denied = () =>
        {
            done = true;
            nextNode = cmd.onFail;
        };

        UnityAction canceled = () =>
        {
            done = true;
            nextNode = cmd.onCancel;
        };

        keypad.OnAccessGranted.AddListener(granted);
        keypad.OnAccessDenied.AddListener(denied);
        keypad.OnCanceled.AddListener(canceled);

        while (!done)
            yield return null;

        keypad.OnAccessGranted.RemoveListener(granted);
        keypad.OnAccessDenied.RemoveListener(denied);
        keypad.OnCanceled.RemoveListener(canceled);

        if (!string.IsNullOrEmpty(nextNode))
            Goto(nextNode);
    }

    #endregion

    #region Utilities
    private string Template(string s)
    {
        if (string.IsNullOrEmpty(s) || data?.meta?.variables == null) return s;

        foreach (var kv in data.meta.variables)
        {
            if (kv.Key == null) continue;
            string token = "{" + kv.Key + "}";
            if (s.Contains(token))
                s = s.Replace(token, kv.Value?.ToString() ?? "");
        }

        if (s.Contains("{"))
        {
            Debug.LogWarning($"[Template] Unresolved variable in text: {s}");
        }

        return s;
    }

    private bool HasItemInStoryState(string itemId)
    {
        return data?.state?.inventory != null && data.state.inventory.Contains(itemId);
    }

    private void BuildPickupBlock(string startNodeId, out List<string> requiredItemIds, out string resumeNodeId)
    {
        requiredItemIds = new List<string>();
        resumeNodeId = null;

        if (!_nodeIndexById.TryGetValue(startNodeId, out var startIdx))
            return;

        for (int i = startIdx; i < _currentNodes.Count; i++)
        {
            var node = _currentNodes[i];
            if (node is PickupNode p)
            {
                if (!string.IsNullOrEmpty(p.itemId))
                    requiredItemIds.Add(p.itemId);
            }
            else
            {
                resumeNodeId = node.id;
                return;
            }
        }

        resumeNodeId = null;
    }

    private string PickRandomInspectLine(ItemDef item)
    {
        if (item.onInspectLines == null || item.onInspectLines.Count == 0)
            return $"{item.name} obtained.";

        int idx = UnityEngine.Random.Range(0, item.onInspectLines.Count);
        return item.onInspectLines[idx];
    }

    private string GetNextScene(NodeDef node)
    {
        if (node == null) return null;
        return node.nextScene;
    }



    #endregion

    #region Debug / Dev Tools
    public void SkipToNextNode()
    {
        if (_currentScene == null)
        {
            Debug.LogError("[dialog] Current scene is null.");
            return;
        }

        if (_currentNodes == null || _currentNodes.Count == 0)
        {
            Debug.LogError("[dialog] No nodes to skip.");
            return;
        }

        int currentIdx = -1;

        if (!string.IsNullOrEmpty(_nextNodeId) && _nodeIndexById.TryGetValue(_nextNodeId, out var pendingIdx))
        {
            currentIdx = pendingIdx - 1;
        }

        if (currentIdx < 0)
        {
            currentIdx = 0;
        }

        int nextIdx = currentIdx + 1;

        if (nextIdx >= _currentNodes.Count)
        {
            Debug.LogWarning("[dialog] End of nodes reached.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(RunScene(_currentScene, _currentNodes[nextIdx].id));
    }

    public void SkipToNextScene()
    {
        if (data == null || data.scenes == null || data.scenes.Count == 0)
        {
            Debug.LogError("[dialog] No scene data.");
            return;
        }

        if (_currentScene == null)
        {
            Debug.LogError("[dialog] Current scene is null.");
            return;
        }

        int currentSceneIndex = data.scenes.FindIndex(s => s.id == _currentScene.id);

        if (currentSceneIndex < 0)
        {
            Debug.LogError("[dialog] Current scene not found in story data.");
            return;
        }

        int nextIndex = currentSceneIndex + 1;

        if (nextIndex >= data.scenes.Count)
        {
            Debug.LogWarning("[dialog] No next scene.");
            return;
        }

        var nextScene = data.scenes[nextIndex];
        if (nextScene == null)
        {
            Debug.LogError("[dialog] Next scene is null.");
            return;
        }

        string nextStartNodeId = null;
        if (nextScene.nodes != null && nextScene.nodes.Count > 0)
            nextStartNodeId = nextScene.nodes[0]?.id;

        Debug.Log($"[CMD] Force move to scene: {nextScene.id}");

        StopAllCoroutines();
        if (_unitySceneNameByStorySceneId.TryGetValue(nextScene.id, out var unitySceneName) &&
            !string.IsNullOrWhiteSpace(unitySceneName))
        {
            Debug.Log($"[dialog] Force loading Unity scene: {unitySceneName}");

            _waitingStorySceneId = nextScene.id;
            _waitingStoryStartNodeId = nextStartNodeId;

            SceneManager.LoadScene(unitySceneName);
        }
        else
        {
            Debug.Log($"[dialog] No Unity scene binding for {nextScene.id}. Staying in current Unity scene.");

            StartScene(nextScene.id, nextStartNodeId);
        }
    }
    #endregion

    public void RestartCurrentStoryScene()
    {
        if (_currentScene == null)
            return;

        string retryScene = _currentScene.retryScene;
        string retryNode = _currentScene.retryNode;

        if (string.IsNullOrEmpty(retryScene))
            retryScene = _currentScene.id;

        _waitingStorySceneId = retryScene;
        _waitingStoryStartNodeId = retryNode;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }
}
