/*
Ferram Aerospace Research v0.14.2
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
using UnityEngine;
using KSP;

namespace ferram4
{
    public class FARControllableSurface : FARWingAerodynamicModel
    {        
        protected Transform movableSection = null;

        protected Transform MovableSection
        {
            get
            {
                if (movableSection == null)
                {
                    movableSection = part.FindModelTransform(transformName);     //And the transform
                    if (!MovableOrigReady)
                    {
                        // In parts copied by symmetry, these fields should already be set,
                        // while the transform may not be in the original orientation anymore.
                        MovableOrig = movableSection.localRotation;         //Its original orientation
                        MovableOrigReady = true;
                    }
                    if (Vector3.Dot(MovableSection.right, part.transform.right) > 0)
                        flipAxis = false;
                    else
                        flipAxis = true;
                }
                return movableSection;
            }
        }

        private bool flipAxis = false;


        [KSPField(isPersistant = false)]
        public Vector3 controlSurfacePivot = new Vector3(1f, 0f, 0f);

        [KSPField(isPersistant = false)]
        public float ctrlSurfFrac = 1;

        [KSPField(isPersistant = false)]
        public string transformName = "obj_ctrlSrf";

        // These TWO fields MUST be set up so that they are copied by Object.Instantiate.
        // Otherwise detaching and re-attaching wings with deflected flaps etc breaks until save/load.
        [SerializeField]
        protected Quaternion MovableOrig = Quaternion.identity;
        [SerializeField]
        private bool MovableOrigReady = false;

//        protected int MovableSectionFlip = 1;

        [UI_Toggle(enabledText = "Active", scene = UI_Scene.Editor, disabledText = "Inactive")]
        [KSPField(guiName = "Pitch", isPersistant = true, guiActiveEditor = true, guiActive = false)]
        public bool pitchaxis = true;

        [UI_Toggle(enabledText = "Active", scene = UI_Scene.Editor, disabledText = "Inactive")]
        [KSPField(guiName = "Yaw", isPersistant = true, guiActiveEditor = true, guiActive = false)]
        public bool yawaxis = true;

        [UI_Toggle(enabledText = "Active", scene = UI_Scene.Editor, disabledText = "Inactive")]
        [KSPField(guiName = "Roll", isPersistant = true, guiActiveEditor = true, guiActive = false)]
        public bool rollaxis = true;

        [UI_Toggle(enabledText = "Active", scene = UI_Scene.Editor, disabledText = "Inactive")]
        [KSPField(guiName = "Flap", isPersistant = true, guiActiveEditor = true, guiActive = false)]
        public bool isFlap;

        [UI_Toggle(enabledText = "Active", scene = UI_Scene.Editor, disabledText = "Inactive")]
        [KSPField(guiName = "Spoiler", isPersistant = true, guiActiveEditor = true, guiActive = false)]
        public bool isSpoiler;

        [KSPField(isPersistant = true, guiName = "Flap setting")]
        public int flapDeflectionLevel = 2;

        [UI_FloatRange(maxValue = 30, minValue = -15, scene = UI_Scene.Editor, stepIncrement = 0.5f)]
        [KSPField(guiName = "Ctrl Dflct", isPersistant = true)]
        public float maxdeflect = 15;

        [UI_FloatRange(maxValue = 85, minValue = -30, scene = UI_Scene.Editor, stepIncrement = 0.5f)]
        [KSPField(guiName = "Flp/splr Dflct", isPersistant = true)]
        public float maxdeflectFlap = 15; 
        
        protected double PitchLocation = 0;
        protected double YawLocation = 0;
        protected double RollLocation = 0;
        protected int flapLocation = 0;

        private double AoAsign = 1;
        private double AoAdesiredControl = 0; //DaMichel: treat desired AoA's from flap and stick inputs separately for different animation rates
        private double AoAdesiredFlap = 0;
        private double AoAcurrentControl = 0; // current deflection due to control inputs
        private double AoAcurrentFlap = 0; // current deflection due to flap/spoiler deployment
        private double AoAoffset = 0; // total current deflection

        private double lastAoAoffset = 0;
        private Vector3d deflectedNormal = Vector3d.forward;

        public static double timeConstant = 0.25;
        public static double timeConstantFlap = 0.2;
        private bool brake = false;
        private bool justStarted = false;

        private Transform lastReferenceTransform = null;


        [FARAction("Activate Spoiler", FARActionGroupConfiguration.ID_SPOILER)] // use our new FARAction for configurable action group assignemnt
        public void ActivateSpoiler(KSPActionParam param)
        {
            brake = !(param.type > 0);
        }

        [FARAction("Increase Flap Deflection", FARActionGroupConfiguration.ID_INCREASE_FLAP_DEFLECTION)] // use our new FARAction for configurable action group assignemnt
        public void IncreaseDeflect(KSPActionParam param)
        {
            param.Cooldown = 0.25f;
            SetDeflection(flapDeflectionLevel + 1);
        }

        [KSPEvent(name = "DeflectMore", active = false, guiActive = true, guiName = "Deflect more")]
        public void DeflectMore()
        {
            SetDeflection(flapDeflectionLevel + 1);
            UpdateFlapDeflect();
        }

        [FARAction("Decrease Flap Deflection", FARActionGroupConfiguration.ID_DECREASE_FLAP_DEFLECTION)] // use our new FARAction for configurable action group assignemnt
        public void DecreaseDeflect(KSPActionParam param)
        {
            param.Cooldown = 0.25f;
            SetDeflection(flapDeflectionLevel - 1);
        }

        [KSPEvent(name = "DeflectLess", active = false, guiActive = true, guiName = "Deflect less")]
        public void DeflectLess()
        {
            SetDeflection(flapDeflectionLevel - 1);
            UpdateFlapDeflect();
        }
        private void UpdateFlapDeflect()
        {
            for (int i = 0; i < part.symmetryCounterparts.Count; i++)
            {
                Part p = part.symmetryCounterparts[i];
                for (int j = 0; j < p.Modules.Count; j++)
                {
                    PartModule m = p.Modules[j];
                    if (m is FARControllableSurface)
                        (m as FARControllableSurface).SetDeflection(this.flapDeflectionLevel);
                }
            }
        }

        private void SetDeflection(int newstate)
        {
            flapDeflectionLevel = Math.Max(0, Math.Min(3, newstate));
            UpdateEvents();
        }

        public void UpdateEvents()
        {
            Fields["flapDeflectionLevel"].guiActive = isFlap;
            Events["DeflectMore"].active = isFlap && flapDeflectionLevel < 3;
            Events["DeflectLess"].active = isFlap && flapDeflectionLevel > 0;
        }
        public override void Start()
        {
            base.Start();
            if (part.Modules.Contains("ModuleControlSurface"))
            {
                part.RemoveModule(part.Modules["ModuleControlSurface"]);
            }

            OnVesselPartsChange += CalculateSurfaceFunctions;
            UpdateEvents();
            justStarted = true;
            if(vessel)
                lastReferenceTransform = vessel.ReferenceTransform;

            if (FARDebugValues.allowStructuralFailures)
            {
                FARPartStressTemplate template;
                foreach (FARPartStressTemplate temp in FARAeroStress.StressTemplates)
                    if (temp.name == "ctrlSurfStress")
                    {
                        template = temp;

                        YmaxForce *= 1 - ctrlSurfFrac;
                        XZmaxForce *= 1 - ctrlSurfFrac;

                        double tmp = template.YmaxStress;    //in MPa
                        tmp *= S * ctrlSurfFrac;
                        YmaxForce += tmp;

                        tmp = template.XZmaxStress;    //in MPa
                        tmp *= S * ctrlSurfFrac;
                        XZmaxForce += tmp;
                        break;
                    }
            }
        }

        public override void FixedUpdate()
        {
            if (justStarted)
                CalculateSurfaceFunctions();

            if (HighLogic.LoadedSceneIsFlight && (object)part != null && (object)vessel != null)
            {
                bool process = part.isControllable || (justStarted && isFlap);

                if (process && (object)MovableSection != null && part.Rigidbody)
                {
                    // Set member vars for desired AoA
                    if (isFlap == true)
                        AoAOffsetFromFlapDeflection();
                    else if (isSpoiler == true)
                        AoAOffsetFromSpoilerDeflection();
                    AoAOffsetFromControl(); 
                    //DaMichel: put deflection change here so that AoAOffsetFromControlInput does only the thing which the name suggests
                    ChangeDeflection();
                    DeflectionAnimation();
                }
            }

            base.FixedUpdate();
            justStarted = false;

            if(vessel && vessel.ReferenceTransform != lastReferenceTransform)
            {
                justStarted = true;
                lastReferenceTransform = vessel.ReferenceTransform;
            }
        }

        #region Deflection

        public void CalculateSurfaceFunctions()
        {
            if (HighLogic.LoadedSceneIsEditor && ((object)vessel == null || (object)part.transform == null))
                return;

            if (isFlap == true)
            {
                if (HighLogic.LoadedSceneIsFlight)
                    flapLocation = (int)Math.Sign(Vector3.Dot(vessel.ReferenceTransform.forward, part.transform.forward));      //figure out which way is up
                else
                    flapLocation = (int)Math.Sign(Vector3.Dot(EditorLogic.startPod.transform.forward, part.transform.forward));      //figure out which way is up
            }
            else if (isSpoiler == true)
            {
                if (HighLogic.LoadedSceneIsFlight)
                    flapLocation = -(int)Math.Sign(Vector3.Dot(vessel.ReferenceTransform.forward, part.transform.forward));      //figure out which way is up
                else
                    flapLocation = -(int)Math.Sign(Vector3.Dot(EditorLogic.startPod.transform.forward, part.transform.forward));      //figure out which way is up
            }

            if (pitchaxis || yawaxis || rollaxis || HighLogic.LoadedSceneIsEditor)
            {
                Vector3 CoM = Vector3.zero;
                float mass = 0;
                for (int i = 0; i < VesselPartList.Count; i++)
                {
                    Part p = VesselPartList[i];

                    CoM += p.transform.position * p.mass;
                    mass += p.mass;

                }
                CoM /= mass;

                if (HighLogic.LoadedSceneIsEditor && (isFlap || isSpoiler))
                    SetControlStateEditor(CoM, 0, 0, 0, FAREditorGUI.CurrentEditorFlapSetting, FAREditorGUI.CurrentEditorSpoilerSetting);

                float roll2 = 0;
                if (HighLogic.LoadedSceneIsEditor)
                {
                    Vector3 CoMoffset = (part.transform.position - CoM).normalized;
                    PitchLocation = Vector3.Dot(part.transform.forward, EditorLogic.startPod.transform.forward) * Math.Sign(Vector3.Dot(CoMoffset, EditorLogic.startPod.transform.up));
                    YawLocation = -Vector3.Dot(part.transform.forward, EditorLogic.startPod.transform.right) * Math.Sign(Vector3.Dot(CoMoffset, EditorLogic.startPod.transform.up));
                    RollLocation = Vector3.Dot(part.transform.forward, EditorLogic.startPod.transform.forward) * Math.Sign(Vector3.Dot(CoMoffset, -EditorLogic.startPod.transform.right));
                    roll2 = Vector3.Dot(part.transform.forward, EditorLogic.startPod.transform.right) * Math.Sign(Vector3.Dot(CoMoffset, EditorLogic.startPod.transform.forward));
                    AoAsign = Math.Sign(Vector3.Dot(part.transform.up, EditorLogic.startPod.transform.up));
                }
                else
                {
                    //Figures out where the ctrl surface is; this must be done after physics starts to get vessel COM
                    Vector3 CoMoffset = (part.transform.position - CoM).normalized;
                    PitchLocation = Vector3.Dot(part.transform.forward, vessel.ReferenceTransform.forward) * Math.Sign(Vector3.Dot(CoMoffset, vessel.ReferenceTransform.up));
                    YawLocation = -Vector3.Dot(part.transform.forward, vessel.ReferenceTransform.right) * Math.Sign(Vector3.Dot(CoMoffset, vessel.ReferenceTransform.up));
                    RollLocation = Vector3.Dot(part.transform.forward, vessel.ReferenceTransform.forward) * Math.Sign(Vector3.Dot(CoMoffset, -vessel.ReferenceTransform.right));
                    roll2 = Vector3.Dot(part.transform.forward, vessel.ReferenceTransform.right) * Math.Sign(Vector3.Dot(CoMoffset, vessel.ReferenceTransform.forward));
                    AoAsign = Math.Sign(Vector3.Dot(part.transform.up, vessel.ReferenceTransform.up));
                }
                //PitchLocation *= PitchLocation * Mathf.Sign(PitchLocation);
                //YawLocation *= YawLocation * Mathf.Sign(YawLocation);
                //RollLocation = RollLocation * RollLocation * Mathf.Sign(RollLocation) + roll2 * roll2 * Mathf.Sign(roll2);
                RollLocation += roll2;
            }
            //DaMichel: this is important to force a reset of the flap/spoiler model orientation to the desired value.
            // What apparently happens on loading a new flight scene is that first the model (obj_ctrlSrf) 
            // orientation is set correctly by DeflectionAnimation(). But then the orientations is mysteriously 
            // zeroed-out. And this definitely doesn't happen in this module. However OnVesselPartsChange
            // subscribers are called afterwards, so we have a chance to fix the broken orientation state.
            lastAoAoffset = double.MaxValue;
        }

        private void AoAOffsetFromSpoilerDeflection()
        {
            if (brake)
                AoAdesiredFlap = maxdeflectFlap * flapLocation;
            else
                AoAdesiredFlap = 0;
            AoAdesiredFlap = FARMathUtil.Clamp(AoAdesiredFlap, -Math.Abs(maxdeflectFlap), Math.Abs(maxdeflectFlap));
        }

        
        private void AoAOffsetFromFlapDeflection()
        {
            AoAdesiredFlap = maxdeflectFlap * flapLocation * flapDeflectionLevel * 0.33333333333;
            AoAdesiredFlap = FARMathUtil.Clamp(AoAdesiredFlap, -Math.Abs(maxdeflectFlap), Math.Abs(maxdeflectFlap));
        }

        private void AoAOffsetFromControl()
        {
            AoAdesiredControl = 0;
            if ((object)vessel != null && vessel.staticPressure > 0)
            {
                if (pitchaxis)
                {
                    AoAdesiredControl += PitchLocation * vessel.ctrlState.pitch;
                }
                if (yawaxis)
                {
                    AoAdesiredControl += YawLocation * vessel.ctrlState.yaw;
                }
                if (rollaxis)
                {
                    AoAdesiredControl += RollLocation * vessel.ctrlState.roll;
                }

                AoAdesiredControl *= AoAsign * maxdeflect;
                AoAdesiredControl = FARMathUtil.Clamp(AoAdesiredControl, -Math.Abs(maxdeflect), Math.Abs(maxdeflect));
            }
        }

        protected override double CalculateAoA(Vector3d velocity)
        {
            // Use the vector computed by DeflectionAnimation
            Vector3d perp = part_transform.TransformDirection(deflectedNormal);
            double PerpVelocity = Vector3d.Dot(perp, velocity.normalized);
            return Math.Asin(FARMathUtil.Clamp(PerpVelocity, -1, 1));
        }

        // Had to add this one since the parent class don't use AoAoffset and adding it would break GetWingInFrontOf
        public double CalculateAoA(Vector3d velocity, double AoAoffset)
        {
            double radAoAoffset = AoAoffset * FARMathUtil.deg2rad * ctrlSurfFrac;
            Vector3 perp = part_transform.TransformDirection(new Vector3d(0, Math.Sin(radAoAoffset), Math.Cos(radAoAoffset)));
            double PerpVelocity = Vector3d.Dot(perp, velocity.normalized);
            return Math.Asin(PerpVelocity);
        }

        //DaMichel: Factored the time evolution for deflection AoA into this function. This one results into an exponential asympotic
        //"decay" towards the desired value. Good for stick inputs, i suppose, and the original method.
        private static double BlendDeflectionExp(double current, double desired, double timeConstant, bool forceSetToDesired)
        {
            double error = desired - current;
            if (!forceSetToDesired && Math.Abs(error) >= 0.1)  // DaMichel: i changed the threshold since i noticed a "bump" at max deflection
            {
                double recip_timeconstant = 1 / timeConstant;
                double tmp1 = error * recip_timeconstant;
                current += FARMathUtil.Clamp((double)TimeWarp.deltaTime * tmp1, -Math.Abs(0.6 * error), Math.Abs(0.6 * error));
            }
            else
                current = desired;
            return current;
        }

        //DaMichel: Similarly, this is used for constant rate movment towards the desired value. I presume it is more realistic for 
        //for slow moving flaps and spoilers. It looks better anyways.
        private static double BlendDeflectionLinear(double current, double desired, double timeConstant, bool forceSetToDesired)
        {
            double error = desired - current;
            if (!forceSetToDesired && Math.Abs(error) >= 0.1)
            {
                double recip_timeconstant = 1 / timeConstant;
                double tmp1 = Math.Sign(error) * recip_timeconstant;
                current += FARMathUtil.Clamp((double)TimeWarp.deltaTime * tmp1, -Math.Abs(0.6 * error), Math.Abs(0.6 * error));
            }
            else
                current = desired;
            return current;
        }

        // Determines current deflection contributions from stick and flap/spoiler settings and update current total deflection (AoAoffset).
        private void ChangeDeflection()
        {
            if (AoAcurrentControl != AoAdesiredControl) AoAcurrentControl = BlendDeflectionExp(AoAcurrentControl, AoAdesiredControl, timeConstant, justStarted);
            if (AoAcurrentFlap  != AoAdesiredFlap) AoAcurrentFlap = BlendDeflectionLinear(AoAcurrentFlap, AoAdesiredFlap, timeConstantFlap, justStarted);
            AoAoffset = AoAcurrentFlap + AoAcurrentControl;
        }

        /// <summary>
        /// This animates a deflection;
        /// </summary>
        protected void DeflectionAnimation()
        {
            // Don't recalculate on insignificant variations
            if (Math.Abs(lastAoAoffset - AoAoffset) < 0.01)
                return;

            lastAoAoffset = AoAoffset;

            // Compute a vector for CalculateAoA
            double radAoAoffset = AoAoffset * FARMathUtil.deg2rad * ctrlSurfFrac;
            deflectedNormal.y = Math.Sin(radAoAoffset);
            deflectedNormal.z = Math.Cos(radAoAoffset);

            // Visually animate the surface
            MovableSection.localRotation = MovableOrig;
            if (AoAoffset != 0)
            {
                if(flipAxis)
                    MovableSection.Rotate(controlSurfacePivot, (float)AoAoffset);
                else
                    MovableSection.Rotate(controlSurfacePivot, (float)-AoAoffset);
            }
        }

        public void SetControlStateEditor(Vector3 CoM, float pitch, float yaw, float roll, int flap, bool brake)
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                Vector3 CoMoffset = (part.transform.position - CoM).normalized;
                PitchLocation = Vector3.Dot(part.transform.forward, EditorLogic.startPod.transform.forward) * Mathf.Sign(Vector3.Dot(CoMoffset, EditorLogic.startPod.transform.up));
                YawLocation = -Vector3.Dot(part.transform.forward, EditorLogic.startPod.transform.right) * Mathf.Sign(Vector3.Dot(CoMoffset, EditorLogic.startPod.transform.up));
                RollLocation = Vector3.Dot(part.transform.forward, EditorLogic.startPod.transform.forward) * Mathf.Sign(Vector3.Dot(CoMoffset, -EditorLogic.startPod.transform.right));
                AoAcurrentControl = 0;
                if (pitchaxis == true)
                {
                    AoAcurrentControl += PitchLocation * pitch;
                }
                if (yawaxis == true)
                {
                    AoAcurrentControl += YawLocation * yaw;
                }
                if (rollaxis == true)
                {
                    AoAcurrentControl += RollLocation * roll;
                }
                AoAcurrentControl = AoAdesiredControl = FARMathUtil.Clamp(AoAcurrentControl, -1, 1) * maxdeflect;
                AoAcurrentFlap = 0;
                if (isFlap == true)
                {
                    int flapDeflectionLevel = flap;
                    flapLocation = (int)Math.Sign(Vector3.Dot(EditorLogic.startPod.transform.forward, part.transform.forward));      //figure out which way is up
                    AoAcurrentFlap += maxdeflectFlap * flapLocation * flapDeflectionLevel * 0.3333333333333;
                }
                else if (isSpoiler == true)
                {
                    flapLocation = -(int)Math.Sign(Vector3.Dot(EditorLogic.startPod.transform.forward, part.transform.forward));      //figure out which way is up
                    AoAcurrentFlap += brake ? maxdeflectFlap * flapLocation : 0;
                }
                AoAdesiredFlap = AoAcurrentFlap;
                AoAoffset = AoAdesiredFlap + AoAdesiredControl;
                DeflectionAnimation();
            }
        }
        #endregion
    }
}
