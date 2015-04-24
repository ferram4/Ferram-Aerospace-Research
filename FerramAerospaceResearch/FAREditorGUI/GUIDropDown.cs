using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FAREditorGUI
{
    class GUIDropDown<T>
    {
        int selectedOption;
        bool listActive = false;
        bool hasActivated = false;
        Vector2 scroll;

        string[] optionStrings;
        T[] options;

        static GUIStyle listStyle;
        static GUIStyle buttonStyle;
        static FARGUIDropDownDisplay displayObject;

        public GUIDropDown(string[] stringOptions, T[] typeOptions) : this(stringOptions, typeOptions, 0) { }

        public GUIDropDown(string[] stringOptions, T[] typeOptions, int defaultOption)
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
                Vector3 upperLeft = GUIUtils.GetMousePos();
                displayObject.ActivateDisplay(this.GetHashCode(), new Rect(upperLeft.x - 5, upperLeft.y - 5, 100, 150), ListDisplay, listStyle);
                hasActivated = true;
                InputLockManager.SetControlLock(ControlTypes.All, "DropdownScrollLock");
            }
            if (hasActivated)
                if (!displayObject.displayRect.Contains(GUIUtils.GetMousePos()))
                {
                    listActive = false;
                    hasActivated = false;
                    displayObject.DisableDisplay();
                    InputLockManager.RemoveControlLock("DropdownScrollLock");
                }
        }

        private void ListDisplay(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll, listStyle);
            for (int i = 0; i < optionStrings.Length; i++)
            {
                if (GUILayout.Button(optionStrings[i], buttonStyle, GUILayout.Height(20)))
                {
                    Debug.Log("Selected " + optionStrings[i]);
                    selectedOption = i;
                    listActive = false;
                    hasActivated = false;
                    displayObject.DisableDisplay();
                    InputLockManager.RemoveControlLock("DropdownScrollLock");
                }
            }
            GUILayout.EndScrollView();
        }
    }

    public class FARGUIDropDownDisplay : MonoBehaviour
    {
        public Rect displayRect;
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
                displayRect = GUILayout.Window(windowID, displayRect, windowFunction, "", listStyle);
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