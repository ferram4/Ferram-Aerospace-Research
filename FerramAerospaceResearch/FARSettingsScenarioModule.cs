using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using KSP;
using FerramAerospaceResearch.FARGUI;
using FerramAerospaceResearch.FARGUI.FAREditorGUI;
using ferram4;

namespace FerramAerospaceResearch
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.SPACECENTER)]
    public class FARSettingsScenarioModule : ScenarioModule
    {
        public static FARSettingsScenarioModule instance;
        public static FARDifficultySettings settings;

        FARSettingsScenarioModule()
        {
            instance = this;
        }

        void Start()
        {
            if (!CompatibilityChecker.IsAllCompatible())
            {
                this.enabled = false;
                return;
            }

            if (settings.newGame)
                PopupDialog.SpawnPopupDialog("Ferram Aerospace Research", "Welcome to KSP with FAR!\n\r\n\rThings will be much harder from here on out; the FAR button in the top-right corner will bring you to difficulty settings if you ever decide to change them.  Have fun!", "OK", false, HighLogic.Skin);

            settings.newGame = false;
        }
        public override void OnSave(ConfigNode node)
        {
            node.AddValue("newGame", settings.newGame);
            node.AddValue("fractionTransonicDrag", settings.fractionTransonicDrag);
            node.AddValue("gaussianVehicleLengthFractionForSmoothing", settings.gaussianVehicleLengthFractionForSmoothing);
            node.AddValue("numAreaSmoothingPasses", settings.numAreaSmoothingPasses);
            node.AddValue("numDerivSmoothingPasses", settings.numDerivSmoothingPasses);
            node.AddValue("customSettings", FARDifficultySettings.customSettings);
            base.OnSave(node);
        }

        public override void OnLoad(ConfigNode node)
        {
            if(settings == null)
                settings = new FARDifficultySettings();
            if (node.HasValue("newGame"))
                settings.newGame = bool.Parse(node.GetValue("newGame"));

            if (node.HasValue("fractionTransonicDrag"))
                settings.fractionTransonicDrag = double.Parse(node.GetValue("fractionTransonicDrag"));
            if (node.HasValue("gaussianVehicleLengthFractionForSmoothing"))
                settings.gaussianVehicleLengthFractionForSmoothing = double.Parse(node.GetValue("gaussianVehicleLengthFractionForSmoothing"));

            if (node.HasValue("numAreaSmoothingPasses"))
                settings.numAreaSmoothingPasses = int.Parse(node.GetValue("numAreaSmoothingPasses"));
            if (node.HasValue("numDerivSmoothingPasses"))
                settings.numDerivSmoothingPasses = int.Parse(node.GetValue("numDerivSmoothingPasses"));

            if (node.HasValue("customSettings"))
                FARDifficultySettings.customSettings = bool.Parse(node.GetValue("customSettings"));
            base.OnLoad(node);
        }
    }

    public class FARDifficultySettings
    {
        public bool newGame = true;
        public double fractionTransonicDrag = 0.625;
        public double gaussianVehicleLengthFractionForSmoothing = 0.015;
        public int numAreaSmoothingPasses = 1;
        public int numDerivSmoothingPasses = 1;

        public static bool customSettings = false;

        static FARDifficultySettings[] presets;
        static string[] presetNames;
        static GUIDropDown<FARDifficultySettings> dropdown;

        public static FARDifficultySettings currentSettings;

        public FARDifficultySettings()
        {
            if(presets == null)
            {
                presets = new FARDifficultySettings[5];
                presetNames = new string[5];

                FARDifficultySettings tmp = new FARDifficultySettings(0.4, 0.03, 2, 2);
                presets[0] = tmp;
                presetNames[0] = "Low Drag, Lenient Design";

                tmp = new FARDifficultySettings(0.6, 0.03, 2, 2);
                presets[1] = tmp;
                presetNames[1] = "Moderate Drag, Lenient Design";

                tmp = new FARDifficultySettings(0.6, 0.015, 1, 1);
                presets[2] = tmp;
                presetNames[2] = "Moderate Drag, Strict Design";

                tmp = new FARDifficultySettings(0.9, 0.015, 1, 1);
                presets[3] = tmp;
                presetNames[3] = "High Drag, Strict Design";

                tmp = new FARDifficultySettings(1, 0.005, 1, 1);
                presets[4] = tmp;
                presetNames[4] = "Full Drag, No Leniency";

                dropdown = new GUIDropDown<FARDifficultySettings>(presetNames, presets, 2);
            }
            currentSettings = FARSettingsScenarioModule.settings;
        }

        private FARDifficultySettings(double transDrag, double gaussianLength, int areaPass, int derivPass)
        {
            newGame = false;
            fractionTransonicDrag = transDrag;
            gaussianVehicleLengthFractionForSmoothing = gaussianLength;
            numAreaSmoothingPasses = areaPass;
            numDerivSmoothingPasses = derivPass;
        }

        public static void DisplaySelection()
        {
            GUILayout.Label("Transonic Drag Settings");
            GUILayout.Label("Absolute magnitude of drag can be scaled, as can how lenient FAR is about enforcing proper area ruling.");

            GUILayout.BeginHorizontal();
            if (!customSettings)
            {
                dropdown.GUIDropDownDisplay(GUILayout.Width(300));
                FARSettingsScenarioModule.settings = dropdown.ActiveSelection;
            }
            else
            {
                GUILayout.BeginVertical();
                FARDifficultySettings settings = FARSettingsScenarioModule.settings;
                settings.fractionTransonicDrag = GUIUtils.TextEntryForDouble("Frac Mach 1 Drag: ", 150, settings.fractionTransonicDrag);
                GUILayout.Label("The below are used in controlling leniency of design.  Higher values for all will result in more leniency");
                settings.gaussianVehicleLengthFractionForSmoothing = GUIUtils.TextEntryForDouble("% Vehicle Length for Smoothing", 250, settings.gaussianVehicleLengthFractionForSmoothing);
                settings.numAreaSmoothingPasses = GUIUtils.TextEntryForInt("Smoothing Passes, Cross-Sectional Area", 250, settings.numAreaSmoothingPasses);
                if (settings.numAreaSmoothingPasses < 0)
                    settings.numAreaSmoothingPasses = 0;
                settings.numDerivSmoothingPasses = GUIUtils.TextEntryForInt("Smoothing Passes, area 2nd deriv", 250, settings.numDerivSmoothingPasses);
                if (settings.numDerivSmoothingPasses < 0)
                    settings.numDerivSmoothingPasses = 0;
                GUILayout.EndVertical();
            }
            if (GUILayout.Button(customSettings ? "Switch Back To Presets" : "Choose Custom Settings"))
                customSettings = !customSettings;
            GUILayout.EndHorizontal();
            currentSettings = FARSettingsScenarioModule.settings;
        }
    }
}
            
