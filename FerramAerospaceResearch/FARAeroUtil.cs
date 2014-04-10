/*
Ferram Aerospace Research v0.13.1
Copyright 2013, Michael Ferrara, aka Ferram4

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
using UnityEngine;

namespace ferram4
{
    static class FARAeroUtil
    {
        private static FloatCurve prandtlMeyerMach = null;
        private static FloatCurve prandtlMeyerAngle = null;
        public static float maxPrandtlMeyerTurnAngle = 0;
        private static FloatCurve pressureBehindShock = null;
        private static FloatCurve machBehindShock = null;
        private static FloatCurve stagnationPressure = null;
//        private static FloatCurve criticalMachNumber = null;
        private static FloatCurve liftslope = null;
        private static FloatCurve maxPressureCoefficient = null;

        public static float areaFactor;
        public static float attachNodeRadiusFactor;
        public static float incompressibleRearAttachDrag;
        public static float sonicRearAdditionalAttachDrag;

        public static Dictionary<int, Vector3> bodyAtmosphereConfiguration = null;
        public static int prevBody = -1;
        public static Vector3 currentBodyAtm = new Vector3();
        public static float currentBodyTemp = 273.15f;

        public static FloatCurve MaxPressureCoefficient
        {
            get
            {
                if (maxPressureCoefficient == null)
                {
                    MonoBehaviour.print("Stagnation Pressure Coefficient Curve Initialized");
                    maxPressureCoefficient = new FloatCurve();

                    float M = 0.05f;
                    //float gamma = 1.4f;

                    maxPressureCoefficient.Add(0, 1);

                    if (currentBodyAtm == new Vector3())
                    {
                        currentBodyAtm.y = 1.4f;
                        currentBodyAtm.z = 8.3145f * 1000f / 28.96f;
                        currentBodyAtm.x = currentBodyAtm.y * currentBodyAtm.z;
                    }

                    while (M < 50)
                    {
                        float value = 0;
                        if (M <= 1)
                        {
                            value = StagnationPressure.Evaluate(M);
                        }
                        else
                        {
                            value = (currentBodyAtm.y + 1) * (currentBodyAtm.y + 1);                  //Rayleigh Pitot Tube Formula; gives max stagnation pressure behind shock
                            value *= M * M;
                            value /= (4 * currentBodyAtm.y * M * M - 2 * (currentBodyAtm.y - 1));
                            value = Mathf.Pow(value, 3.5f);

                            value *= (1 - currentBodyAtm.y + 2 * currentBodyAtm.y * M * M);
                            value /= (currentBodyAtm.y + 1);
                        }
                        value--;                                //and now to conver to pressure coefficient
                        value *= 2 / (currentBodyAtm.y * M * M);


                        maxPressureCoefficient.Add(M, value);


                        if (M < 2)
                            M += 0.1f;
                        else if (M < 5)
                            M += 0.5f;
                        else
                            M += 2.5f;
                    }



                }


                return maxPressureCoefficient;
            }
        }

        public static FloatCurve LiftSlope
        {
            get
            {
                if (liftslope == null)
                {
                    MonoBehaviour.print("Lift Slope Curve Initialized");
                    liftslope = new FloatCurve();
                    for (int i = 0; i < 50; i++)
                    {
                        float tmp = i * i + 4;
                        tmp = Mathf.Sqrt(tmp);
                        tmp += 2;
                        tmp = 1 / tmp;
                        tmp *= 2 * Mathf.PI;
                        liftslope.Add(i, tmp);
                    }
                }
                return liftslope;
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
                    float M = 1;
                    //float gamma = 1.4f;

                    float gamma_ = Mathf.Sqrt((currentBodyAtm.y + 1) / (currentBodyAtm.y - 1));

                    while (M < 250)
                    {
                        float mach = Mathf.Sqrt(M * M - 1);

                        float nu = Mathf.Atan(mach / gamma_);
                        nu *= gamma_;
                        nu -= Mathf.Atan(mach);
                        nu *= 180 / Mathf.PI;

                        float nu_mach = (currentBodyAtm.y - 1) / 2;
                        nu_mach *= M * M;
                        nu_mach++;
                        nu_mach *= M;
                        nu_mach = mach / nu_mach;
                        nu_mach *= 180 / Mathf.PI;

                        prandtlMeyerMach.Add(M, nu, nu_mach, nu_mach);

                        nu_mach = 1 / nu_mach;

                        prandtlMeyerAngle.Add(nu, M, nu_mach, nu_mach);

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
                    float M = 1;
                    //float gamma = 1.4f;


                    float gamma_ = Mathf.Sqrt((currentBodyAtm.y + 1) / (currentBodyAtm.y - 1));

                    while (M < 250)
                    {
                        float mach = Mathf.Sqrt(M * M - 1);

                        float nu = Mathf.Atan(mach / gamma_);
                        nu *= gamma_;
                        nu -= Mathf.Atan(mach);
                        nu *= 180 / Mathf.PI;

                        float nu_mach = (currentBodyAtm.y - 1) / 2;
                        nu_mach *= M * M;
                        nu_mach++;
                        nu_mach *= M;
                        nu_mach = mach / nu_mach;
                        nu_mach *= 180 / Mathf.PI;

                        prandtlMeyerMach.Add(M, nu, nu_mach, nu_mach);

                        nu_mach = 1 / nu_mach;

                        prandtlMeyerAngle.Add(nu, M, nu_mach, nu_mach);

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
                    float ratio;
                    float d_ratio;
                    float M = 1;
                    //float gamma = 1.4f;
                    while (M < 250)  //Calculates the pressure behind a normal shock
                    {
                        ratio = M * M;
                        ratio--;
                        ratio *= 2 * currentBodyAtm.y;
                        ratio /= (currentBodyAtm.y + 1);
                        ratio++;

                        d_ratio = M * 4 * currentBodyAtm.y;
                        d_ratio /= (currentBodyAtm.y + 1);

                        pressureBehindShock.Add(M, ratio, d_ratio, d_ratio);
                        if (M < 3)
                            M += 0.1f;
                        else if (M < 10)
                            M += 0.5f;
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
                    float ratio;
                    float d_ratio;
                    float M = 1;
                    //float gamma = 1.4f;
                    while (M < 250)  //Calculates the pressure behind a normal shock
                    {
                        ratio = (currentBodyAtm.y - 1) / 2;
                        ratio *= M * M;
                        ratio++;
                        ratio /= (currentBodyAtm.y * M * M - (currentBodyAtm.y - 1) / 2);

                        d_ratio = 4 * currentBodyAtm.y * currentBodyAtm.y * Mathf.Pow(M, 4) - 4 * (currentBodyAtm.y - 1) * currentBodyAtm.y * M * M + Mathf.Pow(currentBodyAtm.y - 1, 2);
                        d_ratio = 1 / d_ratio;
                        d_ratio *= 4 * (currentBodyAtm.y * M * M - (currentBodyAtm.y - 1) / 2) * (currentBodyAtm.y - 1) * M - 8 * currentBodyAtm.y * M * (1 + (currentBodyAtm.y - 1) / 2 * M * M);

                        machBehindShock.Add(Mathf.Sqrt(M), ratio);//, d_ratio, d_ratio);
                        if (M < 3)
                            M += 0.1f;
                        else if (M < 10)
                            M += 0.5f;
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
                    float ratio;
                    float d_ratio;
                    float M = 0;
                    //float gamma = 1.4f;
                    while (M < 250)  //calculates stagnation pressure
                    {
                        ratio = M * M;
                        ratio *= (currentBodyAtm.y - 1);
                        ratio /= 2;
                        ratio++;
                        
                        d_ratio = ratio;

                        ratio = Mathf.Pow(ratio, currentBodyAtm.y / (currentBodyAtm.y - 1));

                        d_ratio = Mathf.Pow(d_ratio, (currentBodyAtm.y / (currentBodyAtm.y - 1)) - 1);
                        d_ratio *= M * currentBodyAtm.y;

                        stagnationPressure.Add(M, ratio, d_ratio, d_ratio);
                        if (M < 3)
                            M += 0.1f;
                        else if (M < 10)
                            M += 0.5f;
                        else if (M < 25)
                            M += 2;
                        else
                            M += 25;
                    }
                }
                return stagnationPressure;
            }
        }


/*        public static FloatCurve CriticalMachNumber
        {
            get
            {
                if (criticalMachNumber == null)
                {
                    MonoBehaviour.print("Critical Mach Curve Initialized");
                    criticalMachNumber = new FloatCurve();
                    criticalMachNumber.Add(0, 0.98f);
                    criticalMachNumber.Add(Mathf.PI / 36, 0.86f);
                    criticalMachNumber.Add(Mathf.PI / 18, 0.65f);
                    criticalMachNumber.Add(Mathf.PI / 9, 0.35f);
                    criticalMachNumber.Add(Mathf.PI / 2, 0.3f);
                }
                return criticalMachNumber;
            }
        }*/

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

        private static FloatCurve wingCamberFactor = null;
        private static FloatCurve wingCamberMoment = null;

        public static FloatCurve WingCamberFactor
        {
            get
            {
                if (wingCamberFactor == null)
                {
                    wingCamberFactor = new FloatCurve();
                    wingCamberFactor.Add(0, 0);
                    for (float i = 0.1f; i <= 0.9f; i += 0.1f)
                    {
                        float tmp = i * 2;
                        tmp--;
                        tmp = Mathf.Acos(tmp);

                        tmp = tmp - Mathf.Sin(tmp);
                        tmp /= Mathf.PI;
                        tmp = 1 - tmp;

                        wingCamberFactor.Add(i, tmp);
                    }
                    wingCamberFactor.Add(1, 1);
                }
                return wingCamberFactor;
            }
        }

        public static FloatCurve WingCamberMoment
        {
            get
            {
                if (wingCamberMoment == null)
                {
                    wingCamberMoment = new FloatCurve();
                    for (float i = 0f; i <= 1f; i += 0.1f)
                    {
                        float tmp = i * 2;
                        tmp--;
                        tmp = Mathf.Acos(tmp);

                        tmp = (Mathf.Sin(2 * tmp) - 2 * Mathf.Sin(tmp)) / (8 * (Mathf.PI - tmp + Mathf.Sin(tmp)));

                        wingCamberMoment.Add(i, tmp);
                    }
                }
                return wingCamberMoment;
            }
        }

        public static bool IsNonphysical(Part p)
        {
            return p.physicalSignificance == Part.PhysicalSignificance.NONE ||
                   (HighLogic.LoadedSceneIsEditor &&
                    p != EditorLogic.startPod &&
                    p.PhysicsSignificance == (int)Part.PhysicalSignificance.NONE);
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
        }

        // Checks if there are any ghost parts almost attached to the craft
        public static bool EditorAboutToAttach(bool move_too = false)
        {
            return HighLogic.LoadedSceneIsEditor &&
                   EditorLogic.SelectedPart != null &&
                   (EditorLogic.SelectedPart.potentialParent != null ||
                     (move_too && EditorLogic.SelectedPart == EditorLogic.startPod));
        }

        public static List<Part> ListEditorParts(bool include_selected)
        {
            var list = new List<Part>();

            if (EditorLogic.startPod)
                RecursePartList(list, EditorLogic.startPod);

            if (include_selected && EditorAboutToAttach())
            {
                RecursePartList(list, EditorLogic.SelectedPart);

                foreach (Part sym in EditorLogic.SelectedPart.symmetryCounterparts)
                    RecursePartList(list, sym);
            }

            return list;
        }

        private static void RecursePartList(List<Part> list, Part part)
        {
            list.Add(part);
            foreach (Part p in part.children)
                RecursePartList(list, p);
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
            //if (IsNonphysical(p))
            //    return;

            //MonoBehaviour.print(p + ": " + p.PhysicsSignificance + " " + p.physicalSignificance);

            if (p.Modules.Contains("KerbalEVA"))
                return;

            p.angularDrag = 0;
            if (!p.Modules.Contains("ModuleResourceIntake"))
            {
                p.minimum_drag = 0;
                p.maximum_drag = 0;
                p.dragModelType = "override";
            }
            else
                return;

            p.AddModule("FARBasicDragModel");


            SetBasicDragModuleProperties(p);
        }

        public static void SetBasicDragModuleProperties(Part p)
        {
            FARBasicDragModel d = p.Modules["FARBasicDragModel"] as FARBasicDragModel;
            string title = p.partInfo.title.ToLowerInvariant();

            if (p.Modules.Contains("ModuleAsteroid"))
            {
                FARGeoUtil.BodyGeometryForDrag data = FARGeoUtil.CalcBodyGeometryFromMesh(p);

                FloatCurve TempCurve1 = new FloatCurve();
                float cd = 0.2f; //cd based on diameter
                cd *= Mathf.Sqrt(data.crossSectionalArea / Mathf.PI) * 2 / data.area;

                TempCurve1.Add(-1, cd);
                TempCurve1.Add(1, cd);

                FloatCurve TempCurve2 = new FloatCurve();
                TempCurve2.Add(-1, 0);
                TempCurve2.Add(1, 0);

                FloatCurve TempCurve4 = new FloatCurve();
                TempCurve2.Add(-1, 0);
                TempCurve2.Add(1, 0);

                FloatCurve TempCurve3 = new FloatCurve();
                TempCurve3.Add(-1, 0);
                TempCurve3.Add(1, 0);



                d.BuildNewDragModel(data.area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, data.originToCentroid, data.majorMinorAxisRatio, 0, data.taperCrossSectionArea);
                return;
            } 
            else if (p.Modules.Contains("ModuleRCS") || p.Modules.Contains("ModuleDeployableSolarPanel") || p.Modules.Contains("ModuleLandingGear") || title.Contains("heatshield") || (title.Contains("heat") && title.Contains("shield")))
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
                TempCurve2.Add(-1, 0);
                TempCurve2.Add(1, 0);

                FloatCurve TempCurve3 = new FloatCurve();
                TempCurve3.Add(-1, 0);
                TempCurve3.Add(1, 0);


                float area = FARGeoUtil.CalcBodyGeometryFromMesh(p).area;

                d.BuildNewDragModel(area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, Vector3.zero, 1, 0, 0);
                return;
            }
            else
            {
                FARGeoUtil.BodyGeometryForDrag data = FARGeoUtil.CalcBodyGeometryFromMesh(p);
                FloatCurve TempCurve1 = new FloatCurve();
                FloatCurve TempCurve2 = new FloatCurve();
                FloatCurve TempCurve4 = new FloatCurve();
                FloatCurve TempCurve3 = new FloatCurve();

                float Cn1, Cn2, cutoffAngle, cosCutoffAngle = 0;

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

                        cosCutoffAngle = -Mathf.Cos(cutoffAngle);

                        float axialPressureDrag = PressureDragDueToTaperingConic(data.finenessRatio, data.taperRatio, data.crossSectionalArea, data.area);

                        TempCurve1.Add(-1, axialPressureDrag);
                        TempCurve1.Add(0, Cn2);
                        TempCurve1.Add(1, axialPressureDrag);


                        if (cutoffAngle > 30)
                            TempCurve2.Add(cosCutoffAngle, 0, Cn1, 0);
                        else
                            TempCurve2.Add(-0.9f, 0, Cn1, 0);
                        TempCurve2.Add(-0.8660f, (Mathf.Cos((Mathf.PI / 2 - Mathf.Acos(0.8660f)) / 2) * Mathf.Sin(2 * (Mathf.PI / 2 - Mathf.Acos(0.8660f))) * Cn1), 0, 0);
                        TempCurve2.Add(0, 0);
                        TempCurve2.Add(0.8660f, (Mathf.Cos((Mathf.PI / 2 - Mathf.Acos(0.8660f)) / 2) * Mathf.Sin(2 * (Mathf.PI / 2 - Mathf.Acos(0.8660f))) * Cn1), 0, 0);
                        TempCurve2.Add(1, 0, Cn1, Cn1);

                        TempCurve4.Add(-1, 0, 0, 0);
                        TempCurve4.Add(-0.95f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.95f)), 2) * Cn2 * -0.95f);
                        TempCurve4.Add(-0.8660f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.8660f)), 2) * Cn2 * -0.8660f);
                        TempCurve4.Add(-0.5f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.5f)), 2) * Cn2 * -0.5f);
                        TempCurve4.Add(0, 0);
                        TempCurve4.Add(0.5f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.5f)), 2) * Cn2 * 0.5f);
                        TempCurve4.Add(0.8660f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.8660f)), 2) * Cn2 * 0.8660f);
                        TempCurve4.Add(0.95f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.95f)), 2) * Cn2 * 0.95f);
                        TempCurve4.Add(1, 0, 0, 0);
                    }
                    else
                    {
                        Cn1 = NormalForceCoefficientTerm1(data.finenessRatio, 1 / data.taperRatio, data.crossSectionalArea, data.area);
                        Cn2 = NormalForceCoefficientTerm2(data.finenessRatio, 1 / data.taperRatio, data.crossSectionalArea, data.area);
                        cutoffAngle = cutoffAngleForLift(data.finenessRatio, 1 / data.taperRatio, data.crossSectionalArea, data.area);

                        cosCutoffAngle = Mathf.Cos(cutoffAngle);

                        float axialPressureDrag = PressureDragDueToTaperingConic(data.finenessRatio, 1 / data.taperRatio, data.crossSectionalArea, data.area);

                        if (p.Modules.Contains("FARPayloadFairingModule"))
                            TempCurve1.Add(-1, -0.001f + axialPressureDrag, 0, 0);
                        else
                            TempCurve1.Add(-1, axialPressureDrag, 0, 0);
                        TempCurve1.Add(0, Cn2, 0, 0);
                        if (p.Modules.Contains("FARPayloadFairingModule"))
                            TempCurve1.Add(1, -0.001f + axialPressureDrag, 0, 0);
                        else
                            TempCurve1.Add(1, axialPressureDrag, 0, 0);


                        TempCurve2.Add(-1, 0, -Cn1, -Cn1);
                        TempCurve2.Add(-0.8660f, (-Mathf.Cos((Mathf.PI / 2 - Mathf.Acos(0.8660f)) / 2) * Mathf.Sin(2 * (Mathf.PI / 2 - Mathf.Acos(0.8660f))) * Cn1), 0, 0);
                        TempCurve2.Add(0, 0);
                        TempCurve2.Add(0.8660f, (-Mathf.Cos((Mathf.PI / 2 - Mathf.Acos(0.8660f)) / 2) * Mathf.Sin(2 * (Mathf.PI / 2 - Mathf.Acos(0.8660f))) * Cn1), 0, 0);
                        if(cutoffAngle > 30)
                            TempCurve2.Add(cosCutoffAngle, 0, -Cn1, 0);
                        else
                            TempCurve2.Add(0.9f, 0, -Cn1, 0);

                        TempCurve4.Add(-1, 0, 0, 0);
                        TempCurve4.Add(-0.95f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.95f)), 2) * Cn2 * -0.95f);
                        TempCurve4.Add(-0.8660f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.8660f)), 2) * Cn2 * -0.8660f);
                        TempCurve4.Add(-0.5f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.5f)), 2) * Cn2 * -0.5f);
                        TempCurve4.Add(0, 0);
                        TempCurve4.Add(0.5f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.5f)), 2) * Cn2 * 0.5f);
                        TempCurve4.Add(0.8660f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.8660f)), 2) * Cn2 * 0.8660f);
                        TempCurve4.Add(0.95f, Mathf.Pow(Mathf.Sin(Mathf.Acos(0.95f)), 2) * Cn2 * 0.95f);
                        TempCurve4.Add(1, 0, 0, 0);
                    }


                    /*                    TempCurve2.Add(-1, 0);
                                        TempCurve2.Add(-0.866f, -0.138f);
                                        TempCurve2.Add(-0.5f, -0.239f, 0, 0);
                                        TempCurve2.Add(0, 0);
                                        TempCurve2.Add(0.5f, 0.239f, 0, 0);
                                        TempCurve2.Add(0.866f, 0.138f);
                                        TempCurve2.Add(1, 0);*/

