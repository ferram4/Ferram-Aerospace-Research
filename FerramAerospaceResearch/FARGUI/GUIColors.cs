/*
Ferram Aerospace Research v0.15.1 "Fanno"
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
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI
{
    class GUIColors
    {
        List<Color> colors = null;

        static GUIColors _instance = null;
        public static GUIColors Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GUIColors(); 
                return _instance;
            }
        }

        GUIColors()
        {
            LoadColors();
        }

        public Color this[int index]
        {
            get
            {
                if (_instance == null)
                    _instance = new GUIColors(); 
                return _instance.colors[index];
            }
            set
            {
                if (_instance == null)
                    _instance = new GUIColors(); 
                _instance.colors[index] = value;
            }
        }

        public static Color GetColor(int index)
        {
            if (_instance == null)
                _instance = new GUIColors();

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
        }

        public void SaveColors()
        {
            ConfigNode node = new ConfigNode("@FARGUIColors[default]:FOR[FerramAerospaceResearch]");
            node.AddValue("%ClColor", SaveColor(colors[0]));
            node.AddValue("%CdColor", SaveColor(colors[1]));
            node.AddValue("%CmColor", SaveColor(colors[2]));
            node.AddValue("%L_DColor", SaveColor(colors[3]));

            ConfigNode saveNode = new ConfigNode();
            saveNode.AddNode(node);
            saveNode.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/CustomFARGUIColors.cfg");
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

        private string SaveColor(Color color)
        {
            StringBuilder builder = new StringBuilder();

            //Should return string in format of color.r, color.g, color.b
            builder.Append(color.r);
            builder.Append(",");
            builder.Append(color.g);
            builder.Append(",");
            builder.Append(color.b);

            return builder.ToString();
        }
    }
}
