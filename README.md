Neophyte's Elementary Aerodynamics Replacement v1.3
=========================

Simpler aerodynamics model for Kerbal Space Program, based on a stripped-down version of Ferram Aerospace Research

   Serious thanks:		a.g., for tons of bugfixes and code-refactorings  
            			Taverius, for correcting a ton of incorrect values  
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

Source available at: https://github.com/ferram4/Ferram-Aerospace-Research/tree/NEAR

----------------------------------------------
---------------- INSTALLATION ----------------
----------------------------------------------

Install by merging the GameData folder in the zip with the GameData folder in your KSP install
Multiple copies of ModuleManager will cause issues; make sure you are using MM 2.0.5 or higher

ModuleManager is REQUIRED for NEAR to work properly.  Failing to copy this over will result in strange errors.

----------------------------------------------------------
---------------- LEARNING TO FLY WITH NEAR ----------------
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

Note: All of these say "FAR" at the beginning because NEAR is based on FAR; none of the module names have been changed to reduce the probability of errors

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

1.3v------------------------------------
Features:
Upgrade to ModuleManager v2.5.0
Improved cargo bay and payload fairing detection algorithms
Tweaked intake drag

1.2.1v------------------------------------
Bugfixes:
Fixed a serious issue that would prevent NEAR from loading in flight

1.2v------------------------------------
Features:
0.25 compatibility, with stock support for SP+ parts  
Upgrade CompatibilityChecker  
Disable functions on CompatibilityChecker warnings
Removed vector from CoL indicator to reduce confusion  

Bugfixes:
Fixed control surface reversal on undocking or backwards root part selection  
Fixed some issues involving CoL position with wings when dealing with parts that have multiple colliders  
Fixed some payload fairing and cargo bay part detection issues  

1.1.1v------------------------------------
Tweaks:
Un-nerfed airbreather thrust slightly, also changed velocity curves to be better


1.1v------------------------------------
Tweaks:
Reduced thrust of turbojet and RAPIER to be more appropriate for NEAR
Added tweaks to reduce thrust of all airbreathing engines

Bugfixes:
Fixed issue with payload fairing and cargo bay modules not being added to vehicles in flight
Fixed issue with pre-defined NEAR modules having a reference area of 0

v1.0.3------------------------------------  
Bugfixes:
Fixed a part shielding in editor issue
Fixed more nullref exceptions during craft file loading

v1.0.2------------------------------------  
Bugfixes:
Fixed an issue where NullReferenceExceptions would be spammed in the editor

v1.0.1------------------------------------  
Bugfixes:
Included JsonFx.dll, which is required by ModStats
Relabeled ModStatistics.dll to allow simple overwriting for ModStats updates

v1.0------------------------------------  
Features:
Release, with simpler version of aerodynamics, with no dependence on Mach number, complicated wing shapes and no convoluted aerodynamic readouts
