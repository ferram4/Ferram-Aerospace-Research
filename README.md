<<<<<<< HEAD
Ferram Aerospace Research v0.14.7
=======
Ferram Aerospace Research v0.15 "Euler"
>>>>>>> 89b2865ff34b6d3d23d7e6860f7820d7aa80af02
=========================
Aerodynamics model for Kerbal Space Program

   Serious thanks:
				* a.g., for tons of bugfixes and code-refactorings   
				* stupid_chris, for the RealChuteLite implementation
            			* Taverius, for correcting a ton of incorrect values  
				* Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			* sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			* ialdabaoth (who is awesome), who originally created Module Manager  
                        	* Regex, for adding RPM support  
				* DaMichel, for some ferramGraph updates and some control surface-related features  
            			* Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863

Source available at: https://github.com/ferram4/Ferram-Aerospace-Research

----------------------------------------------
---------------- INSTALLATION ----------------
----------------------------------------------

Install by merging the GameData folder in the zip with the GameData folder in your KSP install

ModuleManager and ModularFlightIntegrator are REQUIRED for FAR to work properly.  Failing to copy this over will result in strange errors.

-----------------------------------------------------
---------------- LEGACY WING CONFIGS ----------------
-----------------------------------------------------

Sample Part.cfg:

For wings
-----------------------------------
```
MODULE  
{  
	name = FARControllableSurface / FARWingAerodynamicModel  
	b_2 = 0.5				//distance from wing root to tip; semi-span  
	MAC = 0.5				//Mean Aerodynamic Chord  
	nonSideAttach = 0			//0 for canard-like / normal wing pieces, 1 for ctrlsurfaces attached to the back of other wing parts  
	TaperRatio = 0.7			//Ratio of tip chord to root chord generally < 1, must be > 0  
	MidChordSweep = 25			//Sweep angle in degrees; measured down the center of the span / midchord position  
	maxdeflect = 15				//Default maximum deflection value; only used by FARControlableSurface  
	controlSurfacePivot = 1, 0, 0;		//Local vector that obj_ctrlSrf pivots about; defaults to 1, 0, 0 (right)  
	ctrlSurfFrac = 0.2			//Value from 0-1, percentage of the part that is a flap; only used by FARControlableSurface  
}
```

For control surfaces, use above but replace FARWingAerodynamicModel with FARControllableSurface and add maxdeflect value

Set all the other winglet/control surface values to zero


CHANGELOG
=======================================================

0.15V------------------------------------  

Compatibility with KSP 1.0, 1.0.1, and 1.0.2
Upgraded to MM 2.6.3
Introduction of ModularFlightIntegrator for interfacing with KSP drag / heating systems without interference with other mods

Replaced previous part-based drag model with new vessel-centered, voxel-powered model:  
	* Generates voxel model of vehicle using part meshes, accounting for part clipping  
	* Drag is calculated for vehicle as a whole, rather than linear combination of parts  
	* Payload fairings and cargo bays are emergent from code and do not require special treatment with configs  
	* Area ruling of vehicles is accounted for; unsmooth area distributions will result in very high drag at and above Mach 1  
	* Body lift accounts for vehicle shape in determining potential and viscous flow contributions  
	* Areas exposed to outside used for stock heating calculations  

Performance optimizations in legacy wing model  
Jet engine windmilling drag accounted for at intakes  

Editor GUI improvements including:  
	* Greater clarity in AoA / Mach sweep tab  
	* Stability deriv GUI math modified for improved accuracy  
	* Stability deriv simulation tweaked to fix some minor issues in displaying and calculating response  
	* Addition of a Transonic Design tab that displays cross-section distribution and drag at Mach 1 for area ruling purposes  

Parachute methods have been replaced with RealChuteLite implementation by stupid_chris:  
	* Less severe parachute deployment  
	* Parachutes melt / break in high Mach number flows  
	* No interference with RealChute  

Changes to FARAPI to get information faster  
	
FARBasicDragModel, FARPayloadFairingModule, FARCargoBayModule are now obsolete and removed from the codebase  
Extensive reorganizing of source to reduce spaghetti and improve maintainability  

Modifications to Firehound and Colibri to function with new flight model  
Addition of Blitzableiter and SkyEye example crafts  

A 1.5x increase to all stock gimbal ranges  

0.14.7V------------------------------------  
Features:  
Raised stalled-wing drag up to proper maximum levels  
Adjusted intake drag to be lower  
Improved method of dealing with very high vertex count parts for geometry purposes  
Upgraded to MM 2.5.13  
Included FAR Colibri, a VTOL by Tetryds as an example craft  

Bugfixes:  
Fixed an issue preventing loading custom-defined FARBasicDragModels  

0.14.7V------------------------------------  
Features:  
Raised stalled-wing drag up to proper maximum levels  
Adjusted intake drag to be lower  
Improved method of dealing with very high vertex count parts for geometry purposes  
Upgraded to MM 2.5.13  
Included FAR Colibri, a VTOL by Tetryds as an example craft  

