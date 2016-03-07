using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FerramAerospaceResearch.PartExtensions;
using Random = System.Random;

/* RealChuteLite is the work of Christophe Savard (stupid_chris), and is licensed the same way than the rest of FAR is.
 * If you have any questions about this code, or want to report something, don't bug ferram about it, ask me
 * directly on GitHub, the forums, or IRC. */

namespace FerramAerospaceResearch.RealChuteLite
{

    public class RealChuteFAR : PartModule, IModuleInfo, IMultipleDragCube, IPartMassModifier
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
        public const float areaDensity = 5.65E-5f, areaCost = 0.075f, staticCd = 1;  //t/m², and F/m² for the first two
        public const double startTemp = 300, maxTemp = 493.15;
        public const double specificHeat = 1700, absoluteZero = -273.15;  //Specific heat in J/kg*K

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
        [KSPField(isPersistant = true)]
        public float preDeployedDiameter = 1, deployedDiameter = 25;
        [KSPField(isPersistant = true)]
        public float caseMass = 0, time = 0;
        [KSPField(isPersistant = true)]
        public bool armed = false, staged = false, initiated = false;
        [KSPField(isPersistant = true, guiActive = true, guiName = "Spare chutes")]
        public int chuteCount = 5;
        [KSPField(isPersistant = true)]
        public string depState = "STOWED";
        [KSPField(isPersistant = true)]
        public float currentArea = 0;
        [KSPField(isPersistant = true)]
        public double chuteTemperature = 300;
        [KSPField(isPersistant = true, guiActive = false, guiName = "Chute temp", guiFormat = "0.00", guiUnits = "°C")]
        public float currentTemp = 20;
        [KSPField(guiActive = false, guiName = "Max temp", guiFormat = "0.00", guiUnits = "°C")]
        public float chuteDisplayMaxTemp = (float)(maxTemp + absoluteZero);
        #endregion

        #region Propreties
        // If the vessel is stopped on the ground
        public bool groundStop
        {
            get { return this.vessel.LandedOrSplashed && this.vessel.horizontalSrfSpeed < this.autoCutSpeed; }
        }

        // If the parachute can be repacked
        public bool canRepack
        {
            get
            {
                return (this.groundStop || this.atmPressure == 0) && deploymentState == DeploymentStates.CUT
                    && this.chuteCount > 0 && FlightGlobals.ActiveVessel.isEVA;
            }
        }

        //If the Kerbal can repack the chute in career mode
        public bool canRepackCareer
        {
            get
            {
                ProtoCrewMember kerbal = FlightGlobals.ActiveVessel.GetVesselCrew()[0];
                return HighLogic.CurrentGame.Mode != Game.Modes.CAREER
                    || (kerbal.experienceTrait.Title == "Engineer" && kerbal.experienceLevel >= 1);
            }
        }

        //Predeployed area of the chute
        public float preDeployedArea
        {
            get { return GetArea(this.preDeployedDiameter); }
        }

        //Deployed area of the chute
        public float deployedArea
        {
            get { return GetArea(this.deployedDiameter); }
        }

        //The current useful convection area
        private double convectionArea
        {
            get
            {
                if (this.deploymentState == DeploymentStates.PREDEPLOYED && this.dragTimer.elapsed.Seconds < (1f / this.semiDeploymentSpeed))
                {
                    return UtilMath.Lerp(0, this.deployedArea, this.dragTimer.elapsed.Seconds * this.semiDeploymentSpeed);
                }
                return this.deployedArea;
            }
        }

        //Mass of the chute
        public float chuteMass
        {
            get { return this.deployedArea * areaDensity; }
        }

        //Total dry mass of the chute part
        public float totalMass
        {
            get
            {
                if (this.caseMass == 0) { this.caseMass = this.part.mass; }
                return this.caseMass + this.chuteMass;
            }
        }

        //Position to apply the force to
        public Vector3 forcePosition
        {
            get { return this.parachute.position; }
        }

        //If the random deployment timer has been spent
        public bool randomDeployment
        {
            get
            {
                if (!this.randomTimer.isRunning) { this.randomTimer.Start(); }

                if (this.randomTimer.elapsed.TotalSeconds >= this.randomTime)
                {
                    this.randomTimer.Reset();
                    return true;
                }
                return false;
            }
        }

