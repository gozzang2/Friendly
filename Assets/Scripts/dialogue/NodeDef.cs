// NodeDef.cs
// Unity-friendly JSON DTOs for your story format.
// - Uses Newtonsoft.Json (Json.NET) for flexible polymorphic parsing.
// - Drop this file into your project (Assets/Scripts/Story/NodeDef.cs).
//
// Install/enable Newtonsoft.Json in Unity:
//   - Unity 2021+ usually includes it as "com.unity.nuget.newtonsoft-json"
//   - Or add via Package Manager.
//
// Usage example:
//   var data = JsonConvert.DeserializeObject<StoryData>(jsonString);
//   var firstScene = data.scenes[0];
//   var firstNode  = firstScene.nodes[0];

using System;
using System.Collections.Generic;

namespace nodedef{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    #region Root

    public class StoryData
    {
        public Meta meta;
        public GameState state;
        public Dictionary<string, ItemDef> items;
        public List<SceneDef> scenes;
    }

    public class Meta{
        public string title;
        public string language;
        public string version;

        // meta.variables is a dictionary in our Json.
        public Dictionary<string, object> variables;
    }

    #endregion

    #region State / Items

    public class GameState
    {
        public Dictionary<string, bool> flags;
        public Dictionary<string, int> vars;
        public List<string> inventory;
    }

    public class ItemDef
    {
        public string name;
        public string desc;
        public List<string> onInspectLines;

        public List<string> addFlags;

        public Dictionary<string, int> addVar;
    }

    #endregion

    #region Scenes

    public class SceneDef
    {
        public string id;
        public string name;
        public string retryScene;
        public string retryNode;

        [JsonConverter(typeof(CommandListConverter))]
        public List<Command> onEnter;

        [JsonConverter(typeof(NodeListConverter))]
        public List<NodeDef> nodes;
    }

    #endregion

    #region Nodes (polymorphic)

    public enum NodeType
    {
        narration,
        dialogue,
        pickup,
        interaction,
        choice,
        action,
        timeSkip,
        uiObjective,
        ending
    }

    [JsonConverter(typeof(NodeConverter))]
    public abstract class NodeDef
    {
        public string id;

        [JsonIgnore]
        public abstract NodeType Type { get; }

        //Common flow fields
        public string next;
        public string nextNode; //used when jumping to another scenes and picking a node
        public string nextScene;

        //for some nodes that have effects array(dialogue, choice)
        [JsonConverter(typeof(CommandListConverter))]
        public List<Command> effects;
    }

    public sealed class NarrationNode : NodeDef
    {
        public override NodeType Type => NodeType.narration;
        public string text;
    }

    public sealed class DialogueNode : NodeDef
    {
        public override NodeType Type => NodeType.dialogue;
        public string speaker;
        public string text;
    }

    public sealed class PickupNode : NodeDef
    {
        public override NodeType Type => NodeType.pickup;
        public string itemId;
        public string worldObjectId;

        [JsonConverter(typeof(CommandListConverter))]
        public List<Command> whenPicked;
    }

    public sealed class InteractionNode : NodeDef
    {
        public override NodeType Type => NodeType.interaction;
        public string target;
        public bool optional;

        [JsonConverter(typeof(CommandListConverter))]
        public List<Command> whenInteract;
    }

    public sealed class ChoiceNode : NodeDef
    {
        public override NodeType Type => NodeType.choice;
        public string prompt;
        public List<ChoiceOption> choices;
    }

    public sealed class ChoiceOption
    {
        public string text;

        [JsonConverter(typeof(CommandListConverter))]
        public List<Command> effects;

        public string next;
        public string nextScene;
        public string nextNode;
    }

    public sealed class ActionNode : NodeDef
    {
        public override NodeType Type => NodeType.action;

        // Your JSON uses "do": [ { "type": ... }, ... ]
        [JsonProperty("do")]
        [JsonConverter(typeof(CommandListConverter))]
        public List<Command> Do;
    }

    public sealed class TimeSkipNode : NodeDef
    {
        public override NodeType Type => NodeType.timeSkip;
        public float seconds;
        public string text;
    }

    public sealed class UiObjectiveNode : NodeDef
    {
        public override NodeType Type => NodeType.uiObjective;
        public string text;
    }

    public sealed class EndingNode : NodeDef
    {
        public override NodeType Type => NodeType.ending;
        public string endingId;
        public string title;
        public string text;
    }

    #endregion

    #region Commands (polymorphic-ish, but stored as a single flexible type)

    /// <summary>
    /// A flexible command that preserves arbitrary fields by type.
    /// This keeps your format extensible without creating 20+ command classes.
    /// You can switch on cmd.type and read the fields you need.
    /// </summary>
    [JsonConverter(typeof(CommandConverter))]
    public sealed class Command
    {
        public bool savePersistent = true;