Bugfixes:  
Fixed an issue preventing loading custom-defined FARBasicDragModels


0.14.6V------------------------------------  
Features:  
Modified skin friction variation with M and Re to closer to that expected by using the Knudsen number  
Changed saving and loading method to allow better behavior when settings need to be cleaned during updates, especially for automated installs  
Modified aerodynamic failures for water landings for compatibility with upcoming BetterBuoyancy  
Option for aerodynamic failures to result in explosions at the joint during failure.  
Serious reworking to handle edge cases with lightly-clipped parts and their effects on blunt body drag (read: when people clip heatshields into the bottom of Mk1 pods and cause problems)  
Upgrade to MM 2.5.6

Bugfixes:  
Fixed an issue that prevented Trajectories from functioning  
Fixed blunt body drag errors with AJE  
Fixed issues involving editor GUI and control surface deflections  
Fixed edge cases involving attach-node blunt body drag being applied when it shouldn't have  
Fixed issues with command pods containing intakes

0.14.5.1V------------------------------------  
Features:  
Add Reynolds Number readout to main flight GUI

Tweaks:  
Adjust skin friction drag for rarefied atmosphere

Bugfixes:  
Fix Stab Deriv GUI from breaking for altitudes above atmosphere  
Fix flaps and spoilers not functioning with negative deflections


0.14.5V------------------------------------  
Features:  
Skin friction drag now varies with Reynolds number; this means much higher skin friction drags at higher altitudes  
Added simple attempt at handling hydrodynamic effects; not detailed, but objects in oceans move much less  
Added color changing options for colorblind users  
Tweak flap and spoiler deflection functions  
Give spoilers faster deflection coefficients  
Update to ModuleManager 2.5.4

Bugfixes:  
Removed spontaneous aero-spline warp drive in some Linux64 versions

0.14.4.1v------------------------------------  
Features:  
Added changes to blunt body drag to make command pods more stable on reentry  
Attempt to account for most inaccurate effects of part clipping  

0.14.4v------------------------------------
Features:  
Default ActionGroups now controlled throuhg dropdown menus rather than string entry  
Stability Deriv tab now takes entry in terms of planet, altitude and Mach Number, not density, temperature and Mach number  
Stability Deriv tab now accounts for reduced gravity due to high speeds

Contributed by HoneyFox:  
	Pitch damper now has an additional gain for greater tuning  
	Control surfaces can now be set to deflect in response to local AoA changes  
	Control surfaces are not On/Off for a given control direction; can be scaled from -100% to 100% for each  

Contributed by Bitronic:  
	Full Tweakscale Support

BugFixes:  
Fixed no shielding with some payload fairings (particularly resized procedural fairings)  
Fixed aero tinting blocking tinting from other mods
	


0.14.3.2v------------------------------------  
Features:  
Contributed by Da Michel:  
	Airspeed settings change readouts in cockpits  

Bugfixes:  
Fixed serious issues with the wing interaction code  
Fixed an issue where wind velocity was applied in the opposite direction that was expected  


0.14.3.1v------------------------------------  
Features:  
Improved performance in editor and flight for vessel configuration changes  
Fliht GUI appears in mapview  

Bugfixes:  
Fixed neverending stall resulting from wing interactions with sudden changes in velocity vector direction  
Fixed flight GUI issues when passing another vehicle  


0.14.3v------------------------------------
Features:
Refactored wing interaction code:  
	Wing interactions should be smoother  
	Code should be less processor intensive  

Upgrade to ModuleManager v2.5.1  
Added stall visualization to aero force visualization  
Added ability to scale wing mass up or down for additional strength / weight savings (addedby NathanKell)   
Improved cargo bay and payload fairing detection algorithm  

Tweaks:  
Reduced intake drag  
Decreased wing mass per area slightly  

Bugfixes:
Fixed aero visualization leaving parachutes glowing brightly  
Fixed some critical errors for when config files do not have values listed  
Fixed an issue with AppLauncher buttons multiplying when KSP fails at loading a particular vessel

0.14.2v------------------------------------
Features:
0.25 compatibility, with stock support for SP+ parts  
Upgrade CompatibilityChecker  
Disable functions on CompatibilityChecker warnings

Prototype aero force visualization in flight  
Removed vector from CoL indicator to reduce confusion  
More Get functions for the FARAPI  
Estimated range and endurance readouts in the Flight Data UI  
See and dump FAR module data in the VAB / SPH using the Editor GUI  
Some runtime optimizations  

Contributed by Da Michel:  
	Implement separate deflection speeds for flaps / spoilers  
	Allow preferred default action groups for spoilers / flaps  
Contributed by regex:  
	Add some RPM integration  
Contributed by Ippo:  
	FARWind class for 3rd-party wind implementation


Bugfixes:
Fixed some vessel-switching FAR GUI issues  
Fixed control surface reversal on undocking or backwards root part selection  
Fixed some issues involving CoL position with wings when dealing with parts that have multiple colliders  
Fixed some payload fairing and cargo bay part detection issues 