/*                    if (p.partInfo.title.ToLowerInvariant().Contains("nose"))
                    {
                        TempCurve3.Add(-1, -0.1f, 0, 0);
                        TempCurve3.Add(-0.5f, -0.1f, 0, 0);
                        TempCurve3.Add(0, -0.1f, 0, 0);
                        TempCurve3.Add(0.8660f, 0f, 0, 0);
                        TempCurve3.Add(1, 0.1f, 0, 0);
                    }
                    else
                    {*/

                    float cdM = MomentDueToTapering(data.finenessRatio, data.taperRatio, data.crossSectionalArea, data.area);

                        TempCurve3.Add(-1, cdM);
                        TempCurve3.Add(-0.5f, cdM * 2);
                        TempCurve3.Add(0, cdM * 3);
                        TempCurve3.Add(0.5f, cdM * 2);
                        TempCurve3.Add(1, cdM);
//                    }

                }
//                if (p.Modules.Contains("FARPayloadFairingModule"))
//                    data.area /= p.symmetryCounterparts.Count + 1;

                d.BuildNewDragModel(data.area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, data.originToCentroid, data.majorMinorAxisRatio, cosCutoffAngle, data.taperCrossSectionArea);
                return;
            }
        }


        //Approximate drag of a tapering conic body
        public static float PressureDragDueToTaperingConic(float finenessRatio, float taperRatio, float crossSectionalArea, float surfaceArea)
        {
            float b = 1 + (taperRatio / (1 - taperRatio));

            float refbeta = 2f * Mathf.Clamp(finenessRatio, 1, Mathf.Infinity) / Mathf.Sqrt(Mathf.Pow(2.5f, 2) - 1);     //Reference beta for the calculation; currently based at Mach 2.5
            float cdA;
            if (float.IsNaN(b) || float.IsInfinity(b))
            {
                return 0;
            }
            else if (b > 1)
            {
                cdA = 2 * (2 * b * b - 2 * b + 1) * Mathf.Log(2 * refbeta) - 2 * Mathf.Pow(b - 1, 2) * Mathf.Log(1 - 1 / b) - 1;
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
                cdA = 2 * Mathf.Log(2 * refbeta) - 1;
            }

            cdA *= 0.25f / (finenessRatio * finenessRatio);
            cdA *= crossSectionalArea / surfaceArea;

            cdA /= 1.31f;   //Approximate subsonic drag...

            //if (float.IsNaN(cdA))
            //    return 0;

            return cdA;
        }
        
        //Approximate drag of a tapering parabolic body
        public static float PressureDragDueToTaperingParabolic(float finenessRatio, float taperRatio, float crossSectionalArea, float surfaceArea)
        {
            float exponent = 2 + (Mathf.Sqrt(Mathf.Clamp(finenessRatio, 0.1f, Mathf.Infinity)) - 2.2f) / 3;

            float cdA = 4.6f * Mathf.Pow(Mathf.Abs(taperRatio - 1), 2 * exponent);
            cdA *= 0.25f / (finenessRatio * finenessRatio);
            cdA *= crossSectionalArea / surfaceArea;

            cdA /= 1.35f;   //Approximate subsonic drag...

            
            float taperCrossSectionArea = Mathf.Sqrt(crossSectionalArea / Mathf.PI);
            taperCrossSectionArea *= taperRatio;
            taperCrossSectionArea = Mathf.Pow(taperCrossSectionArea, 2) * Mathf.PI;

            taperCrossSectionArea = Mathf.Abs(taperCrossSectionArea - crossSectionalArea);      //This is the cross-sectional area of the tapered section

            float maxcdA = taperCrossSectionArea * (incompressibleRearAttachDrag + sonicRearAdditionalAttachDrag);
            maxcdA /= surfaceArea;      //This is the maximum drag that this part can create

            cdA = Mathf.Clamp(cdA, 0, maxcdA);      //make sure that stupid amounts of drag don't exist

            return cdA;
        }


        //This returns the normal force coefficient based on surface area due to potential flow
        public static float NormalForceCoefficientTerm1(float finenessRatio, float taperRatio, float crossSectionalArea, float surfaceArea)
        {
            float Cn1 = 0;
            //float radius = Mathf.Sqrt(crossSectionalArea * 0.318309886184f);
            //float length = radius * 2 * finenessRatio;

            /*//Assuming a linearly tapered cone
            Cn1 = 1 + (taperRatio - 1) + (taperRatio - 1) * (taperRatio - 1) / 3;
            Cn1 *= Mathf.PI * radius * radius * length;*/

            Cn1 = crossSectionalArea * (1 - taperRatio * taperRatio);
            Cn1 /= surfaceArea;

            return Cn1;
            //return 0;
        }

        //This returns the normal force coefficient based on surface area due to viscous flow
        public static float NormalForceCoefficientTerm2(float finenessRatio, float taperRatio, float crossSectionalArea, float surfaceArea)
        {
            float Cn2 = 0;
            float radius = Mathf.Sqrt(crossSectionalArea * 0.318309886184f);
            float length = radius * 2 * finenessRatio;

            //Assuming a linearly tapered cone
            Cn2 = radius * (1 + taperRatio) * length * 0.5f;
            Cn2 *= 2 * 1.2f;
            Cn2 /= surfaceArea;

            return Cn2;
            //return 0;
        }

        public static float cutoffAngleForLift(float finenessRatio, float taperRatio, float crossSectionalArea, float surfaceArea)
        {
            float angle = 0;

            angle = (1 - taperRatio) / (2 * finenessRatio);
            angle = Mathf.Atan(angle);

            return angle;
        }

        public static float MomentDueToTapering(float finenessRatio, float taperRatio, float crossSectionalArea, float surfaceArea)
        {

            float rad = crossSectionalArea / Mathf.PI;
            rad = Mathf.Sqrt(rad);

            float cdM = 0f;
            if (taperRatio < 1)
            {
                float dragDueToTapering = PressureDragDueToTaperingConic(finenessRatio, taperRatio, crossSectionalArea, surfaceArea);

                cdM -= dragDueToTapering * rad * taperRatio;    //This applies the drag force over the front of the tapered area multiplied by the distance it acts from the center of the part (radius * taperRatio)
            }
            else
            {
                taperRatio = 1 / taperRatio;
                float dragDueToTapering = PressureDragDueToTaperingConic(finenessRatio, taperRatio, crossSectionalArea, surfaceArea);

                cdM += dragDueToTapering * rad * taperRatio;    //This applies the drag force over the front of the tapered area multiplied by the distance it acts from the center of the part (radius * taperRatio)
            }

            //cdM *= 1 / surfaceArea;

            return cdM;
        }

        //This approximates e^x; it's slightly inaccurate, but good enough.  It's much faster than an actual exponential function
        //It runs on the assumption e^x ~= (1 + x/256)^256
        public static float ExponentialApproximation(float x)
        {
            float exp = 1f + x * 0.00390625f;
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


        public static float GetMachNumber(CelestialBody body, float altitude, Vector3 velocity)
        {
            float MachNumber = 0;
            if (HighLogic.LoadedSceneIsFlight)
            {
                //continue updating Mach Number for debris
                UpdateCurrentActiveBody(body);
                float temp = Mathf.Max(0.1f, currentBodyTemp + FlightGlobals.getExternalTemperature(altitude, body));
                float Soundspeed = Mathf.Sqrt(temp * currentBodyAtm.x);// * 401.8f;              //Calculation for speed of sound in ideal gas using air constants of gamma = 1.4 and R = 287 kJ/kg*K

                MachNumber = velocity.magnitude / Soundspeed;

                if (MachNumber < 0)
                    MachNumber = 0;

            }
            return MachNumber;
        }

        public static float GetCurrentDensity(CelestialBody body, Vector3 worldLocation)
        {
            UpdateCurrentActiveBody(body);

            float temp = Mathf.Max(0.1f, currentBodyTemp + FlightGlobals.getExternalTemperature(worldLocation));

            float pressure = (float)FlightGlobals.getStaticPressure(worldLocation, body) * 101300f;     //Need to convert atm to Pa

            return pressure / (temp * currentBodyAtm.z);
        }

        public static float GetCurrentDensity(CelestialBody body, float altitude)
        {
            UpdateCurrentActiveBody(body);

            if (altitude > body.maxAtmosphereAltitude)
                return 0;

            float temp = Mathf.Max(0.1f, currentBodyTemp + FlightGlobals.getExternalTemperature(altitude, body));

            float pressure = (float)FlightGlobals.getStaticPressure(altitude, body) * 101300f;     //Need to convert atm to Pa

            return pressure / (temp * currentBodyAtm.z);
        }

        // Vessel has altitude and cached pressure, and both density and sound speed need temperature
        public static float GetCurrentDensity(Vessel vessel, out float soundspeed)
        {
            float altitude = (float)vessel.altitude;
            CelestialBody body = vessel.mainBody;

            soundspeed = 1e+6f;

            if ((object)body == null || altitude > body.maxAtmosphereAltitude)
                return 0;

            UpdateCurrentActiveBody(body);

            float temp = Mathf.Max(0.1f, currentBodyTemp + FlightGlobals.getExternalTemperature(altitude, body));
            float pressure = (float)vessel.staticPressure * 101300f;     //Need to convert atm to Pa

            soundspeed = Mathf.Sqrt(temp * currentBodyAtm.x); // * 401.8f;              //Calculation for speed of sound in ideal gas using air constants of gamma = 1.4 and R = 287 kJ/kg*K

            return pressure / (temp * currentBodyAtm.z);
        }

        public static void UpdateCurrentActiveBody(CelestialBody body)
        {
            if ((object)body != null && body.flightGlobalsIndex != prevBody)
            {
                UpdateCurrentActiveBody(body.flightGlobalsIndex);
                if (body.name == "Jool" || body.name == "Sentar")
                    currentBodyTemp += FARAeroUtil.JoolTempOffset;
            }
        }

        public static void UpdateCurrentActiveBody(int index)
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
        public static float CalculateSinWeakObliqueShockAngle(float MachNumber, float gamma, float deflectionAngle)
        {
            float M2 = MachNumber * MachNumber;
            float recipM2 = 1 / M2;
            float sin2def = FARMathUtil.FastSin(deflectionAngle);
            sin2def *= sin2def;

            float b = M2 + 2;
            b *= recipM2;
            b += gamma * sin2def;
            b = -b;

            float c = gamma + 1;
            c *= c * 0.25f;
            c += (gamma - 1) * recipM2;
            c *= sin2def;
            c += (2 * M2 + 1) * recipM2 * recipM2;

            float d = sin2def - 1;
            d *= recipM2 * recipM2;

            float Q = c * 0.33333333f - b * b * 0.111111111f;
            float R = 0.16666667f * b * c - 0.5f * d - 0.037037037f * b * b * b;
            float D = Q * Q * Q + R * R;

            if (D > 0.001f)
                return float.NaN;

            float phi = Mathf.Atan(Mathf.Sqrt(Mathf.Clamp(-D, 0, Mathf.Infinity)) / R);
            if (R < 0)
                phi += Mathf.PI;
            phi *= 0.33333333f;

            float chiW = -0.33333333f * b - Mathf.Sqrt(Mathf.Clamp(-Q, 0, Mathf.Infinity)) * (FARMathUtil.FastCos(phi) - 1.7320508f * FARMathUtil.FastSin(phi));

            float betaW = Mathf.Sqrt(Mathf.Clamp(chiW, 0, Mathf.Infinity));

            return betaW;
        }

        public static float CalculateSinMaxShockAngle(float MachNumber, float gamma)
        {
            float M2 = MachNumber * MachNumber;
            float gamP1_2_M2 = (gamma + 1) * 0.5f * M2;

            float b = gamP1_2_M2;
            b = 2 - b;
            b *= M2;

            float a = gamma * M2 * M2;

            float c = gamP1_2_M2 + 1;
            c = -c;

            float sin2def = -b + Mathf.Sqrt(Mathf.Clamp(b * b - 4 * a * c, 0, Mathf.Infinity));
            sin2def /= (2 * a);

            return Mathf.Sqrt(sin2def);
        }
    }
}
