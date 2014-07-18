/*
Neophyte's Elementary Aerodynamics Replacement v1.0.2
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Neophyte's Elementary Aerodynamics Replacement.

    Neophyte's Elementary Aerodynamics Replacement is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Neophyte's Elementary Aerodynamics Replacement.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
            			Duxwing, for copy editing the readme
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NEAR
{
    public static class FARAeroUtil
    {
        public static double areaFactor;
        public static double attachNodeRadiusFactor;
        public static double rearNodeDragFactor;

        public static bool loaded = false;

        public static void LoadAeroDataFromConfig()
        {
            if (loaded)
                return;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("NEARAeroData"))
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
                if (node.HasValue("rearNodeDragFactor"))
                    double.TryParse(node.GetValue("rearNodeDragFactor"), out rearNodeDragFactor);

                if (node.HasValue("ctrlSurfTimeConstant"))
                    double.TryParse(node.GetValue("ctrlSurfTimeConstant"), out FARControllableSurface.timeConstant);
            }            
            loaded = true;
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
                double cd = 0.2; //cd based on diameter
                cd *= Math.Sqrt(data.crossSectionalArea / Math.PI) * 2 / data.area;

                TempCurve1.Add(-1, (float)cd);
                TempCurve1.Add(1, (float)cd);

                FloatCurve TempCurve2 = new FloatCurve();
                TempCurve2.Add(-1, 0);
                TempCurve2.Add(1, 0);

                FloatCurve TempCurve4 = new FloatCurve();
                TempCurve2.Add(-1, 0);
                TempCurve2.Add(1, 0);

                FloatCurve TempCurve3 = new FloatCurve();
                TempCurve3.Add(-1, 0);
                TempCurve3.Add(1, 0);



                d.BuildNewDragModel(data.area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, data.originToCentroid, data.majorMinorAxisRatio, 0, data.taperCrossSectionArea, double.MaxValue, double.MaxValue);
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
                TempCurve2.Add(-1, 0);
                TempCurve2.Add(1, 0);

                FloatCurve TempCurve3 = new FloatCurve();
                TempCurve3.Add(-1, 0);
                TempCurve3.Add(1, 0);


                double area = FARGeoUtil.CalcBodyGeometryFromMesh(p).area;

                d.BuildNewDragModel(area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, Vector3.zero, 1, 0, 0, double.MaxValue, double.MaxValue);
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


                        if (cutoffAngle > 30)
                            TempCurve2.Add((float)cosCutoffAngle, 0, (float)Cn1, 0);
                        else
                            TempCurve2.Add(-0.9f, 0, (float)Cn1, 0);
                        TempCurve2.Add(-0.8660f, (float)(Math.Cos((Math.PI * 0.5 - Math.Acos(0.8660f)) * 0.5) * Math.Sin(2 * (Math.PI * 0.5 - Math.Acos(0.8660f))) * Cn1), 0, 0);
                        TempCurve2.Add(0, 0);
                        TempCurve2.Add(0.8660f, (float)(Math.Cos((Math.PI * 0.5 - Math.Acos(0.8660f)) * 0.5) * Math.Sin(2 * (Math.PI * 0.5 - Math.Acos(0.8660f))) * Cn1), 0, 0);
                        TempCurve2.Add(1, 0, (float)Cn1, (float)Cn1);

                        TempCurve4.Add(-1, 0, 0, 0);
                        TempCurve4.Add(-0.95f, (float)(Math.Pow(Math.Sin(Math.Acos(0.95f)), 2) * Cn2 * -0.95f));
                        TempCurve4.Add(-0.8660f, (float)(Math.Pow(Math.Sin(Math.Acos(0.8660f)), 2) * Cn2 * -0.8660f));
                        TempCurve4.Add(-0.5f, (float)(Math.Pow(Math.Sin(Math.Acos(0.5f)), 2) * Cn2 * -0.5f));
                        TempCurve4.Add(0, 0);
                        TempCurve4.Add(0.5f, (float)(Math.Pow(Math.Sin(Math.Acos(0.5f)), 2) * Cn2 * 0.5f));
                        TempCurve4.Add(0.8660f, (float)(Math.Pow(Math.Sin(Math.Acos(0.8660f)), 2) * Cn2 * 0.8660f));
                        TempCurve4.Add(0.95f, (float)(Math.Pow(Math.Sin(Math.Acos(0.95f)), 2) * Cn2 * 0.95f));
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
                        if (cutoffAngle > 30)
                            TempCurve2.Add((float)cosCutoffAngle, 0, (float)-Cn1, 0);
                        else
                            TempCurve2.Add(0.9f, 0, (float)-Cn1, 0);

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

                    float cdM = (float)MomentDueToTapering(data.finenessRatio, data.taperRatio, data.crossSectionalArea, data.area);

                    TempCurve3.Add(-1, cdM);
                    TempCurve3.Add(-0.5f, cdM * 2);
                    TempCurve3.Add(0, cdM * 3);
                    TempCurve3.Add(0.5f, cdM * 2);
                    TempCurve3.Add(1, cdM);

                }
//                if (p.Modules.Contains("FARPayloadFairingModule"))
//                    data.area /= p.symmetryCounterparts.Count + 1;

                d.BuildNewDragModel(data.area * FARAeroUtil.areaFactor, TempCurve1, TempCurve2, TempCurve4, TempCurve3, data.originToCentroid, data.majorMinorAxisRatio, cosCutoffAngle, data.taperCrossSectionArea, YmaxForce, XZmaxForce);
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

            double maxcdA = taperCrossSectionArea * (rearNodeDragFactor);
            maxcdA /= surfaceArea;      //This is the maximum drag that this part can create

            cdA = FARMathUtil.Clamp(cdA, 0, maxcdA);      //make sure that stupid amounts of drag don't exist

            return cdA;
        }


        //This returns the normal force coefficient based on surface area due to potential flow
        public static double NormalForceCoefficientTerm1(double finenessRatio, double taperRatio, double crossSectionalArea, double surfaceArea)
        {
            double Cn1 = 0;
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
            //return 0;
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

        //Calculates Oswald's Efficiency e using Sheval's Method
        public static double CalculateOswaldsEfficiency(double AR, double CosSweepAngle, double Cd0)
        {
            double e = 1 - 0.02 * Math.Pow(AR, 0.7) * Math.Pow(Math.Acos(CosSweepAngle), 2.2);
            double tmp = AR * Cd0 * Mathf.PI + 1;
            e /= tmp;

            return e;
        }

        /*public static double SupersonicWingCna(double AR, double tanSweep, double B, double taperRatio, out bool subsonicLE)
        {
            //double B = Math.Sqrt(M * M - 1);
            double m = 1 / tanSweep;

            double AR_ = AR * B;
            double m_ = m * B;
            double k = taperRatio + 1;
            k = AR_ * k;
            k = k / (k - 4 * m_ * (1 - taperRatio));

            double machLineVal = 4 * m_ * taperRatio;
            machLineVal /= (taperRatio + 1) * (1 - m_);

            if (m_ >= 1)    //Supersonic leading edge
            {
                subsonicLE = false;
                if (AR_ < machLineVal)      //Mach line intercepts tip chord
                {
                    double m_k = m_ * k;
                    double fourm_k = 4 * m_k;
                    double invkm_kplusone = (m_k + 1) * k;
                    invkm_kplusone = 1 / invkm_kplusone;

                    double line4 = fourm_k + AR_ * (3 * k + 1);
                    line4 = (fourm_k * (AR_ - 1) + AR_ * (k - 1)) / line4;
                    line4 = Math.Acos(line4);

                    double tmp = (m_ - 1) * invkm_kplusone;
                    tmp = Math.Sqrt(tmp);
                    line4 *= tmp;

                    tmp = fourm_k + AR_ * (1 + 3 * k);
                    tmp *= tmp;
                    tmp /= (4 * AR_ * (k + 1));
                    line4 *= tmp;

                    double line3 = fourm_k - AR_ * (k - 1);
                    line3 = (fourm_k * (1 - AR_) + AR_ * (3 * k + 1)) / line4;
                    line3 = Math.Acos(line3);

                    tmp = (m_ + 1) * invkm_kplusone;
                    tmp = Math.Sqrt(tmp);
                    line3 *= tmp;

                    tmp = fourm_k - AR_ * (k - 1);
                    tmp *= tmp;
                    tmp /= (4 * AR_ * (k - 1));
                    line3 *= -tmp;


                    double line2 = fourm_k + AR_ * (k - 1);
                    line2 = (fourm_k * (AR_ - 1) - AR_ * (k + 3)) / line4;
                    line2 = -Math.Acos(line2);

                    line2 += Math.Acos(-1 / m_k);

                    tmp = (m_k + 1) * (m_k - 1);
                    tmp = Math.Sqrt(tmp);
                    tmp = k * Math.Sqrt(m_ * m_ - 1) / tmp;
                    line2 *= tmp;

                    tmp = Math.Acos(1 / m_);
                    tmp /= k;
                    line2 += tmp;

                    tmp = fourm_k + AR_ * (k - 1);
                    tmp *= tmp;
                    tmp /= (2 * AR_ * (k * k - 1));
                    line2 *= tmp;

                    double Cna = line2 + line3 + line4;
                    Cna /= (Math.PI * B * Math.Sqrt(m_ * m_ - 1));
                    return Cna;
                }
                else            //Mach line intercepts trailing edge
                {
                    double m_k = m_ * k;
                    double fourm_k = 4 * m_k;
                    double line2 = fourm_k - AR_ * (k - 1);
                    line2 *= Math.PI * line2;
                    line2 /= 4 * AR_ * (k - 1);

                    double tmp = (m_k + 1) * k;
                    tmp = m_ + 1 / tmp;
                    tmp = Math.Sqrt(tmp);

                    line2 *= -tmp;

                    double line1 = (m_k - 1) * (m_k + 1);
                    line1 = Math.Sqrt(line1);

                    tmp = Math.Sqrt(m_ * m_ - 1) * k;
                    line1 = tmp / line1;

                    line1 *= Math.Acos(-1 / m_k);

                    line1 += Math.Acos(1 / m_) / k;

                    tmp = fourm_k + AR_ * (k - 1);
                    tmp *= tmp;
                    tmp /= (2 * AR_ * (k * k - 1));

                    line1 *= tmp;

                    double Cna = line1 + line2;
                    Cna /= (Math.PI * B * Math.Sqrt(m_ * m_ - 1));
                    return Cna;
                }
            }
            else                       //Subsonic leading edge
            {
                subsonicLE = true;
                double w = 4 * m_ / (AR_ * (1 + taperRatio));
                double n = FARMathUtil.Clamp(1 - (1 - taperRatio) * w, 0, 1);

                //Debug.Log("n " + n);

                double longSqrtTerm = (1 + m_) * (n + 1) + w * (m_ - 1);
                longSqrtTerm *= (w + n - 1);
                longSqrtTerm = Math.Sqrt(longSqrtTerm);

                double smallACosTerm = 1 + m_ * n + w * (m_ - 1);
                smallACosTerm /= (m_ + n);
                smallACosTerm = Math.Acos(smallACosTerm);

                double invOneMinusMPow = Math.Pow(1 - m_, -1.5);

                double line4 = 2 * (m_ + n) * (1 - m_) + (1 + n);
                line4 = (1 + m_) * (1 + n) - w * (1 - m_) / line4;
                line4 *= longSqrtTerm;

                double line3 = (n + w) * (m_ - n) + 2 * (1 - w) + m_ + n;
                line3 /= (1 + n + w) * (m_ + n);
                line3 = Math.Acos(line3);

                double tmp = 1 + n + w;
                tmp *= tmp;
                tmp *= 0.25 * Math.Pow(1 + n, -1.5);
                line3 *= tmp;

                line3 -= smallACosTerm * invOneMinusMPow;

                double line34 = line3 + line4;

                tmp = 4 * AR;
                tmp /= (Math.PI * Math.Sqrt(1 + m_));
                line34 *= tmp;

                double line2 = w * n * (m_ - 1) + m_ * (n * n - 1) * Math.Sqrt(1 + m_);
                line2 /= (m_ + n) * (n * n - 1) * (m_ - 1);
                line2 *= longSqrtTerm;
                
                double line1 = (1 + m_) * (n * n - 1) + w * (1 + m_ * n);
                line1 /= w * (m_ + n);
                line1 = -Math.Asin(line1);
                line1 += Math.Asin(n);

                line1 *= w * w * Math.Pow(Math.Abs(1 - n * n), -1.5);

                tmp = n * w * w / (1 - n * n);
                line1 += tmp;

                //Debug.Log("line1 " + line1);

                tmp = Math.Sqrt(1 + m) * invOneMinusMPow * smallACosTerm;
                line1 += tmp;

                //Debug.Log("line1 " + line1);

                double line12 = line1 + line2;
                line12 *= AR;
                line12 /= FARMathUtil.CompleteEllipticIntegralSecondKind(m_, 1e-6);


                double Cna = line12 + line34;
                //Debug.Log("Cna " + Cna);
                return Cna;
            }
        }


        public static double SubsonicLECnaa(double E, double B, double tanSweep, double AoA)
        {
            return 0;
            double Eparam = E * B / tanSweep;
            double AoAparam = Math.Tan(AoA) * B;
            if (AoA > 1)
                AoAparam = 1 / AoAparam;

            double Cnaa = 0;

            return Cnaa;
        }*/
    }
}
