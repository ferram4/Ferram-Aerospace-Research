/*
Ferram Aerospace Research v0.14.6
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
using System.Linq;
using System.IO;
using UnityEngine;

namespace ferram4
{
    public static class FARAeroUtil
    {
        private static FloatCurve prandtlMeyerMach = null;
        private static FloatCurve prandtlMeyerAngle = null;
        public static double maxPrandtlMeyerTurnAngle = 0;
        private static FloatCurve pressureBehindShock = null;
        private static FloatCurve machBehindShock = null;
        private static FloatCurve stagnationPressure = null;
//        private static FloatCurve criticalMachNumber = null;
        //private static FloatCurve liftslope = null;
        private static FloatCurve maxPressureCoefficient = null;

        public static double areaFactor;
        public static double attachNodeRadiusFactor;
        public static double incompressibleRearAttachDrag;
        public static double sonicRearAdditionalAttachDrag;
        public static double radiusOfCurvatureBluntBody;
        public static double massPerWingAreaSupported;
        public static double massStressPower;
        public static bool AJELoaded;

        public static Dictionary<int, double[]> bodyAtmosphereConfiguration = null;
        public static int prevBody = -1;
        public static double[] currentBodyAtm = new double[5];
        public static double currentBodyTemp = 273.15;
        public static double currentBodyAtmPressureOffset = 0;

        public static bool loaded = false;
        
        //Based on ratio of density of water to density of air at SL
        private const double UNDERWATER_DENSITY_FACTOR_MINUS_ONE = 814.51020408163265306122448979592;
        //Standard Reynolds number for transition from laminar to turbulent flow
        private const double TRANSITION_REYNOLDS_NUMBER = 5e5;

        public static void SaveCustomAeroDataToConfig()
        {
            ConfigNode node = new ConfigNode("@FARAeroData[default]:FOR[FerramAerospaceResearch]");
            node.AddValue("%areaFactor", areaFactor);
            node.AddValue("%attachNodeDiameterFactor", attachNodeRadiusFactor * 2);
            node.AddValue("%incompressibleRearAttachDrag", incompressibleRearAttachDrag);
            node.AddValue("%sonicRearAdditionalAttachDrag", sonicRearAdditionalAttachDrag);
            node.AddValue("%radiusOfCurvatureBluntBody", radiusOfCurvatureBluntBody); 
            node.AddValue("%massPerWingAreaSupported", massPerWingAreaSupported);
            node.AddValue("%massStressPower", massStressPower);
            node.AddValue("%ctrlSurfTimeConstant", FARControllableSurface.timeConstant);
            node.AddValue("%ctrlSurfTimeConstantFlap", FARControllableSurface.timeConstantFlap);
            node.AddValue("%ctrlSurfTimeConstantSpoiler", FARControllableSurface.timeConstantSpoiler);

            node.AddNode(new ConfigNode("!BodyAtmosphericData,*"));

            foreach (KeyValuePair<int, double[]> pair in bodyAtmosphereConfiguration)
            {
                node.AddNode(CreateAtmConfigurationConfigNode(pair.Key, pair.Value));
            }

            ConfigNode saveNode = new ConfigNode();
            saveNode.AddNode(node);
            saveNode.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/CustomFARAeroData.cfg");
        }

        private static ConfigNode CreateAtmConfigurationConfigNode(int bodyIndex, double[] atmProperties)
        {
            ConfigNode node = new ConfigNode("BodyAtmosphericData");
            node.AddValue("index", bodyIndex);

            double gasMolecularWeight = 8314.5 / atmProperties[2];
            node.AddValue("specHeatRatio", atmProperties[1]);
            node.AddValue("gasMolecularWeight", gasMolecularWeight);
            node.AddValue("viscosityAtReferenceTemp", atmProperties[3]);
            node.AddValue("referenceTemp", atmProperties[4]);

            return node;
        }

        public static void LoadAeroDataFromConfig()
        {
            if (loaded)
                return;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("FARAeroData"))
            {
                if (node == null)
                    continue;

                if(node.HasValue("areaFactor"))
                    double.TryParse(node.GetValue("areaFactor"), out areaFactor);
                if (node.HasValue("attachNodeDiameterFactor"))
                {
                    double.TryParse(node.GetValue("attachNodeDiameterFactor"), out attachNodeRadiusFactor);
                    attachNodeRadiusFactor *= 0.5;
                }
                if (node.HasValue("incompressibleRearAttachDrag"))
                    double.TryParse(node.GetValue("incompressibleRearAttachDrag"), out incompressibleRearAttachDrag);
                if (node.HasValue("sonicRearAdditionalAttachDrag"))
                    double.TryParse(node.GetValue("sonicRearAdditionalAttachDrag"), out sonicRearAdditionalAttachDrag);

                if (node.HasValue("radiusOfCurvatureBluntBody"))
                    double.TryParse(node.GetValue("radiusOfCurvatureBluntBody"), out radiusOfCurvatureBluntBody);

                if (node.HasValue("massPerWingAreaSupported"))
                    double.TryParse(node.GetValue("massPerWingAreaSupported"), out massPerWingAreaSupported);

                if (node.HasValue("massStressPower"))
                    double.TryParse(node.GetValue("massStressPower"), out massStressPower);

                if (node.HasValue("ctrlSurfTimeConstant"))
                    double.TryParse(node.GetValue("ctrlSurfTimeConstant"), out FARControllableSurface.timeConstant);

                if (node.HasValue("ctrlSurfTimeConstantFlap"))
                    double.TryParse(node.GetValue("ctrlSurfTimeConstantFlap"), out FARControllableSurface.timeConstantFlap);

                if (node.HasValue("ctrlSurfTimeConstantSpoiler"))
                    double.TryParse(node.GetValue("ctrlSurfTimeConstantSpoiler"), out FARControllableSurface.timeConstantSpoiler);

                FARAeroUtil.bodyAtmosphereConfiguration = new Dictionary<int, double[]>();
                foreach (ConfigNode bodyProperties in node.GetNodes("BodyAtmosphericData"))
                {
                    if (bodyProperties == null || !bodyProperties.HasValue("index") || !bodyProperties.HasValue("specHeatRatio")
                        || !bodyProperties.HasValue("gasMolecularWeight") || !bodyProperties.HasValue("viscosityAtReferenceTemp")
                        || !bodyProperties.HasValue("referenceTemp"))
                        continue;

                    double[] Rgamma_and_gamma = new double[5];
                    double tmp;
                    double.TryParse(bodyProperties.GetValue("specHeatRatio"), out tmp);
                    Rgamma_and_gamma[1] = tmp;

                    double.TryParse(bodyProperties.GetValue("gasMolecularWeight"), out tmp);

                    Rgamma_and_gamma[2] = 8.3145 * 1000 / tmp;
                    Rgamma_and_gamma[0] = Rgamma_and_gamma[1] * Rgamma_and_gamma[2];

                    double.TryParse(bodyProperties.GetValue("viscosityAtReferenceTemp"), out tmp);

                    Rgamma_and_gamma[3] = tmp;

                    double.TryParse(bodyProperties.GetValue("referenceTemp"), out tmp);

                    Rgamma_and_gamma[4] = tmp;
                    int index;
                    int.TryParse(bodyProperties.GetValue("index"), out index);

                    FARAeroUtil.bodyAtmosphereConfiguration.Add(index, Rgamma_and_gamma);
                }

            }

            //For any bodies that lack a configuration
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (bodyAtmosphereConfiguration.ContainsKey(body.flightGlobalsIndex))
                    continue;

                double[] Rgamma_and_gamma = new double[5];
                Rgamma_and_gamma[1] = 1.4;
                Rgamma_and_gamma[2] = 8.3145 * 1000 / 28.96;
                Rgamma_and_gamma[0] = Rgamma_and_gamma[1] * Rgamma_and_gamma[2];

                Rgamma_and_gamma[3] = 1.7894e-5;
                Rgamma_and_gamma[4] = 288;

                FARAeroUtil.bodyAtmosphereConfiguration.Add(body.flightGlobalsIndex, Rgamma_and_gamma);
            }

            foreach (AssemblyLoader.LoadedAssembly assembly in AssemblyLoader.loadedAssemblies)
            {
                if (assembly.assembly.GetName().Name == "AJE")
                {
                    AJELoaded = true;
                }
            }

            SetDefaultValuesIfNoValuesLoaded();
          
            loaded = true;

            string forceUpdatePath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/FARForceDataUpdate.cfg";
            if (File.Exists(forceUpdatePath))
                File.Delete(forceUpdatePath);

            //Get Kerbin
            currentBodyAtm = bodyAtmosphereConfiguration[1];
        }

        private static void SetDefaultValuesIfNoValuesLoaded()
        {
            if (areaFactor == 0)
                areaFactor = 1;
            if (attachNodeRadiusFactor == 0)
                attachNodeRadiusFactor = 0.625;
            if (incompressibleRearAttachDrag == 0)
                incompressibleRearAttachDrag = 0.01;
            if (sonicRearAdditionalAttachDrag == 0)
                sonicRearAdditionalAttachDrag = 0.2;
            if (massPerWingAreaSupported == 0)
                massPerWingAreaSupported = 0.05;
            if (massStressPower == 0)
                massStressPower = 1.2;
        }

        public static double MaxPressureCoefficientCalc(double M)
        {
            if (M <= 0)
                return 1;
            double value;
            double gamma = currentBodyAtm[1];
            if (M <= 1)
                value = StagnationPressureCalc(M);
            else
            {

                value = (gamma + 1) * M;                  //Rayleigh Pitot Tube Formula; gives max stagnation pressure behind shock
                value *= value;
                value /= (4 * gamma * M * M - 2 * (gamma - 1));
                value = Math.Pow(value, gamma / (gamma - 1));

                value *= (1 - gamma + 2 * gamma * M * M);
                value /= (gamma + 1);
            }
            value--;                                //and now to convert to pressure coefficient
            value *= 2 / (gamma * M * M);

            return value;
        }

        public static double StagnationPressureCalc(double M)
        {

            double ratio;
            double gamma = currentBodyAtm[1];
            ratio = M * M;
            ratio *= (gamma - 1);
            ratio *= 0.5;
            ratio++;

            ratio = Math.Pow(ratio, gamma / (gamma - 1));
            return ratio;
        }

        public static double PressureBehindShockCalc(double M)
        {
            double ratio;
            double gamma = currentBodyAtm[1];
            ratio = M * M;
            ratio--;
            ratio *= 2 * gamma;
            ratio /= (gamma + 1);
            ratio++;

            return ratio;

        }

        public static double MachBehindShockCalc(double M)
        {
            double ratio;
            double gamma = currentBodyAtm[1];
            ratio = (gamma - 1) * 0.5;
            ratio *= M * M;
            ratio++;
            ratio /= (gamma * M * M - (gamma - 1) * 0.5);
            ratio = Math.Sqrt(ratio);

            return ratio;
        }

        public static FloatCurve MaxPressureCoefficient
        {
            get
            {
                if (maxPressureCoefficient == null)
                {
                    MonoBehaviour.print("Stagnation Pressure Coefficient Curve Initialized");
                    maxPressureCoefficient = new FloatCurve();

                    double M = 0.1;
                    //float gamma = 1.4f;

                    maxPressureCoefficient.Add(0, 1, 0, 0);

                    if (currentBodyAtm[0] == 0)
                    {
                        UpdateCurrentActiveBody(0, FlightGlobals.Bodies[1]);
                    }
                    double gamma = currentBodyAtm[1];

                    while (M < 50)
                    {
                        double value = 0;
                        double d_value = 0;
                        if (M <= 1)
                        {
                            value = StagnationPressureCalc(M);

                            d_value = (gamma - 1) * M * M * 0.5 + 1;
                            d_value *= M;
                            d_value = value * 2 / d_value;

                            double tmp = value - 1;
                            tmp *= 4;
                            tmp /= gamma * M * M * M;
                            d_value -= tmp;

                        }
                        else
                        {
                            value = (gamma + 1) * M;                  //Rayleigh Pitot Tube Formula; gives max stagnation pressure behind shock
                            value *= value;

                            d_value = value;

                            value /= (4 * gamma * M * M - 2 * (gamma - 1));
                            value = Math.Pow(value, gamma / (gamma - 1));

                            value *= (1 - gamma + 2 * gamma * M * M);
                            value /= (gamma + 1);

                            d_value /= (4 * M * M - 2) * gamma + 2;
                            d_value = Math.Pow(d_value, gamma / (gamma - 1));
                            //d_value = gamma + 1 - d_value;
                            d_value *= 4;
                            d_value /= M * M * M * gamma * (gamma + 1);

                        }

                        value--;                                //and now to convert to pressure coefficient
                        value *= 2 / (gamma * M * M);


                        maxPressureCoefficient.Add((float)M, (float)value, (float)d_value, (float)d_value);


                        if (M < 2)
                            M += 0.1;
                        else if (M < 5)
                            M += 0.5;
                        else
                            M += 2.5;
                    }
                }

                return maxPressureCoefficient;
            }
        }

        public static FloatCurve PrandtlMeyerMach
        {
            get{
                if (prandtlMeyerMach == null)
                {
                    MonoBehaviour.print("Prandlt-Meyer Expansion Curves Initialized");
                    prandtlMeyerMach = new FloatCurve();
                    prandtlMeyerAngle = new FloatCurve();
                    double M = 1;
                    //float gamma = 1.4f;

                    double gamma_ = Math.Sqrt((currentBodyAtm[1] + 1) / (currentBodyAtm[1] - 1));

                    while (M < 250)
                    {
                        double mach = Math.Sqrt(M * M - 1);

                        double nu = Math.Atan(mach / gamma_);
                        nu *= gamma_;
                        nu -= Math.Atan(mach);
                        nu *= FARMathUtil.rad2deg;

                        double nu_mach = (currentBodyAtm[1] - 1) / 2;
                        nu_mach *= M * M;
                        nu_mach++;
                        nu_mach *= M;
                        nu_mach = mach / nu_mach;
                        nu_mach *= FARMathUtil.rad2deg;

                        prandtlMeyerMach.Add((float)M, (float)nu, (float)nu_mach, (float)nu_mach);

                        nu_mach = 1 / nu_mach;

                        prandtlMeyerAngle.Add((float)nu, (float)M, (float)nu_mach, (float)nu_mach);

                        if (M < 3)
                            M += 0.1f;
                        else if (M < 10)
                            M += 0.5f;
                        else if (M < 25)
                            M += 2;
                        else
                            M += 25;
                    }

                    maxPrandtlMeyerTurnAngle = gamma_ - 1;
                    maxPrandtlMeyerTurnAngle *= 90;
                }
                return prandtlMeyerMach;
            }
        }

        public static FloatCurve PrandtlMeyerAngle
        {
            get
            {
                if (prandtlMeyerAngle == null)
                {
                    MonoBehaviour.print("Prandlt-Meyer Expansion Curves Initialized");
                    prandtlMeyerMach = new FloatCurve();
                    prandtlMeyerAngle = new FloatCurve();
                    double M = 1;
                    //float gamma = 1.4f;

                    double gamma_ = Math.Sqrt((currentBodyAtm[1] + 1) / (currentBodyAtm[1] - 1));

                    while (M < 250)
                    {
                        double mach = Math.Sqrt(M * M - 1);

                        double nu = Math.Atan(mach / gamma_);
                        nu *= gamma_;
                        nu -= Math.Atan(mach);
                        nu *= FARMathUtil.rad2deg;

                        double nu_mach = (currentBodyAtm[1] - 1) / 2;
                        nu_mach *= M * M;
                        nu_mach++;
                        nu_mach *= M;
                        nu_mach = mach / nu_mach;
                        nu_mach *= FARMathUtil.rad2deg;

                        prandtlMeyerMach.Add((float)M, (float)nu, (float)nu_mach, (float)nu_mach);

                        nu_mach = 1 / nu_mach;

                        prandtlMeyerAngle.Add((float)nu, (float)M, (float)nu_mach, (float)nu_mach);

                        if (M < 3)
                            M += 0.1;
                        else if (M < 10)
                            M += 0.5;
                        else if (M < 25)
                            M += 2;
                        else
                            M += 25;
                    }

                    maxPrandtlMeyerTurnAngle = gamma_ - 1;
                    maxPrandtlMeyerTurnAngle *= 90;
                }
                return prandtlMeyerAngle;
            }
        }


        public static FloatCurve PressureBehindShock
        {
            get
            {
                if (pressureBehindShock == null)
                {
                    MonoBehaviour.print("Normal Shock Pressure Curve Initialized");
                    pressureBehindShock = new FloatCurve();
                    double ratio;
                    double d_ratio;
                    double M = 1;
                    //float gamma = 1.4f;
                    while (M < 250)  //Calculates the pressure behind a normal shock
                    {
                        ratio = M * M;
                        ratio--;
                        ratio *= 2 * currentBodyAtm[1];
                        ratio /= (currentBodyAtm[1] + 1);
                        ratio++;

                        d_ratio = M * 4 * currentBodyAtm[1];
                        d_ratio /= (currentBodyAtm[1] + 1);

                        pressureBehindShock.Add((float)M, (float)ratio, (float)d_ratio, (float)d_ratio);
                        if (M < 3)
                            M += 0.1;
                        else if (M < 10)
                            M += 0.5;
                        else if (M < 25)
                            M += 2;
                        else
                            M += 25;
                    }
                }
                return pressureBehindShock;
            }
        }

        public static FloatCurve MachBehindShock
        {
            get
            {
                if (machBehindShock == null)
                {
                    MonoBehaviour.print("Normal Shock Mach Number Curve Initialized");
                    machBehindShock = new FloatCurve();
                    double ratio;
                    double d_ratio;
                    double M = 1;
                    //float gamma = 1.4f;
                    while (M < 250)  //Calculates the pressure behind a normal shock
                    {
                        ratio = (currentBodyAtm[1] - 1) / 2;
                        ratio *= M * M;
                        ratio++;
                        ratio /= (currentBodyAtm[1] * M * M - (currentBodyAtm[1] - 1) / 2);

                        d_ratio = 4 * currentBodyAtm[1] * currentBodyAtm[1] * Math.Pow(M, 4) - 4 * (currentBodyAtm[1] - 1) * currentBodyAtm[1] * M * M + Math.Pow(currentBodyAtm[1] - 1, 2);
                        d_ratio = 1 / d_ratio;
                        d_ratio *= 4 * (currentBodyAtm[1] * M * M - (currentBodyAtm[1] - 1) / 2) * (currentBodyAtm[1] - 1) * M - 8 * currentBodyAtm[1] * M * (1 + (currentBodyAtm[1] - 1) / 2 * M * M);

                        machBehindShock.Add((float)Math.Sqrt(M), (float)ratio, (float)d_ratio, (float)d_ratio);
                        if (M < 3)
                            M += 0.1;
                        else if (M < 10)
                            M += 0.5;
                        else if (M < 25)
                            M += 2;
                        else
                            M += 25;
                    }
                }
                return machBehindShock;
            }
        }

        public static FloatCurve StagnationPressure
        {
            get
            {
                if (stagnationPressure == null)
                {
                    MonoBehaviour.print("Stagnation Pressure Curve Initialized");
                    stagnationPressure = new FloatCurve();
                    double ratio;
                    double d_ratio;
                    double M = 0;
                    //float gamma = 1.4f;
                    while (M < 250)  //calculates stagnation pressure
                    {
                        ratio = M * M;
                        ratio *= (currentBodyAtm[1] - 1);
                        ratio /= 2;
                        ratio++;
                        
                        d_ratio = ratio;

                        ratio = Math.Pow(ratio, currentBodyAtm[1] / (currentBodyAtm[1] - 1));

                        d_ratio = Math.Pow(d_ratio, (currentBodyAtm[1] / (currentBodyAtm[1] - 1)) - 1);
                        d_ratio *= M * currentBodyAtm[1];

                        stagnationPressure.Add((float)M, (float)ratio, (float)d_ratio, (float)d_ratio);
                        if (M < 3)
                            M += 0.1;
                        else if (M < 10)
                            M += 0.5;
                        else if (M < 25)
                            M += 2;
                        else
                            M += 25;
                    }
                }
                return stagnationPressure;
            }
        }

        public static bool IsNonphysical(Part p)
        {
            return p.physicalSignificance == Part.PhysicalSignificance.NONE ||
                   (HighLogic.LoadedSceneIsEditor &&
                    p != EditorLogic.RootPart &&
                    p.PhysicsSignificance == (int)Part.PhysicalSignificance.NONE);
        }

        private static List<FARWingAerodynamicModel> curEditorWingCache = null;

        public static List<FARWingAerodynamicModel> CurEditorWings
        {
            get
            {
                if (curEditorWingCache == null)
                    curEditorWingCache = ListEditorWings(false);
                return curEditorWingCache;
            }
        }
        // Parts currently added to the vehicle in the editor
        private static List<Part> CurEditorPartsCache = null;

        public static List<Part> CurEditorParts
        {
            get
            {
                if (CurEditorPartsCache == null)
                    CurEditorPartsCache = ListEditorParts(false);
                return CurEditorPartsCache;
            }
        }

        // Parts currently added, plus the ghost part(s) about to be attached
        private static List<Part> AllEditorPartsCache = null;

        public static List<Part> AllEditorParts
        {
            get
            {
                if (AllEditorPartsCache == null)
                    AllEditorPartsCache = ListEditorParts(true);
                return AllEditorPartsCache;
            }
        }

        public static void ResetEditorParts()
        {
            AllEditorPartsCache = CurEditorPartsCache = null;
            curEditorWingCache = null;
        }

        // Checks if there are any ghost parts almost attached to the craft
        public static bool EditorAboutToAttach(bool move_too = false)
        {
            return HighLogic.LoadedSceneIsEditor &&
                   EditorLogic.SelectedPart != null &&
                   (EditorLogic.SelectedPart.potentialParent != null ||
                     (move_too && EditorLogic.SelectedPart == EditorLogic.RootPart));
        }

        public static List<Part> ListEditorParts(bool include_selected)
        {
            var list = new List<Part>();

            if (EditorLogic.RootPart)
                RecursePartList(list, EditorLogic.RootPart);

            if (include_selected && EditorAboutToAttach())
            {
                RecursePartList(list, EditorLogic.SelectedPart);

                for (int i = 0; i < EditorLogic.SelectedPart.symmetryCounterparts.Count; i++)
                {
                    Part sym = EditorLogic.SelectedPart.symmetryCounterparts[i];
                    RecursePartList(list, sym);
                }
            }

            return list;
        }

        public static List<FARWingAerodynamicModel> ListEditorWings(bool include_selected)
        {
            List<Part> list = CurEditorParts;

            List<FARWingAerodynamicModel> wings = new List<FARWingAerodynamicModel>();
            for(int i = 0; i < list.Count; i++)
            {
                Part p = list[i];
                FARWingAerodynamicModel wing = p.GetComponent<FARWingAerodynamicModel>();
                if ((object)wing != null)
                    wings.Add(wing);
            }
            return wings;
        }

        private static void RecursePartList(List<Part> list, Part part)
        {
            list.Add(part);
            for (int i = 0; i < part.children.Count; i++)
            {
                Part p = part.children[i];
                RecursePartList(list, p);
            }
        }

        private static int RaycastMaskVal = 0, RaycastMaskEdit;
        private static String[] RaycastLayers = {
            "Default", "TransparentFX", "Local Scenery", "Disconnected Parts"
        };

        public static int RaycastMask
        {
            get
            {
                // Just to avoid the opaque integer constant; maybe it's enough to
                // document what layers come into it, but this is more explicit.
                if (RaycastMaskVal == 0)
                {
                    foreach (String name in RaycastLayers)
                        RaycastMaskVal |= (1 << LayerMask.NameToLayer(name));

                    // When parts are being dragged in the editor, they are put into this
                    // layer; however we have to raycast them, or the visible CoL will be
                    // different from the one after the parts are attached.
                    RaycastMaskEdit = RaycastMaskVal | (1 << LayerMask.NameToLayer("Ignore Raycast"));

                    Debug.Log("FAR Raycast mask: "+RaycastMaskVal+" "+RaycastMaskEdit);
                }

                return EditorAboutToAttach(true) ? RaycastMaskEdit : RaycastMaskVal;
            }
        }

        //This approximates e^x; it's slightly inaccurate, but good enough.  It's much faster than an actual exponential function
        //It runs on the assumption e^x ~= (1 + x/256)^256
        public static double ExponentialApproximation(double x)
        {
            double exp = 1d + x * 0.00390625;
            exp *= exp;
            exp *= exp;
            exp *= exp;
            exp *= exp;
            exp *= exp;
            exp *= exp;
            exp *= exp;
            exp *= exp;

            return exp;
        }

        public static double GetFailureForceScaling(CelestialBody body, double altitude)
        {
            if (!body.ocean || altitude > 0)
                return 1;

            double densityMultFactor = Math.Max(-altitude, 1);
            densityMultFactor *= UNDERWATER_DENSITY_FACTOR_MINUS_ONE * 0.05;     //base it on the density factor

            return densityMultFactor;
        }

        public static double GetFailureForceScaling(Vessel vessel)
        {
            if (!vessel.mainBody.ocean || vessel.altitude > 0)
                return 1;

            double densityMultFactor = Math.Max(-vessel.altitude, 1);
            densityMultFactor *= UNDERWATER_DENSITY_FACTOR_MINUS_ONE * 0.05;     //base it on the density factor

            return densityMultFactor;
        }

        public static double GetCurrentDensity(CelestialBody body, double altitude, bool densitySmoothingAtOcean = true)
        {
            double pressure, temperature;
            pressure = FlightGlobals.getStaticPressure(altitude, body);
            temperature = FlightGlobals.getExternalTemperature(altitude, body);

            double density = FlightGlobals.getAtmDensity(pressure, temperature, body);

            if (altitude < 0 && densitySmoothingAtOcean)
            {
                double densityMultFromOcean = Math.Max(-altitude, 1);
                densityMultFromOcean *= UNDERWATER_DENSITY_FACTOR_MINUS_ONE;
                densityMultFromOcean++;
                density *= densityMultFromOcean;
            }

            return density;
        }

        // Vessel has altitude and cached pressure, and both density and sound speed need temperature
        public static double GetCurrentDensity(Vessel vessel, bool densitySmoothingAtOcean = true)
        {
            double altitude = vessel.altitude;
            CelestialBody body = vessel.mainBody;
            double density = vessel.atmDensity;

            if (altitude < 0 && densitySmoothingAtOcean)
            {
                double densityMultFromOcean = Math.Max(-altitude, 1);
                densityMultFromOcean *= UNDERWATER_DENSITY_FACTOR_MINUS_ONE;
                densityMultFromOcean++;
                density *= densityMultFromOcean;
            }

            return density;
        }

        public static double CalculateCurrentViscosity(double tempInK)
        {
            double visc = currentBodyAtm[3];        //get viscosity

            double tempRat = tempInK / currentBodyAtm[4];
            tempRat *= tempRat * tempRat;
            tempRat = Math.Sqrt(tempRat);

            visc *= (currentBodyAtm[4] + 110);
            visc /= (tempInK + 110);
            visc *= tempRat;

            return visc;
        }


        public static double ReferenceTemperatureRatio(double machNumber, double recoveryFactor)
        {
            double tempRatio = machNumber * machNumber;
            tempRatio *= (currentBodyAtm[1] - 1);
            tempRatio *= 0.5;       //account for stagnation temp

            tempRatio *= recoveryFactor;    //this accounts for adiabatic wall temp ratio

            tempRatio *= 0.58;
            tempRatio += 0.032 * machNumber * machNumber;

            tempRatio++;
            return tempRatio;
        }

        public static double CalculateReynoldsNumber(double density, double lengthScale, double vel, double machNumber, double temp)
        {
            if (lengthScale == 0)
                return 0;

            double refTemp = temp * ReferenceTemperatureRatio(machNumber, 0.843);
            double visc = CalculateCurrentViscosity(refTemp);
            double Re = lengthScale * density * vel / visc;
            return Re;
        }

        public static double SkinFrictionDrag(double density, double lengthScale, double vel, double machNumber, double temp)
        {
            if (lengthScale == 0)
                return 0;

            double Re = CalculateReynoldsNumber(density, lengthScale, vel, machNumber, temp);

            if (Re < TRANSITION_REYNOLDS_NUMBER)
            {
                double invSqrtRe = 1 / Math.Sqrt(Re);
                double lamCf = 1.328 * invSqrtRe;

                double rarefiedGasVal = machNumber / Re;
                if(rarefiedGasVal > 0.01)
                {
                    return lamCf + (0.25 - lamCf) * (rarefiedGasVal - 0.01) / (0.99 + rarefiedGasVal);
                }
                return lamCf;
            }

            double transitionFraction = TRANSITION_REYNOLDS_NUMBER / Re;

            double laminarCf = 1.328 / Math.Sqrt(TRANSITION_REYNOLDS_NUMBER);
            double turbulentCfInLaminar = 0.074 / Math.Pow(TRANSITION_REYNOLDS_NUMBER, 0.2);
            double turbulentCf = 0.074 / Math.Pow(Re, 0.2);

            return turbulentCf - transitionFraction * (turbulentCfInLaminar - laminarCf);
        }

        public static double SkinFrictionDrag(double reynoldsNumber, double machNumber)
        {

            if (reynoldsNumber < TRANSITION_REYNOLDS_NUMBER)
            {
                double invSqrtRe = 1 / Math.Sqrt(reynoldsNumber);
                double lamCf = 1.328 * invSqrtRe;

                double rarefiedGasVal = machNumber / reynoldsNumber;
                if (rarefiedGasVal > 0.01)
                {
                    return lamCf + (0.25 - lamCf) * (rarefiedGasVal - 0.01) / (0.99 + rarefiedGasVal);
                }
                return lamCf;
            }

            double transitionFraction = TRANSITION_REYNOLDS_NUMBER / reynoldsNumber;

            double laminarCf = 1.328 / Math.Sqrt(TRANSITION_REYNOLDS_NUMBER);
            double turbulentCfInLaminar = 0.074 / Math.Pow(TRANSITION_REYNOLDS_NUMBER, 0.2);
            double turbulentCf = 0.074 / Math.Pow(reynoldsNumber, 0.2);

            return turbulentCf - transitionFraction * (turbulentCfInLaminar - laminarCf);
        }

        public static void UpdateCurrentActiveBody(CelestialBody body)
        {
            if ((object)body != null && body.flightGlobalsIndex != prevBody)
            {
                UpdateCurrentActiveBody(body.flightGlobalsIndex, body);
            }
        }

        public static void UpdateCurrentActiveBody(int index, CelestialBody body)
        {
            if (index != prevBody)
            {
                prevBody = index;
                currentBodyAtm = bodyAtmosphereConfiguration[prevBody];
                currentBodyTemp = 273.15f;

                prandtlMeyerMach = null;
                prandtlMeyerAngle = null;
                pressureBehindShock = null;
                machBehindShock = null;
                stagnationPressure = null;
                maxPressureCoefficient = null;
            }
        }
        
        //Based on NASA Contractor Report 187173, Exact and Approximate Oblique Shock Equations for Real-Time Applications
        public static double CalculateSinWeakObliqueShockAngle(double MachNumber, double gamma, double deflectionAngle)
        {
            double M2 = MachNumber * MachNumber;
            double recipM2 = 1 / M2;
            double sin2def = Math.Sin(deflectionAngle);
            sin2def *= sin2def;

            double b = M2 + 2;
            b *= recipM2;
            b += gamma * sin2def;
            b = -b;

            double c = gamma + 1;
            c *= c * 0.25f;
            c += (gamma - 1) * recipM2;
            c *= sin2def;
            c += (2 * M2 + 1) * recipM2 * recipM2;

            double d = sin2def - 1;
            d *= recipM2 * recipM2;

            double Q = c * 0.33333333 - b * b * 0.111111111;
            double R = 0.16666667 * b * c - 0.5f * d - 0.037037037 * b * b * b;
            double D = Q * Q * Q + R * R;

            if (D > 0.001)
                return double.NaN;

            double phi = Math.Atan(Math.Sqrt(FARMathUtil.Clamp(-D, 0, double.PositiveInfinity)) / R);
            if (R < 0)
                phi += Math.PI;
            phi *= 0.33333333;

            double chiW = -0.33333333 * b - Math.Sqrt(FARMathUtil.Clamp(-Q, 0, double.PositiveInfinity)) * (Math.Cos(phi) - 1.7320508f * Math.Sin(phi));

            double betaW = Math.Sqrt(FARMathUtil.Clamp(chiW, 0, double.PositiveInfinity));

            return betaW;
        }

        public static double CalculateSinMaxShockAngle(double MachNumber, double gamma)
        {
            double M2 = MachNumber * MachNumber;
            double gamP1_2_M2 = (gamma + 1) * 0.5 * M2;

            double b = gamP1_2_M2;
            b = 2 - b;
            b *= M2;

            double a = gamma * M2 * M2;

            double c = gamP1_2_M2 + 1;
            c = -c;

            double tmp = b * b - 4 * a * c;

            double sin2def = -b + Math.Sqrt(FARMathUtil.Clamp(tmp, 0, double.PositiveInfinity));
            sin2def /= (2 * a);

            return Math.Sqrt(sin2def);
        }

        public static double MaxShockAngleCheck(double MachNumber, double gamma, out bool attachedShock)
        {
            double M2 = MachNumber * MachNumber;
            double gamP1_2_M2 = (gamma + 1) * 0.5 * M2;

            double b = gamP1_2_M2;
            b = 2 - b;
            b *= M2;

            double a = gamma * M2 * M2;

            double c = gamP1_2_M2 + 1;
            c = -c;

            double tmp = b * b - 4 * a * c;

            if (tmp > 0)
                attachedShock = true;
            else
                attachedShock = false;

            return tmp;
        }

        //Calculates Oswald's Efficiency e using Shevell's Method
        public static double CalculateOswaldsEfficiency(double AR, double CosSweepAngle, double Cd0)
        {
            double e = 1 - 0.02 * FARMathUtil.PowApprox(AR, 0.7) * FARMathUtil.PowApprox(Math.Acos(CosSweepAngle), 2.2);
            double tmp = AR * Cd0 * Mathf.PI + 1;
            e /= tmp;

            return e;
        }
    }
}
