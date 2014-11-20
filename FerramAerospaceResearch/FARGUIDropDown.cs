/*
Ferram Aerospace Research v0.14.3.2
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Ferram Aerospace Research is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */



using System;
using System.Collections.Generic;
using UnityEngine;

namespace ferram4
{
    public class FARGUIDropDown<T>
    {
        int selectedOption;
        bool listActive = false;
        bool hasActivated = false;

        Rect listRect;

        string[] optionStrings;
        T[] options;

        static GUIStyle listStyle;
        static GUIStyle buttonStyle;
        static FARGUIDropDownDisplay displayObject;

        public FARGUIDropDown(string[] stringOptions, T[] typeOptions) : this(stringOptions, typeOptions, 0) { }

        public FARGUIDropDown(string[] stringOptions, T[] typeOptions, int defaultOption)
        {
            optionStrings = stringOptions;
            options = typeOptions;

            selectedOption = defaultOption;

            if(displayObject == null)
            {
                GameObject o = new GameObject();

                o.AddComponent<FARGUIDropDownDisplay>();
                displayObject = o.GetComponent<FARGUIDropDownDisplay>();
            }
        }

        public T ActiveSelection()
        {
            return options[selectedOption];
        }

        public void GUIDropDownDisplay(params GUILayoutOption[] GUIoptions)
        {
            if (GUILayout.Button(optionStrings[selectedOption], GUIoptions))
            {
                Vector3 upperLeft = FARGUIUtils.GetMousePos();
                listRect = new Rect(upperLeft.x - 5, upperLeft.y - 5, 100, 20 * options.Length);
                listActive = true;
            }
            if (listStyle == null)
            {
                listStyle = new GUIStyle(GUI.skin.window);
                listStyle.padding = new RectOffset(1, 1, 1, 1);
            }
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.padding = new RectOffset(0, 0, 0, 0);
            }
            if (listActive && !hasActivated)
            {
                displayObject.ActivateDisplay(this.GetHashCode(), listRect, ListDisplay, listStyle);
                hasActivated = true;
                //if (!listRect.Contains(FARGUIUtils.GetMousePos()))
                //   listActive = false;
            }
            if(hasActivated)
                if (!listRect.Contains(FARGUIUtils.GetMousePos()))
                {
                    listActive = false;
                    hasActivated = false;
                    displayObject.DisableDisplay();
                }
        }

        private void ListDisplay(int id)
        {
            GUILayout.BeginVertical(listStyle, GUILayout.ExpandHeight(true));
            for (int i = 0; i < optionStrings.Length; i++)
            {
                if (GUILayout.Button(optionStrings[i], buttonStyle, GUILayout.Height(20)))
                {
                    Debug.Log("Selected " + optionStrings[i]);
                    selectedOption = i;
                    listActive = false;
                    hasActivated = false;
                    displayObject.DisableDisplay();
                }
            }
            GUILayout.EndVertical();
        }
    }

    public class FARGUIDropDownDisplay : MonoBehaviour
    {
        Rect displayRect;
        int windowID;
        GUI.WindowFunction windowFunction;
        GUIStyle listStyle;

        private void Start()
        {
            this.enabled = true;
        }


        private void OnGUI()
        {
            if (windowFunction != null)
            {
                GUILayout.Window(windowID, displayRect, windowFunction, "", listStyle);
            }
        }

        public void ActivateDisplay(int id, Rect rect, GUI.WindowFunction window, GUIStyle style)
        {
            windowID = id;
            displayRect = rect;
            windowFunction = window;
            listStyle = style;
        }

        public void DisableDisplay()
        {
            windowFunction = null;

        }
    }
}
