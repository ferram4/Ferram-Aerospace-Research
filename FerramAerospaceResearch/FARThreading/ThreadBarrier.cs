/*
Ferram Aerospace Research v0.15.6.5 "Knudsen"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2015, Michael Ferrara, aka Ferram4

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
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values  
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
                        	Regex, for adding RPM support  
				DaMichel, for some ferramGraph updates and some control surface-related features  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FerramAerospaceResearch.FARThreading
{
    //Allows multiple threads to wait before continuing
    //Is equivalent to the Barrier class from .NET 4.0
    public class ThreadBarrier
    {
        object _lockerObject = new object();
        int _threadParticipatingCount;

        int currentCountOdd = 0;
        int currentCountEven = 0;

        bool useEvenCount = false;

        public ThreadBarrier(int threadParticipatingCount)
        {
            _threadParticipatingCount = threadParticipatingCount;
        }

        public void SignalAndWait()
        {
            lock (_lockerObject)
            {
                if (useEvenCount)
                {
                    currentCountEven++;
                    if (currentCountEven >= _threadParticipatingCount)
                    {
                        useEvenCount = false;
                        currentCountOdd = 0;
                        Monitor.PulseAll(_lockerObject);
                    }
                    else
                        Monitor.Wait(_lockerObject);
                }
                else
                {
                    currentCountOdd++;
                    if (currentCountOdd >= _threadParticipatingCount)
                    {
                        useEvenCount = true;
                        currentCountEven = 0;
                        Monitor.PulseAll(_lockerObject);
                    }
                    else
                        Monitor.Wait(_lockerObject);
                }
            }
        }
        /*


                        lock (sweepPlane)           //Used as generic locker in solidification; due to careful design, locks are not needed for reading and writing to indices, so this slows nothing
                {
                    Debug.Log("Entered 1 " + threadInd);
                    synced = false;         //first, identify that we are not synced
                    threadsQueued--;          //Decrement items queued, since we are completed

                    Monitor.PulseAll(sweepPlane);       //Pulse everything waiting on sweepPlane, since we've updated the blocking condition; this is to let thread 0 go if it completed early

                    while ((threadsQueued > 0 && threadInd == 0) || (!synced && threadInd != 0))   //Then, check if there are still items to complete and if it is synced or not
                        Monitor.Wait(sweepPlane);                           //If all items are completed, but it is not synced, the 0th thread may continue
                                                                            //Other threads only care about being synced, but since entering this block automatically indicates not synced, this is not a problem
                    if (threadInd == 0)
                    {                                //If all items are completed, then the 0th thread continues
                        threadsQueued = 4;               //It then increments itemsQueued to the number of total threads
                        synced = true;                  //And indicates that we are synced
                        Debug.Log("bleh");
                    }
                    Monitor.PulseAll(sweepPlane);   //And pulses all the others so that work may continue
                    Debug.Log("Leaving 1 " + threadInd);
                }*/

    }
}
