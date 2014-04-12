Ferram Aerospace Research
=========================

Aerodynamics model for Kerbal Space Program


Serious thanks:		a.g., for tons of bugfixes and code-refactorings
			Taverius, for correcting a ton of incorrect values
			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
			ialdabaoth (who is awesome), who originally created Module Manager
			Duxwing, for copy editing the readme

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
	localForwardVector = 1,0,0	//a unti vector defining "forward" for this part; 1,0,0 is standard for msot stock-compliant parts
	majorMinorAxisRatio = 1		//the ratio of the part's "forward" length to its "side" length, used for drag and lift calculations
	taperCrossSectionAreaRatio = 0;	//the part's tapered area projected on a plane normal to the "up" vector, divided by surface area; used to handle changes in drag at hypersonic speeds
	CenterOfDrag = 0,0,0		//a vector defining the part's CoD
}

For both of these, set MaxDrag and MinDrag to 0



CHANGELOG
=======================================================

0.13v------------------------------------
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


0.8.4.1v------------------------------------
Features:
FARWingAerodynamicModel variations now override existing winglet and controlsurface modules

Bugfixes:
Fixed a major bug that would cause problems with loading the saved positions of the FAR windows


0.8.4v------------------------------------
Features:
Updated wing interactions to be smoother and more realisitic
Updated FAR Editor Static Analysis to show more of the moment coefficient line

BugFixes:
Fixed some issues affecting the functionality of payload fairings / cargo bays
 
0.8.3v------------------------------------
Features:
Some optimizations
Added a limit on the change in amount of stall per frame
Changes to how Aerodynamic Center / Center of Lift is affected by stall; planes should behave better / more realistically in stall
Reduced skin friction drag on non-wing parts
Updated sweep calculations to be more realistic
Updated control systems to include the effect of control surface delay in the control laws
Updated FAR Editor GUI to include more accurate moment of inertia calculations
Changes to make code more compatible with other mod parts
Tweaked Basic Jet and TurboJet (now TurboRamJet) thrust as a function of velocity; jets have had thrust nerfed

BugFixes:
Fixed an issue where payload fairings and cargo bays would not find the correct parts to shield from aerodynamics
Fixed an issue where subsonic lift used a non-linear form but low transonic lift did not
Fixed an issue where TSFC was not calculated correctly

0.8.2.2v------------------------------------
Features:
Reduced drag of payload fairings slightly

Bugfixes:
Fixed an issue where drag would not be updated when payload fairings were decoupled
Fixed an issue where cargo bay parts would throw NullReferenceExceptions

0.8.2.1v------------------------------------
Features:
Minor optimizations to condensation effect

Bugfixes:
Fixed a major issue with control surfaces causing crashes when the time step becomes too large


0.8.2v------------------------------------
Features:
Support for cargo bay parts
Tweaked Turbojet engine velocity curve
Minor adjustment to aerodynamic smoothing functions
Added AoA limiter
Nose cone drag increased
Payload fairing drag decreased
New condensation effects caused by high lift and transonic flight

Bugfixes:
Fixed an issue where the fuel fraction value would null out to NaN and cause the rest of the Flight Data GUI to stop updating
Fixed an issue where the air usage value was calculated improperly
Fixed an issue where stalling was not handled properly, causing unphysical stall characteristics
Fixed an issue where the Flight Data GUI would calculate pitch, yaw and roll angles improperly


0.8.1v------------------------------------
Features:
Some optimizations
Wing sweep calculation changed to be more accurate
Supersonic wing code accounts for relative effects of subsonic LE (wing inside Mach cone) and supersonic LE (wing outside Mach cone)
Updated wing lift variation with angle of attack to account for sweep properly
Tweaked in-editor lateral dynamics simulation to be more accurate
Tweaked some drag / lift values for pods
Flight Data UI now includes pitch, heading and roll angles, along with angle of attack and sideslip angle
Flight Data UI displays ratio of air provided by intakes to air demanded by engines; can be used to predict flameouts
Turbojet engines have lower exhaust velocity; 1800 instead of 2400

Bugfixes:
Fixed an error in the wing interference calculations that could lead to unphysical unsymmetrical loadings on symmetrical ships
Fixed an error in the in-editor simulation that neglected the full effects of non-wing parts


0.8v------------------------------------
Features:
Added unsteady aerodynamics to wing code; this should help with dynamic stability
Reduced smoothing of aerodynamic forces to improve dynamic stability
Increased zero-lift drag for all objects
Decreased transonic drag for all objects
Decreased deployed drag of landing gear and other animating parts
Jet engine thrust increased slightly to balance higher zero-lift drag
Moved CoL forward by 0.1 MAC for supersonic aerodynamics; this reduces stability
Added feature to calculate aircraft properties, including stability derivatives, in-editor
Added ability to simulate airplane dynamics using a linear approximation of the physics using the stability derivatives

