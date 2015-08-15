using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch
{
    public static class FARAnimOverrides
    {
        private static Dictionary<string, string> animOverrides;

        public static void LoadAnimOverrides()
        {
            animOverrides = new Dictionary<string, string>();
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("FARAnimOverride");
            for (int i = 0; i < nodes.Length; i++)
            {
                string moduleName = nodes[i].GetValue("moduleName");
                string animNameField = nodes[i].GetValue("animNameField");
                if (moduleName != null && animNameField != null && moduleName != string.Empty && animNameField != string.Empty)
                    animOverrides.Add(moduleName, animNameField);
            }
        }

        public static bool OverrideExists(string moduleName)
        {
            return animOverrides.ContainsKey(moduleName);
        }

        public static string FieldNameForModule(string moduleName)
        {
            try
            {
                return animOverrides[moduleName];
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
