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
