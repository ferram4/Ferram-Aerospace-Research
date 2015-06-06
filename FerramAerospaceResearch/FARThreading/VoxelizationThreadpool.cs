/*
Ferram Aerospace Research v0.15.2 "Ferri"
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
using System.Threading;

namespace FerramAerospaceResearch.FARThreading
{
    //This class only exists to ensure that the ThreadPool is not choked with requests to start running voxels, which will deadlock the entire voxelization process when the MaxThread limit is reached because they will be unable to start up their various worker threads
    class VoxelizationThreadpool
    {
        static VoxelizationThreadpool _instance;
        public static VoxelizationThreadpool Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new VoxelizationThreadpool();

                return _instance;
            }
        }

        Thread[] _threads;
        Queue<Action> queuedVoxelizations;
        const int THREAD_COUNT = 8;

        VoxelizationThreadpool()
        {
            _threads = new Thread[THREAD_COUNT];
            queuedVoxelizations = new Queue<Action>();
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(ExecuteQueuedVoxelization);
                //_threads[i].IsBackground = true;
                _threads[i].Start();
            }
            ThreadSafeDebugLogger.Instance.RegisterMessage("Threads created...");
        }

        ~VoxelizationThreadpool()
        {
            for (int i = 0; i < _threads.Length; i++)
            {
                QueueVoxelization(null);        //this will pass a null action to each thread, ending it
            }
        }

        void ExecuteQueuedVoxelization()
        {
            while (true)
            {
                Action task;
                lock (this)
                {
                    while (queuedVoxelizations.Count == 0)
                    {
                        Monitor.Wait(this);
                    }

                    task = queuedVoxelizations.Dequeue();
                }
                if (task != null)
                    task();
                else
                    break;
            }
        }

        public void QueueVoxelization(Action voxelAction)
        {
            lock (this)
            {
                queuedVoxelizations.Enqueue(voxelAction);
                Monitor.Pulse(this);
            }
        }
    }
}
