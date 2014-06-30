using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ferram4
{
 /*   public class BuildingAPlane : TutorialScenario
    {
        TutorialPage welcome, fuselage, CoMCoL, aerodynamics, wings, tail, verttail, controlsurf1, controlsurf2, engines, landinggear, complete;
        protected override void OnAssetSetup()
        {
//            instructorPrefabName = "Instructor_Gene";
        }


        int numtmp;
        protected override void OnTutorialSetup()
        {
            #region welcome

            welcome = new TutorialPage("welcome");
            welcome.windowTitle = "Airplane Design 101";
            welcome.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            welcome.OnDrawContent = () =>
            {
                GUILayout.Label("Welcome to the airplane design tutorial, sponsored by Ferram Aerospace Research.\n\nFerram Aerospace Research: Crashing your planes so you don't have to!\n\nNow, select a cockpit and we'll proceed.");

                //if (GUILayout.Button("Next")) Tutorial.GoToNextPage();
                if(EditorLogic.startPod)
                    Tutorial.GoToNextPage();
            };
            Tutorial.AddPage(welcome);
            #endregion
            #region CoMCoL

            CoMCoL = new TutorialPage("CoMCoL");
            CoMCoL.windowTitle = "Center of Mass and Center of Lift";
            CoMCoL.OnEnter = (KFSMState st) =>
            {
                instructor.PlayEmoteRepeating(instructor.anim_idle_lookAround, 2f);
            };
            CoMCoL.OnDrawContent = () =>
            {
                GUILayout.Label("In designing an airplane there are two absolutely key locations: the Center of Mass and the Center of Lift.\n\nThe center of mass is somewhat self-explanatory; all of the mass of the plane is balanced around that point.  The center of lift is the point where the sum of all the lifting forces on the plane act; where this is in relation to the center of mass determines almost everything about how the plane flies.\n\nTo turn them on, click the wing and weight icons in the bottom left corner of the screen.  We'll continue when they're on.");

                if (GUILayout.Button("Next")) Tutorial.GoToNextPage();
            };


            Tutorial.AddPage(CoMCoL);
            #endregion
            #region fuselage

            fuselage = new TutorialPage("fuselage");
            fuselage.windowTitle = "Building The Fuselage";
            fuselage.OnEnter = (KFSMState st) =>
            {
                numtmp = 4;
                instructor.PlayEmote(instructor.anim_true_smileA);
            };
            fuselage.OnDrawContent = () =>
            {
                numtmp = 4;
                foreach (Part p in EditorLogic.SortedShipList)
                    if (p is FuelTank)
                        numtmp--;
                GUILayout.Label("First things first, you need fuselage parts filled with fuel, or you can't go anywhere.  Slap on some fuselage parts, and try to make it look pretty.  Observe how the center of mass moves as you add parts.\n\n" + numtmp.ToString() + " more fuel tanks needed.");
            };
            fuselage.SetAdvanceCondition((KFSMState st) =>
            {
                return numtmp <= 0;
            });
            Tutorial.AddPage(fuselage);

            #endregion

            #region aerodynamics

            aerodynamics = new TutorialPage("aerodynamics");
            aerodynamics.windowTitle = "Some Aerodynamic Considerations...";
            aerodynamics.OnEnter = (KFSMState st) =>
            {
                instructor.PlayEmoteRepeating(instructor.anim_idle_lookAround, 5f);
            };
            aerodynamics.OnDrawContent = () =>
            {
                GUILayout.Label("Now that you have the fuselage, you might notice that it isn't necessarily all that aerodynamic looking, since the fuselage ends abruptly at the back.  Depending on which cockpit you chose, you need to do some aerodynamic work in front of it as well.  Go and put on nose cones or tail pylons until all of those are covered up, and then we'll proceed to the wings.");
            };
            aerodynamics.SetAdvanceCondition((KFSMState st) =>
            {
                bool cont = true;

                foreach (Part p in EditorLogic.SortedShipList)
                    foreach (AttachNode a in p.attachNodes)
                        if (a.nodeType == AttachNode.NodeType.Stack)
                            if (a.attachedPart == null)
                                cont = false;
                return cont;
            });
            Tutorial.AddPage(aerodynamics);

            #endregion
            #region wings

            wings = new TutorialPage("wings");
            wings.windowTitle = "Adding Wings";
            wings.OnEnter = (KFSMState st) =>
            {
                numtmp = 1;
                instructor.PlayEmote(instructor.anim_true_smileB);
            };
            wings.OnDrawContent = () =>
            {
                numtmp = 4;
                foreach (Part p in EditorLogic.SortedShipList)
                    foreach (PartModule m in p.Modules)
                        if (m is WingAerodynamicModel)
                        {
                            numtmp--;
                            break;
                        }
                GUILayout.Label("Now we need wings, or it isn't a plane.  Turn on symmetry and add a few wing parts to the plane near the center of mass so we can lift this thing into the air.");
            };
            wings.SetAdvanceCondition((KFSMState st) =>
            {
                return numtmp <= 0;
            });

            Tutorial.AddPage(wings);

            #endregion

            #region tail

            tail = new TutorialPage("tail");
            tail.windowTitle = "Adding a Tail";
            tail.OnEnter = (KFSMState st) =>
            {
                instructor.PlayEmoteRepeating(instructor.anim_idle, 2f);
            };
            tail.OnDrawContent = () =>
            {

                GUILayout.Label("If you look, the plane's center of lift is likely in front of or right on top of the center of mass.  In order for the plane to be stable, the center of lift needs to be behind the center of mass.\n\nThink about it: when a plane pitches up, it makes more lift.  If there's more lift in front of the center of mass, the plane wants to pitch up MORE, which creates more lift, which pitches it up more, until it's upside-down and flying backwards.\n\nTo fix this, most planes have a horizontal tail near the back of the plane.  Add some wing parts near the back to make it stable.");
                if (GUILayout.Button("Next")) Tutorial.GoToNextPage();
            };

            Tutorial.AddPage(tail);

            #endregion

            #region verttail

            verttail = new TutorialPage("verttail");
            verttail.windowTitle = "Adding a Vertical Stabilizer";
            verttail.OnEnter = (KFSMState st) =>
            {
                instructor.PlayEmote(instructor.anim_true_thumbUp);
            };
            verttail.OnDrawContent = () =>
            {

                GUILayout.Label("Good, the plane shouldn't pitch out of control now.  But it needs a vertical stabilizer or else it can start sliding sideways through the air, so adding one might make some sense.  To be honest, you CAN get away without it if you want, but it isn't necessarily a smart idea if you're inexperienced at piloting.");
                if (GUILayout.Button("Next")) Tutorial.GoToNextPage();
            };

            Tutorial.AddPage(verttail);

            #endregion

            #region controlsurf1

            controlsurf1 = new TutorialPage("controlsurf1");
            controlsurf1.windowTitle = "Adding Control Surfaces";
            controlsurf1.OnEnter = (KFSMState st) =>
            {
                numtmp = 1;
                instructor.PlayEmote(instructor.anim_true_nodA);
            };
            controlsurf1.OnDrawContent = () =>
            {
                numtmp = 1;
                foreach (Part p in EditorLogic.SortedShipList)
                    foreach (PartModule m in p.Modules)
                        if (m is WingAerodynamicModel)
                        {
                            if((m as WingAerodynamicModel).maxdeflect > 0)
                                numtmp--;
                            break;
                        }
                GUILayout.Label("Okay, now we need some control surfaces so we can pilot this thing.  Go select some and put them on.");
            };
            controlsurf1.SetAdvanceCondition((KFSMState st) =>
            {
                return numtmp <= 0;
            });
            Tutorial.AddPage(controlsurf1);

            #endregion
            #region controlsurf2

            controlsurf2 = new TutorialPage("controlsurf2");
            controlsurf2.windowTitle = "FAR Control Surface Interface";
            controlsurf2.OnEnter = (KFSMState st) =>
            {
                instructor.PlayEmote(instructor.anim_true_nodB);
            };
            controlsurf2.OnDrawContent = () =>
            {

                GUILayout.Label("The control dialog that just opened allows you to select what the control surface will do.  You can set its maximum deflection to get more control out of a particular surface or reduce the amount one deflects to avoid stalling it.  You can also choose which commands it responds to--pitch, yaw, roll or some combination.\n\nReally, it needs at least elevators on the tail and ailerons on the wings for pitch and roll control, respectively.  You might be able to get away without a rudder for yaw depending on the design, but I'd add one for control on the runway during takeoff.");
                if (GUILayout.Button("Next")) Tutorial.GoToNextPage();
            };

            Tutorial.AddPage(controlsurf2);
            #endregion
            #region engines

            engines = new TutorialPage("engines");
            engines.windowTitle = "Engines";
            engines.OnEnter = (KFSMState st) =>
            {
                numtmp = 1;
                instructor.PlayEmote(instructor.anim_true_nodB);
            };
            engines.OnDrawContent = () =>
            {
                numtmp = 1;
                foreach (Part p in EditorLogic.SortedShipList)
                    if (p is AtmosphericEngine)
                        numtmp--;

                GUILayout.Label("Now we need engines for it to take off.  Grab the engine nacelle part and put it somewhere on the fuselage.  Put an intake on the front and a jet engine on the back.  Keep in mind that fuel will drain from everywhere on the plane to the tanks near the engines, so engine placement is key to make sure your plane doesn't become unstable in flight.");
            };
            engines.SetAdvanceCondition((KFSMState st) =>
            {
                return numtmp <= 0;
            });

            Tutorial.AddPage(engines);
            #endregion
            #region landinggear

            landinggear = new TutorialPage("landinggear");
            landinggear.windowTitle = "Landing Gear";
            landinggear.OnEnter = (KFSMState st) =>
            {
                numtmp = 1;
                instructor.PlayEmote(instructor.anim_true_nodB);
            };
            landinggear.OnDrawContent = () =>
            {
                numtmp = 3;
                foreach (Part p in EditorLogic.SortedShipList)
                    if(p.Modules.Contains("ModuleLandingGear"))
                        numtmp--;

                GUILayout.Label("All that's left now is landing gear so it can take off.  As a general rule, the main landing gear (the ones furthest back) are placed at or just behind the center of mass to help with pitching the plane up.");
            };
            landinggear.SetAdvanceCondition((KFSMState st) =>
            {
                return numtmp <= 0;
            });
            Tutorial.AddPage(landinggear);
            #endregion

            #region complete

            complete = new TutorialPage("complete");
            complete.windowTitle = "FAR Airplane Design 101 - Complete!";
            complete.OnEnter = (KFSMState st) =>
            {
                instructor.PlayEmote(instructor.anim_true_thumbsUp);
            };
            complete.OnDrawContent = () =>
            {


                GUILayout.Label("You've just walked through building a plane that'll fly.  Go and try taking her out and seeing how you did.");
                if (GUILayout.Button("Finish"))
                {
                    Destroy(this);
                }

            
            };
            Tutorial.AddPage(complete);

            #endregion
            Tutorial.StartTutorial(welcome);
        }
    }

    public class FAR_Airplane_Flight_101 : TutorialScenario
    {
        TutorialPage welcome, FAR_Flight_GUI, pre_takeoff, takeoff, flaps, climb, turning, cruise1, cruise2, descent, glideslope, landing, conclusion;
        Vessel plane;
        Machmeter flightinfo;
        protected override void OnAssetSetup()
        {
            instructorPrefabName = "Instructor_Gene";

            plane = FlightGlobals.ActiveVessel;
            foreach (PartModule m in plane.rootPart.Modules)
                if (m is Machmeter)
                    flightinfo = (m as Machmeter);
        }



        protected override void OnTutorialSetup()
        {
            #region welcome

            welcome = new TutorialPage("welcome");
            welcome.windowTitle = "Airplane Flight 101";
            welcome.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            welcome.OnDrawContent = () =>
            {
                GUILayout.Label("Welcome to airplane flight 101, where you'll learn how to pilot (and hopefully not crash) a plane.  We'll cover the very basics here, such as takeoff, landing, and minor manuevers.");
                if (GUILayout.Button("Next")) Tutorial.GoToNextPage();

            };
            Tutorial.AddPage(welcome);
            #endregion
            #region FAR_Flight_GUI

            FAR_Flight_GUI = new TutorialPage("FAR_Flight_GUI");
            FAR_Flight_GUI.windowTitle = "FAR GUI";
            FAR_Flight_GUI.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            FAR_Flight_GUI.OnDrawContent = () =>
            {
                GUILayout.Label("Somewhere on your screen should be the Ferram Aerospace Research Graphical User Interface, which gives you access to a great deal of information and control over the plane.  This will give you Mach Number (speed of the plane relative to the speed of sound) and air density values, as well as access to flight information and some flight assistance systems.");
                if (GUILayout.Button("Next")) Tutorial.GoToNextPage();

            };
            Tutorial.AddPage(FAR_Flight_GUI);
            #endregion

            #region pre_takeoff

            pre_takeoff = new TutorialPage("pre_takeoff");
            pre_takeoff.windowTitle = "Pre-Takeoff";
            pre_takeoff.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            pre_takeoff.OnDrawContent = () =>
            {
                GUILayout.Label("Takeoff consists of two stages: Ground roll and initial climb.\n\nDuring ground roll the plane is accelerating to a safe take-off speed.  Depending on the size of the plane and its engines, this can last almost all of the runway.  At the end of ground roll you will rotate the plane, pitching it up to generate lift.\n\nInitial climb is the portion of takeoff when the plane has left the ground and is trying to gain altitude and airspeed simultaneously.  Generally, this is where most crashes happen, because pilots are too eager to gain altitude and the plane loses speed and crashes.\n\nStart your engines and begin ground roll whenever you're ready.");

            };
            pre_takeoff.SetAdvanceCondition((KFSMState st) =>
                {
                    return plane.srf_velocity.magnitude > 0.2;
                });
            Tutorial.AddPage(pre_takeoff);
            #endregion
            #region takeoff

            takeoff = new TutorialPage("takeoff");
            takeoff.windowTitle = "Takeoff";
            takeoff.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            takeoff.OnDrawContent = () =>
            {
                GUILayout.Label("Let your plane get up to speed, say ~70 m/s and then pull back on the stick.  This should allow the plane to take off easily.  Try not to crash it outright, and climb to ~150m.");


            };
            takeoff.SetAdvanceCondition((KFSMState st) =>
            {
                return plane.altitude > 98;
            });
            Tutorial.AddPage(takeoff);
            #endregion

            #region flaps

            flaps = new TutorialPage("flaps");
            flaps.windowTitle = "Flaps";
            flaps.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            flaps.OnDrawContent = () =>
            {
                GUILayout.Label("During takeoff your plane needed high-lift devices called flaps to actually lift off at a reasonable speed.  However, flaps add a large amount of drag and can make the plane harder to control.  Use '0' to reduce the flap deflection level to 0.");


            };
            flaps.SetAdvanceCondition((KFSMState st) =>
            {
                return flightinfo.FlapDeflect == "0";
            });
            Tutorial.AddPage(flaps);
            #endregion
            #region climb

            climb = new TutorialPage("climb");
            climb.windowTitle = "Trim and Climb";
            climb.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            climb.OnDrawContent = () =>
            {
                GUILayout.Label("Now we must trim the aircraft.  This is when you set a neutral point for the controls so that it doesn't change its attitude.  You can use ALT + " + GameSettings.PITCH_UP.name + " and " + GameSettings.PITCH_DOWN.name + " to adjust your pitch trim, which should be th only one we need to care about.  After you do that, climb to 3000 meters so we have some room to recover if something goes wrong and so we can fly a little faster.");

            };
            climb.SetAdvanceCondition((KFSMState st) =>
            {
                return plane.altitude >= 3000;
            });
            Tutorial.AddPage(climb);
            #endregion
            #region turning

            turning = new TutorialPage("turning");
            turning.windowTitle = "Turning";
            turning.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            turning.OnDrawContent = () =>
            {
                GUILayout.Label("Ok, so let's talk about turning.  Airplanes don't turn like cars or other ground vehicles do, where they stay level while they change direction.  The rudder isn't strong enough to turn a plane.  Planes need to roll and use their wings to lift them into the turn.  Roll the plane about 20 degrees and pull back and see what happens.");
                if (GUILayout.Button("Next")) Tutorial.GoToNextPage();

            };

            Tutorial.AddPage(turning);
            #endregion

            #region cruise1

            cruise1 = new TutorialPage("cruise1");
            cruise1.windowTitle = "Cruise and Loiter";
            cruise1.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            cruise1.OnDrawContent = () =>
            {
                GUILayout.Label("Now on to cruising and loitering, the most time-consuming part of flying.  Cruise is when the plane is flying at a mostly-constant altitude and is trying to cove the most ground possible in a give amount of time.  Loiter is when the plane is simply trying to stay in the air for as long as possible.  Open up the Flight Data panel and we'll talk about some of this.");

            };
            cruise1.SetAdvanceCondition((KFSMState st) =>
            {
                return flightinfo.FlightDataWindow;
            });


            Tutorial.AddPage(cruise1);
            #endregion

            #region cruise2

            cruise2 = new TutorialPage("cruise2");
            cruise2.windowTitle = "Flight Data Window";
            cruise2.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            cruise2.OnDrawContent = () =>
            {
                GUILayout.Label("All the numbers in here an be used to determine how effectively the plane is flying.  The help window that you can open here will give you more details on all of these.  I should note that the comparisons are really only valid if the plane is not accelerating, so while trying to reach cruise speed you should not care too much about these.  Keep in mind that flight efficiency greatly decreases above and near Mach 1.  Close the window to continue.");

            };
            cruise2.SetAdvanceCondition((KFSMState st) =>
            {
                return !flightinfo.FlightDataWindow;
            });


            Tutorial.AddPage(cruise2);
            #endregion

            #region descent

            descent = new TutorialPage("descent");
            descent.windowTitle = "Descent";
            descent.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            descent.OnDrawContent = () =>
            {
                GUILayout.Label("Turn the plane back towards the KSC and we'll prepare for landing.  To descend, you can simply pitch down and full-throttle through the lower atmosphere, but aerodynamic effects can rip the wings off.  A smarter way is to reduce throttle and let the plane slow down and lose altitude that way.  For now, drop to 1000m and we'll set up for landing.");

            };
            descent.SetAdvanceCondition((KFSMState st) =>
            {
                return plane.altitude <= 1000;
            });


            Tutorial.AddPage(descent);
            #endregion
            #region glideslope

            glideslope = new TutorialPage("glideslope");
            glideslope.windowTitle = "Glide Slope";
            glideslope.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            glideslope.OnDrawContent = () =>
            {
                GUILayout.Label("Now line the plane up with the runway.  We should still be a few kilometers out from the KSC, so let the plane lose altitude slowly, and don't try to force it.  We used flaps to help us take off, so now we will use them to help us land.  Set the flaps to deflection level 3 and we will continue.");

            };
            glideslope.SetAdvanceCondition((KFSMState st) =>
            {
                return flightinfo.FlapDeflect == "3";
            });


            Tutorial.AddPage(glideslope);
            #endregion
            #region landing

            landing = new TutorialPage("landing");
            landing.windowTitle = "Landing";
            landing.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            landing.OnDrawContent = () =>
            {
                GUILayout.Label("Don't let your vertical speed increase too much as you bring the plane down.  Make sure to keep your speed up a little so that you don't stall and bring her in slowly.  Make sure to pull up a little right before landing so that the plane hits the ground nicely and doesn't break apart.");

            };
            landing.SetAdvanceCondition((KFSMState st) =>
            {
                return plane.Landed;
            });


            Tutorial.AddPage(landing);
            #endregion
            #region conclusion

            conclusion = new TutorialPage("conclusion");
            conclusion.windowTitle = "Airplane Flight 101 - Complete";
            conclusion.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            conclusion.OnDrawContent = () =>
            {
                GUILayout.Label("Very nice.  Now you can either take off again and continue flying, or try out something of your own.  Have fun!");
                if (GUILayout.Button("Finish"))
                {
                    Destroy(this);
                }

            };


            Tutorial.AddPage(conclusion);
            #endregion

            Tutorial.StartTutorial(welcome);
        }
    }*/

    public class FAREditorTutorial : TutorialScenario
    {
        TutorialPage welcome, selectship, ctrlsurfGUI1, ctrlsurfGUI2, ctrlsurfGUI3;
        protected override void OnTutorialSetup()
        {
            #region welcome

            welcome = new TutorialPage("welcome");
            welcome.windowTitle = "FAR Editor Tutorial";
            welcome.OnEnter = (KFSMState st) =>
            {
                instructor.StopRepeatingEmote();
            };
            welcome.OnDrawContent = () =>
            {
                GUILayout.Label("Welcome to the lecture on the use of the new Ferram Aerospace Research Control and Analysis Systems Tools (FAR-CAST).  I am famous rocket scientist Werhner von Kerman, and I will be instructing you in the use of these tools to perfect your airplane and spaceplane design.");

                if (GUILayout.Button("Next"))
                    Tutorial.GoToNextPage();
            };
            Tutorial.AddPage(welcome);
            #endregion

            #region selectship

            selectship = new TutorialPage("selectship");
            selectship.windowTitle = "FAR Editor Tutorial";
            selectship.OnEnter = (KFSMState st) =>
            {
                instructor.PlayEmote(instructor.anim_idle_lookAround);
            };
            selectship.OnDrawContent = () =>
            {
                GUILayout.Label("You should begin by selecting a plane to load; I assume that you have some experience in plane design, even if you are not as skilled as famous airplane designers Wilbur and Orville Kerman were.\n\r\n\rIf you have difficulty deciding on a plane to select, I would suggest loading the FAR Velocitas.");

                if(EditorLogic.SortedShipList.Count > 0)
                    Tutorial.GoToNextPage();

            };
            Tutorial.AddPage(selectship);
            #endregion

            #region selectship

            ctrlsurfGUI1 = new TutorialPage("ctrlsurfGUI1");
            ctrlsurfGUI1.windowTitle = "FAR Control Systems";
            ctrlsurfGUI1.OnEnter = (KFSMState st) =>
            {
                instructor.PlayEmote(instructor.anim_idle_lookAround);
            };
            ctrlsurfGUI1.OnDrawContent = () =>
            {
                GUILayout.Label("This should do.  On the right of your screen should should see the FAR-CAST interface.  There are currently two modes: control and analysis.  It is currently in control mode, which is sued to specify the behavior of any aerodynamic control surfaces attached to the vehicle.");

                if (EditorLogic.SortedShipList.Count > 0)
                    Tutorial.GoToNextPage();

            };
            Tutorial.AddPage(ctrlsurfGUI1);
            #endregion
        }
    }
}