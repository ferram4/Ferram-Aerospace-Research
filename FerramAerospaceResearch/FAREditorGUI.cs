/*
Ferram Aerospace Research v0.14.6
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


//TODO:
//Fix requirement to double-run stability deriv sim

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;

namespace ferram4
{
    public class FAREditorGUIHelp
    {
        /*private void AnalysisHelpGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Box("The analysis window is designed to help you determine the performance of your airplane before you attempt to fly it by calculating various aerodynamic parameters.", BackgroundStyle);

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.Label("<b>Analyzer modes:</b>\n\rSweep AoA (Angle of attack)\n\rSweep Mach\n\r\n\r<b>Sweep AoA:</b> Vary AoA of plane at a constant Mach number.  Set angles using lower and upper bounds and choose enough points for accuracy.  Analyzer will produce two curves; one with increasing AoA, one with decreasing to display effects of stall. The sweep from high to low AoA is displayed in darker tones. \n\r\n\r<b>Sweep Mach:</b> Vary Mach number at a constant AoA.  Will only sweep from lower Mach Number to upper.  Will not accept negative Mach Numbers.\n\r\n\r<b>Parameters Drawn:</b> Cl, Cd, Cm, L/D\n\r\n\r<b>Cl:</b> Lift coefficient; describes the lift of the plane after removing effects of air density and velocity.  Will increase with AoA until stall, where it will drop greatly.  AoA must be lowered greatly before stall ends.\n\r\n\r<b>Cd:</b> Drag coefficient; like the above, but for drag.  Notice the large increase following stall.\n\r\n\r<b>Cm:</b> Pitching moment coefficient; Angular force (think torque) applied to the plane when effects of air density and velocity are removed; must decrease with angle of attack for the plane to be stable.\n\r\n\r<b>L/D:</b> Lift over drag; measure of how efficiently the plane flies.");
            GUILayout.EndVertical();

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void StabDerivHelpGUI(int windowID)
        {
            GUILayout.Box("The data and stability derivative GUI is designed to help you analyze the dynamic properties of your aircraft at a glance.  The simulation GUI is designed to display the dynamic motion of the vehicle as predicted from the stability derivatives.", BackgroundStyle);

            analysisHelpTab = (AnalysisHelpTab)GUILayout.SelectionGrid((int)analysisHelpTab, AnalysisHelpTab_str, 3, ButtonStyle);
            GUILayout.BeginVertical(BackgroundStyle);


            if (analysisHelpTab == AnalysisHelpTab.AIRCRAFT_PROPERTIES)
            {
                GUILayout.Label("<b>Surface Area, Scaled Chord and Scaled Span</b>");
                //GUILayout.Space(5);
                GUILayout.Label("These are used in scaling the aerodynamic properties of the aircraft; larger wing surface areas increase lift, larger scaled chords increase pitching moments and larger scaled spans increase lateral forces and moments.");
                //GUILayout.Space(10);

                GUILayout.Label("<b>Moments of Inertia</b>");
                //GUILayout.Space(5);
                GUILayout.Label("These describe the vehicle's resistance to rotation about a given axis caused by its mass distribution about that axis.  Larger Ixx increases rolling inertia, larger Iyy increases pitching inertia and larger Izz increases yawing inertia.");
                //GUILayout.Space(10);

                GUILayout.Label("<b>Products of Inertia</b>");
                //GUILayout.Space(5);
                GUILayout.Label("These describe the vehicle's resistance to rotation about a given axis caused by its mass distribution about a different axis; it is caused by a vehicle's assymmetry.  For a standard airplane, Ixy and Iyz should be near 0, or the stability analysis will be invalid.");
            }
            if (analysisHelpTab == AnalysisHelpTab.LONGITUDINAL)
            {
                GUILayout.Label("<b>Basics</b>");
                GUILayout.Space(5);
                GUILayout.Label("Longitudinal motion refers to the interaction between the motion and forces acting in the forwards / backwards and downwards / upwards directions (relative to the vehicle) and pitching rates and moments; this is the motion is primarily due to keeping the aircraft moving and aloft.  It consists of two overlapping responses: the short-period response and the phugoid (long-period) response.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Short-Period Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("The short-period response is essentially the aircraft pitching up and down without any significant change in forward velocity; It is so named because it consists of relatively rapid oscillations (high frequency = short period) and it typically damps out within a few oscillations, though with some designs the oscillations will damp out within a single period.  This motion generally becomes less stable as flight velocity increases.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Phugoid Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("The phugoid motion consists of an exchange between altitude (displayed as pitch angle θ) and forward velocity; it is named after a a mis-translation of the phrase \"to fly\".  The lower the lift-to-drag ratio (L/D) of the aircraft, the more damped (and thus, stable) this mode becomes.  Therefore, it typically becomes more stable with increasing velocity.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Stability Derivatives</b>");
                GUILayout.Space(5);
                GUILayout.Label("These describe the changes in forces / moments with respect to (wrt) changes in some value.\n\r\n\r<b>Legend:</b>\n\rX_: forward forces; positive forwards\n\rZ_: vertical forces; positive downwards\n\rM_: pitching moment; positive upwards\n\r\n\r[]u: wrt changes in forward velocity\n\r[]w: wrt changes in downward velocity\n\r[]q: wrt changes in pitch-rate\n\r[]δe: wrt changes in pitch-control");
            }
            if (analysisHelpTab == AnalysisHelpTab.LATERAL)
            {
                GUILayout.Label("<b>Basics</b>");
                GUILayout.Space(5);
                GUILayout.Label("Lateral motion refers to the interaction between the rates and moments in rolling and yawing and the motion and forces in the sideways direction; this motion is primarily due to keeping the aircraft facing in a given direction.  It consists of three different motions: the roll subsidence response, the spiral response, and the dutch roll response.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Roll Subsidence Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("This is a simple damped, non-oscillating motion due to the roll rate.  If the vehicle is rolling, the roll rate will drop to zero.  This motion also controls the upper limit on rolling rate.");
                GUILayout.Space(10);
                GUILayout.Label("<b>Spiral Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("The spiral motion is either a slight convergent or slightly divergent non-oscillating motion caused by an interaction between the vehicle's yaw and roll stability that, when unstable, often causes the plane to fly in an ever-tightening spiral with an increase in sideslip angle and roll angle until the plane crashes; note that this is not a spin, which requires unequal stalling on the wings.  This motion is usually subconsciously corrected by pilots before it becomes noticable in good visibilty conditions.  Any attempt to increase the stability of the spiral motion will inevitably decrease the stability of the dutch roll motion (see below).");
                GUILayout.Space(10);
                GUILayout.Label("<b>Dutch Roll Motion</b>");
                GUILayout.Space(5);
                GUILayout.Label("The dutch roll motion consists of an exchange between sideslip angle and roll angle that is best described by the plane \"wagging\" in flight; it is named after a motion that appears in ice skating.  While this motion is normally stable, it is often very lightly damped, which can make flight difficult.  It generally becomes less stable as velocity increases and attempts to make the dutch roll motion damp faster will inevitably cause lowered stability in the spiral motion (see above).");

                GUILayout.Space(10);
                GUILayout.Label("<b>Stability Derivatives</b>");
                GUILayout.Space(5);
                GUILayout.Label("These describe the changes in forces / moments with respect to (wrt) changes in some value.\n\r\n\r<b>Legend:</b>\n\rY_: sideways forces; positive right\n\rN_: yawing moment; positive right\n\rL_: rolling moment; positive right\n\r\n\r[]ß: wrt changes in sideslip angle\n\r[]p: wrt changes in roll-rate\n\r[]r: wrt changes in yaw-rate");
            }


            GUILayout.EndVertical();

            GUI.DragWindow();
        }*/
    }
}

