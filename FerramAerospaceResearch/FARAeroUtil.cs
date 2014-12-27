/*
Ferram Aerospace Research v0.14.5.1
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

            FARBasicDragModel.SetBluntBodyParams(radiusOfCurvatureBluntBody);
          
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

        private static float joolTempOffset = 0;

        public static float JoolTempOffset      //This is another kluge hotfix for the "Jool's atmosphere is below 0 Kelvin bug"
        {                                         //Essentially it just shifts its outer atmosphere temperature up to 4 Kelvin
            get{
                if(joolTempOffset == 0)
                {
                    CelestialBody Jool = null;
                    foreach (CelestialBody body in  FlightGlobals.Bodies)
                        if (body.GetName() == "Jool")
                        {
                            Jool = body;
                            break;
                        }
                    Jool.atmoshpereTemperatureMultiplier *= 0.5f;
                    float outerAtmTemp = FlightGlobals.getExternalTemperature(138000f, Jool) + 273.15f;
                    joolTempOffset = 25f - outerAtmTemp;
                    joolTempOffset = Mathf.Clamp(joolTempOffset, 0.1f, Mathf.Infinity);
                }
                

            return joolTempOffset;
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
        private static List<FARBasicDragModel> curEditorOtherDragCache = null;

        public static List<FARWingAerodynamicModel> CurEditorWings
        {
            get
            {
                if (curEditorWingCache == null)
                    curEditorWingCache = ListEditorWings(false);
                return curEditorWingCache;
            }
        }

        public static List<FARBasicDragModel> CurEditorOtherDrag
        {
            get
            {
                if (curEditorOtherDragCache == null)
                    curEditorOtherDragCache = ListEditorOtherDrag(false);
                return curEditorOtherDragCache;
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
            curEditorOtherDragCache = null;
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

        public static List<FARBasicDragModel> ListEditorOtherDrag(bool include_selected)
        {
            List<Part> list = CurEditorParts;

            List<FARBasicDragModel> otherDrag = new List<FARBasicDragModel>();
            for (int i = 0; i < list.Count; i++)
            {
                Part p = list[i];
                FARBasicDragModel dragModule = p.GetComponent<FARBasicDragModel>();
                if ((object)dragModule != null)
                    otherDrag.Add(dragModule);
            }
            return otherDrag;
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

        public static void AddBasicDragModule(Part p)
        {
            AddBasicDragModuleWithoutDragPropertySetup(p);

            SetBasicDragModuleProperties(p);
        }

        public static void AddBasicDragModuleWithoutDragPropertySetup(Part p)
        {
            if (p.Modules.Contains("KerbalEVA"))
                return;

            p.minimum_drag = 0;
            p.maximum_drag = 0;
            p.dragModelType = "override";
            p.angularDrag = 0;

            if (p.Modules.Contains("ModuleResourceIntake"))
                if (AJELoaded)
                    return;


            p.AddModule("FARBasicDragModel");
        }

        public static void SetBasicDragModuleProperties(Part p)
        {
            FARBasicDragModel d = p.Modules["FARBasicDragModel"] as FARBasicDragModel;
            string title = p.partInfo.title.ToLowerInvariant();

            if (p.Modules.Contains("ModuleAsteroid"))
            {
                FARGeoUtil.BodyGeometryForDrag data = FARGeoUtil.CalcBodyGeometryFromMesh(p);

                FloatCurve TempCurve1 = new FloatCurve();
                double cd = 0.2; //cd based on diameter
                cd *= Math.Sqrt(data.crossSectionalArea / Math.PI) * 2 / data.area;

                TempCurve1.Add(-1, (float)cd);
                TempCurve1.Add(1, (float)cd);

                FloatCurve TempCurve2 = new FloatCurve();
                TempCurve2.Add(-1, 0);
                TempCurve2.Add(1, 0);

                FloatCurve TempCurve4 = new FloatCurve();
                TempCurve4.Add(-1, 0);
                TempCurve4.Add(1, 0);

                FloatCurve TempCurve3 = new FloatCurve();
                TempCurve3.Add(-1, 0);
                TempCurve3.Add(1, 0);



                d.BuildNewDragModel(data.area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, data.originToCentroid, data.majorMinorAxisRatio, 0, data.taperCrossSectionArea, double.MaxValue, double.MaxValue, Math.Sqrt(data.crossSectionalArea / Math.PI) * data.finenessRatio * FARAeroUtil.areaFactor);
                return;
            }
            else if (FARPartClassification.IncludePartInGreeble(p, title))
            {
                FloatCurve TempCurve1 = new FloatCurve();
                /*if (title.Contains("heatshield") || (title.Contains("heat") && title.Contains("shield")))
                    TempCurve1.Add(-1, 0.3f);
                else*/
                TempCurve1.Add(-1, 0);
                TempCurve1.Add(0, 0.02f);
                TempCurve1.Add(1, 0);

                FloatCurve TempCurve2 = new FloatCurve();
                TempCurve2.Add(-1, 0);
                TempCurve2.Add(1, 0);

                FloatCurve TempCurve4 = new FloatCurve();
                TempCurve4.Add(-1, 0);
                TempCurve4.Add(1, 0);

                FloatCurve TempCurve3 = new FloatCurve();
                TempCurve3.Add(-1, 0);
                TempCurve3.Add(1, 0);

                FARGeoUtil.BodyGeometryForDrag data = FARGeoUtil.CalcBodyGeometryFromMesh(p);


                d.BuildNewDragModel(data.area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, Vector3.zero, 1, 0, 0, double.MaxValue, double.MaxValue, Math.Sqrt(data.crossSectionalArea / Math.PI) * data.finenessRatio * FARAeroUtil.areaFactor);
                return;
            }
            else
            {
                FARGeoUtil.BodyGeometryForDrag data = FARGeoUtil.CalcBodyGeometryFromMesh(p);
                FloatCurve TempCurve1 = new FloatCurve();
                FloatCurve TempCurve2 = new FloatCurve();
                FloatCurve TempCurve4 = new FloatCurve();
                FloatCurve TempCurve3 = new FloatCurve();
                double YmaxForce = double.MaxValue;
                double XZmaxForce = double.MaxValue;

                double Cn1, Cn2, cutoffAngle, cosCutoffAngle = 0;

                if (title.Contains("truss") || title.Contains("strut") || title.Contains("railing") || p.Modules.Contains("ModuleWheel"))
                {
                    TempCurve1.Add(-1, 0.1f);
                    TempCurve1.Add(1, 0.1f);
                    TempCurve2.Add(-1, 0f);
                    TempCurve2.Add(1, 0f);
                    TempCurve3.Add(-1, 0f);
                    TempCurve3.Add(1, 0f);
                }
                else if (title.Contains("plate") || title.Contains("panel"))
                {
                    TempCurve1.Add(-1, 1.2f);
                    TempCurve1.Add(0, 0f);
                    TempCurve1.Add(1, 1.2f);
                    TempCurve2.Add(-1, 0f);
                    TempCurve2.Add(1, 0f);
                    TempCurve3.Add(-1, 0f);
                    TempCurve3.Add(1, 0f);
                }
                else
                {

                    if (data.taperRatio <= 1)
                    {
                        Cn1 = NormalForceCoefficientTerm1(data.finenessRatio, data.taperRatio, data.crossSectionalArea, data.area);
                        Cn2 = NormalForceCoefficientTerm2(data.finenessRatio, data.taperRatio, data.crossSectionalArea, data.area);
                        cutoffAngle = cutoffAngleForLift(data.finenessRatio, data.taperRatio, data.crossSectionalArea, data.area);

                        cosCutoffAngle = -Math.Cos(cutoffAngle);

                        double axialPressureDrag = PressureDragDueToTaperingConic(data.finenessRatio, data.taperRatio, data.crossSectionalArea, data.area);

                        TempCurve1.Add(-1, (float)axialPressureDrag);
                        TempCurve1.Add(0, (float)Cn2);
                        TempCurve1.Add(1, (float)axialPressureDrag);


                        TempCurve2.Add((float)cosCutoffAngle, 0, (float)Cn1, 0);
                        TempCurve2.Add(-0.8660f, (float)(Math.Cos((Math.PI * 0.5 - Math.Acos(0.8660)) * 0.5) * Math.Sin(2 * (Math.PI * 0.5 - Math.Acos(0.8660))) * Cn1), 0, 0);
                        TempCurve2.Add(0, 0);
                        TempCurve2.Add(0.8660f, (float)(Math.Cos((Math.PI * 0.5 - Math.Acos(0.8660)) * 0.5) * Math.Sin(2 * (Math.PI * 0.5 - Math.Acos(0.8660))) * Cn1), 0, 0);
                        TempCurve2.Add(1, 0, (float)Cn1, (float)Cn1);

                        TempCurve4.Add(-1, 0, 0, 0);
                        TempCurve4.Add(-0.95f, (float)(Math.Pow(Math.Sin(Math.Acos(0.95)), 2) * Cn2 * -0.95));
                        TempCurve4.Add(-0.8660f, (float)(Math.Pow(Math.Sin(Math.Acos(0.8660)), 2) * Cn2 * -0.8660));
                        TempCurve4.Add(-0.5f, (float)(Math.Pow(Math.Sin(Math.Acos(0.5f)), 2) * Cn2 * -0.5));
                        TempCurve4.Add(0, 0);
                        TempCurve4.Add(0.5f, (float)(Math.Pow(Math.Sin(Math.Acos(0.5)), 2) * Cn2 * 0.5));
                        TempCurve4.Add(0.8660f, (float)(Math.Pow(Math.Sin(Math.Acos(0.8660)), 2) * Cn2 * 0.8660));
                        TempCurve4.Add(0.95f, (float)(Math.Pow(Math.Sin(Math.Acos(0.95)), 2) * Cn2 * 0.95));
                        TempCurve4.Add(1, 0, 0, 0);
                    }
                    else
                    {
                        Cn1 = NormalForceCoefficientTerm1(data.finenessRatio, 1 / data.taperRatio, data.crossSectionalArea, data.area);
                        Cn2 = NormalForceCoefficientTerm2(data.finenessRatio, 1 / data.taperRatio, data.crossSectionalArea, data.area);
                        cutoffAngle = cutoffAngleForLift(data.finenessRatio, 1 / data.taperRatio, data.crossSectionalArea, data.area);

                        cosCutoffAngle = Math.Cos(cutoffAngle);

                        double axialPressureDrag = PressureDragDueToTaperingConic(data.finenessRatio, 1 / data.taperRatio, data.crossSectionalArea, data.area);

                        TempCurve1.Add(-1, (float)axialPressureDrag, 0, 0);
                        TempCurve1.Add(0, (float)Cn2, 0, 0);
                        TempCurve1.Add(1, (float)axialPressureDrag, 0, 0);


                        TempCurve2.Add(-1, 0, (float)-Cn1, (float)-Cn1);
                        TempCurve2.Add(-0.8660f, (float)((-Math.Cos((Math.PI *0.5 - Math.Acos(0.8660)) *0.5) * Math.Sin(2 * (Math.PI *0.5 - Math.Acos(0.8660))) * Cn1)), 0, 0);
                        TempCurve2.Add(0, 0);
                        TempCurve2.Add(0.8660f, (float)((-Math.Cos((Math.PI *0.5 - Math.Acos(0.8660)) *0.5) * Math.Sin(2 * (Math.PI *0.5 - Math.Acos(0.8660))) * Cn1)), 0, 0);
                        TempCurve2.Add((float)cosCutoffAngle, 0, (float)-Cn1, 0);

                        TempCurve4.Add(-1, 0, 0, 0);
                        TempCurve4.Add(-0.95f, (float)(Math.Pow(Math.Sin(Math.Acos(0.95)), 2) * Cn2 * -0.95));
                        TempCurve4.Add(-0.8660f, (float)(Math.Pow(Math.Sin(Math.Acos(0.8660)), 2) * Cn2 * -0.8660));
                        TempCurve4.Add(-0.5f, (float)(Math.Pow(Math.Sin(Math.Acos(0.5)), 2) * Cn2 * -0.5));
                        TempCurve4.Add(0, 0);
                        TempCurve4.Add(0.5f, (float)(Math.Pow(Math.Sin(Math.Acos(0.5)), 2) * Cn2 * 0.5));
                        TempCurve4.Add(0.8660f, (float)(Math.Pow(Math.Sin(Math.Acos(0.8660)), 2) * Cn2 * 0.8660));
                        TempCurve4.Add(0.95f, (float)(Math.Pow(Math.Sin(Math.Acos(0.95)), 2) * Cn2 * 0.95));
                        TempCurve4.Add(1, 0, 0, 0);
                    }

                    float cdM = (float)MomentDueToTapering(data.finenessRatio, data.taperRatio, data.crossSectionalArea, data.area);

                    TempCurve3.Add(-1, cdM);
                    TempCurve3.Add(-0.5f, cdM * 2);
                    TempCurve3.Add(0, cdM * 3);
                    TempCurve3.Add(0.5f, cdM * 2);
                    TempCurve3.Add(1, cdM);


                    if (HighLogic.LoadedSceneIsFlight && !FARAeroStress.PartIsGreeble(p, data.crossSectionalArea, data.finenessRatio, data.area) && FARDebugValues.allowStructuralFailures)
                    {
                        FARPartStressTemplate template = FARAeroStress.DetermineStressTemplate(p);

                        YmaxForce = template.YmaxStress;    //in MPa
                        YmaxForce *= data.crossSectionalArea;

                        /*XZmaxForce = 2 * Math.Sqrt(data.crossSectionalArea / Math.PI);
                        XZmaxForce = XZmaxForce * data.finenessRatio * XZmaxForce;
                        XZmaxForce *= template.XZmaxStress;*/

                        XZmaxForce = template.XZmaxStress * data.area * 0.5;

                        //Debug.Log("Template: " + template.name + " YmaxForce: " + YmaxForce + " XZmaxForce: " + XZmaxForce);
                    }

                }

                d.BuildNewDragModel(data.area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, data.originToCentroid, data.majorMinorAxisRatio, cosCutoffAngle, data.taperCrossSectionArea, YmaxForce, XZmaxForce, Math.Sqrt(data.crossSectionalArea / Math.PI) * data.finenessRatio * FARAeroUtil.areaFactor);
                return;
            }
        }


        //Approximate drag of a tapering conic body
        public static double PressureDragDueToTaperingConic(double finenessRatio, double taperRatio, double crossSectionalArea, double surfaceArea)
        {
            double b = 1 + (taperRatio / (1 - taperRatio));

            double refbeta = 2f * FARMathUtil.Clamp(finenessRatio, 1, double.PositiveInfinity) / Math.Sqrt(Math.Pow(2.5f, 2) - 1);     //Reference beta for the calculation; currently based at Mach 2.5
            double cdA;
            if (double.IsNaN(b) || double.IsInfinity(b))
            {
                return 0;
            }
            else if (b > 1)
            {
                cdA = 2 * (2 * b * b - 2 * b + 1) * Math.Log(2 * refbeta) - 2 * Math.Pow(b - 1, 2) * Math.Log(1 - 1 / b) - 1;
                cdA /= b * b * b * b;
                //Based on linear supersonic potential for a conic body, from
                //The Theoretical Wave-Drag of Some Bodies of Revolution, L. E. FRAENKEL, 1955
                //MINISTRY OF SUPPLY
                //AERONAUTICAL RESEARCH COUNCIL
                //REPORTS AND MEMORANDA
                //London
            }
            else
            {
                cdA = 2 * Math.Log(2 * refbeta) - 1;
            }

            cdA *= 0.25 / (finenessRatio * finenessRatio);
            cdA *= crossSectionalArea / surfaceArea;

            cdA /= 1.31;   //Approximate subsonic drag...

            //if (float.IsNaN(cdA))
            //    return 0;

            return cdA;
        }
        
        //Approximate drag of a tapering parabolic body
        public static double PressureDragDueToTaperingParabolic(double finenessRatio, double taperRatio, double crossSectionalArea, double surfaceArea)
        {
            double exponent = 2 + (Math.Sqrt(FARMathUtil.Clamp(finenessRatio, 0.1, double.PositiveInfinity)) - 2.2) / 3;

            double cdA = 4.6 * Math.Pow(Math.Abs(taperRatio - 1), 2 * exponent);
            cdA *= 0.25 / (finenessRatio * finenessRatio);
            cdA *= crossSectionalArea / surfaceArea;

            cdA /= 1.35;   //Approximate subsonic drag...


            double taperCrossSectionArea = Math.Sqrt(crossSectionalArea / Math.PI);
            taperCrossSectionArea *= taperRatio;
            taperCrossSectionArea = Math.Pow(taperCrossSectionArea, 2) * Math.PI;

            taperCrossSectionArea = Math.Abs(taperCrossSectionArea - crossSectionalArea);      //This is the cross-sectional area of the tapered section

            double maxcdA = taperCrossSectionArea * (incompressibleRearAttachDrag + sonicRearAdditionalAttachDrag);
            maxcdA /= surfaceArea;      //This is the maximum drag that this part can create

            cdA = FARMathUtil.Clamp(cdA, 0, maxcdA);      //make sure that stupid amounts of drag don't exist

            return cdA;
        }


        //This returns the normal force coefficient based on surface area due to potential flow
        public static double NormalForceCoefficientTerm1(double finenessRatio, double taperRatio, double crossSectionalArea, double surfaceArea)
        {
            double Cn1 = 0;

            Cn1 = crossSectionalArea * (1 - taperRatio * taperRatio);
            Cn1 /= surfaceArea;

            return Cn1;
        }

        //This returns the normal force coefficient based on surface area due to viscous flow
        public static double NormalForceCoefficientTerm2(double finenessRatio, double taperRatio, double crossSectionalArea, double surfaceArea)
        {
            double Cn2 = 0;
            double radius = Math.Sqrt(crossSectionalArea * 0.318309886184f);
            double length = radius * 2 * finenessRatio;

            //Assuming a linearly tapered cone
            Cn2 = radius * (1 + taperRatio) * length * 0.5f;
            Cn2 *= 2 * 1.2f;
            Cn2 /= surfaceArea;

            return Cn2;
        }

        public static double cutoffAngleForLift(double finenessRatio, double taperRatio, double crossSectionalArea, double surfaceArea)
        {
            double angle = 0;

            angle = (1 - taperRatio) / (2 * finenessRatio);
            angle = Math.Atan(angle);

            return angle;
        }

        public static double MomentDueToTapering(double finenessRatio, double taperRatio, double crossSectionalArea, double surfaceArea)
        {

            double rad = crossSectionalArea / Math.PI;
            rad = Math.Sqrt(rad);

            double cdM = 0f;
            if (taperRatio < 1)
            {
                double dragDueToTapering = PressureDragDueToTaperingConic(finenessRatio, taperRatio, crossSectionalArea, surfaceArea);

                cdM -= dragDueToTapering * rad * taperRatio;    //This applies the drag force over the front of the tapered area multiplied by the distance it acts from the center of the part (radius * taperRatio)
            }
            else
            {
                taperRatio = 1 / taperRatio;
                double dragDueToTapering = PressureDragDueToTaperingConic(finenessRatio, taperRatio, crossSectionalArea, surfaceArea);

                cdM += dragDueToTapering * rad * taperRatio;    //This applies the drag force over the front of the tapered area multiplied by the distance it acts from the center of the part (radius * taperRatio)
            }

            //cdM *= 1 / surfaceArea;

            return cdM;
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


        public static double GetMachNumber(CelestialBody body, double altitude, Vector3d velocity)
        {
            return GetMachNumber(body, altitude, velocity.magnitude);
        }
        
        public static double GetMachNumber(CelestialBody body, double altitude, double v_scalar)
        {
            double MachNumber = 0;
            if (HighLogic.LoadedSceneIsFlight)
            {
                //continue updating Mach Number for debris
                UpdateCurrentActiveBody(body);
                double temp = Math.Max(0.1, currentBodyTemp + FlightGlobals.getExternalTemperature((float)altitude, body));
                double Soundspeed = Math.Sqrt(temp * currentBodyAtm[0]);// * 401.8f;              //Calculation for speed of sound in ideal gas using air constants of gamma = 1.4 and R = 287 kJ/kg*K

                MachNumber = v_scalar / Soundspeed;

                if (MachNumber < 0)
                    MachNumber = 0;

            }
            return MachNumber;
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

        public static double GetCurrentDensity(CelestialBody body, Vector3 worldLocation, bool densitySmoothingAtOcean = true)
        {
            return GetCurrentDensity(body, (Vector3d)worldLocation, densitySmoothingAtOcean);
        }

        public static double GetCurrentDensity(CelestialBody body, Vector3d worldLocation, bool densitySmoothingAtOcean = true)
        {
            UpdateCurrentActiveBody(body);

            double altitude = body.GetAltitude(worldLocation);

            double temp = Math.Max(0.1, currentBodyTemp + FlightGlobals.getExternalTemperature((float)altitude, body));

            double pressure = FlightGlobals.getStaticPressure(worldLocation, body);
            if (pressure > 0)
                pressure = (pressure - currentBodyAtmPressureOffset) * 101300;     //Need to convert atm to Pa

            if (altitude < 0 && densitySmoothingAtOcean)
            {
                double densityMultFromOcean = Math.Max(-altitude, 1);
                densityMultFromOcean *= UNDERWATER_DENSITY_FACTOR_MINUS_ONE;
                densityMultFromOcean++;
                pressure *= densityMultFromOcean;
            }

            return pressure / (temp * currentBodyAtm[2]);
        }

        public static double GetCurrentDensity(CelestialBody body, double altitude, bool densitySmoothingAtOcean = true)
        {
            UpdateCurrentActiveBody(body);

            if (altitude > body.maxAtmosphereAltitude)
                return 0;

            double temp = Math.Max(0.1, currentBodyTemp + FlightGlobals.getExternalTemperature((float)altitude, body));

            double pressure = FlightGlobals.getStaticPressure(altitude, body);
            if (pressure > 0)
                pressure = (pressure - currentBodyAtmPressureOffset) * 101300;     //Need to convert atm to Pa

            if (altitude < 0 && densitySmoothingAtOcean)
            {
                double densityMultFromOcean = Math.Max(-altitude, 1);
                densityMultFromOcean *= UNDERWATER_DENSITY_FACTOR_MINUS_ONE;
                densityMultFromOcean++;
                pressure *= densityMultFromOcean;
            }

            return pressure / (temp * currentBodyAtm[2]);
        }

        // Vessel has altitude and cached pressure, and both density and sound speed need temperature
        public static double GetCurrentDensity(Vessel vessel, out double soundspeed, bool densitySmoothingAtOcean = true)
        {
            double altitude = vessel.altitude;
            CelestialBody body = vessel.mainBody;

            soundspeed = 1e+6f;

            if ((object)body == null || altitude > body.maxAtmosphereAltitude)
                return 0;

            UpdateCurrentActiveBody(body);

            double temp = Math.Max(0.1, currentBodyTemp + FlightGlobals.getExternalTemperature((float)altitude, body));

            double pressure = 0;
            if (vessel.staticPressure > 0)
                pressure = (vessel.staticPressure - currentBodyAtmPressureOffset) * 101300;     //Need to convert atm to Pa

            soundspeed = Math.Sqrt(temp * currentBodyAtm[0]); // * 401.8f;              //Calculation for speed of sound in ideal gas using air constants of gamma = 1.4 and R = 287 kJ/kg*K

            if (altitude < 0 && densitySmoothingAtOcean)
            {
                double densityMultFromOcean = Math.Max(-altitude, 1);
                densityMultFromOcean *= UNDERWATER_DENSITY_FACTOR_MINUS_ONE;
                densityMultFromOcean++;
                pressure *= densityMultFromOcean;
            }

            return pressure / (temp * currentBodyAtm[2]);
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


        public static double SkinFrictionDrag(double density, double lengthScale, double vel, double machNumber, double temp)
        {
            if (lengthScale == 0)
                return 0;

            double refTemp = temp * ReferenceTemperatureRatio(machNumber, 0.843);
            double visc = CalculateCurrentViscosity(refTemp);
            double Re = lengthScale * density * vel / visc;

            if (Re < TRANSITION_REYNOLDS_NUMBER)
            {
                double invSqrtRe = 1 / Math.Sqrt(Re);
                double lamCf = 1.328 * invSqrtRe;

                double rarefiedGasVal = machNumber * invSqrtRe;
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

        public static void UpdateCurrentActiveBody(CelestialBody body)
        {
            if ((object)body != null && body.flightGlobalsIndex != prevBody)
            {
                UpdateCurrentActiveBody(body.flightGlobalsIndex, body);
//                if (body.name == "Jool" || body.name == "Sentar")
                if(body.pqsController == null)
                    currentBodyTemp += FARAeroUtil.JoolTempOffset;
            }
        }

        public static void UpdateCurrentActiveBody(int index, CelestialBody body)
        {
            if (index != prevBody)
            {
                prevBody = index;
                currentBodyAtm = bodyAtmosphereConfiguration[prevBody];
                currentBodyTemp = 273.15f;
                if(body.useLegacyAtmosphere && body.atmosphere)
                {
                    currentBodyAtmPressureOffset = body.atmosphereMultiplier * 1e-6;
                }
                else
                    currentBodyAtmPressureOffset = 0;

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
