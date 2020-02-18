using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Repeat.utilities;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repeat.compiler;
using Repeat.ipc;
using log4net;

namespace Repeat.userDefinedAction {
    class TaskManager {

        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string SUCCESS = "Success";
        private const string FAILURE = "Failure";

        private RepeatClient client;
        private CSCompiler compiler;
        private UserDefinedAction emptyAction;
        private Dictionary<string, UserDefinedAction> actions;

        public TaskManager(RepeatClient client) {
            this.client = client;
            this.compiler = new CSCompiler(".");
            actions = new Dictionary<string, UserDefinedAction>();
            emptyAction = new EmptyAction("");
        }

        public JObject ProcessMessage(JObject message) {
            string action = "";
            JToken parametersJSON = null;
            foreach (JProperty property in message.Properties()) {
                if (property.Name == "task_action") {
                    action = property.Value.ToString();
                } else if (property.Name == "parameters") {
                    parametersJSON = property.Value;
                }
            }
            JArray parameters = parametersJSON.Value<JArray>();

            if (action == "create_task") {
                string fileName = parameters.Children().First().Value<string>();
                JObject result = CreateTask(fileName);
                return result;
            } else if (action == "run_task") {
                JEnumerable<JToken> parameterList = parameters.Children();
                string taskID = parameterList.First().Value<string>();
                Activation activation = parseActivationFromRunTaskParameters(parameters);

                return RunTask(taskID, activation);
            } else if (action == "remove_task") {
                string taskID = parameters.Children().First().Value<string>();
                return RemoveTask(taskID);
            } else {
                return GenerateReply(FAILURE, "Unknown action " + action);
            }
        }

        private JObject RunTask(string id, Activation invoker) {
            Console.WriteLine("Doing id " + id);
            UserDefinedAction toDo;
            if (actions.TryGetValue(id, out toDo)) {
                toDo.controller = this.client;
                toDo.activation = invoker;
                toDo.invoker = invoker.hotkeys; // Legacy

                try {
                    toDo.Action();
                } catch (Exception e) {
                    Console.WriteLine("CCCCCCCC " + e.StackTrace + " -- " + e.Message);
                    return GenerateReply(FAILURE, "Encountered exception while executing task\n" + e.StackTrace);
                }

                return GenerateReply(SUCCESS, GenerateTaskReply(id, toDo));
            } else {
                return GenerateReply(FAILURE, "Unknown action with id " + id);
            }
        }

        private JObject CreateTask(string filePath) {
            if (!File.Exists(filePath)) {
                return GenerateReply(FAILURE, "File " + filePath + " does not exist.");
            } else {
                string sourceCode = FileUtility.ReadFile(filePath);
                if (sourceCode == null) {
                    return GenerateReply(FAILURE, "Unreadable file " + filePath);
                }

                UserDefinedAction action = null;
                try {
                    action = compiler.Compile(sourceCode);
                } catch (Exception e) {
                    logger.Warn("Unable to compile source code\n" + e.StackTrace);
                }
                if (action == null) {
                    return GenerateReply(FAILURE, "Cannot compile file " + filePath);
                } else {
                    string newId = System.Guid.NewGuid().ToString();
                    actions[newId] = action;
                    action.FileName = filePath;
                    logger.Info("Successfully compiled source code.");
                    return GenerateReply(SUCCESS, GenerateTaskReply(newId, action));
                }
            }
        }

        private JObject RemoveTask(string id) {
            UserDefinedAction toRemove;
            if (actions.TryGetValue(id, out toRemove)) {
                actions.Remove(id);
                return GenerateReply(SUCCESS, GenerateTaskReply(id, toRemove));
            } else {
                return GenerateReply(SUCCESS, GenerateTaskReply(id, emptyAction));
            }
        }

