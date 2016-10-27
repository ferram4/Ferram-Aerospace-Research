using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.UI.Screens;
using FerramAerospaceResearch.PartExtensions;
using Random = System.Random;

/* RealChuteLite is the work of Christophe Savard (stupid_chris), and is licensed the same way than the rest of FAR is.
 * If you have any questions about this code, or want to report something, don't annoy ferram about it, ask me
 * directly on GitHub, the forums, or IRC. */

namespace FerramAerospaceResearch.RealChuteLite
{

    public class RealChuteFAR : PartModule, IModuleInfo, IMultipleDragCube, IPartMassModifier, IPartCostModifier
    {
        /// <summary>
        /// Parachute deployment states
        /// </summary>
        public enum DeploymentStates
        {
            NONE,
            STOWED,
            PREDEPLOYED,
            DEPLOYED,
            CUT
        }

        /// <summary>
        /// Parachute deployment safety state
        /// </summary>
        public enum SafeState
        {
            SAFE,
            RISKY,
            DANGEROUS
        }

        #region Constants
        //Material constants
        public const string materialName = "Nylon";
        public const float areaDensity = 5.65E-5f, areaCost = 0.075f, staticCd = 1;     //t/m², and F/m² for the first two
        public const double startTemp = 300, maxTemp = 493.15;                          //In °K
        public const double specificHeat = 1700, absoluteZero = -273.15;                //Specific heat in J/kg*K

        //More useful constants
        public const int maxSpares = 5;
        public const string stowed = "STOWED", predeployed = "PREDEPLOYED", deployed = "DEPLOYED", cut = "CUT";
        public static readonly string[] cubeNames = { "STOWED", "RCDEPLOYED", "DEPLOYED", "SEMIDEPLOYED", "PACKED" };

        //Quick enum parsing/tostring dictionaries
        private static readonly Dictionary<DeploymentStates, string> names = new Dictionary<DeploymentStates, string>(5)
        #region Names
        {
            { DeploymentStates.NONE, string.Empty },
            { DeploymentStates.STOWED, stowed },
            { DeploymentStates.PREDEPLOYED, predeployed },
            { DeploymentStates.DEPLOYED, deployed },
            { DeploymentStates.CUT, cut }
        };
        #endregion
        private static readonly Dictionary<string, DeploymentStates> states = new Dictionary<string, DeploymentStates>(5)
        #region States
        {
            { string.Empty, DeploymentStates.NONE },
            { stowed, DeploymentStates.STOWED },
            { predeployed, DeploymentStates.PREDEPLOYED },
            { deployed, DeploymentStates.DEPLOYED },
            { cut, DeploymentStates.CUT }
        };
        #endregion
        #endregion

        #region KSPFields
        //Stealing values from the stock module
        [KSPField]
        public float autoCutSpeed = 0.5f;
        [KSPField(guiName = "Min pressure", isPersistant = true, guiActive = true, guiActiveEditor = true), UI_FloatRange(stepIncrement = 0.01f, maxValue = 0.5f, minValue = 0.01f)]
        public float minAirPressureToOpen = 0.01f;
        [KSPField(guiName = "Altitude", isPersistant = true, guiActive = true, guiActiveEditor = true), UI_FloatRange(stepIncrement = 50f, maxValue = 5000f, minValue = 50f)]
        public float deployAltitude = 700;
        [KSPField]
        public string capName = "cap", canopyName = "canopy";
        [KSPField]
        public string semiDeployedAnimation = "semiDeploy", fullyDeployedAnimation = "fullyDeploy";
        [KSPField]
        public float semiDeploymentSpeed = 0.5f, deploymentSpeed = 0.16667f;
        [KSPField]
        public bool invertCanopy = true;

        //Persistant fields
        [KSPField(isPersistant = false)]    //this cannot be persistent to ensure that bad values aren't saved, and since these chutes aren't customizable there's no reason to save this
        public float preDeployedDiameter = 1, deployedDiameter = 25;
        [KSPField(isPersistant = true)]
        public float caseMass, time;
        [KSPField(isPersistant = true)]
        public bool armed, staged, initiated;
        [KSPField(isPersistant = true, guiActive = true, guiName = "Spare chutes")]
        public int chuteCount = 5;
        [KSPField(isPersistant = true)]
        public string depState = "STOWED";
        [KSPField(isPersistant = true)]
        public float currentArea;
        [KSPField(isPersistant = true)]
        public double chuteTemperature = 300;
        [KSPField(isPersistant = true, guiActive = false, guiName = "Chute temp", guiFormat = "0.00", guiUnits = "°C")]
        public float currentTemp = 20;
        [KSPField(guiActive = false, guiName = "Max temp", guiFormat = "0.00", guiUnits = "°C")]
        public float chuteDisplayMaxTemp = (float)(maxTemp + absoluteZero);
        #endregion

        #region Propreties
        // If the vessel is stopped on the ground
        public bool GroundStop
        {
            get { return this.vessel.LandedOrSplashed && this.vessel.horizontalSrfSpeed < this.autoCutSpeed; }
        }

        // If the parachute can be repacked
        public bool CanRepack
        {
            get
            {
                return (this.GroundStop || this.atmPressure == 0) && this.DeploymentState == DeploymentStates.CUT
                       && this.chuteCount > 0 && FlightGlobals.ActiveVessel.isEVA;
            }
        }

