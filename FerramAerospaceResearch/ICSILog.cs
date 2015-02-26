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

//Ported from C++ ICSILog code, original license GPLv2 as follows:

/*
    ICSI header file of the logarithm function based on a lookup table
    icsi_log source code and fill_icsi_log_table header

    Version 0.6 beta
    Build date: November 13th, 2007

    Copyright (C) 2007 International Computer Science Institute
    1947 Center Street. Suite 600
    Berkeley, CA 94704
    
    Contact information: 
         Oriol Vinyals	vinyals@icsi.berkeley.edu
         Gerald Friedland 	fractor@icsi.berkeley.edu

    Acknowledgements:
    Thanks to Harrison Ainsworth (hxa7241@gmail.com) for his idea that
    doubled the accuracy.

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along
    with this program; if not, write to the Free Software Foundation, Inc.,
    51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

using System;

namespace FerramAerospaceResearch
{
    static unsafe class ICSILog
    {
        private static float[] icsiLogTable;
        private static int bitsUsed;

        static ICSILog()
        {
            FillICSILogTable(4);
        }

                    /*
            This method fills a given array of floats with the information necessary to compute the icsi_log. This method has to be called before any call to icsi_log.
            Parameters:
	            n is the number of bits used from the mantissa (0<=n<=23). Higher n means higher accuracy but slower execution. We found that a good value for n is 14.
            Return values: void
            */
        static void FillICSILogTable(int n)
        {
            icsiLogTable = new float[(2 << n) * sizeof(float)];
            float numlog;
            int incr;
            int* exp_ptr = ((int*)&numlog);
            int x = *exp_ptr; /*x is the float treated as an integer*/
            x = 0x3F800000; /*set the exponent to 0 so numlog=1.0*/
            *exp_ptr = x;
            incr = 1 << (23 - n); /*amount to increase the mantissa*/
            for (int i = 0; i < 2 << n; i++)
            {
                icsiLogTable[i] = (float)Math.Log(numlog, 2); /*save the log of the value*/
                x += incr;
                *exp_ptr = x; /*update the float value*/
            }
            bitsUsed = n;
        }


            /* ICSIlog V 2.0 */
        static void FillICSILogTableV2(int precision)
        {
            /* step along table elements and x-axis positions
              (start with extra half increment, so the steps intersect at their midpoints.) */
            float oneToTwo = 1.0f + (1.0f / (float)(1 << (precision + 1)));
            icsiLogTable = new float[(1 << precision)];

            int i;
            for (i = 0; i < (1 << precision); ++i)
            {
                // make y-axis value for table element
                icsiLogTable[i] = (float)Math.Log(oneToTwo) / 0.69314718055995f;

                oneToTwo += 1.0f / (float)(1 << precision);
            }
            bitsUsed = precision;
        }

            /*
            This method computes the icsi_log. A fast approximation of the log() function with adjustable accuracy.
            Parameters:
            val should be an IEEE 753 float with a value in the interval ]0,+inf[. A value smaller or equal zero results in undefined behaviour. 
	        lookup_table requires a float* pointing to the table created by fill_icsi_log_table.
	        n is the number of bits used from the mantissa (0<=n<=23). Higher n means higher accuracy but slower execution. We found that a good value for n is 14.
            Return values: approximation of the natural logarithm of val
            */

        public static float Log(float val)
        {
            int x = *((int*)&val); /*x is the float treated as an integer*/
            int log_2 = ((x >> 23) & 255) - 127; /*this is the exponent part*/
            x &= 0x7FFFFF;
            x = x >> (23 - bitsUsed); /*this is the index we should retrieve*/
            val = icsiLogTable[x];
            return ((val + log_2) * 0.69314718f); /*natural logarithm*/
        }

        /* ICSIlog v2.0 */
        public static float LogV2(float val)
        {
            /* get access to float bits */
            int* pVal = (int*)(&val);

            /* extract exponent and mantissa (quantized) */
            float exp = ((*pVal >> 23) & 255) - 127;
            int man = (*pVal & 0x7FFFFF) >> (23 - bitsUsed);

            /* exponent plus lookup refinement */
            return (exp + icsiLogTable[man]) * 0.69314718055995f;
        }
    }
}
