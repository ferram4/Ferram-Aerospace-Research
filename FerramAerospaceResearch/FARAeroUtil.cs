/*
Ferram Aerospace Research v0.15.5.4 "Hoerner"
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
using System.IO;
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch
{
    public static class FARAeroUtil
    {
        private static FloatCurve prandtlMeyerMach = null;
        private static FloatCurve prandtlMeyerAngle = null;
        public static double maxPrandtlMeyerTurnAngle = 0;
//        private static FloatCurve criticalMachNumber = null;
        //private static FloatCurve liftslope = null;

        public static double massPerWingAreaSupported;
        public static double massStressPower;
        public static bool AJELoaded;

        public static Dictionary<int, double[]> bodyAtmosphereConfiguration = null;
        public static int prevBodyIndex = -1;
        public static double[] currentBodyVisc = new double[2];
        private static CelestialBody currentBody = null;
        public static CelestialBody CurrentBody
        {
            get
            {
                if ((object)currentBody == null)
                {
                    if (FlightGlobals.Bodies[1] || !FlightGlobals.ActiveVessel)
                        currentBody = FlightGlobals.Bodies[1];
                    else
                        currentBody = FlightGlobals.ActiveVessel.mainBody;

                }

                return currentBody;
            }
        }

        public static bool loaded = false;
        
        //Based on ratio of density of water to density of air at SL
        private const double UNDERWATER_DENSITY_FACTOR_MINUS_ONE = 814.51020408163265306122448979592;
        //Standard Reynolds number for transition from laminar to turbulent flow
        private const double TRANSITION_REYNOLDS_NUMBER = 5e5;
        //Multiplier to skin friction due to surface roughness; approximately an 8% increase in drag
        private const double ROUGHNESS_SKIN_FRICTION_MULTIPLIER = 1.08;

        public static void SaveCustomAeroDataToConfig()
        {
            ConfigNode node = new ConfigNode("@FARAeroData[default]:FOR[FerramAerospaceResearch]");
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

            node.AddValue("viscosityAtReferenceTemp", atmProperties[0]);
            node.AddValue("referenceTemp", atmProperties[1]);

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
                    if (bodyProperties == null || !(bodyProperties.HasValue("index") || bodyProperties.HasValue("name")) || !bodyProperties.HasValue("viscosityAtReferenceTemp")
                        || !bodyProperties.HasValue("referenceTemp"))
                        continue;

                    double[] Rgamma_and_gamma = new double[2];
                    double tmp;
                    double.TryParse(bodyProperties.GetValue("viscosityAtReferenceTemp"), out tmp);

                    Rgamma_and_gamma[0] = tmp;

                    double.TryParse(bodyProperties.GetValue("referenceTemp"), out tmp);

                    Rgamma_and_gamma[1] = tmp;
                    int index = -1;

                    if(bodyProperties.HasValue("name"))
                    {
                        string name = bodyProperties.GetValue("name");

                        foreach(CelestialBody body in FlightGlobals.Bodies)
                            if(body.bodyName == name)
                            {
                                index = body.flightGlobalsIndex;
                                break;
                            }
                    }
                    
                    if(index < 0)
                        int.TryParse(bodyProperties.GetValue("index"), out index);

                    FARAeroUtil.bodyAtmosphereConfiguration.Add(index, Rgamma_and_gamma);
                }

            }

            //For any bodies that lack a configuration, use Earth-like properties
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (bodyAtmosphereConfiguration.ContainsKey(body.flightGlobalsIndex))
                    continue;

                double[] Rgamma_and_gamma = new double[2];
                Rgamma_and_gamma[0] = 1.7894e-5;
                Rgamma_and_gamma[1] = 288;

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
            currentBodyVisc = bodyAtmosphereConfiguration[1];
        }

        private static void SetDefaultValuesIfNoValuesLoaded()
        {
            if (massPerWingAreaSupported == 0)
                massPerWingAreaSupported = 0.05;
            if (massStressPower == 0)
                massStressPower = 1.2;
        }

        public static double MaxPressureCoefficientCalc(double M)
        {
            double gamma = CurrentBody.atmosphereAdiabaticIndex;

            if (M <= 0)
                return 1;
            double value;
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
            double gamma = CurrentBody.atmosphereAdiabaticIndex;

            double ratio;
            ratio = M * M;
            ratio *= (gamma - 1);
            ratio *= 0.5;
            ratio++;

            ratio = Math.Pow(ratio, gamma / (gamma - 1));
            return ratio;
        }

        public static double PressureBehindShockCalc(double M)
        {
            double gamma = CurrentBody.atmosphereAdiabaticIndex;

            double ratio;
            ratio = M * M;
            ratio *= 2 * gamma;
            ratio -= (gamma - 1);
            ratio /= (gamma + 1);

            return ratio;

        }

        public static double MachBehindShockCalc(double M)
        {
            double gamma = CurrentBody.atmosphereAdiabaticIndex;

            double ratio;
            ratio = (gamma - 1);
            ratio *= M * M;
            ratio += 2;
            ratio /= (2 * gamma * M * M - (gamma - 1));
            ratio = Math.Sqrt(ratio);

            return ratio;
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
                    double gamma = CurrentBody.atmosphereAdiabaticIndex;

                    double gamma_ = Math.Sqrt((gamma + 1) / (gamma - 1));

                    while (M < 250)
                    {
                        double mach = Math.Sqrt(M * M - 1);

                        double nu = Math.Atan(mach / gamma_);
                        nu *= gamma_;
                        nu -= Math.Atan(mach);
                        nu *= FARMathUtil.rad2deg;

                        double nu_mach = (gamma - 1) / 2;
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
                    double gamma = CurrentBody.atmosphereAdiabaticIndex;
                    double gamma_ = Math.Sqrt((gamma + 1) / (gamma - 1));

                    while (M < 250)
                    {
                        double mach = Math.Sqrt(M * M - 1);

                        double nu = Math.Atan(mach / gamma_);
                        nu *= gamma_;
                        nu -= Math.Atan(mach);
                        nu *= FARMathUtil.rad2deg;

                        double nu_mach = (gamma - 1) / 2;
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
        public static double GetCurrentDensity(Part p)
        {
            return (p.atmDensity * (1.0 - p.submergedPortion) + p.vessel.mainBody.oceanDensity * 1000 * p.submergedPortion * p.submergedDragScalar);// * fi.pseudoReDragMult);
        }

        public static double CalculateCurrentViscosity(double tempInK)
        {
            double visc = currentBodyVisc[0];        //get viscosity

            double tempRat = tempInK / currentBodyVisc[1];
            tempRat *= tempRat * tempRat;
            tempRat = Math.Sqrt(tempRat);

            visc *= (currentBodyVisc[1] + 110);
            visc /= (tempInK + 110);
            visc *= tempRat;

            return visc;
        }


        public static double ReferenceTemperatureRatio(double machNumber, double recoveryFactor, double gamma)
        {
            double tempRatio = machNumber * machNumber;
            tempRatio *= (gamma - 1);
            tempRatio *= 0.5;       //account for stagnation temp

            tempRatio *= recoveryFactor;    //this accounts for adiabatic wall temp ratio

            tempRatio *= 0.58;
            tempRatio += 0.032 * machNumber * machNumber;

            tempRatio++;
            return tempRatio;
        }

        public static double CalculateReynoldsNumber(double density, double lengthScale, double vel, double machNumber, double externalTemp, double gamma)
        {
            if (lengthScale == 0)
                return 0;

            double refTemp = externalTemp * ReferenceTemperatureRatio(machNumber, 0.843, gamma);
            double visc = CalculateCurrentViscosity(refTemp);
            double Re = lengthScale * density * vel / visc;
            return Re;
        }

        public static double SkinFrictionDrag(double density, double lengthScale, double vel, double machNumber, double externalTemp, double gamma)
        {
            if (lengthScale == 0)
                return 0;

            double Re = CalculateReynoldsNumber(density, lengthScale, vel, machNumber, externalTemp, gamma);

            return SkinFrictionDrag(Re, machNumber);
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
                    return (lamCf + (0.25 - lamCf) * (rarefiedGasVal - 0.01) / (0.99 + rarefiedGasVal)) * ROUGHNESS_SKIN_FRICTION_MULTIPLIER;
                }
                return lamCf * ROUGHNESS_SKIN_FRICTION_MULTIPLIER;
            }

            double transitionFraction = TRANSITION_REYNOLDS_NUMBER / reynoldsNumber;

            double laminarCf = 1.328 / Math.Sqrt(TRANSITION_REYNOLDS_NUMBER);
            double turbulentCfInLaminar = 0.074 / Math.Pow(TRANSITION_REYNOLDS_NUMBER, 0.2);
            double turbulentCf = 0.074 / Math.Pow(reynoldsNumber, 0.2);

            return (turbulentCf - transitionFraction * (turbulentCfInLaminar - laminarCf)) * ROUGHNESS_SKIN_FRICTION_MULTIPLIER;
        }

        public static void UpdateCurrentActiveBody(CelestialBody body)
        {
            if ((object)body != null && body.flightGlobalsIndex != prevBodyIndex)
            {
                UpdateCurrentActiveBody(body.flightGlobalsIndex, body);
            }
        }

        public static void UpdateCurrentActiveBody(int index, CelestialBody body)
        {
            if (index != prevBodyIndex)
            {
                prevBodyIndex = index;
                currentBodyVisc = bodyAtmosphereConfiguration[prevBodyIndex];
                currentBody = body;

                prandtlMeyerMach = null;
                prandtlMeyerAngle = null;
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

        //More modern, accurate Oswald's Efficiency
        //http://www.fzt.haw-hamburg.de/pers/Scholz/OPerA/OPerA_PUB_DLRK_12-09-10.pdf
        public static double CalculateOswaldsEfficiencyNitaScholz(double AR, double CosSweepAngle, double Cd0, double taperRatio)
        {
            //model coupling between taper and sweep
            double deltaTaper = Math.Acos(CosSweepAngle) * FARMathUtil.rad2deg;
            deltaTaper = ExponentialApproximation(-0.0375 * deltaTaper);
            deltaTaper *= 0.45;
            deltaTaper -= 0.357;

            taperRatio -= deltaTaper;
            
            //theoretic efficiency assuming an unswept wing with no sweep
            double straightWingE = 0.0524 * taperRatio;
            straightWingE += -0.15;
            straightWingE *= taperRatio;
            straightWingE += 0.1659;
            straightWingE *= taperRatio;
            straightWingE += -0.0706;
            straightWingE *= taperRatio;
            straightWingE += 0.0119;

            //Efficiency assuming only sweep and taper contributions; still need viscous contributions
            double theoreticE = straightWingE * AR + 1;
            theoreticE = 1 / theoreticE;

            double eWingInterference = 0.974008 * theoreticE;// 1 - 2 * (fuse dia / span)^2, using avg val for ratio (0.114) because it isn't easy to get here
                                                             //this results in this being a simple constant

            double e = 0.38 * Cd0 * AR * Math.PI;   //accounts for changes due to Mach number and compressibility
            e *= eWingInterference;
            e += 1;
            e = eWingInterference / e;

            return e;
        }
    }
}
