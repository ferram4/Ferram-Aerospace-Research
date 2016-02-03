/*
Ferram Aerospace Research v0.15.5.5 "Hugoniot"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2015, Michael Ferrara, aka Ferram4

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
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values  
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
                        	Regex, for adding RPM support  
				DaMichel, for some ferramGraph updates and some control surface-related features  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI
{
    public class GUIDropDown<T>
    {
        private int selectedOption;
        private bool isActive = false;
        private bool toggleBtnState = false;
        private Vector2 scrollPos;

        private string[] stringOptions;
        private T[] typeOptions;

        public T ActiveSelection
        {
            get { return typeOptions[selectedOption]; }
        }

        private static GUIStyle listStyle;
        private static GUIStyle toggleBtnStyle;
        private static GUIStyle dropdownItemStyle;
        private static GUIStyle selectedItemStyle;

        public GUIDropDown(string[] stringOptions, T[] typeOptions) : this(stringOptions, typeOptions, 0) { }

        public GUIDropDown(string[] stringOptions, T[] typeOptions, int defaultOption)
        {
            this.stringOptions = stringOptions;
            this.typeOptions = typeOptions;

            selectedOption = defaultOption;
        }

        public void GUIDropDownDisplay(params GUILayoutOption[] guiOptions)
        {
            InitStyles();

            FARGUIDropDownDisplay display = FARGUIDropDownDisplay.Instance;
            toggleBtnState = GUILayout.Toggle(toggleBtnState, "▼ " + stringOptions[selectedOption] + " ▼", toggleBtnStyle, guiOptions);

            // Calcuate absolute regions for the button and dropdown list, this only works when
            // Event.current.type == EventType.Repaint
            Vector2 relativePos = GUIUtility.GUIToScreenPoint(new Vector2(0, 0));
            Rect btnRect = GUILayoutUtility.GetLastRect();
            btnRect.x += relativePos.x;
            btnRect.y += relativePos.y;
            Rect dropdownRect = new Rect(btnRect.x, btnRect.y + btnRect.height, btnRect.width, 150);

            if (!isActive && toggleBtnState && Event.current.type == EventType.Repaint)
            {
                // User activated the dropdown
                ShowList(btnRect, dropdownRect);
            }
            else if (isActive && (!toggleBtnState || !display.ContainsMouse()))
            {
                // User deactivated the downdown or moved the mouse cursor away
                HideList();
            }
        }

        private void InitStyles()
        {
            if (listStyle == null)
            {
                listStyle = new GUIStyle(GUI.skin.window);
                listStyle.padding = new RectOffset(1, 1, 1, 1);
            }
            if (toggleBtnStyle == null)
            {
                toggleBtnStyle = new GUIStyle(GUI.skin.button);
                toggleBtnStyle.normal.textColor
                    = toggleBtnStyle.focused.textColor
                    = Color.white;
                toggleBtnStyle.hover.textColor
                    = toggleBtnStyle.active.textColor
                    = toggleBtnStyle.onActive.textColor
                    = Color.yellow;
                toggleBtnStyle.onNormal.textColor
                    = toggleBtnStyle.onFocused.textColor
                    = toggleBtnStyle.onHover.textColor
                    = Color.green;
            }
            if (dropdownItemStyle == null)
            {
                dropdownItemStyle = new GUIStyle(GUI.skin.button);
                dropdownItemStyle.padding = new RectOffset(2, 2, 2, 2);
                dropdownItemStyle.margin.top = 1;
                dropdownItemStyle.margin.bottom = 1;
            }
            if (selectedItemStyle == null)
            {
                selectedItemStyle = new GUIStyle(GUI.skin.button);
                selectedItemStyle.padding = new RectOffset(2, 2, 2, 2);
                selectedItemStyle.margin.top = 1;
                selectedItemStyle.margin.bottom = 1;
                selectedItemStyle.normal.textColor
                    = selectedItemStyle.focused.textColor
                    = selectedItemStyle.hover.textColor
                    = selectedItemStyle.active.textColor
                    = selectedItemStyle.onActive.textColor
                    = selectedItemStyle.onNormal.textColor
                    = selectedItemStyle.onFocused.textColor
                    = selectedItemStyle.onHover.textColor
                    = XKCDColors.KSPNotSoGoodOrange;
            }
        }

        private void ShowList(Rect btnRect, Rect dropdownRect)
        {
            if (!isActive)
            {
                toggleBtnState = isActive = true;
                FARGUIDropDownDisplay.Instance.ActivateDisplay(this.GetHashCode(), btnRect, dropdownRect, OnDisplayList, listStyle);
                InputLockManager.SetControlLock(ControlTypes.All, "DropdownScrollLock");
            }
        }

        private void HideList()
        {
            if (isActive)
            {
                toggleBtnState = isActive = false;
                FARGUIDropDownDisplay.Instance.DisableDisplay();
                InputLockManager.RemoveControlLock("DropdownScrollLock");
            }
        }

        private void OnDisplayList(int id)
        {
            GUI.BringWindowToFront(id);
            scrollPos = GUILayout.BeginScrollView(scrollPos, listStyle);
            for (int i = 0; i < stringOptions.Length; i++)
            {
                // Highlight the selected item
                GUIStyle tmpStyle = (selectedOption == i) ? selectedItemStyle : dropdownItemStyle;
                if (GUILayout.Button(stringOptions[i], tmpStyle))
                {
                    Debug.Log("Selected " + stringOptions[i]);
                    selectedOption = i;
                    HideList();
                }
            }
            GUILayout.EndScrollView();
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class FARGUIDropDownDisplay : MonoBehaviour
    {
        private static FARGUIDropDownDisplay instance;
        public static FARGUIDropDownDisplay Instance
        {
            get { return instance; }
        }

        private Rect btnRect;
        private Rect displayRect;
        private int windowId;
        private GUI.WindowFunction windowFunction;
        private GUIStyle listStyle;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            this.enabled = true;
            GameObject.DontDestroyOnLoad(this);
        }


        private void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            if (windowFunction != null)
            {
                displayRect = GUILayout.Window(windowId, displayRect, windowFunction, "", listStyle, GUILayout.Height(0));
            }
        }

        public bool ContainsMouse()
        {
            return btnRect.Contains(GUIUtils.GetMousePos()) ||
                   displayRect.Contains(GUIUtils.GetMousePos());
        }

        public void ActivateDisplay(int id, Rect btnRect, Rect rect, GUI.WindowFunction func, GUIStyle style)
        {
            this.windowId = id;
            this.btnRect = btnRect;
            this.displayRect = rect;
            this.windowFunction = func;
            this.listStyle = style;
        }

        public void DisableDisplay()
        {
            windowFunction = null;
        }
    }
}