        private Activation parseActivationFromRunTaskParameters(JArray parameters) {
            JEnumerable<JToken> parameterList = parameters.Children();
            string taskID = parameterList.First().Value<string>();
            JObject invokerJSON = parameterList.Skip(1).First().Value<JObject>();

            List<int> hotkeys = new List<int>();
            List<int> keySequence = new List<int>();
            ActivationVariable activationVariable = null;
            string activationPhrase = null;
            string mouseGesture = null;
            foreach (JProperty property in invokerJSON.Properties()) {
                JToken token = property.Value;
                if (property.Name == "hotkey") {
                    // Get the first hotkey, or leave as empty list.
                    JArray hotkeyListJSON = token.Value<JArray>();
                    foreach (JArray hotkey in hotkeyListJSON.Children<JArray>()) {
                        foreach (JObject keyObject in hotkey.Children()) {
                            foreach (JProperty keyProperty in keyObject.Properties()) {
                                JToken keyToken = keyProperty.Value;
                                if (keyProperty.Name == "key") {
                                    hotkeys.Add(keyToken.Value<int>());
                                }
                            }
                        }
                        break;
                    }
                } else if (property.Name == "key_sequence") {
                    // Get the first key sequence, or leave as empty list.
                    JArray hotkeyListJSON = token.Value<JArray>();
                    foreach (JArray hotkey in hotkeyListJSON.Children<JArray>()) {
                        foreach (JObject keyObject in hotkey.Children()) {
                            foreach (JProperty keyProperty in keyObject.Properties()) {
                                JToken keyToken = keyProperty.Value;
                                if (keyProperty.Name == "key") {
                                    hotkeys.Add(keyToken.Value<int>());
                                }
                            }
                        }
                        break;
                    }
                } else if (property.Name == "variables") {
                    // Get the first variable, or leave as empty.
                    string varNamespace = "";
                    string name = "";
                    JArray variableList = token.Value<JArray>();
                    foreach (JObject variable in variableList.Children<JObject>()) {
                        foreach (JProperty innerVariable in variable.Properties()) {
                            if (innerVariable.Name != "variable") {
                                continue;
                            }
                            JToken innerVariableToken = innerVariable.Value;
                            JObject innerVariableObject = innerVariableToken.Value<JObject>();
                            foreach (JProperty prop in innerVariableObject.Properties()) {
                                JToken keyToken = prop.Value;
                                if (prop.Name == "namespace") {
                                    varNamespace = prop.Value.ToString();
                                } else if (prop.Name == "name") {
                                    name = prop.Value.ToString();
                                }
                            }
                        }

                        activationVariable = new ActivationVariable{
                            varNamespace = varNamespace,
                            name = name
                        };
                        break;
                    }
                } else if (property.Name == "phrases") {
                    // Get the first phrase, or leave as empty.
                    JArray phrasesList = token.Value<JArray>();
                    foreach (JObject phrase in phrasesList.Children<JObject>()) {
                        foreach (JProperty prop in phrase.Properties()) {
                            JToken keyToken = prop.Value;
                            if (prop.Name == "value") {
                                activationPhrase = prop.Value.ToString();
                            }
                        }
                        break;
                    }
                }
                else if (property.Name == "mouse_gesture") {
                    // Get the first mouse gesture, or leave as null.
                    JArray mouseGestureListJSON = token.Value<JArray>();
                    foreach (JObject gesture in mouseGestureListJSON.Children<JObject>()) {
                        foreach (JProperty prop in gesture.Properties()) {
                            if (prop.Name == "name") {
                                mouseGesture = prop.Value.ToString();
                            }
                        }
                        break;
                    }
                }
            }

            Activation activation = new Activation();
            activation.hotkeys = hotkeys;
            activation.keySequence = keySequence;
            activation.activationVariable = activationVariable;
            activation.phrase = activationPhrase;
            activation.mouseGesture = mouseGesture;
            return activation;
        }

        private JObject GenerateReply(string status, object message) {
            return new JObject(
                new JProperty("status", status),
                new JProperty("message", message),
                new JProperty("is_reply_message", true)
                );
        }

        private JObject GenerateTaskReply(string id, UserDefinedAction task) {
            return new JObject(
                new JProperty("id", id), 
                new JProperty("file_name", task.FileName)
                );
        }
    }
}
