using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI
{
    static class GUIUtils
    {
        public static Rect ClampToScreen(Rect window)
        {
            window.x = Mathf.Clamp(window.x, -window.width + 20, Screen.width - 20);
            window.y = Mathf.Clamp(window.y, -window.height + 20, Screen.height - 20);

            return window;
        }

        public static double TextEntryForDouble(string label, int labelWidth, double prevValue)
        {
            string valString = prevValue.ToString("F5");
            TextEntryField(label, labelWidth, ref valString);

            if (!Regex.IsMatch(valString, @"^[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?$"))
                return prevValue;

            return double.Parse(valString);
        }

        public static int TextEntryForInt(string label, int labelWidth, int prevValue)
        {
            string valString = prevValue.ToString();
            TextEntryField(label, labelWidth, ref valString);

            if (!Regex.IsMatch(valString, @"^[-+]?[0-9]*"))
                return prevValue;

            return int.Parse(valString);
        }

        public static void TextEntryField(string label, int labelWidth, ref string inputOutput)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(labelWidth));
            inputOutput = GUILayout.TextField(inputOutput);
            GUILayout.EndHorizontal();
        }

        public static Vector3 GetMousePos()
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            return mousePos;
        }
    }
}