        public string type;
        public string next;

        // Common optional fields across command types
        public string text;
        public string speaker;

        public string flag;
        public bool? value;

        public string var;
        public int? delta; // some systems like to store "value"; we map below.

        public string name;    // sound/emote/etc.
        public string id;      // video id, etc.
        public string target;  // emote target, etc.
        public string mode;    // light/vignette modes
        public float? intensity;
        public float? strength;

        // inputCode
        public string expected;
        public string onSuccess;
        public string onFail;
        public string onCancel;

        // branch
        public List<Condition> conditions;

        [JsonConverter(typeof(CommandListConverter))]
        public List<Command> then;

        [JsonConverter(typeof(CommandListConverter))]
        public List<Command> @else;

        public Dictionary<string, JToken> extra;
    }

    public sealed class Condition
    {
        public string flag;
        public string flagNot;
    }

    #endregion

    #region Converters

    public sealed class NodeListConverter : JsonConverter<List<NodeDef>>
    {
        public override List<NodeDef> ReadJson(JsonReader reader, Type objectType, List<NodeDef> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var arr = JArray.Load(reader);
            var list = new List<NodeDef>(arr.Count);
            foreach (var token in arr)
            {
                var node = token.ToObject<NodeDef>(serializer);
                list.Add(node);
            }
            return list;
        }

        public override void WriteJson(JsonWriter writer, List<NodeDef> value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

    public sealed class NodeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(NodeDef).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var typeStr = obj.Value<string>("type") ?? "";

            NodeDef node = typeStr switch
            {
                "narration"  => new NarrationNode(),
                "dialogue"   => new DialogueNode(),
                "pickup"     => new PickupNode(),
                "interaction"=> new InteractionNode(),
                "choice"     => new ChoiceNode(),
                "action"     => new ActionNode(),
                "timeSkip"   => new TimeSkipNode(),
                "uiObjective"=> new UiObjectiveNode(),
                "ending"     => new EndingNode(),
                _ => throw new JsonSerializationException($"Unknown node type: '{typeStr}'")
            };
                    // Populate common + specific fields
            serializer.Populate(obj.CreateReader(), node);
            return node;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // When writing back, include a "type" field.
            var jo = JObject.FromObject(value, serializer);
            if (value is NodeDef n)
                jo["type"] = n.Type.ToString();
            jo.WriteTo(writer);
        }
    }

    public sealed class CommandConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Command);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var cmd = new Command();

            // type (required for behavior)
            cmd.type = obj.Value<string>("type");

            // Populate known fields safely
            cmd.text = obj.Value<string>("text");
            cmd.speaker = obj.Value<string>("speaker");

            cmd.flag = obj.Value<string>("flag");
            cmd.value = obj["value"]?.Type == JTokenType.Boolean ? obj.Value<bool?>("value") : obj.Value<bool?>("value");

            // var ops: your JSON uses {"type":"addVar","var":"trust_npc","value":1}
            cmd.var = obj.Value<string>("var");
            cmd.delta = obj.Value<int?>("value");

            cmd.name = obj.Value<string>("name");
            cmd.id = obj.Value<string>("id");
            cmd.target = obj.Value<string>("target");

            cmd.mode = obj.Value<string>("mode");
            cmd.intensity = obj.Value<float?>("intensity");
            cmd.strength = obj.Value<float?>("strength");

            // inputCode
            cmd.expected = obj.Value<string>("expected");
            cmd.onSuccess = obj.Value<string>("onSuccess");
            cmd.onFail = obj.Value<string>("onFail");
            cmd.onCancel = obj.Value<string>("onCancel");

            // branch
            cmd.conditions = obj["conditions"]?.ToObject<List<Condition>>(serializer);

            if (obj["then"] is JArray thenArr)
                cmd.then = thenArr.ToObject<List<Command>>(serializer);

            if (obj["else"] is JArray elseArr)
                cmd.@else = elseArr.ToObject<List<Command>>(serializer);

            // Store extras (anything not already captured)
            var extras = new Dictionary<string, JToken>();
            foreach (var prop in obj.Properties())
            {
                if (IsKnownCommandField(prop.Name)) continue;
                extras[prop.Name] = prop.Value;
            }
            cmd.extra = extras.Count > 0 ? extras : null;

            return cmd;
        }

        private static bool IsKnownCommandField(string name)
        {
            switch (name)
            {
                case "type":
                case "text":
                case "speaker":
                case "flag":
                case "value":
                case "var":
                case "name":
                case "id":
                case "target":
                case "mode":
                case "intensity":
                case "strength":
                case "expected":
                case "onSuccess":
                case "onFail":
                case "onCancel":
                case "conditions":
                case "then":
                case "else":
                    return true;
                default:
                    return false;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Basic write-back
            serializer.Serialize(writer, value);
        }
    }

    #endregion
}