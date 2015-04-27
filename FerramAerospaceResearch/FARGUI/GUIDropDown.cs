using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI
{
    class GUIDropDown<T>
    {
        int selectedOption;
        bool listActive = false;
        bool hasActivated = false;
        Vector2 scroll;

        string[] optionStrings;
        T[] options;

        T activeSelection;
        public T ActiveSelection
        {
            get { return activeSelection; }
        }

        static GUIStyle listStyle;
        static GUIStyle buttonStyle;
        static FARGUIDropDownDisplay displayObject;

        public GUIDropDown(string[] stringOptions, T[] typeOptions) : this(stringOptions, typeOptions, 0) { }

        public GUIDropDown(string[] stringOptions, T[] typeOptions, int defaultOption)
        {
            optionStrings = stringOptions;
            options = typeOptions;

            selectedOption = defaultOption;
            activeSelection = typeOptions[selectedOption];

            if(displayObject == null)
            {
                GameObject o = new GameObject();

                o.AddComponent<FARGUIDropDownDisplay>();
                displayObject = o.GetComponent<FARGUIDropDownDisplay>();
            }
        }

        public void GUIDropDownDisplay(params GUILayoutOption[] GUIOptions)
        {
            if (GUILayout.Button(optionStrings[selectedOption], GUIOptions))
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
                displayObject.ActivateDisplay(this.GetHashCode(), new Rect(upperLeft.x - 5, upperLeft.y - 5, 100, 150), ListDisplay, listStyle, GUIOptions);
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
                    activeSelection = options[i];
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
        GUILayoutOption[] GUIOptions;

        private void Start()
        {
            this.enabled = true;
        }


        private void OnGUI()
        {
            if (windowFunction != null)
            {
                displayRect = GUILayout.Window(windowID, displayRect, windowFunction, "", listStyle, GUIOptions);
            }
        }

        public void ActivateDisplay(int id, Rect rect, GUI.WindowFunction window, GUIStyle style, params GUILayoutOption[] GUIOptions)
        {
            windowID = id;
            displayRect = rect;
            windowFunction = window;
            listStyle = style;
            this.GUIOptions = GUIOptions;
        }

        public void DisableDisplay()
        {
            windowFunction = null;

        }
    }
}