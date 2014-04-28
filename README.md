Ferram Aerospace Research v0.13.2.1
=========================

Aerodynamics model for Kerbal Space Program

   Serious thanks:		a.g., for tons of bugfixes and code-refactorings  
            			Taverius, for correcting a ton of incorrect values  
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863

Source available at: https://github.com/ferram4/Ferram-Aerospace-Research

----------------------------------------------
---------------- INSTALLATION ----------------
----------------------------------------------

Install by merging the GameData folder in the zip with the GameData folder in your KSP install

ModuleManager and 000_Toolbar are REQUIRED for FAR to work properly.  Failing to copy these over will result in strange errors.

----------------------------------------------------------
---------------- LEARNING TO FLY WITH FAR ----------------
----------------------------------------------------------

-- ROCKETS --

Consider aerodynamic forces lest they sunder your vehicle or render it uncontrollable.

General troubleshooting suggestions:

-- Research gravity turns. With stock KSP, a gravity turn means "go straight up and then pitch over 45 degrees all at once" whereas with FAR, it means a gentler, smoother turning over: throughout it stay within 5 degrees of the surface prograde marker (slightly more than the size of the circle on the marker). WARNING: Large angles of attack can cause a loss of control in many designs.

-- Reduce your TWR. Old stock KSP designs can replace every Mainsail with a Skipper and still fly. Large TWRs tend to cause overspeeding in the lower atmosphere, which can cause aerodynamic forces to overpower control authority.

-- Use more serial staging and less parallel staging because with FAR installed, achieving orbit requires less dV and longer rockets usually are more aerodynamically stable. Also, as the mass drains out of the first stage's tanks, so the CoM moves forward, further stabilizing the rocket.

-- Make your first stage last until the upper atmosphere; early staging events can suddenly change the launch vehicle's dynamics and, by Murphy's Law, at an inopportune time.

-- Add fins to the bottom if you need an extra little bit of control. Their effectiveness will drop near Mach 1 due to transonic effects, but they can help on some troublesome designs.

-- ASAS in the atmosphere can cause flexing oscillations that reduce control due to uneven aerodynamic effects.

-- Instead of launching entire bases and space station sections, launch them in pieces and assembles them in orbit.


-- AIRPLANES AND SPACEPLANES --

Planes have much larger lateral aerodynamic forces on them than have rockets and therefore are more difficult to design in KSP. Let real planes inspire you and while in the SPH remember each planes's purpose, be it subsonic heavy transport, supersonic fighter, or stunt special.

General troubleshooting suggestions:

-- If the CoL is before the CoM, then increasing the plane's angle of attack (which increases lift) causes the plane to pitch up and thereby gain more lift and therefore pitch up, and so on unto many, many flips. Check where the CoL is located in the editor: in the static analysis tab in the FAR GUI, a negative slope for the moment coefficient (Cm, the yellow line on the graph) indicates a stable plane.

-- Aerodynamic forces change with Mach number; a plane that was perfectly stable at subsonic speeds could become unstable at supersonic speeds (or vice versa, depending on the design). Use the static analysis tab in the FAR GUI in the VAB / SPH to determine how its performance changes with Mach number. Consider sweeping angle of attack at all Mach numbers at which you expect to fly.

-- The CoM will shift when fuel drains; your plane can become unstable (or too stable to be controlled) if the CoM shifts too much.

-- So design your plane's wings that the frontmost lifting surface stalls first, whereafter stall (in order) the canards, main wing, and horizontal tail. The plane therefore will downward pitch if it begins to stall.

-- A larger vertical tail (placed further back) will dampen yaw and ease landing.

-- Sweeping a supersonic plane's wings proportionally to its speed reduces supersonic drag.


Sample Part.cfg:

For wings
-----------------------------------
MODULE  
{  
	name = FARControllableSurface / FARWingAerodynamicModel  
	b_2 = 0.5				//distance from wing root to tip; semi-span  
	MAC = 0.5				//Mean Aerodynamic Chord  
	e = 0.9					//Oswald's Efficiency, 0-1, increases drag from lift  
	nonSideAttach = 0			//0 for canard-like / normal wing pieces, 1 for ctrlsurfaces attached to the back of other wing parts  
	TaperRatio = 0.7			//Ratio of tip chord to root chord generally < 1, must be > 0  
	MidChordSweep = 25			//Sweep angle in degrees; measured down the center of the span / midchord position  
	maxdeflect = 15				//Default maximum deflection value; only used by FARControlableSurface  
	controlSurfacePivot = 1, 0, 0;		//Local vector that obj_ctrlSrf pivots about; defaults to 1, 0, 0 (right)  
	ctrlSurfFrac = 0.2			//Value from 0-1, percentage of the part that is a flap; only used by FARControlableSurface  
}  

For control surfaces, use above but replace FARWingAerodynamicModel with FARControllableSurface and add maxdeflect value

Set all the other winglet/control surface values to zero

Other Drag (not normally needed; only for very strange objects)
---------------------------
MODULE  
{  
	name = FARBasicDragModel  
	S = 1				//Surface Area  
	CdCurve				//Drag coefficient at various angles  
	{  
		key = -1 0		//backwards  
		key = 0 0.3		//sideways  
		key = 1.0 0		//forwards  
	}  
	ClCurve  
	{  
		key = -1 0		//Lift coefficient  
		key = -0.5 -0.03  
		key = 0 0  
		key = 0.5 0.03  
		key = 1 0  
	}  
	CmCurve				//Moment coefficient  
	{  
		key = -1 0  
		key = -0.5 -0.01	//keeping angle and moment signs the same results in pitch instability; it will try to flip over  
		key = 0 0		//making them opposite signs results in pitch stability; it will try to angle fully forward  
		key = 0.5 0.01  
		key = 1 0  
	}  
	localUpVector = 0,1,0		//a unit vector defining "up" for this part; 0,1,0 is standard for most stock-compliant parts  
	localForwardVector = 1,0,0	//a unti vector defining "forward" for this part; 1,0,0 is standard for most stock-compliant parts  
	majorMinorAxisRatio = 1		//the ratio of the part's "forward" length to its "side" length, used for drag and lift calculations  
	taperCrossSectionAreaRatio = 0;	//the part's tapered area projected on a plane normal to the "up" vector, divided by surface area; used to handle changes in drag at hypersonic speeds  
	CenterOfDrag = 0,0,0		//a vector defining the part's CoD  
}  

For both of these, set MaxDrag and MinDrag to 0



CHANGELOG
=======================================================

0.13.2.1v------------------------------------  
Features:
Update to Module Manager 2.0.5 to avoid an on-loading bug with that version of MM

Bugfixes:
Fixed wings and control surfaces not respecting turning aerodynamic failures off
Fix to EAS, IAS, etc. displaying 0 as the speed


0.13.2v------------------------------------  
Features:  
Update to Module Manager 2.0.3
Aerodynamically-induced structural failures will now be applied.  Beware of high dynamic pressure
Updated flight status section of Flight GUI to be somewhat more useful and handle more situations
Added IAS and velocity in knots, mph and km/h to the airspeed settings
Reduced supersonic and transonic drag to somewhat more reasonable levels
Increased floating point precision to double; also gets better performance

Added debug menu in Space Center view, with the following options:
	Option to switch between direct calculation of supersonic functions and using pre-computed splines to adjust performance on individual computers
	Option to switch on / off various debug data about FAR drag model applied to particular parts
	Option to disable aerodynamic failures

Switch to defining payload fairings and cargo bays using ConfigNodes
Switch to defining aerodynamic properties using ConfigNodes
Ability to define part modules that will exempt a part from getting FAR drag modules in a ConfigNode

BugFixes:
Fixed Firespitter and B9 landing gear making excessive drag
Fixed issue where some mod control surface parts would not display deflections unless the surfaces were moving



0.13.1v------------------------------------  
Features:  
License change to GPL v3.  
Moved source to Github.  All glory to the Octocat!  
Tweak to control surface function to prevent weird roll coupling with yaw / pitch inputs for 3-way symmetry

BugFixes:  
Fixed issue involving improperly defined engine fairings causing FAR to "crash" inside the game  
Fixed issue where landing gear could have different drag properties depending on the part they were attached to  
Fixed issue where EAS and Mach would not display on the navball  