        //If the Kerbal can repack the chute in career mode
        public bool CanRepackCareer
        {
            get
            {
                ProtoCrewMember kerbal = FlightGlobals.ActiveVessel.GetVesselCrew()[0];
                return HighLogic.CurrentGame.Mode != Game.Modes.CAREER
                    || kerbal.experienceTrait.Title == "Engineer" && kerbal.experienceLevel >= 1;
            }
        }

        //Predeployed area of the chute
        public float PreDeployedArea
        {
            get { return GetArea(this.preDeployedDiameter); }
        }

        //Deployed area of the chute
        public float DeployedArea
        {
            get { return GetArea(this.deployedDiameter); }
        }

        //The current useful convection area
        private double ConvectionArea
        {
            get
            {
                if (this.DeploymentState == DeploymentStates.PREDEPLOYED && this.dragTimer.Elapsed.Seconds < 1 / this.semiDeploymentSpeed)
                {
                    return UtilMath.Lerp(0, this.DeployedArea, this.dragTimer.Elapsed.Seconds * this.semiDeploymentSpeed);
                }
                return this.DeployedArea;
            }
        }

        //Mass of the chute
        public float ChuteMass
        {
            get { return this.DeployedArea * areaDensity; }
        }

        //Total dry mass of the chute part
        public float TotalMass
        {
            get
            {
                if (this.caseMass == 0) { this.caseMass = this.part.mass; }
                return this.caseMass + this.ChuteMass;
            }
        }

        //Position to apply the force to
        public Vector3 ForcePosition
        {
            get { return this.parachute.position; }
        }

        //If the random deployment timer has been spent
        public bool RandomDeployment
        {
            get
            {
                if (!this.randomTimer.IsRunning) { this.randomTimer.Start(); }

                if (this.randomTimer.Elapsed.TotalSeconds >= this.randomTime)
                {
                    this.randomTimer.Reset();
                    return true;
                }
                return false;
            }
        }

        //If the parachute is in a high enough atmospheric pressure to deploy
        public bool PressureCheck
        {
            get { return this.atmPressure >= this.minAirPressureToOpen; }
        }

        //If the parachute can deploy
        public bool CanDeploy
        {
            get
            {
                if (this.GroundStop || this.atmPressure == 0) { return false; }
                if (this.DeploymentState == DeploymentStates.CUT) { return false; }
                if (this.PressureCheck) { return true; }
                return !this.PressureCheck && this.IsDeployed;
            }
        }

        //If the parachute is deployed
        public bool IsDeployed
        {
            get
            {
                switch (this.DeploymentState)
                {
                    case DeploymentStates.PREDEPLOYED:
                    case DeploymentStates.DEPLOYED:
                        return true;

                    default:
                        return false;
                }
            }
        }

        //Persistent deployment state
        public DeploymentStates DeploymentState
        {
            get
            {
                if (this.state == DeploymentStates.NONE) { this.DeploymentState = states[this.depState]; }
                return this.state;
            }
            set
            {
                this.state = value;
                this.depState = names[value];
            }
        }

        //The inverse thermal mass of the parachute
        public double InvThermalMass
        {
            get
            {
                if (this.thermMass == 0)
                {
                    this.thermMass = 1 / (specificHeat * this.ChuteMass);
                }
                return this.thermMass;
            }
        }

        //The current chute emissivity constant
        public double ChuteEmissivity
        {
            get
            {
                if (this.chuteTemperature < 293.15) { return 0.72; }
                return this.chuteTemperature > 403.15 ? 0.9 : UtilMath.Lerp(0.72, 0.9, ((this.chuteTemperature - 293.15) / 110) + 293.15);
            }
        }

        //Bold KSP style GUI label
        private static GUIStyle boldLabel;
        public static GUIStyle BoldLabel
        {
            get
            {
                if (boldLabel == null)
                {
                    boldLabel = new GUIStyle(HighLogic.Skin.label) { fontStyle = FontStyle.Bold };
                }
                return boldLabel;
            }
        }

        //Yellow KSP style GUI label
        private static GUIStyle yellowLabel;
        public static GUIStyle YellowLabel
        {
            get
            {
                if (yellowLabel == null)
                {
                    yellowLabel = new GUIStyle(HighLogic.Skin.label)
                    {
                        normal = { textColor = XKCDColors.BrightYellow },
                        hover = { textColor = XKCDColors.BrightYellow }
                    };
                }
                return yellowLabel;
            }
        }

        //Red KSP style GUI label
        private static GUIStyle redLabel;
        public static GUIStyle RedLabel
        {
            get
            {
                if (redLabel == null)
                {
                    redLabel = new GUIStyle(HighLogic.Skin.label)
                    {
                        normal = { textColor = XKCDColors.Red },
                        hover = { textColor = XKCDColors.Red }
                    };
                }
                return redLabel;
            }
        }