        //If the parachute is in a high enough atmospheric pressure to deploy
        public bool pressureCheck
        {
            get
            {
                return this.atmPressure >= this.minAirPressureToOpen;
            }
        }

        //If the parachute can deploy
        public bool canDeploy
        {
            get
            {
                if (this.groundStop || this.atmPressure == 0) { return false; }
                else if (this.deploymentState == DeploymentStates.CUT) { return false; }
                else if (this.pressureCheck) { return true; }
                else if (!this.pressureCheck && this.isDeployed) { return true; }
                return false;
            }
        }

        //If the parachute is deployed
        public bool isDeployed
        {
            get
            {
                switch (this.deploymentState)
                {
                    case DeploymentStates.PREDEPLOYED:
                    case DeploymentStates.DEPLOYED:
                        return true;
                }
                return false;
            }
        }

        //Persistent deployment state
        public DeploymentStates deploymentState
        {
            get
            {
                if (this.state == DeploymentStates.NONE) { this.deploymentState = states[this.depState]; }
                    return state;
            }
            set
            {
                this.state = value;
                this.depState = names[value];
            }
        }

        //The inverse thermal mass of the parachute
        public double invThermalMass
        {
            get
            {
                if (this.thermMass == 0)
                {
                    this.thermMass = 1d / (specificHeat * this.chuteMass);
                }
                return thermMass;
            }
        }

        //The current chute emissivity constant
        public double chuteEmissivity
        {
            get
            {
                if (this.chuteTemperature < 293.15) { return 0.72; }
                else if (this.chuteTemperature > 403.15) { return 0.9; }
                else
                {
                    return UtilMath.Lerp(0.72, 0.9, ((this.chuteTemperature - 293.15) / 110) + 293.15);
                }
            }
        }

        //Bold KSP style GUI label
        private static GUIStyle _boldLabel = null;
        public static GUIStyle boldLabel
        {
            get
            {
                if (_boldLabel == null)
                {
                    GUIStyle style = new GUIStyle(HighLogic.Skin.label);
                    style.fontStyle = FontStyle.Bold;
                    _boldLabel = style;
                }
                return _boldLabel;
            }
        }

        //Yellow KSP style GUI label
        private static GUIStyle _yellowLabel = null;
        public static GUIStyle yellowLabel
        {
            get
            {
                if (_yellowLabel == null)
                {
                    GUIStyle style = new GUIStyle(HighLogic.Skin.label);
                    style.normal.textColor = XKCDColors.BrightYellow;
                    style.hover.textColor = XKCDColors.BrightYellow;
                    _yellowLabel = style;
                }
                return _yellowLabel;
            }
        }

        //Red KSP style GUI label
        private static GUIStyle _redLabel = null;
        public static GUIStyle redLabel
        {
            get
            {
                if (_redLabel == null)
                {
                    GUIStyle style = new GUIStyle(HighLogic.Skin.label);
                    style.normal.textColor = XKCDColors.Red;
                    style.hover.textColor = XKCDColors.Red;
                    _redLabel = style;
                }
                return _redLabel;
            }
        }

        //Quick access to the part GUI events
        private BaseEvent _deploy = null, _disarm = null, _cut = null, _repack = null;
        private BaseEvent deploy
        {
            get
            {
                if (this._deploy == null) { this._deploy = Events["GUIDeploy"]; }
                return this._deploy;
            }
        }
        private BaseEvent disarm
        {
            get
            {
                if (this._disarm == null) { this._disarm = Events["GUIDisarm"]; }
                return this._disarm;
            }
        }
        private BaseEvent cutE
        {
            get
            {
                if (this._cut == null) { this._cut = Events["GUICut"]; }
                return this._cut;
            }
        }
        private BaseEvent repack
        {
            get
            {
                if (this._repack == null) { this._repack = Events["GUIRepack"]; }
                return this._repack;
            }
        }
        #endregion

