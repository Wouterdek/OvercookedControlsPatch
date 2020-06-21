using System;
using System.Collections.Generic;
using System.IO;
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
    }
}