        //Quick access to the part GUI events
        private BaseEvent deploy, disarm, cutE, repack;
        private BaseEvent DeployE
        {
            get { return this.deploy ?? (this.deploy = this.Events["GUIDeploy"]); }
        }
        private BaseEvent Disarm
        {
            get { return this.disarm ?? (this.disarm = this.Events["GUIDisarm"]); }
        }
        private BaseEvent CutE
        {
            get { return this.cutE ?? (this.cutE = this.Events["GUICut"]); }
        }
        private BaseEvent Repack
        {
            get { return this.repack ?? (this.repack = this.Events["GUIRepack"]); }
        }
        #endregion

        #region Fields
        //Flight
        private Vector3 dragVector, pos = new Vector3d();
        private readonly PhysicsWatch failedTimer = new PhysicsWatch(), randomTimer = new PhysicsWatch();
        private PhysicsWatch dragTimer = new PhysicsWatch();
        private bool displayed, showDisarm;
        private double asl, trueAlt;
        private double atmPressure, atmDensity;
        private float sqrSpeed;
        private double thermMass, convFlux;

        //Part
        private Transform parachute, cap;
        private Rigidbody rigidbody;
        private float randomX, randomY, randomTime;
        private DeploymentStates state = DeploymentStates.NONE;
        private SafeState safeState = SafeState.SAFE;
        private float massDelta;

        //GUI
        private bool visible, hid;
        private readonly int id = Guid.NewGuid().GetHashCode();
        private Rect window, drag;
        private Vector2 scroll;
        #endregion