0.13v------------------------------------  
Features:  
Full 0.23.5 compatibility  
Greater limits on control surface deflections  
Animations will cause part aerodynamics to be recalculated  
Implemented special asteroid-handling function  
Official release of all v0.13x1 features  
Updated to Toolbar v1.7.1


0.13x1v------------------------------------  
Features:  
Integrated numerous code optimizations and fixes from a.g.  
Rearward-facing tapered parts will produce less drag at hypersonic speeds, as they should; this may affect the stability of some designs  
Implemented more exact tapering drag based on cones rather than parabolas  
Wing sweep is better accounted for in multi-part assemblies  
Updated to Toolbar v1.7.0  

Tweaks:  
Reduction in skin friction drag to more proper levels  
Some changes to control surfaces to make them play better with SAS  

Bugfixes:  
Fixed a typo in the config that would allow turboramjets to accelerate without limit  


0.12.5.2v------------------------------------  
Bugfixes:  
Removed all code that could possibly result in very large surface areas on load for vehicles not near the floating origin  


0.12.5.1v------------------------------------  
Bugfixes:  
Fixed a serious issue where crafts loaded in space would not calculate their surface areas properly  


0.12.5v------------------------------------  
Features:  
Updated to Toolbar v1.4.0  
Reduced the severity of the forward aerodynamic center shift in the transonic regime; forward msot location is at .2 chord, as opposed to 0.025 chord (0 is leading edge)  
Moved aerodynamic center of supersonic wings backwards; currently 0.4 chord, was 0.35 chord  
Improved geometry detection for payload fairings, improving the drag simulation on them.  
Settings for the FAR control systems are now saved in the config.xml  

Bugfixes:  
Fixed an issue where the flap and wing interaction code would cause wings to not apply any forces if the lift coefficient was equal to zero  
Fixed a serious issue where the flap code could cause very large forces to be applied at very low angles of attack  
Fixed an issue where the CoL was not placed properly in the editor and where the editor analysis tab was inaccurate in its data.  
Second attempt at fixing severe supersonic roll twitching  

0.12.4v------------------------------------  
Features:  
Updated to Toolbar v1.3.0 and Module Manager 1.5.6  
Increases in body lift; added a new craft, the FAR Ugly Duckling to demonstrate  
Some tweaks to control surface tweakables  
Control surfaces now have a default time constant of 0.25s  

Bugfixes:  
Fixed an issue that would cause the static analysis panel of the editor GUI to not display accurate pitching moment data  
Fixed an issue that would cause the CoL indicator to be incorrect  
Fixed an issue where sporadic roll twitches would occur in supersonic flight  
Fixed an issue where flaps and spoilers would be limited to control surface deflections rather than the set deflections  
Fixed an issue where control surface interactions with wings were inaccurate, thanks a.g.  



0.12.3v------------------------------------  
Bugfixes:  
Fixed an issue where intakes could produce massive amounts of negative drag, causing bad times for all  
Fixed an issue where nullreferenceexceptions would be thrown in the editor when dealing with fairings  
Fixed a conversion error in the Flight Data GUI  
Fixed an issue involving flaps not having the proper scaling  
Fixed an issue where flaps deflected in the editor would cause weird things to happen when they were cloned  

0.12.2v------------------------------------  
Bugfixes:  
Fixed an issue where the CoL was shifted in the wrong direction by sweep  


0.12.1v------------------------------------  
Bugfixes:  
Fixed a gamebreaking issue where parts would initially feel sea-level drag after being decoupled.  


0.12v------------------------------------  
Features:  
Update to work with KSP 0.23!  
Control surface attribution handled through tweakables  
Control surfaces can now act as flaps & control surfaces OR as spoilers & control surfaces  
Refactored flap code to be simpler and more deterministic  
Updated supersonic stall characteristics to be less severe  

First implementation of atmospheric composition!  
	--Density (used for aero calculations) is now a function of pressure, temperature and gas properties, not just an upscaling of pressure  
	--Speed of sound is dependent on gas properties, with heavier gases resulting in lower speeds of sound  
	--Kerbin is given an atmospphere equivalent to Earth's: ~21% O2, ~78% N2, ~1% Ar  
	--Duna and Eve are given atmospheres similiar to Mars and Venus: ~95% CO2, ~5% N2  
	--Jool is given an atmosphere equivalent to a gas giant: ~90% H2, 10% He; it's temperature curve is also shifted to create more appropriate temperatures  
	--Laythe is given an oxygenated atmosphere with volcanic components: ~21% 02, ~9% N2, ~35% CO2, ~35% SO2  
	--Any additional bodies can be modified in the config.xml; properties default to Earth atmosphere  