        #region Fields
        //Flight
        private Vector3 dragVector = new Vector3(), pos = new Vector3d();
        private PhysicsWatch deploymentTimer = new PhysicsWatch(), failedTimer = new PhysicsWatch(), launchTimer = new PhysicsWatch(), dragTimer = new PhysicsWatch();
        private bool displayed = false, showDisarm = false;
        private double ASL, trueAlt;
        private double atmPressure, atmDensity;
        private float sqrSpeed;
        private double thermMass = 0, convFlux = 0, extTemp = 0;


        //Part
        private Animation anim = null;
        private Transform parachute = null, cap = null;
        private PhysicsWatch randomTimer = new PhysicsWatch();
        private float randomX, randomY, randomTime;
        private DeploymentStates state = DeploymentStates.NONE;
        private SafeState safeState = SafeState.SAFE;
        private float massDelta = 0;

        //GUI
        private bool visible = false, hid = false;
        private int ID = Guid.NewGuid().GetHashCode();
        private GUISkin skins = HighLogic.Skin;
        private Rect window = new Rect(), drag = new Rect();
        private Vector2 scroll = new Vector2();
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
            this.deploy.active = true;
            DeactivateRC();
        }

        //Repacks chute from EVA if in space or on the ground
        [KSPEvent(guiActive = false, active = true, externalToEVAOnly = true, guiActiveUnfocused = true, guiName = "Repack chute", unfocusedRange = 5)]
        public void GUIRepack()
        {
            if (this.canRepack)
            {
                if (!this.canRepackCareer)
                {
                    ScreenMessages.PostScreenMessage("Only a level 1 and higher engineer can repack a parachute", 5, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }

                this.part.Effect("rcrepack");
                this.repack.guiActiveUnfocused = false;
                this.part.stackIcon.SetIconColor(XKCDColors.White);
                if (this.chuteCount != -1) { this.chuteCount--; }
                this.deploymentState = DeploymentStates.STOWED;
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
                if (HighLogic.LoadedSceneIsEditor) { parachutes.AddRange(EditorLogic.SortedShipList.Where(p => p.Modules.Contains("RealChuteFAR")).Select(p => (RealChuteFAR)p.Modules["RealChuteFAR"])); }
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
            if (this.isDeployed) { Cut(); }
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
            return (float)((diameter * diameter * Math.PI) / 4d);
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
            foreach (Part part in this.part.symmetryCounterparts)
            {
                RealChuteFAR module = part.Modules["RealChuteFAR"] as RealChuteFAR;
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
            else { this.part.inverseStage = Staging.CurrentStage; }
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
            return (float)this.atmDensity * this.sqrSpeed * staticCd * area / 2000f;
        }

        //Gives the cost for this parachute
        public float GetModuleCost(float defaultCost)
        {
            return (float)Math.Round(this.deployedArea * areaCost);
        }

        //For IPartMassModifier
        public float GetModuleMass(float defaultMass)
        {
            return massDelta;
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
            float time = Time.time;
            this.parachute.Rotate(new Vector3(5 * (Mathf.PerlinNoise(time, this.randomX + Mathf.Sin(time)) - 0.5f), 5 * (Mathf.PerlinNoise(time, this.randomY + Mathf.Sin(time)) - 0.5f), 0));
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
            this.deploymentState = DeploymentStates.PREDEPLOYED;
            this.parachute.gameObject.SetActive(true);
            this.cap.gameObject.SetActive(false);
            this.part.PlayAnimation(this.semiDeployedAnimation, this.semiDeploymentSpeed);
            this.dragTimer.Start();
            this.part.DragCubes.SetCubeWeight("PACKED", 0);
            this.part.DragCubes.SetCubeWeight("RCDEPLOYED", 1);
            Fields["currentTemp"].guiActive = true;
            Fields["chuteDisplayMaxTemp"].guiActive = true;
        }

        //Parachute deployment
        public void Deploy()
        {
            this.part.stackIcon.SetIconColor(XKCDColors.RadioactiveGreen);
            this.part.Effect("rcdeploy");
            this.deploymentState = DeploymentStates.DEPLOYED;
            this.dragTimer.Restart();
            this.part.PlayAnimation(this.fullyDeployedAnimation, this.deploymentSpeed);
        }

        //Parachute cutting
        public void Cut()
        {
            this.part.Effect("rccut");
            this.deploymentState = DeploymentStates.CUT;
            this.parachute.gameObject.SetActive(false);
            this.currentArea = 0;
            this.dragTimer.Reset();
            this.currentTemp = (float)(startTemp + absoluteZero);
            this.chuteTemperature = startTemp;
            Fields["currentTemp"].guiActive = false;
            Fields["chuteDisplayMaxTemp"].guiActive = false;
            SetRepack();
        }

        //Calculates parachute deployed area
        private float DragDeployment(float time, float debutDiameter, float endDiameter)
        {
            if (!this.dragTimer.isRunning) { this.dragTimer.Start(); }

            double t = this.dragTimer.elapsed.TotalSeconds;
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
            this.convFlux = this.vessel.convectiveCoefficient * UtilMath.Lerp(1d, 1d + (Math.Sqrt(this.vessel.mach * this.vessel.mach * this.vessel.mach) * (this.vessel.dynamicPressurekPa / 101.325)),
                    (this.vessel.mach - PhysicsGlobals.FullToCrossSectionLerpStart) / (PhysicsGlobals.FullToCrossSectionLerpEnd))
                    * (this.vessel.externalTemperature - this.chuteTemperature);
        }

        //Calculates the temperature of the chute and cuts it if needed
        private bool CalculateChuteTemp()
        {
            if (this.chuteTemperature < PhysicsGlobals.SpaceTemperature) { this.chuteTemperature = startTemp; }

            double emissiveFlux = 0;
            if (chuteTemperature > 0d)
            {
                double temp2 = this.chuteTemperature * this.chuteTemperature;
                emissiveFlux = 2 * PhysicsGlobals.StefanBoltzmanConstant * this.chuteEmissivity * PhysicsGlobals.RadiationFactor * temp2 * temp2;
            }
            this.chuteTemperature = Math.Max(PhysicsGlobals.SpaceTemperature, this.chuteTemperature + ((this.convFlux - emissiveFlux) * 0.001 * this.convectionArea * this.invThermalMass * TimeWarp.fixedDeltaTime));
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
            part.stackIcon.SetBackgroundColor(XKCDColors.White);
            SafeState s;
            if (this.vessel.externalTemperature <= maxTemp || convFlux < 0) { s = SafeState.SAFE; }
            else
            {
                if (this.chuteTemperature + (0.001 * convFlux * this.invThermalMass * this.deployedArea * 0.35) <= maxTemp) { s = SafeState.RISKY; }
                else { s = SafeState.DANGEROUS; }
            }

            if (this.safeState != s)
            {
                this.safeState = s;
                switch(this.safeState)
                {
                    case SafeState.SAFE:
                        part.stackIcon.SetBackgroundColor(XKCDColors.White); break;

                    case SafeState.RISKY:
                        part.stackIcon.SetBackgroundColor(XKCDColors.BrightYellow); break;

                    case SafeState.DANGEROUS:
                        part.stackIcon.SetBackgroundColor(XKCDColors.Red); break;
                }
            }
        }

        //Initializes parachute animations
        private void InitializeAnimationSystem()
        {
            //I know this seems random, but trust me, it's needed, else some parachutes don't animate, because fuck you, that's why.
            this.anim = this.part.FindModelAnimators(this.capName).FirstOrDefault();

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
                        parachute.gameObject.SetActive(false);
                        cap.gameObject.SetActive(true);
                        break;
                    }

                case "RCDEPLOYED":      //This is not a predeployed state, no touchy
                    {
                        parachute.gameObject.SetActive(false);
                        cap.gameObject.SetActive(false);
                        break;
                    }

                case "SEMIDEPLOYED":    //  stock 
                    {
                        parachute.gameObject.SetActive(true);
                        cap.gameObject.SetActive(false);
                        part.SkipToAnimationTime(this.semiDeployedAnimation, 0f, 1f); // to the end of the animation
                        break;
                    }

                case "DEPLOYED":        //  stock 
                    {
                        parachute.gameObject.SetActive(true);
                        cap.gameObject.SetActive(false);
                        part.SkipToAnimationTime(this.fullyDeployedAnimation, 0f, 1f);  // to the end of the animation
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
        #endregion

        #region Functions
        private void Update()
        {
            if (!CompatibilityChecker.IsAllCompatible() || !HighLogic.LoadedSceneIsFlight) { return; }

            //Makes the chute icon blink if failed
            if (this.failedTimer.isRunning)
            {
                double time = this.failedTimer.elapsed.TotalSeconds;
                if (time <= 2.5)
                {
                    if (!this.displayed)
                    {
                        ScreenMessages.PostScreenMessage("Parachute deployment failed.", 2.5f, ScreenMessageStyle.UPPER_CENTER);
                        if (this.part.ShieldedFromAirstream) { ScreenMessages.PostScreenMessage("Reason: parachute is shielded from airstream.", 2.5f, ScreenMessageStyle.UPPER_CENTER);}
                        else if (this.groundStop) { ScreenMessages.PostScreenMessage("Reason: stopped on the ground.", 2.5f, ScreenMessageStyle.UPPER_CENTER); }
                        else if (this.atmPressure == 0) { ScreenMessages.PostScreenMessage("Reason: in space.", 2.5f, ScreenMessageStyle.UPPER_CENTER); }
                        else { ScreenMessages.PostScreenMessage("Reason: too high.", 2.5f, ScreenMessageStyle.UPPER_CENTER); }
                        this.displayed = true;
                    }
                    if (time < 0.5 || (time >= 1 && time < 1.5) || time >= 2) { this.part.stackIcon.SetIconColor(XKCDColors.Red); }
                    else { this.part.stackIcon.SetIconColor(XKCDColors.White); }
                }
                else
                {
                    this.displayed = false;
                    this.part.stackIcon.SetIconColor(XKCDColors.White);
                    this.failedTimer.Reset();
                }
            }

            this.disarm.active = (this.armed || this.showDisarm);
            this.deploy.active = !this.staged && this.deploymentState != DeploymentStates.CUT;
            this.cutE.active = this.isDeployed;
            this.repack.guiActiveUnfocused = this.canRepack;
        }

        private void FixedUpdate()
        {
            //Flight values
            if (!CompatibilityChecker.IsAllCompatible() || !HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null || this.part.Rigidbody == null) { return; }
            this.pos = this.part.partTransform.position;
            this.ASL = FlightGlobals.getAltitudeAtPos(this.pos);
            this.trueAlt = this.ASL;
            if (this.vessel.mainBody.pqsController != null)
            {
                double terrainAlt = this.vessel.pqsAltitude;
                if (!this.vessel.mainBody.ocean || terrainAlt > 0) { this.trueAlt -= terrainAlt; }
            }
            this.atmPressure = FlightGlobals.getStaticPressure(this.ASL, this.vessel.mainBody) * PhysicsGlobals.KpaToAtmospheres;
            this.atmDensity = part.atmDensity;
            Vector3 velocity = this.part.Rigidbody.velocity + Krakensbane.GetFrameVelocityV3f();
            this.sqrSpeed = velocity.sqrMagnitude;
            this.dragVector = -velocity.normalized;

            if (this.atmDensity > 0) { CalculateChuteFlux(); }
            else { this.convFlux = 0; }                

            CalculateSafeToDeployEstimate();

            if (!this.staged && GameSettings.LAUNCH_STAGES.GetKeyDown() && this.vessel.isActiveVessel && (this.part.inverseStage == vessel.currentStage - 1 || vessel.currentStage == 0)) { ActivateRC(); }

            if (this.staged)
            {
                //Checks if the parachute must disarm
                if (this.armed)
                {
                    this.part.stackIcon.SetIconColor(XKCDColors.LightCyan);
                    if (this.canDeploy) { this.armed = false; }
                }
                //Parachute deployments
                else
                {
                    //Parachutes
                    if (this.canDeploy)
                    {
                        if (this.isDeployed)
                        {
                            if (!CalculateChuteTemp()) { return; }
                            FollowDragDirection();
                        }

                        switch (this.deploymentState)
                        {
                            case DeploymentStates.STOWED:
                                {
                                    this.part.stackIcon.SetIconColor(XKCDColors.LightCyan);
                                    if (this.pressureCheck && this.randomDeployment) { PreDeploy(); }
                                    break;
                                }

                            case DeploymentStates.PREDEPLOYED:
                                {
                                    this.part.Rigidbody.AddForceAtPosition(DragForce(0, this.preDeployedDiameter, 1f / this.semiDeploymentSpeed), this.forcePosition, ForceMode.Force);
                                    if (this.trueAlt <= this.deployAltitude && this.dragTimer.elapsed.TotalSeconds >= 1f / this.semiDeploymentSpeed) { Deploy(); }
                                    break;
                                }

                            case DeploymentStates.DEPLOYED:
                                {
                                    this.part.rb.AddForceAtPosition(DragForce(this.preDeployedDiameter, this.deployedDiameter, 1f / this.deploymentSpeed), this.forcePosition, ForceMode.Force);
                                    break;
                                }

                            default:
                                break;
                        }
                    }
                    //Deactivation
                    else
                    {
                        if (this.isDeployed) { Cut(); }
                        else
                        {
                            this.failedTimer.Start();
                            StagingReset();
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (!CompatibilityChecker.IsAllCompatible() || (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)) { return; }
            //Hide/show UI event removal
            GameEvents.onHideUI.Remove(HideUI);
            GameEvents.onShowUI.Remove(ShowUI);
        }
        #endregion

        #region Overrides
        public override void OnStart(PartModule.StartState state)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) { return; }
            if (!CompatibilityChecker.IsAllCompatible())
            {
                Actions.ForEach(a => a.active = false);
                Events.ForEach(e =>
                    {
                        e.active = false;
                        e.guiActive = false;
                        e.guiActiveEditor = false;
                    });
                Fields["chuteCount"].guiActive = false;
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
            float tmpPartMass = this.totalMass;
            this.massDelta = 0f;
            if ((object)(this.part.partInfo) != null && (object)(this.part.partInfo.partPrefab) != null)
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

                if (this.canRepack) { SetRepack(); }

                if (this.time != 0) { this.dragTimer = new PhysicsWatch(this.time); }
                if (this.deploymentState != DeploymentStates.STOWED)
                {
                    this.part.stackIcon.SetIconColor(XKCDColors.Red);
                    this.cap.gameObject.SetActive(false);
                }

                if (this.staged && this.isDeployed)
                {
                    this.parachute.gameObject.SetActive(true);
                    switch(this.deploymentState)
                    {
                        case DeploymentStates.PREDEPLOYED:
                            this.part.SkipToAnimationTime(this.semiDeployedAnimation, this.semiDeploymentSpeed, Mathf.Clamp01(this.time)); break;
                        case DeploymentStates.DEPLOYED:
                            this.part.SkipToAnimationTime(this.fullyDeployedAnimation, this.deploymentSpeed, Mathf.Clamp01(this.time)); break;
                        default:
                            break;
                    }
                }

                DragCubeList cubes = this.part.DragCubes;
                //Set stock cubes to 0
                cubes.SetCubeWeight("PACKED", 0);
                cubes.SetCubeWeight("SEMIDEPLOYED", 0);
                cubes.SetCubeWeight("DEPLOYED", 0);

                //Sets RC cubes
                if (this.deploymentState == DeploymentStates.STOWED)
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
            float tmpPartMass = this.totalMass;
            this.massDelta = 0f;

            if ((object)(this.part.partInfo) != null && (object)(this.part.partInfo.partPrefab) != null)
            {
                this.massDelta = tmpPartMass - this.part.partInfo.partPrefab.mass;
            }
            if (HighLogic.LoadedScene == GameScenes.LOADING)
            {
                if (this.deployAltitude <= 500) { this.deployAltitude += 200; }
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
            float tmpPartMass = this.totalMass;
            this.massDelta = 0f;
            if ((object)(this.part.partInfo) != null && (object)(this.part.partInfo.partPrefab) != null)
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
            b.AppendFormat("<b>Predeployment speed</b>: {0}s\n", Math.Round(1f / this.semiDeploymentSpeed, 1, MidpointRounding.AwayFromZero));
            b.AppendFormat("<b>Deployment speed</b>: {0}s\n", Math.Round(1f / this.deploymentSpeed, 1, MidpointRounding.AwayFromZero));
            return b.ToString();
        }
        #endregion

        #region GUI
        private void OnGUI()
        {
            if (!CompatibilityChecker.IsAllCompatible() && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)) { return; }

            //Info window visibility
            if (this.visible && !this.hid)
            {
                this.window = GUILayout.Window(this.ID, this.window, Window, "RealChute Info Window", this.skins.window);
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
            GUILayout.Label(b.ToString(), this.skins.label);

            //Beggining scroll
            this.scroll = GUILayout.BeginScrollView(this.scroll, false, false, this.skins.horizontalScrollbar, this.skins.verticalScrollbar, this.skins.box);
            GUILayout.Space(5);
            GUILayout.Label("General:", boldLabel, GUILayout.Width(120));

            //General labels
            b = new StringBuilder("Autocut speed: ").Append(this.autoCutSpeed).AppendLine("m/s");
            b.Append("Spare chutes: ").Append(chuteCount);
            GUILayout.Label(b.ToString(), this.skins.label);

            //Specific labels
            GUILayout.Label("___________________________________________", boldLabel);
            GUILayout.Space(3);
            GUILayout.Label("Main chute:", boldLabel, GUILayout.Width(120));
            //Initial label
            b = new StringBuilder();
            b.AppendLine("Material: " + materialName);
            b.AppendLine("Drag coefficient: " + staticCd.ToString("0.0"));
            b.Append("Predeployed diameter: ").Append(this.preDeployedDiameter).Append("m\nArea: ").Append(this.preDeployedArea.ToString("0.###")).AppendLine("m²");
            b.Append("Deployed diameter: ").Append(this.deployedDiameter).Append("m\nArea: ").Append(this.deployedArea.ToString("0.###")).Append("m²");
            GUILayout.Label(b.ToString(), this.skins.label);

            //DeploymentSafety
            switch (this.safeState)
            {
                case SafeState.SAFE:
                    GUILayout.Label("Deployment safety: safe", skins.label); break;

                case SafeState.RISKY:
                    GUILayout.Label("Deployment safety: risky", yellowLabel); break;

                case SafeState.DANGEROUS:
                    GUILayout.Label("Deployment safety: dangerous", redLabel); break;
            }

            //Temperature info
            b = new StringBuilder();
            b.Append("Chute max temperature: ").Append(maxTemp + absoluteZero).AppendLine("°C");
            b.Append("Current chute temperature: ").Append(Math.Round(this.chuteTemperature + absoluteZero, 1, MidpointRounding.AwayFromZero)).Append("°C");
            GUILayout.Label(b.ToString(), this.chuteTemperature / maxTemp > 0.85 ? redLabel : this.skins.label);

            //Predeployment pressure selection
            GUILayout.Label("Predeployment pressure: " + this.minAirPressureToOpen + "atm", this.skins.label);
            if (HighLogic.LoadedSceneIsFlight)
            {
                //Predeployment pressure slider
                this.minAirPressureToOpen = GUILayout.HorizontalSlider(this.minAirPressureToOpen, 0.005f, 1, this.skins.horizontalSlider, this.skins.horizontalSliderThumb);
            }

            //Deployment altitude selection
            GUILayout.Label("Deployment altitude: " + this.deployAltitude + "m", this.skins.label);
            if (HighLogic.LoadedSceneIsFlight)
            {
                //Deployment altitude slider
                this.deployAltitude = GUILayout.HorizontalSlider(this.deployAltitude, 50, 10000, this.skins.horizontalSlider, this.skins.horizontalSliderThumb);
            }

            //Other labels
            b = new StringBuilder();
            b.Append("Predeployment speed: ").Append(Math.Round(1f / this.semiDeploymentSpeed, 1, MidpointRounding.AwayFromZero)).AppendLine("s");
            b.Append("Deployment speed: ").Append(Math.Round(1f / this.deploymentSpeed, 1, MidpointRounding.AwayFromZero)).Append("s");
            GUILayout.Label(b.ToString(), this.skins.label);

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

        //Creates a centered GUI button
        public static void CenteredButton(string text, Action action)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(text, HighLogic.Skin.button, GUILayout.Width(150)))
            {
                action();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        #endregion
    }
}