        #region Part GUI
        //Deploys the parachutes if possible
        [KSPEvent(guiActive = true, active = true, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Deploy Chute", unfocusedRange = 5)]
        public void GUIDeploy()
        {
            ActivateRC();
        }

        //Cuts main chute chute
        [KSPEvent(guiActive = true, active = true, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Cut chute", unfocusedRange = 5)]
        public void GUICut()
        {
            Cut();
        }

        [KSPEvent(guiActive = true, active = true, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Disarm chute", unfocusedRange = 5)]
        public void GUIDisarm()
        {
            this.armed = false;
            this.showDisarm = false;
            this.part.stackIcon.SetIconColor(XKCDColors.White);
            this.DeployE.active = true;
            DeactivateRC();
        }

        //Repacks chute from EVA if in space or on the ground
        [KSPEvent(guiActive = false, active = true, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Repack chute", unfocusedRange = 5)]
        public void GUIRepack()
        {
            if (this.CanRepack)
            {
                if (!this.CanRepackCareer)
                {
                    ScreenMessages.PostScreenMessage("Only a level 1 and higher engineer can repack a parachute", 5, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                this.part.Effect("rcrepack");
                this.Repack.guiActiveUnfocused = false;
                this.part.stackIcon.SetIconColor(XKCDColors.White);
                if (this.chuteCount != -1) { this.chuteCount--; }
                this.DeploymentState = DeploymentStates.STOWED;
                this.randomTimer.Reset();
                this.time = 0;
                this.cap.gameObject.SetActive(true);
                this.part.DragCubes.SetCubeWeight("PACKED", 1);
                this.part.DragCubes.SetCubeWeight("RCDEPLOYED", 0);
            }
        }

        //Shows the info window
        [KSPEvent(guiActive = true, active = true, guiActiveEditor = true, guiName = "Toggle info")]
        public void GUIToggleWindow()
        {
            if (!this.visible)
            {
                List<RealChuteFAR> parachutes = new List<RealChuteFAR>();
                if (HighLogic.LoadedSceneIsEditor) { parachutes.AddRange(EditorLogic.SortedShipList.Where(p => p.Modules.Contains<RealChuteFAR>()).Select(p => p.Modules.GetModule<RealChuteFAR>())); }
                else if (HighLogic.LoadedSceneIsFlight) { parachutes.AddRange(this.vessel.FindPartModulesImplementing<RealChuteFAR>()); }
                if (parachutes.Count > 1 && parachutes.Exists(p => p.visible))
                {
                    RealChuteFAR module = parachutes.Find(p => p.visible);
                    this.window.x = module.window.x;
                    this.window.y = module.window.y;
                    module.visible = false;
                }
            }
            this.visible = !this.visible;
        }
        #endregion

        #region Action groups
        //Deploys the parachutes if possible
        [KSPAction("Deploy chute")]
        public void ActionDeploy(KSPActionParam param)
        {
            ActivateRC();
        }

        //Cuts main chute
        [KSPAction("Cut chute")]
        public void ActionCut(KSPActionParam param)
        {
            if (this.IsDeployed) { Cut(); }
        }

        [KSPAction("Disarm chute")]
        public void ActionDisarm(KSPActionParam param)
        {
            if (this.armed) { GUIDisarm(); }
        }
        #endregion

        #region Methods
        //Returns the canopy area of the given Diameter
        public float GetArea(float diameter)
        {
            return (float)((diameter * diameter * Math.PI) / 4);
        }

        //Activates the parachute
        public void ActivateRC()
        {
            this.staged = true;
            this.armed = true;
            print("[RealChute]: " + this.part.partInfo.name + " was activated in stage " + this.part.inverseStage);
        }

        //Deactiates the parachute
        public void DeactivateRC()
        {
            this.staged = false;
            print("[RealChute]: " + this.part.partInfo.name + " was deactivated");
        }

        //Copies stats from the info window to the symmetry counterparts
        private void CopyToCouterparts()
        {
            foreach (Part p in this.part.symmetryCounterparts)
            {
                RealChuteFAR module = (RealChuteFAR)p.Modules["RealChuteFAR"];
                module.minAirPressureToOpen = this.minAirPressureToOpen;
                module.deployAltitude = this.deployAltitude;
            }
        }

        //Deactivates the part
        public void StagingReset()
        {
            DeactivateRC();
            this.armed = false;
            if (this.part.inverseStage != 0) { this.part.inverseStage = this.part.inverseStage - 1; }
            else { this.part.inverseStage = StageManager.CurrentStage; }
        }

        //Allows the chute to be repacked if available
        public void SetRepack()
        {
            this.part.stackIcon.SetIconColor(XKCDColors.Red);
            StagingReset();
        }

        //Drag formula calculations
        public float DragCalculation(float area)
        {
            return ((float)this.atmDensity * this.sqrSpeed * staticCd * area) / 2000;
        }

        //Gives the cost for this parachute
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return (float)Math.Round(this.DeployedArea * areaCost);
        }

        public ModifierChangeWhen GetModuleCostChangeWhen()
        {
            return ModifierChangeWhen.FIXED;
        }

        //For IPartMassModifier
        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return this.massDelta;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.FIXED;
        }

        //Not needed
        public Callback<Rect> GetDrawModulePanelCallback()
        {
            return null;
        }

        //Sets module info title
        public string GetModuleTitle()
        {
            return "RealChute";
        }

        //Sets part info field
        public string GetPrimaryField()
        {
            return string.Empty;
        }

        //Event when the UI is hidden (F2)
        private void HideUI()
        {
            this.hid = true;
        }

        //Event when the UI is shown (F2)
        private void ShowUI()
        {
            this.hid = false;
        }

        //Adds a random noise to the parachute movement
        private void ParachuteNoise()
        {
            this.parachute.Rotate(new Vector3(5 * (Mathf.PerlinNoise(Time.time, this.randomX + Mathf.Sin(Time.time)) - 0.5f), 5 * (Mathf.PerlinNoise(Time.time, this.randomY + Mathf.Sin(Time.time)) - 0.5f), 0));
        }

        //Makes the canopy follow drag direction
        private void FollowDragDirection()
        {
            if (this.dragVector.sqrMagnitude > 0)
            {
                this.parachute.rotation = Quaternion.LookRotation(this.invertCanopy ? this.dragVector : -this.dragVector, this.parachute.up);
            }
            ParachuteNoise();
        }

        //Parachute predeployment
        public void PreDeploy()
        {
            this.part.stackIcon.SetIconColor(XKCDColors.BrightYellow);
            this.part.Effect("rcpredeploy");
            this.DeploymentState = DeploymentStates.PREDEPLOYED;
            this.parachute.gameObject.SetActive(true);
            this.cap.gameObject.SetActive(false);
            this.part.PlayAnimation(this.semiDeployedAnimation, this.semiDeploymentSpeed);
            this.dragTimer.Start();
            this.part.DragCubes.SetCubeWeight("PACKED", 0);
            this.part.DragCubes.SetCubeWeight("RCDEPLOYED", 1);
            this.Fields["currentTemp"].guiActive = true;
            this.Fields["chuteDisplayMaxTemp"].guiActive = true;
        }

        //Parachute deployment
        public void Deploy()
        {
            this.part.stackIcon.SetIconColor(XKCDColors.RadioactiveGreen);
            this.part.Effect("rcdeploy");
            this.DeploymentState = DeploymentStates.DEPLOYED;
            this.dragTimer.Restart();
            this.part.PlayAnimation(this.fullyDeployedAnimation, this.deploymentSpeed);
        }

        //Parachute cutting
        public void Cut()
        {
            this.part.Effect("rccut");
            this.DeploymentState = DeploymentStates.CUT;
            this.parachute.gameObject.SetActive(false);
            this.currentArea = 0;
            this.dragTimer.Reset();
            this.currentTemp = (float)(startTemp + absoluteZero);
            this.chuteTemperature = startTemp;
            this.Fields["currentTemp"].guiActive = false;
            this.Fields["chuteDisplayMaxTemp"].guiActive = false;
            SetRepack();
        }

        //Calculates parachute deployed area
        private float DragDeployment(float time, float debutDiameter, float endDiameter)
        {
            if (!this.dragTimer.IsRunning) { this.dragTimer.Start(); }

            double t = this.dragTimer.Elapsed.TotalSeconds;
            this.time = (float)t;
            if (t <= time)
            {
                /* While this looks linear, area scales with the square of the diameter, and therefore
                 * Deployment will be quadratic. The previous exponential function was too slow at first and rough at the end */
                float currentDiam = Mathf.Lerp(debutDiameter, endDiameter, (float)(t / time));
                this.currentArea = GetArea(currentDiam);
                return this.currentArea;
            }
            this.currentArea = GetArea(endDiameter);
            return this.currentArea;
        }

        //Drag force vector
        private Vector3 DragForce(float debutDiameter, float endDiameter, float time)
        {
            return DragCalculation(DragDeployment(time, debutDiameter, endDiameter)) * this.dragVector;
        }

        //Calculates convective flux
        private void CalculateChuteFlux()
        {
            this.convFlux = this.vessel.convectiveCoefficient * UtilMath.Lerp(1, 1 + (Math.Sqrt(this.vessel.mach * this.vessel.mach * this.vessel.mach) * (this.vessel.dynamicPressurekPa / 101.325)),
                            (this.vessel.mach - PhysicsGlobals.FullToCrossSectionLerpStart) / PhysicsGlobals.FullToCrossSectionLerpEnd)
                            * (this.vessel.externalTemperature - this.chuteTemperature);
        }

        //Calculates the temperature of the chute and cuts it if needed
        private bool CalculateChuteTemp()
        {
            if (this.chuteTemperature < PhysicsGlobals.SpaceTemperature) { this.chuteTemperature = startTemp; }

            double emissiveFlux = 0;
            if (this.chuteTemperature > 0)
            {
                double temp2 = this.chuteTemperature * this.chuteTemperature;
                emissiveFlux = 2 * PhysicsGlobals.StefanBoltzmanConstant * this.ChuteEmissivity * PhysicsGlobals.RadiationFactor * temp2 * temp2;
            }
            this.chuteTemperature = Math.Max(PhysicsGlobals.SpaceTemperature, this.chuteTemperature + ((this.convFlux - emissiveFlux) * 0.001 * this.ConvectionArea * this.InvThermalMass * TimeWarp.fixedDeltaTime));
            if (this.chuteTemperature > maxTemp)
            {
                ScreenMessages.PostScreenMessage("<color=orange>[RealChute]: " + this.part.partInfo.title + "'s parachute has been destroyed due to aero forces and heat.</color>", 6f, ScreenMessageStyle.UPPER_LEFT);
                Cut();
                return false;
            }
            this.currentTemp = (float)(this.chuteTemperature + absoluteZero);
            return true;
        }

        //estimates whether it is safe to deploy the chute or not
        private void CalculateSafeToDeployEstimate()
        {
            SafeState s;
            if (this.vessel.externalTemperature <= maxTemp || this.convFlux < 0) { s = SafeState.SAFE; }
            else
            {
                s = this.chuteTemperature + (0.001 * this.convFlux * this.InvThermalMass * this.DeployedArea * 0.35) <= maxTemp ? SafeState.RISKY : SafeState.DANGEROUS;
            }

            if (this.safeState != s)
            {
                this.safeState = s;
                switch(this.safeState)
                {
                    case SafeState.SAFE:
                        this.part.stackIcon.SetBackgroundColor(XKCDColors.White); break;

                    case SafeState.RISKY:
                        this.part.stackIcon.SetBackgroundColor(XKCDColors.BrightYellow); break;

                    case SafeState.DANGEROUS:
                        this.part.stackIcon.SetBackgroundColor(XKCDColors.Red); break;
                }
            }
        }

        //Initializes parachute animations
        private void InitializeAnimationSystem()
        {
            //I know this seems random, but trust me, it's needed, else some parachutes don't animate, because fuck you, that's why.
            // ReSharper disable once UnusedVariable -> Needed to animate all parts (stupid_chris)
            Animation anim = this.part.FindModelAnimators(this.capName).FirstOrDefault();

            this.cap = this.part.FindModelTransform(this.capName);
            this.parachute = this.part.FindModelTransform(this.canopyName);
            this.parachute.gameObject.SetActive(true);
            this.part.InitiateAnimation(this.semiDeployedAnimation);
            this.part.InitiateAnimation(this.fullyDeployedAnimation);
            this.parachute.gameObject.SetActive(false);
        }

        //Sets the part in the correct position for DragCube rendering
        public void AssumeDragCubePosition(string name)
        {
            if (string.IsNullOrEmpty(name)) { return; }
            InitializeAnimationSystem();
            switch (name)
            {
                //DaMichel: now we handle the stock behaviour, too.
                case "PACKED":          //stock
                case "STOWED":
                    {
                        this.parachute.gameObject.SetActive(false);
                        this.cap.gameObject.SetActive(true);
                        break;
                    }

                case "RCDEPLOYED":      //This is not a predeployed state, no touchy
                    {
                        this.parachute.gameObject.SetActive(false);
                        this.cap.gameObject.SetActive(false);
                        break;
                    }

                case "SEMIDEPLOYED":    //  stock 
                    {
                        this.parachute.gameObject.SetActive(true);
                        this.cap.gameObject.SetActive(false);
                        this.part.SkipToAnimationTime(this.semiDeployedAnimation, 0, 1); // to the end of the animation
                        break;
                    }

                case "DEPLOYED":        //  stock 
                    {
                        this.parachute.gameObject.SetActive(true);
                        this.cap.gameObject.SetActive(false);
                        this.part.SkipToAnimationTime(this.fullyDeployedAnimation, 0, 1);  // to the end of the animation
                        break;
                    }
            }
        }

        //Gives DragCube names
        public string[] GetDragCubeNames()
        {   
            return cubeNames;
        }

        //Unused
        public bool UsesProceduralDragCubes()
        {
            return false;
        }

        // TODO 1.2: provide actual implementation of this new method
        public bool IsMultipleCubesActive
        {
            get
            {
                return true;
            }
        }

        //Info window
        private void Window(int id)
        {
            //Header
            GUI.DragWindow(this.drag);
            GUILayout.BeginVertical();

            //Top info labels
            StringBuilder b = new StringBuilder("Part name: ").AppendLine(this.part.partInfo.title);
            b.Append("Symmetry counterparts: ").AppendLine(this.part.symmetryCounterparts.Count.ToString());
            b.Append("Part mass: ").Append(this.part.TotalMass().ToString("0.###")).AppendLine("t");
            GUILayout.Label(b.ToString());

            //Beggining scroll
            this.scroll = GUILayout.BeginScrollView(this.scroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);
            GUILayout.Space(5);
            GUILayout.Label("General:", BoldLabel, GUILayout.Width(120));

            //General labels
            b = new StringBuilder("Autocut speed: ").Append(this.autoCutSpeed).AppendLine("m/s");
            b.Append("Spare chutes: ").Append(this.chuteCount);
            GUILayout.Label(b.ToString());

            //Specific labels
            GUILayout.Label("___________________________________________", BoldLabel);
            GUILayout.Space(3);
            GUILayout.Label("Main chute:", BoldLabel, GUILayout.Width(120));
            //Initial label
            b = new StringBuilder();
            b.AppendLine("Material: " + materialName);
            b.AppendLine("Drag coefficient: " + staticCd.ToString("0.0"));
            b.Append("Predeployed diameter: ").Append(this.preDeployedDiameter).Append("m\nArea: ").Append(this.PreDeployedArea.ToString("0.###")).AppendLine("m²");
            b.Append("Deployed diameter: ").Append(this.deployedDiameter).Append("m\nArea: ").Append(this.DeployedArea.ToString("0.###")).Append("m²");
            GUILayout.Label(b.ToString());

            //DeploymentSafety
            switch (this.safeState)
            {
                case SafeState.SAFE:
                    GUILayout.Label("Deployment safety: safe"); break;

                case SafeState.RISKY:
                    GUILayout.Label("Deployment safety: risky", YellowLabel); break;

                case SafeState.DANGEROUS:
                    GUILayout.Label("Deployment safety: dangerous", RedLabel); break;
            }

            //Temperature info
            b = new StringBuilder();
            b.Append("Chute max temperature: ").Append(maxTemp + absoluteZero).AppendLine("°C");
            b.Append("Current chute temperature: ").Append(Math.Round(this.chuteTemperature + absoluteZero, 1, MidpointRounding.AwayFromZero)).Append("°C");
            GUILayout.Label(b.ToString(), this.chuteTemperature / maxTemp > 0.85 ? RedLabel : GUI.skin.label);

            //Predeployment pressure selection
            GUILayout.Label("Predeployment pressure: " + this.minAirPressureToOpen + "atm");
            if (HighLogic.LoadedSceneIsFlight)
            {
                //Predeployment pressure slider
                this.minAirPressureToOpen = GUILayout.HorizontalSlider(this.minAirPressureToOpen, 0.005f, 1);
            }

            //Deployment altitude selection
            GUILayout.Label("Deployment altitude: " + this.deployAltitude + "m");
            if (HighLogic.LoadedSceneIsFlight)
            {
                //Deployment altitude slider
                this.deployAltitude = GUILayout.HorizontalSlider(this.deployAltitude, 50, 10000);
            }

            //Other labels
            b = new StringBuilder();
            b.Append("Predeployment speed: ").Append(Math.Round(1 / this.semiDeploymentSpeed, 1, MidpointRounding.AwayFromZero)).AppendLine("s");
            b.Append("Deployment speed: ").Append(Math.Round(1 / this.deploymentSpeed, 1, MidpointRounding.AwayFromZero)).Append("s");
            GUILayout.Label(b.ToString());

            //End scroll
            GUILayout.EndScrollView();

            //Copy button if in flight
            if (HighLogic.LoadedSceneIsFlight && this.part.symmetryCounterparts.Count > 0)
            {
                CenteredButton("Copy to others chutes", CopyToCouterparts);
            }

            //Close button
            CenteredButton("Close", () => this.visible = false);

            //Closer
            GUILayout.EndVertical();
        }
        #endregion

        #region Static methods
        //Creates a centered GUI button
        public static void CenteredButton(string text, Callback callback)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(text, HighLogic.Skin.button, GUILayout.Width(150)))
            {
                callback();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        #endregion

        #region Functions
        private void Update()
        {
            if (!CompatibilityChecker.IsAllCompatible() || !HighLogic.LoadedSceneIsFlight) { return; }

            //Makes the chute icon blink if failed
            if (this.failedTimer.IsRunning)
            {
                double time = this.failedTimer.Elapsed.TotalSeconds;
                if (time <= 2.5)
                {
                    if (!this.displayed)
                    {
                        ScreenMessages.PostScreenMessage("Parachute deployment failed.", 2.5f, ScreenMessageStyle.UPPER_CENTER);
                        if (this.part.ShieldedFromAirstream) { ScreenMessages.PostScreenMessage("Reason: parachute is shielded from airstream.", 2.5f, ScreenMessageStyle.UPPER_CENTER);}
                        else if (this.GroundStop) { ScreenMessages.PostScreenMessage("Reason: stopped on the ground.", 2.5f, ScreenMessageStyle.UPPER_CENTER); }
                        else if (this.atmPressure == 0) { ScreenMessages.PostScreenMessage("Reason: in space.", 2.5f, ScreenMessageStyle.UPPER_CENTER); }
                        else { ScreenMessages.PostScreenMessage("Reason: too high.", 2.5f, ScreenMessageStyle.UPPER_CENTER); }
                        this.displayed = true;
                    }
                    if (time < 0.5 || time >= 1 && time < 1.5 || time >= 2) { this.part.stackIcon.SetIconColor(XKCDColors.Red); }
                    else { this.part.stackIcon.SetIconColor(XKCDColors.White); }
                }
                else
                {
                    this.displayed = false;
                    this.part.stackIcon.SetIconColor(XKCDColors.White);
                    this.failedTimer.Reset();
                }
            }

            this.Disarm.active = this.armed || this.showDisarm;
            this.DeployE.active = !this.staged && this.DeploymentState != DeploymentStates.CUT;
            this.CutE.active = this.IsDeployed;
            this.Repack.guiActiveUnfocused = this.CanRepack;
        }

        private void FixedUpdate()
        {
            //Flight values
            if (!CompatibilityChecker.IsAllCompatible() || !HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null || this.part.Rigidbody == null) { return; }
            this.pos = this.part.partTransform.position;
            this.asl = FlightGlobals.getAltitudeAtPos(this.pos);
            this.trueAlt = this.asl;
            if (this.vessel.mainBody.pqsController != null)
            {
                double terrainAlt = this.vessel.pqsAltitude;
                if (!this.vessel.mainBody.ocean || terrainAlt > 0) { this.trueAlt -= terrainAlt; }
            }
            this.atmPressure = FlightGlobals.getStaticPressure(this.asl, this.vessel.mainBody) * PhysicsGlobals.KpaToAtmospheres;
            this.atmDensity = this.part.atmDensity;
            Vector3 velocity = this.part.Rigidbody.velocity + Krakensbane.GetFrameVelocityV3f();
            this.sqrSpeed = velocity.sqrMagnitude;
            this.dragVector = -velocity.normalized;

            if (this.atmDensity > 0) { CalculateChuteFlux(); }
            else { this.convFlux = 0; }                

            CalculateSafeToDeployEstimate();

            if (!this.staged && GameSettings.LAUNCH_STAGES.GetKeyDown() && this.vessel.isActiveVessel && (this.part.inverseStage == StageManager.CurrentStage - 1 || StageManager.CurrentStage == 0)) { ActivateRC(); }

            if (this.staged)
            {
                //Checks if the parachute must disarm
                if (this.armed)
                {
                    this.part.stackIcon.SetIconColor(XKCDColors.LightCyan);
                    if (this.CanDeploy) { this.armed = false; }
                }
                //Parachute deployments
                else
                {
                    //Parachutes
                    if (this.CanDeploy)
                    {
                        if (this.IsDeployed)
                        {
                            if (!CalculateChuteTemp()) { return; }
                            FollowDragDirection();
                        }
                        this.part.GetComponentCached(ref this.rigidbody);
                        switch (this.DeploymentState)
                        {
                            case DeploymentStates.STOWED:
                                {
                                    this.part.stackIcon.SetIconColor(XKCDColors.LightCyan);
                                    if (this.PressureCheck && this.RandomDeployment) { PreDeploy(); }
                                    break;
                                }

                            case DeploymentStates.PREDEPLOYED:
                                {
                                    this.rigidbody.AddForceAtPosition(DragForce(0, this.preDeployedDiameter, 1f / this.semiDeploymentSpeed), this.ForcePosition, ForceMode.Force);
                                    if (this.trueAlt <= this.deployAltitude && this.dragTimer.Elapsed.TotalSeconds >= 1f / this.semiDeploymentSpeed) { Deploy(); }
                                    break;
                                }

                            case DeploymentStates.DEPLOYED:
                                {
                                    this.rigidbody.AddForceAtPosition(DragForce(this.preDeployedDiameter, this.deployedDiameter, 1f / this.deploymentSpeed), this.ForcePosition, ForceMode.Force);
                                    break;
                                }
                        }
                    }
                    //Deactivation
                    else
                    {
                        if (this.IsDeployed) { Cut(); }
                        else
                        {
                            this.failedTimer.Start();
                            StagingReset();
                        }
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (CompatibilityChecker.IsAllCompatible() && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor) && this.visible && !this.hid)
            {
                GUI.skin = HighLogic.Skin;
                this.window = GUILayout.Window(this.id, this.window, Window, "RealChute Info Window");
            }
        }

        private void OnDestroy()
        {
            if (!CompatibilityChecker.IsAllCompatible() || !HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor) { return; }
            //Hide/show UI event removal
            GameEvents.onHideUI.Remove(HideUI);
            GameEvents.onShowUI.Remove(ShowUI);
        }
        #endregion

        #region Overrides
        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }
            if (!CompatibilityChecker.IsAllCompatible())
            {
                this.Actions.ForEach(a => a.active = false);
                this.Events.ForEach(e =>
                    {
                        e.active = false;
                        e.guiActive = false;
                        e.guiActiveEditor = false;
                    });
                this.Fields["chuteCount"].guiActive = false;
                return;
            }

            //Staging icon
            this.part.stagingIcon = "PARACHUTES";
            InitializeAnimationSystem();

            //First initiation of the part
            if (!this.initiated)
            {
                this.initiated = true;
                this.armed = false;
                this.chuteCount = maxSpares;
                this.cap.gameObject.SetActive(true);
            }
            float tmpPartMass = this.TotalMass;
            this.massDelta = 0;
            if (this.part.partInfo != null && (object)this.part.partInfo.partPrefab != null)
            {
                this.massDelta = tmpPartMass - this.part.partInfo.partPrefab.mass;
            }

            //Flight loading
            if (HighLogic.LoadedSceneIsFlight)
            {
                Random random = new Random();
                this.randomTime = (float)random.NextDouble();
                this.randomX = (float)(random.NextDouble() * 100);
                this.randomY = (float)(random.NextDouble() * 100);

                //Hide/show UI event addition
                GameEvents.onHideUI.Add(HideUI);
                GameEvents.onShowUI.Add(ShowUI);

                if (this.CanRepack) { SetRepack(); }

                if (this.time != 0) { this.dragTimer = new PhysicsWatch(this.time); }
                if (this.DeploymentState != DeploymentStates.STOWED)
                {
                    this.part.stackIcon.SetIconColor(XKCDColors.Red);
                    this.cap.gameObject.SetActive(false);
                }

                if (this.staged && this.IsDeployed)
                {
                    this.parachute.gameObject.SetActive(true);
                    switch(this.DeploymentState)
                    {
                        case DeploymentStates.PREDEPLOYED:
                            this.part.SkipToAnimationTime(this.semiDeployedAnimation, this.semiDeploymentSpeed, Mathf.Clamp01(this.time)); break;
                        case DeploymentStates.DEPLOYED:
                            this.part.SkipToAnimationTime(this.fullyDeployedAnimation, this.deploymentSpeed, Mathf.Clamp01(this.time)); break;
                    }
                }

                DragCubeList cubes = this.part.DragCubes;
                //Set stock cubes to 0
                cubes.SetCubeWeight("PACKED", 0);
                cubes.SetCubeWeight("SEMIDEPLOYED", 0);
                cubes.SetCubeWeight("DEPLOYED", 0);

                //Sets RC cubes
                if (this.DeploymentState == DeploymentStates.STOWED)
                {
                    cubes.SetCubeWeight("PACKED", 1);
                    cubes.SetCubeWeight("RCDEPLOYED", 0);
                }
                else
                {
                    cubes.SetCubeWeight("PACKED", 0);
                    cubes.SetCubeWeight("RCDEPLOYED", 1);
                }
            }

            //GUI
            this.window = new Rect(200, 100, 350, 400);
            this.drag = new Rect(0, 0, 350, 30);
        }

        public override void OnLoad(ConfigNode node)
        {
            if (!CompatibilityChecker.IsAllCompatible()) { return; }
            if (HighLogic.LoadedScene == GameScenes.LOADING || !PartLoader.Instance.IsReady() || this.part.partInfo == null)
            {
                if (this.deployAltitude <= 500) { this.deployAltitude += 200; }
            }
            else
            {
                Part prefab = this.part.partInfo.partPrefab;
                this.massDelta = prefab == null ? 0 : this.TotalMass - prefab.mass;
            }
        }

        public override void OnActive()
        {
            if (!this.staged)
            {
                ActivateRC();
            }
        }

        public override string GetInfo()
        {
            if (!CompatibilityChecker.IsAllCompatible()) { return string.Empty; }
            //Info in the editor part window
            float tmpPartMass = this.TotalMass;
            this.massDelta = 0;
            if (this.part.partInfo != null && (object)this.part.partInfo.partPrefab != null)
            {
                this.massDelta = tmpPartMass - this.part.partInfo.partPrefab.mass;
            }

            StringBuilder b = new StringBuilder();
            b.AppendFormat("<b>Case mass</b>: {0}\n", this.caseMass);
            b.AppendFormat("<b>Spare chutes</b>: {0}\n", maxSpares);
            b.AppendFormat("<b>Autocut speed</b>: {0}m/s\n", this.autoCutSpeed);
            b.AppendLine("<b>Parachute material</b>: " + materialName);
            b.AppendFormat("<b>Drag coefficient</b>: {0:0.0}\n", staticCd);
            b.AppendFormat("<b>Chute max temperature</b>: {0}°C\n", maxTemp + absoluteZero);
            b.AppendFormat("<b>Predeployed diameter</b>: {0}m\n", this.preDeployedDiameter);
            b.AppendFormat("<b>Deployed diameter</b>: {0}m\n", this.deployedDiameter);
            b.AppendFormat("<b>Minimum deployment pressure</b>: {0}atm\n", this.minAirPressureToOpen);
            b.AppendFormat("<b>Deployment altitude</b>: {0}m\n", this.deployAltitude);
            b.AppendFormat("<b>Predeployment speed</b>: {0}s\n", Math.Round(1 / this.semiDeploymentSpeed, 1, MidpointRounding.AwayFromZero));
            b.AppendFormat("<b>Deployment speed</b>: {0}s\n", Math.Round(1 / this.deploymentSpeed, 1, MidpointRounding.AwayFromZero));
            return b.ToString();
        }

        public override bool IsStageable()
        {
            return true;
        }
        #endregion
    }
}
