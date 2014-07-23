/*
Ferram Aerospace Research v0.14.1
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
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
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using KSP;

namespace ferram4
{
    public static class FARPartClassification
    {
        public static bool loaded = false;

        public static List<string> greebleTitles = new List<string>();
        public static List<string> greebleModules = new List<string>();

        public static List<string> exemptModules = new List<string>();

        public static List<string> payloadFairingTitles = new List<string>();
        public static List<string> cargoBayTitles = new List<string>();

        public static void SaveCustomClassificationTemplates()
        {
            ConfigNode node = new ConfigNode("@FARPartClassification[Default]:FINAL");
            node.AddNode(new ConfigNode("!GreebleTitle"));
            node.AddNode(StringOverrideNode(greebleTitles, "GreebleTitle", "titleContains"));
            node.AddNode(new ConfigNode("!GreebleModule"));
            node.AddNode(StringOverrideNode(greebleModules, "GreebleModule", "hasModule"));
            node.AddNode(new ConfigNode("!ExemptModule"));
            node.AddNode(StringOverrideNode(exemptModules, "ExemptModule", "hasModule"));
            node.AddNode(new ConfigNode("!PayloadFairing"));
            node.AddNode(StringOverrideNode(payloadFairingTitles, "PayloadFairing", "title"));
            node.AddNode(new ConfigNode("!CargoBay"));
            node.AddNode(StringOverrideNode(cargoBayTitles, "CargoBay", "title"));

            ConfigNode saveNode = new ConfigNode();
            saveNode.AddNode(node);
            saveNode.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/CustomFARPartClassification.cfg");
        }

        private static ConfigNode StringOverrideNode(List<string> stringList, string nodeName, string fieldName)
        {
            ConfigNode node = new ConfigNode(nodeName);
            int i = 0;

            foreach (string s in stringList)
            {
                string tmp = fieldName;
                i++;
                node.AddValue(tmp, s);
            }

            return node;
        }

        public static void LoadClassificationTemplates()
        {
            if (loaded)
                return;
            greebleTitles.Clear();
            greebleModules.Clear();
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("FARPartClassification"))
            {
                if (node == null)
                    continue;
                if (node.HasNode("GreebleTitle"))
                {
                    ConfigNode titles = node.GetNode("GreebleTitle");
                    foreach (string titleString in titles.GetValues("titleContains"))
                    {
                        greebleTitles.Add(titleString.ToLowerInvariant());
                    }
                }
                if (node.HasNode("GreebleModule"))
                {
                    ConfigNode modules = node.GetNode("GreebleModule");
                    foreach (string moduleString in modules.GetValues("hasModule"))
                    {
                        greebleModules.Add(moduleString);
                    }
                }
                if(node.HasNode("ExemptModule"))
                {
                    ConfigNode modules = node.GetNode("ExemptModule");
                    foreach (string moduleString in modules.GetValues("hasModule"))
                    {
                        exemptModules.Add(moduleString);
                    }
                }

                if (node.HasNode("PayloadFairing"))
                {
                    ConfigNode fairing = node.GetNode("PayloadFairing");

                    foreach (string title in fairing.GetValues("title"))
                    {
                        payloadFairingTitles.Add(title);
                    }
                }
                if (node.HasNode("CargoBay"))
                {
                    ConfigNode fairing = node.GetNode("CargoBay");

                    foreach (string title in fairing.GetValues("title"))
                    {
                        cargoBayTitles.Add(title);
                    }
                }

            }
            loaded = true;
        }

        public static bool IncludePartInGreeble(Part p, string title)
        {
            foreach (string moduleString in greebleModules)
                if (p.Modules.Contains(moduleString))
                    return true;

            foreach (string titleString in greebleTitles)
                if (title.Contains(titleString))
                    return true;

            return false;
            //return p.Modules.Contains("ModuleRCS") || p.Modules.Contains("ModuleDeployableSolarPanel") || p.Modules.Contains("ModuleLandingGear") || p.Modules.Contains("FSwheel") || title.Contains("heatshield") || (title.Contains("heat") && title.Contains("shield")) || title.Contains("ladder") || title.Contains("mobility") || title.Contains("railing");
        }

        public static bool ExemptPartFromGettingDragModel(Part p, string title)
        {
            foreach (string moduleString in exemptModules)
                if (p.Modules.Contains(moduleString))
                    return true;

            //p.Modules.Contains("LaunchClamp") || p.Modules.Contains("FARBaseAerodynamics") || p.Modules.Contains("KerbalEVA") || p.Modules.Contains("ModuleControlSurface") || p.Modules.Contains("ModuleResourceIntake") || p.Modules.Contains("ModuleParachute")
            return false;
        }

        public static bool PartIsPayloadFairing(Part p, string title)
        {
            foreach (string titleString in payloadFairingTitles)
                if (title.Contains(titleString))
                    return true;

            return false;
        }

        public static bool PartIsCargoBay(Part p, string title)
        {
            foreach (string titleString in cargoBayTitles)
                if (title.Contains(titleString))
                    return true;

            return false;
        }
    }
}
