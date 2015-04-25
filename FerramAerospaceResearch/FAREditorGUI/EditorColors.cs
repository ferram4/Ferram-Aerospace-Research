using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FAREditorGUI
{
    class EditorColors
    {
        List<Color> colors = null;

        static EditorColors _instance = null;
        public static EditorColors Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new EditorColors(); 
                return _instance;
            }
        }

        EditorColors()
        {
            LoadColors();
        }

        public Color this[int index]
        {
            get
            {
                if (_instance == null)
                    _instance = new EditorColors(); 
                return _instance.colors[index];
            }
            set
            {
                if (_instance == null)
                    _instance = new EditorColors(); 
                _instance.colors[index] = value;
            }
        }

        public static Color GetColor(int index)
        {
            if (_instance == null)
                _instance = new EditorColors();

            return _instance.colors[index];
        }

        public void LoadColors()
        {
            colors = new List<Color>();

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("FARGUIColors"))
            {
                if (node.HasValue("ClColor"))
                    colors.Add(ReadColor(node.GetValue("ClColor")));

                if (node.HasValue("CdColor"))
                    colors.Add(ReadColor(node.GetValue("CdColor")));

                if (node.HasValue("CmColor"))
                    colors.Add(ReadColor(node.GetValue("CmColor")));

                if (node.HasValue("L_DColor"))
                    colors.Add(ReadColor(node.GetValue("L_DColor")));
            }
            Debug.Log(colors.Count + " colors");
        }

        private Color ReadColor(string input)
        {
            char[] separators = { ',', ' ', ';' };
            string[] splitValues = input.Split(separators);

            int curIndex = 0;
            Color color = new Color();
            color.a = 1;
            for (int i = 0; i < splitValues.Length; i++)
            {
                string s = splitValues[i];
                if (s.Length > 0)
                {
                    float val;
                    if (float.TryParse(s, out val))
                    {
                        if (curIndex == 0)
                            color.r = val;
                        else if (curIndex == 1)
                            color.g = val;
                        else
                        {
                            color.b = val;
                            return color;
                        }
                        curIndex++;
                    }
                }
            }

            return color;
        }
    }
}