Bugfixes:
Fixed an issue where the beginnings of interaction with Krakensbane at 750m/s could act a little weird, causing unphysical forces
Fixed an issue where ladders would add an unphysical amount of drag




0.7.3.1v------------------------------------
BugFixes:
Fixed an issue where the Dynamic Control Adjustment System increased control at high speeds rather than decreasing it
Fixed an issue where an issue where the CoL indicator in the editor would assume the landing gear was deployed, rather than stowed


0.7.3v------------------------------------
Features:
Update part.cfgs to use 0.18.2 values
Supersonic calculations now model wings as having diamond-shaped airfoil cross-sections as opposed to flat plate cross-sections as before; supersonic flight made more realistic as a result
Transonic code updated to include changes in supersonic calculations
Removed Dynamic Control Adjustment System accounting for Mach Number; it didn't end well
Some more updates to streamlining detection

BugFixes:
Fixed an issue where the wing leveler had an integral term that would cause it to fail to keep the plane level; integral term removed
Fixed an issue where negative deflection values for control surfaces would prevent the model from animating
Fixed an issue where an interaction with the Re-entry Heat Heat Shield could cause the pod to re-enter nose-first


0.7.2v------------------------------------
Features:
Editor GUI accounts can calculate stability of empty vehicles (doesn't move CoM indicator yet, but the analysis graph shows the change in pitching moment)
Editor GUI allows you to change pitch control settings in analysis
Modified stall behavior for supersonic flight to be less forgiving
Dynamic Control Adjustment System now accounts for changes due to Mach Number
TurboJet engines have lower maximum velocity

BugFixes:
Fixed more errors in part.cfg files
Fixed a major issue that caused unphysically optimistic, easy-to-control aerodynamics at supersonic speeds
Fixed an issue where supersonic drag could become infinite at high angles of attack and cause plane disassembly
Fixed an issue where supersonic lift and drag were twice as large as they should have been




0.7.1v------------------------------------
Features:
Refined approximations of drag for RCS / Landing gear non-wing mod parts
Tweaked transonic drag to be more forgiving
Control surfaces now react as first-order systems with a time constant of 0.1s
Pitch and Yaw Dampers now respond to changes in angle of attack and sideslip angle, respectively, rather than angular velocity
Pitch and Yaw Dampers refined to be more useful in combating dynamic instability / limit-cycle oscillations
Preemptively updated payload fairing code to account for upcoming changes to KW Rocketry Pack
Added ability to switch between Surface TAS (default), EAS and Mach number for surface velocity
Relative air density now set relative to Kerbin sea level for all bodies
Modification to kluge fix for Jool's unphysical temperatures: atmospheric temperaure offset to put the lowest temp at 4 kelvin
Mk2 and Mk3 fuselages make more body lift

BugFixes:
Fixed an issue where the static analysis tab of the editor GUI would display erroneous values of moment coefficient
Fixed an issue where flap performance could not be determined using the editor GUI
Fixed an issue where clicking through the editor GUI became possible
Fixed an issue where the Editor GUI would allow negative lower bounds for Mach number sweeps
Fixed an issue where the Editor GUI would limit Mach number upper bounds to 90
Fixed an issue where the Editor GUI would allow the upper bound to be placed below the lower bound
Fixed some issues involving interpolation of important supersonic values that would cause wonky physics
Fixed some errors in part.cfg files




0.7v------------------------------------
Features:
Approximations of drag for non-wing mod parts; no guarantees about accuracy
Payload fairings work in a realistic fashion
Implemented airbrake option for control surfaces
Transonic drag model tweaked
Supersonic drag tweaked
Editor and Flight GUIs edited to be less intrusive when minimized


BugFixes:
Fixed an issue where streamlining would not be handled properly, leading to assymetric aerodynamic forces



0.6.1v------------------------------------
Features:
Drag model for all non-wing parts updated to expose more parameters for editing
Drag model now models streamlining slightly better, gives more leeway with design
Drag model now accounts for streamlining of the fuel tank from rocket engine clusters, rather than ignoring it as previously
Aerodynamics should be more forgiving with rocket design; that said, some rocket designs will still be aerodynamically unstable
Added a smoothing pass to aerodynamic moments (torques) to help make designs more stable
Changed wing interaction effects a little bit to give more realistic effects
Fully implemented ability to see effects of flaps on aerodynamic performance using Editor GUI
Implemented a simple stall warning on the flight GUI


BugFixes:
Fixed an issue introduced in 0.6 that removed some beneficial effects of wing interaction, such as lower wave drag (drag from high Mach Numbers)
Fixed an issue where the small probe strut parts would throw NullReferenceExceptions
Fixed an issue where docking could cause problems with the flight GUI




0.6v------------------------------------
Features:
Updated to KSP 0.18!
Drag model extended to all new stock parts
Stall model updated; partial stalls now possible
Transonic aerodynamic code updated; should be smoother
Separate flap parts no longer necessary; can change regular control surfaces to flaps in editor
First pass of flaps being controlled through action groups


0.5.4.3v------------------------------------
Features:
Updated the aerodynamic smoothing parameters to make the G-Forces a little less jittery.

BugFixes:
Fixed an issue introduced in 0.5.2 where NullReferenceExceptions would be thrown by a wing or spoiler part if it was the root part of a piece of debris and said debris went on rails.  Thanks to The_Destroyer for blundering into it!
Changed LV-N drag model to be consistent with other rocket engines.


0.5.4.2v------------------------------------
BugFixes:
Fixed an error in the Mach Number calculation introduced in the previous hotfix.  Derp.

0.5.4.1v------------------------------------
BugFixes:
Fixed an issue where encountering temperatures below absolute zero (yes, the game does have this 0_o) would cause the game to crash; Found by The Destroyer at Jool



0.5.4v------------------------------------
Features:
Tweaked minimum drag values upwards
Supersonic drag changed to be less dependent on angle of attack, but with a larger minimum value
Spoiler drag effectiveness now varies with Mach Number
Tweaked brakes on the larger landing gear to be weaker, should prevent them from breaking off as often
TSFC values now accounts for rocket engines
Active rocket engine drag now varies with throttle, below 1/2 throttle incurs drag penalty
Wings now stall if a non-wing part is in front of them blocking airflow
All control surfaces now start set up to act on all axes; this is just so that ships originally made with this work

Bugs Fixed:
Fixed an issue where spoilers could cause the Flight Data GUI to bug out
Fixed an issue where wings and spoilers would react in a way that resulted in ALL GUIs bugging out and throw NullReferenceExceptions
Fixed an issue there the Flight Systems GUI would not reappear after switching vessels



0.5.3.1v------------------------------------
Features:
Transonic lift code tweaked for balance; will likely get more tweaks as time goes on

Bugs Fixed:
Fixed an issue where stall code could lead to game-breaking NaN errors
Fixed an issue where Flight GUI could suffer display errors


0.5.3v------------------------------------
Features:
Added body lift to all non-wing parts
Wings lose lifting effectiveness in transonic region (Mach 0.9 - 1.05) and the drag rise has been increased
Turbojet engine has been tweaked to have twice as much thrust and a lower exhaust velocity
Center of Lift moves backwards when wings are stalled, as in real life--should help make stall a more survivable situation
Center of Lift indicator in editor now handles aerodynamic forces from all parts
Added Small Spoiler part

Bugs Fixed:
Fixed an issue where the non-wing drag model threw NullReferenceExceptions from debris parts
Fixed an issue where control surfaces threw NullReferenceExceptions during startup



0.5.2v------------------------------------
Features:
Modified lift and drag models; they can now be applied to ANY part and work properly.  Thanks to C7 for pointing out how to make it work.
Drag values modified for most parts.
Added spoiler part.  Deploys with the brake button, stalls wing parts behind it on activation.

Bugs Fixed:
Fixed an issue where negative drag could occur as a result of an error in the compressibility calculations.
Fixed an issue where the editor GUI would not accept changes to control surface parameters.
Fixed an issue where the editor GUI stated unphysical stall angles.
Fixed an issue where the editor GUI always thought that the deflection set would cause the control surface to stall.


0.5.1v------------------------------------
Features:
Added larger landing gear (uses default landing gear model)
Added 0.5m (actually 0.625m) diameter engine parts. Engine parts originally by C7 Studios.


0.5v------------------------------------
Features:
First version of in-editor Aerodynamic Analysis panel
	-Can determine stall angle before flight
	-Can find maximum ratio of Lift to Drag
	-Can determine effects of Mach number on all parameters
	-Can determine how stable (or unstable) the plane is

Bugs Fixed:
Fixed an error in the supersonic lift and drag calculations that caused unphysical values
Fixed a bug where you could click through the in-editor (which is such a common bug no-one thinks of it as one =p)



0.4.2v----------------------------------
Features:
Added code to reduce lift from biplane, triplane, etc. wing configurations
Optimized non-linear supersonic flow calculations
Added a small deployable flap, as per Xune's not-quite-a-request-but-close-enough and I want one too

Bugs Fixed:
Fixed an issue where the CoL on the swept wing and tailfin were placed wrong
Fixed an issue where duplicate control surface entries would appear in the editor
Fixed an issue where using the "undo" command in the editor would cause the control surface dialog to get screwy
Fixed an issue with the advanced canard visually deflecting backwards; this also removed necessity for band-aid on small control surface
Fixed an issue where the GUI would try to calculate mach number out of atmosphere
Fixed an issue where the relative air density could go to NaN if above an airless celestial body

0.4.1v----------------------------------
Features:

Increased fuel efficiency of basic jet engine
Increased efficiency of high-aspect ratio wings (L/D is now more realistic)
Added non-linear supersonic flow approximation (as opposed to earlier linear supersonic flow)  This should be more realistic at Mach numbers below ~1.5
Extended drag fix to all parts

Bugs Fixed:
Fixed an issue where NullReferenceExceptions were thrown when wings got too close to the ground or to buildings




0.4v------------------------------------
Features Added:

Drag fix applied to all airplane parts
Mach effects applied to non-lifting drag model
Drag areas increased to more realistic values for non-lifting parts

Lift of rearward wing parts can affect the lift of parts in front of them; e.g. deflected flaps increase lift of pieces ahead of them
Stall propagation should be less system intensive
Supersonic and transonic lift and drag equations tweaked

Basic Jet Engine and TurboJet Engine thrust tweaked downward to balance lower drag
Basic Jet Engine fuel consumption cut in half
TurboJet Engine chokes hard in lower atmosphere

GUI windows now save positions

First version of Flight Data window

Bugs Fixed:
Removed vortex lift for delta and swept wings, since it could cause unphysical drag values
Fixed an error in the exposure calculation that could lead to assymetrical lift on the vehicle
Fixed an error in the sweep calculation that could cause unrealistic lift and drag values
Fixed an issue where a wing could inherit stall from itself, causing infinite stall
Fixed an issue where the might of Krakensbane would mistake planes for mini-krakens and summon a mystical vacuum bubble to seal them off from aerodynamics.  This generally did not end well.



0.3v------------------------------------

Features Added:
FAR no longer throws jealous fits when Lazor System or MechJeb is onboard

All GUIs can be minimized; current implementation is placeholder for more flashy version


Wing "exposure" system updated to be less finagle-y and more aerodynamic based
Wing exposure calculated in editor to update CoL marker
Wing stall angle now based on geometry and "exposure" effects
Wing sweep now affects lift calculations and Mach effects
Wing parts with very high sweep now calculate "vortex lift" typical of delta wings

Overhaul of in-editor control surface GUI
Control surfaces move faster at 200 degrees/s (was ~80 degrees/s)
Control surface max deflection can be set in editor
Symmetry counterparts do not appear on the GUI anymore, since they get the same settings anyway
Added help window to in-editor GUI

Bugs Fixed:
Fixed an issue where NullReferenceExceptions were thrown during wing dismemberment

Known Issues:
Bogus control surface entries can appear in the editor GUI if a control surface is attached to another part that is in the heirarchy of an assembly being attached with symmetry.
Center of Lift indicator can make mistakes when parts are attached; CoL Update button added to control surface GUI as band-aid fix.


0.2v------------------------------------

Features Added:
Lift and drag values lowered to proper values based on units (were previously 10 times proper values)

Wing parts compatible with 0.17 Center of Lift editor function
Wing parts inherit stalls from parts in front of them
Wing parts given lower stall angle of attacks
Wing parts areas, spans, chords and aspect ratios adjusted to properly fit in-game models

Control surfaces can now be set in the editor; ability to do so in flight removed
Control surfaces can be set to act on multiple control axes
Control surfaces now take 1/4 second to fully deflect; this affects control as well as aesthetics

Flaps added (placeholder use of standard control surface model)
Flaps given 4 deflection settings; no deflection up to full deflection (currently 45 degrees)
Flaps take 3 seconds to move between deflection settings

"Flight Buddy" renamed to "FAR Flight Systems"
Wing Leveler moved to "FAR Flight Assistance Systems" window
Yaw Damper, Pitch Damper and Dynamic Control Systems added
Help function added for control systems

Added part.cfgs for original winglet parts

Bugs Fixed:
Fixed an issue with the Small Control Surface part where it would deflect the wrong direction
Fixed an issue that resulted in the wrong placement of the Aerodynamic Center (Center of Lift) that resulted in wrong wing exposure values

Known Issues:
NullReferenceExceptions are thrown during destruction of wing parts.  Doesn't seem to break anything, just makes crashes laggy like before 0.16.
Bogus control surface entries can appear in the editor GUI if a control surface is attached to another part that is in the heirarchy of an assembly being attached with symmetry.
Center of Lift indicator does not account for wing exposure


0.1v------------------------------------

Release
Lift goes with V^2
Drag of wings and otherwise nonfunctional parts (not engines, fueltanks, etc) doesn't go with mass
Drag accounts for parts attached to stack nodes
First pass of wing "exposure" system that finds nearby parts and adjusts lift and drag accordingly
Fixed infiniglide bug
Fixed no lift being generated from wings placed sideways
