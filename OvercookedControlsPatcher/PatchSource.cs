using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InControl;

namespace OvercookedControlsPatcher
{
    class PatchSource
    {
        [AddMethod(nameof(StandardActionSet))]
        private static bool LoadControlsFromFile(StandardActionSet actionSet, string filename)
        {
            if (!File.Exists(filename))
            {
                return false;
            }

            string[] lines = File.ReadAllLines(filename);
            if (lines.Length <= 1)
            {
                return false;
            }

            HashSet<string> validButtonNames = new HashSet<string>(Enum.GetNames(typeof(ControlPadInput.Button)));
            HashSet<string> validValueNames = new HashSet<string>(Enum.GetNames(typeof(ControlPadInput.Value)));
            HashSet<string> validKeyNames = new HashSet<string>(Enum.GetNames(typeof(Key)));
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("#"))
                {
                    List<char> seperators = new List<char> {'=', '#', '.'};
                    string[] lineParts = lines[i].Replace(" ", "").Split(seperators.ToArray());
                    if (lineParts.Length >= 3 && validKeyNames.Contains(lineParts[2]))
                    {
                        Key key = (Key) Enum.Parse(typeof(Key), lineParts[2], true);
                        if (lineParts[0].Equals("Button") && validButtonNames.Contains(lineParts[1]))
                        {
                            var button = (ControlPadInput.Button) Enum.Parse(typeof(ControlPadInput.Button), lineParts[1], true);
                            actionSet.ButtonActions[button].AddDefaultBinding(new Key[]
                            {
                                key
                            });
                        }
                        else if (lineParts[0].Equals("PosVal") && validValueNames.Contains(lineParts[1]))
                        {
                            var value = (ControlPadInput.Value) Enum.Parse(typeof(ControlPadInput.Value), lineParts[1], true);
                            actionSet.m_pveValueActions[value].AddDefaultBinding(new Key[]
                            {
                                key
                            });
                        }
                        else if (lineParts[0].Equals("NegVal") && validValueNames.Contains(lineParts[1]))
                        {
                            var value = (ControlPadInput.Value) Enum.Parse(typeof(ControlPadInput.Value), lineParts[1], true);
                            actionSet.m_nveValueActions[value].AddDefaultBinding(new Key[]
                            {
                                key
                            });
                        }
                    }
                }
            }

            return true;
        }

        [ReplaceMethod(nameof(StandardActionSet))]
        public static void ModifiyForCombinedKeyboard(StandardActionSet actionSet)
        {
            if (StandardActionSet.LoadControlsFromFile(actionSet, "input_combined.txt"))
            {
                return;
            }
            StandardActionSet.ModifiyForCombinedKeyboard(actionSet);
        }

        [ReplaceMethod(nameof(StandardActionSet))]
        public static void ModifiyForSplitKeyboard(StandardActionSet actionSet)
        {
            if (StandardActionSet.LoadControlsFromFile(actionSet, "input_split.txt"))
            {
                return;
            }
            StandardActionSet.ModifiyForSplitKeyboard(actionSet);
        }

        [AddField(nameof(PCPadInputProvider))]
        public static bool keyboardInitialized = false;

        [AddMethod(nameof(PCPadInputProvider))]
        public static string[] GetControlFiles()
        {
            List<string> files = new List<string>();
            for (int i = 1; i < 6; ++i) //Can't do Directory.GetFiles because of exception handler issues
            {
                var path = "input_keyboard_" + i + ".txt";
                if (File.Exists(path))
                {
                    files.Add(path);
                }
            }

            return files.ToArray();
        }

        [ReplaceMethod(nameof(PCPadInputProvider))]
        public static void UpdateKeyboardButtons()
        {
            if (!keyboardInitialized)
            {
                var controlFiles = GetControlFiles();
                if (controlFiles != null)
                {
                    foreach (var file in controlFiles)
                    {
                        global::PCPadInputProvider.m_allDevices.Add(CreateKeyboardForControlFile(file));
                    }

                    keyboardInitialized = true;
                }
            }
            global::PCPadInputProvider.UpdateKeyboardButtons();
        }

        [AddMethod(nameof(StandardActionSet))]
        public static StandardActionSet CreateKeyboardForControlFile(string filename)
        {
            StandardActionSet actionSet = new StandardActionSet();
            actionSet.ResetActions();
            LoadControlsFromFile(actionSet, filename);
            return actionSet;
        }
    }
}