Payload fairing titles can be specified in the config.xml  
Cargo fairing titles can be specified in the config.xml  
Control surface time constant has been reduced to 0.05s to reduce delay and SAS-induced wobbles; can be modified in the config.xml  

GUI now integrated with blizzy78's Toolbar plugin, redistributed per the license  

Bugfixes:  
Fixed an issue where body potential lift was calculated incorrectly, allowing things to fly easily when they should not have  


0.11v------------------------------------  
Features:  
Cylinder crossflow drag is now more accurately modeled; cylinders make less drag when the crossflow Mach number is below the critical Mach number (M = 0.4)  
More updates and fixes to Editor GUI and Center of Lift indicator  
Reduced stability of command pods and reentering objects to more reasonable levels; lifting reentries are now easier to manage, but sane mass distributions are required for proper stability  
Optimizations in all constantly-running code and some reductions in memory usage  

Update to use ModuleManager 1.5, by sarbian  
Some attach node corrections for stock parts that had incorrect attach sizes, causing poor drag modeling  

Ability to modify some aerodynamic properties in the config.xml, including:  
	--Area Factor: a multiplier to increase or decrease aerodynamic forces; 1 by default  
	--Attach Node Diameter Factor: how many meters in diameter an attach node size applies to; 1.25 by default  
	--Incompressible Rear Attach Drag: the drag coefficient of a rear-facing attach node at Mach 0  
	--Sonic Additional Rear Attach Drag: additional drag above incompressible at Mach 1  

Includes set up for Kerbal Updater  

BugFixes:  
Fixed an issue where cargo bays that started closed would not properly shield parts unless it was opened, then closed  

0.10v------------------------------------  
Features:  
Many, many updates and fixes to the Editor GUI and Center of Lift indicator, courtesy of a.g., including, but not limited to:  
	--CoL indicator offset error fixed  
	--Many more options in the static analysis window  
	--Moment coefficient zero crossings are marked  
	--Fixes to the stability derivative calculations  

Code refactoring by sarbian for future interfacing with MechJeb  
Hypersonic body lift is now more properly modeled; lifting reentries with command pods can now be properly done  

Bugfixes:  
Fixed an issue where the Editor GUI could end up inaccessible due to the minimize button being hidden by stock GUI elements  

0.9.7v------------------------------------  
Bugfixes:  
Fixed some issues with detecting proper part orientation for calculating drag data (thanks a.g.!)  
Fixed an issue where some roll control surfaces would not deflect properly  
Fixed an issue where fuselage crossflow drag was too high  



0.9.6.3v------------------------------------  
Features:  
Added ability to select pivot axis for FARControllableSurface modules; useful for modders  
Flight Data GUI prints reference area  
Windows can now be moved almost all the way off the screen; a small lip will remain visible for players to grab  
GUIs now save their minimized / unminimized state  

Bugfixes:  
Fixed an issue where highly tapered parts would produce absurd amounts of drag, above what they should  



0.9.6.2v------------------------------------  
Bugfixes:  
Fixed an issue with command pods not being unshielded when fairings were detached  
Fixed an issue where the drag of some parts would not be calculated properly  
Fixed an issue where multiple "isShielded" values would appear in the GUI  



0.9.6.1v------------------------------------  
Features:  
Updated Moment of Inertia calculations to be slightly more accurate  
Updated Flight GUI to make activating control systems simpler and to make modifying control system gains easier  

Bugfixes:  
Fixed an issue where some 3rd party intakes would make very large drag for no reason  
Fixed an issue where command pods would not be shielded by fairings or cargo bays  
Attempted workaround of drag from air intakes.  Requires further testing.  

Known Issues:  
CoL will not always updated properly in editor; cause currently unknown  


0.9.6v------------------------------------  
Features:  
Airbrakes can now be assigned to action groups; default to brakes group  
Editor GUI location saved now  
New method of saving GUI locations (thanks to asmi for these!)  
GUIs are now clamped to the boundaries of the screen  
Support for procedural fuselage stuff and overhaul of the fairing boundary code based on that  

