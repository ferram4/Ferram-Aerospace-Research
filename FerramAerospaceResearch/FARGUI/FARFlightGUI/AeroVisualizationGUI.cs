/*
Ferram Aerospace Research v0.15.6.4 "Kleinhans"
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
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI.FARFlightGUI
{
    public class AeroVisualizationGUI
    {
        private bool _tintForCl;
        public bool TintForCl
        {
            get { return _tintForCl; }
        }

        private bool _tintForCd;
        public bool TintForCd
        {
            get { return _tintForCd; }
        }

        private bool _tintForStall;
        public bool TintForStall
        {
            get { return _tintForStall; }
        }

        // Cl for full tinting (wings)
        private double _fullySaturatedCl = 0.5;
        public double FullySaturatedCl
        {
            get { return _fullySaturatedCl; }
        }

        // Cd for full tinting (wings)
        private double _fullySaturatedCd = 0.1;
        public double FullySaturatedCd
        {
            get { return _fullySaturatedCd; }
        }

        // Cl for full tinting (non-wing parts)
        private double _fullySaturatedClBody = 0.05;
        public double FullySaturatedClBody
        {
            get { return _fullySaturatedClBody; }
        }

        // Cd for full tinting (non-wing parts)
        private double _fullySaturatedCdBody = 0.01;
        public double FullySaturatedCdBody
        {
            get { return _fullySaturatedCdBody; }
        }

        // Stalled % for full tinting
        private double _fullySaturatedStall = 10.0;
        public double FullySaturatedStall
        {
            get { return _fullySaturatedStall; }
        }

        public bool AnyVisualizationActive
        {
            get { return _tintForCl || _tintForCd || _tintForStall; }
        }

        public AeroVisualizationGUI()
        {
            LoadSettings();
        }

        public void SettingsDisplay()
        {
            GUILayout.Label("Aero Visualization Settings");

            GUILayout.BeginVertical(FlightGUI.boxStyle);
            _tintForCl = GUILayout.Toggle(_tintForCl, "Tint Cl");
            _fullySaturatedCl = GUIUtils.TextEntryForDouble("Full Tint Cl (wings):", 125, _fullySaturatedCl);
            _fullySaturatedClBody = GUIUtils.TextEntryForDouble("Full Tint Cl (body):", 125, _fullySaturatedClBody);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(FlightGUI.boxStyle);
            _tintForCd = GUILayout.Toggle(_tintForCd, "Tint Cd");
            _fullySaturatedCd = GUIUtils.TextEntryForDouble("Full Tint Cd (wings):", 125, _fullySaturatedCd);
            _fullySaturatedCdBody = GUIUtils.TextEntryForDouble("Full Tint Cd (body):", 125, _fullySaturatedCdBody);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(FlightGUI.boxStyle);
            _tintForStall = GUILayout.Toggle(_tintForStall, "Tint Stall");
            _fullySaturatedStall = GUIUtils.TextEntryForDouble("Full Tint Stalled %:", 125, _fullySaturatedStall);
            GUILayout.EndVertical();

            // Allowing toggling arrows here because why not...
            PhysicsGlobals.AeroForceDisplay = GUILayout.Toggle(PhysicsGlobals.AeroForceDisplay, "Show Aero Arrows");
        }

        public void SaveSettings()
        {
            List<ConfigNode> flightGUISettings = FARSettingsScenarioModule.FlightGUISettings;
            if (flightGUISettings == null)
            {
                Debug.LogError("Could not save Aero Visualization Settings because settings config list was null");
            }
            ConfigNode node = null;
            for (int i = 0; i < flightGUISettings.Count; i++)
                if (flightGUISettings[i].name == "AeroVizSettings")
                {
                    node = flightGUISettings[i];
                    break;
                }

            if (node == null)
            {
                node = new ConfigNode("AeroVizSettings");
                flightGUISettings.Add(node);
            }
            node.ClearData();

            node.AddValue("fullySaturatedCl", _fullySaturatedCl);
            node.AddValue("fullySaturatedCd", _fullySaturatedCd);
            node.AddValue("fullySaturatedClBody", _fullySaturatedClBody);
            node.AddValue("fullySaturatedCdBody", _fullySaturatedCdBody);
            node.AddValue("fullySaturatedStall", _fullySaturatedStall);
        }

        void LoadSettings()
        {
            List<ConfigNode> flightGUISettings = FARSettingsScenarioModule.FlightGUISettings;

            ConfigNode node = null;
            for (int i = 0; i < flightGUISettings.Count; i++)
                if (flightGUISettings[i].name == "AeroVizSettings")
                {
                    node = flightGUISettings[i];
                    break;
                }

            if (node != null)
            {
                double tmp;
                if (double.TryParse(node.GetValue("fullySaturatedCl"), out tmp))
                    _fullySaturatedCl = tmp;
                if (double.TryParse(node.GetValue("fullySaturatedCd"), out tmp))
                    _fullySaturatedCd = tmp;
                if (double.TryParse(node.GetValue("fullySaturatedClBody"), out tmp))
                    _fullySaturatedClBody = tmp;
                if (double.TryParse(node.GetValue("fullySaturatedCdBody"), out tmp))
                    _fullySaturatedCdBody = tmp;
                if (double.TryParse(node.GetValue("fullySaturatedStall"), out tmp))
                    _fullySaturatedStall = tmp;
            }
        }
    }
}