Bugfixes:  
Fixed an issue where all airbrakes in the flight scene activated regardless of whether vehicle was being controlled or even controllable  
Fixed an issue where the Static Analysis tab in the Editor GUI would freeze if AoA Sweep was run to 90 degrees  
Fixed an issue in the part taper code that created unphysically low drag  


0.9.5.5v------------------------------------  
Features:  
Drag of blunt bodies adjusted to be closer to pre-0.9.5.2 levels  

Bugfixes:  
Fixed an issue where the L/D graph was not scaled properly  
Fixed an issue where the L/D graph did not have its scaling labeled  

0.9.5.4v------------------------------------  
Features:  
Modified lift of blunt bodies to be lower and drag of blunt bodies to be higher  
Changed scaling of L/D in editor GUI  


0.9.5.3v------------------------------------  
Features:  
Updated ModuleManager.dll  
0.21 KSP  

Undocumented Change at Release:  
Updated lift and drag of blunt bodies  


0.9.5.2v------------------------------------  
Features:  
Added compatibility with Procedural Fairings  
Tweaked lift and drag of blunt bodies and fuselages  

BugFixes:  
Fixed an error in drag-due-to-taper of non-lifting parts code  
May have fixed an issue with cargo bays shielding parts outside the bay  


0.9.5.1v------------------------------------  
BugFixes:  
Fixed an issue where Kerbals on EVA would spam the debug log with errors  
Fixed an issue that could sometimes lead to the nullspace bug  

0.9.5v------------------------------------  
Features:  
Switch over to Module Manager based file changes  
Drag and moment tendencies of tapered parts is now accounted for  
Cargo bays work with Firespitter animation modules   
Transonic and supersonic drag modified to be more realistic  
Optimizations to wing code  

BugFixes:  
Fixed an issue where the entire vessel would be unaffected by aerodynamics when a cargo bay was chosen as the vessel root part  
Fixed an issue where some B9 aerospace intakes would have unrealistic amounts of drag  
Possibly fixed an issue where cargo bays would shield parts outside the bay; this will require further testing  




0.9.4v------------------------------------  
Features:  
Update to 0.20's new file layout (note: still needs to overwrite stock configs for some parts)  
Uses new method to start partless plugin  

Thanks to a.g. for these:  
Update to air intake measurment method  
Wing leveler and Yaw Damper now work with trim  
Flaps are now persistent, can be activated by right-clicking  

BugFixes:  
Fixed an issue where Size 0 (0.625m) attachnodes would not apply drag  



0.9.3.1v------------------------------------  
Features:  
Targets .NET 3.5 framework rather than .NET 4.0 so it can be used by other plugins  

BugFixes:  
Fixed an issue where attachnodes would be inconsistent in whether they would add drag or not  
Fixed an issue where part orientation in world space affected drag independently of orientation with respect to airflow  
Fixed an issue where payload fairings would not apply drag and cause minor lag  


0.9.3v------------------------------------  
Features:  
Updated drag calculations based on unused attach nodes to improve performance and make more sense  

BugFixes:  
Fixed a previous known issue where the CoL would end up off center in the editor due to wing parts  
Fixed a bug that prevented FAR Control Sys modules from being applied to command parts  


0.9.2v------------------------------------  
Features:  
Updated CoL algorithm to be more accurate and make the indicator more useful  
Updated some wing interaction algorithms  
FAR Control Sys module automatically applied to all command pods  
FAR Editor GUI now independent of parts  

BugFixes:  
Fixed an issue where debris would cause massive lag  
Fixed some issues where wings would become incredibly oversensitive and shred planes at abnormally low speeds  

Known Issue:  
CoL can still end up off center; bug is caused by wing parts only and is being investigated  


0.9.1v------------------------------------  
BugFixes:  
Fixed an issue where some large wings would have unrealistically large lift forces that would tear them apart  
Fixed an issue where 3rd party heatshield parts were not recognized  


0.9v------------------------------------  
Features:  
Removed condensation effect to reduce overhead; has been replaced by stock vapor effects  

BugFixes:  
Fixed an issue where a control surfaces would not recognize the orientation of the vehicle after staging / undocking.  