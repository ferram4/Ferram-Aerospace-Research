/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// File:	StringBuilderExtNumeric.cs
// Date:	2017
// Author:	Virindi, modified from original byu Gavin Pugh
// Details:	Extension methods for the 'StringBuilder' standard .NET class, to allow garbage-free concatenation of
//			a selection of simple numeric types.  
//
// Copyright (c) Virindi 2017, modified from Gavin Pugh 2010 - Released under the zlib license: http://www.opensource.org/licenses/zlib-license.php
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace StringLeakTest
{
	public static partial class StringBuilderExtensions
	{
		// These digits are here in a static array to support hex with simple, easily-understandable code. 
		// Since A-Z don't sit next to 0-9 in the ascii table.
		private static readonly char[]	ms_digits = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

		private static readonly uint	ms_default_decimal_places = 5; //< Matches standard .NET formatting dp's
		private static readonly char	ms_default_pad_char = '0';

		//! Convert a given unsigned integer value to a string and concatenate onto the stringbuilder. Any base value allowed.
		public static StringBuilder Concat( this StringBuilder string_builder, uint uint_val, uint pad_amount, char pad_char, uint base_val )
		{
			Debug.Assert( pad_amount >= 0 );
			Debug.Assert( base_val > 0 && base_val <= 16 );

			// Calculate length of integer when written out
			uint length = 0;
			uint length_calc = uint_val;

			do
			{
				length_calc /= base_val;
				length++;
			}
			while ( length_calc > 0 );

			// Pad out space for writing.
			string_builder.Append( pad_char, (int)Math.Max( pad_amount, length ));

			int strpos = string_builder.Length;

			// We're writing backwards, one character at a time.
			while ( length > 0 )
			{
				strpos--;

				// Lookup from static char array, to cover hex values too
				string_builder[strpos] = ms_digits[uint_val % base_val];

				uint_val /= base_val;
				length--;
			}

			return string_builder;
		}

		//! Convert a given unsigned integer value to a string and concatenate onto the stringbuilder. Assume no padding and base ten.
		public static StringBuilder Concat( this StringBuilder string_builder, uint uint_val )
		{
			string_builder.Concat( uint_val, 0, ms_default_pad_char, 10 );
			return string_builder;
		}

		//! Convert a given unsigned integer value to a string and concatenate onto the stringbuilder. Assume base ten.
		public static StringBuilder Concat( this StringBuilder string_builder, uint uint_val, uint pad_amount )
		{
			string_builder.Concat( uint_val, pad_amount, ms_default_pad_char, 10 );
			return string_builder;
		}

		//! Convert a given unsigned integer value to a string and concatenate onto the stringbuilder. Assume base ten.
		public static StringBuilder Concat( this StringBuilder string_builder, uint uint_val, uint pad_amount, char pad_char )
		{
			string_builder.Concat( uint_val, pad_amount, pad_char, 10 );
			return string_builder;
		}

		//! Convert a given signed integer value to a string and concatenate onto the stringbuilder. Any base value allowed.
		public static StringBuilder Concat( this StringBuilder string_builder, int int_val, uint pad_amount, char pad_char, uint base_val )
		{
			Debug.Assert( pad_amount >= 0 );
			Debug.Assert( base_val > 0 && base_val <= 16 );

			// Deal with negative numbers
			if (int_val < 0)
			{
				string_builder.Append( '-' );
				uint uint_val = uint.MaxValue - ((uint) int_val ) + 1; //< This is to deal with Int32.MinValue
				string_builder.Concat( uint_val, pad_amount, pad_char, base_val );
			}
			else
			{
				string_builder.Concat((uint)int_val, pad_amount, pad_char, base_val );
			}

			return string_builder;
		}

		//! Convert a given signed integer value to a string and concatenate onto the stringbuilder. Assume no padding and base ten.
		public static StringBuilder Concat( this StringBuilder string_builder, int int_val )
		{
			string_builder.Concat( int_val, 0, ms_default_pad_char, 10 );
			return string_builder;
		}

		//! Convert a given signed integer value to a string and concatenate onto the stringbuilder. Assume base ten.
		public static StringBuilder Concat( this StringBuilder string_builder, int int_val, uint pad_amount )
		{
			string_builder.Concat( int_val, pad_amount, ms_default_pad_char, 10 );
			return string_builder;
		}

		//! Convert a given signed integer value to a string and concatenate onto the stringbuilder. Assume base ten.
		public static StringBuilder Concat( this StringBuilder string_builder, int int_val, uint pad_amount, char pad_char )
		{
			string_builder.Concat( int_val, pad_amount, pad_char, 10 );
			return string_builder;
		}

		/*
		//*****************************************************************************
		// A few basic tests for the implementation of string to float in Concat below.
		//*****************************************************************************
		static void Concat_Float_TestIndividual(float test, uint digits)
		{
			//Our implementation is not correct for the rounded portion of the partial final
			//digit. So for verification we will ignore the significance of the last digit (it is
			//expected to be possibly off when rounding occurs).
			int roundtodigits = (int)digits - 1;
			if (roundtodigits < 0) roundtodigits = 0;
			Decimal testd = Math.Round((Decimal)test, roundtodigits);
			test = (float)testd;

			StringBuilder sb1 = new StringBuilder();
			sb1.Concat(test, digits);
			string res1 = sb1.ToString();

			StringBuilder sb2 = new StringBuilder();
			sb2.AppendFormat(test.ToString("N" + digits.ToString()));
			string res2 = sb2.ToString();

			if (!string.Equals(res1, res2))
			{
				Console.WriteLine("ERR: {0}: {1} != {2}", test, res1, res2);
			}
		}

		static void Concat_Float_TestMany()
		{
			Random r = new Random();
			for (int i = 0; i < 100000; ++i)
			{
				float test = (float)(r.NextDouble() * (double)r.Next(-1000, 1001));
				uint digits = (uint)r.Next(0, 5);

				Concat_Float_TestIndividual(test, digits);
			}

			//Some examples that were known to cause problems
			Concat_Float_TestIndividual(0.005538003f, 4);
			Concat_Float_TestIndividual(0.09f, 1);
			Concat_Float_TestIndividual(0.09850061f, 1);
			Concat_Float_TestIndividual(0.9764563f, 1);
			Concat_Float_TestIndividual(0.5771284f, 2);
			Concat_Float_TestIndividual(318.0505f, 3);
			Concat_Float_TestIndividual(78.45879f, 7);
			Concat_Float_TestIndividual(-0.780569f, 3);
			Concat_Float_TestIndividual(224.4155f, 3);
			Concat_Float_TestIndividual(195.895f, 2);
		}
		*/

		//! Convert a given float value to a string and concatenate onto the stringbuilder
		// NOTE: This implementation is not strictly identical to .NET's implementation with respect to rounding.
		// Also note that decimal separators (",") are not included either.
		public static StringBuilder Concat(this StringBuilder string_builder, float float_val, uint decimal_places, uint pad_amount, char pad_char)
		{
			Debug.Assert(pad_amount >= 0);

			//Deal with the non-numeric float values first...
			if (float.IsNaN(float_val))
			{
				string_builder.Append("NaN");
				return string_builder;
			}
			else if (float.IsInfinity(float_val))
			{
				if (float.IsPositiveInfinity(float_val))
				{
					string_builder.Append("Infinity");
					return string_builder;
				}
				else if (float.IsInfinity(float_val))
				{
					string_builder.Append("-Infinity");
					return string_builder;
				}
			}

			if (decimal_places == 0)
			{
				// No decimal places, just round up and print it as an int

				// Agh, Math.Floor() just works on doubles/decimals. Don't want to cast! Let's do this the old-fashioned way.
				int int_val;
				if (float_val >= 0.0f)
				{
					// Round up
					int_val = (int)(float_val + 0.5f);
				}
				else
				{
					// Round down for negative numbers
					int_val = (int)(float_val - 0.5f);
				}

				string_builder.Concat(int_val, pad_amount, pad_char, 10);
			}
			else
			{
				// We must operate below on the multiply loop as a decimal type, because
				// regular floating point will create inaccuracy in the multiplication. For instance,
				// 0.58f * 10f - 5f = 0.7999998f.
				// Rounding must also occur before the separation of integer and decimal parts,
				// so that the two parts agree.
				// Also note that asking Math.Round to round a float to more decimal places
				// than actually can be represented with a float will produce unexpected results.
				// Because of this problem, this technique cannot perfectly match the output of ToString("N"+x),
				// but it is close enough.
				int round_to_places = (int)decimal_places;
				if (round_to_places > 5) round_to_places = 5;
				Decimal decimal_val = (Decimal)Math.Round(float_val, round_to_places);
				int int_part = (int)decimal_val;

				// Special case: -0.xx
				if ((int_part == 0) && (decimal_val < 0m))
					string_builder.Append('-');

				// First part is easy, just cast to an integer
				string_builder.Concat(int_part, pad_amount, pad_char, 10);

				// Decimal point
				string_builder.Append('.');

				// Work out remainder we need to print after the d.p.
				Decimal remainder = Math.Abs(decimal_val - int_part);

				// Multiply up to become an int that we can print
				do
				{
					remainder *= 10m;
					decimal_places--;

					if (remainder < 1.0m) string_builder.Append(ms_digits[0]);
				}
				while (decimal_places > 0);

				// All done, print that as an int!
				// Note that if the entire number is zero, we already padded out completely with zeroes above.
				uint remainder_as_uint = (uint)remainder;
				if (remainder_as_uint != 0)
					string_builder.Concat(remainder_as_uint, 0, '0', 10);
			}
			return string_builder;
		}
		
		//! Convert a given float value to a string and concatenate onto the stringbuilder. Assumes five decimal places, and no padding.
		public static StringBuilder Concat(this StringBuilder string_builder, float float_val)
		{
			string_builder.Concat(float_val, ms_default_decimal_places, 0, ms_default_pad_char);
			return string_builder;
		}

		//! Convert a given float value to a string and concatenate onto the stringbuilder. Assumes no padding.
		public static StringBuilder Concat( this StringBuilder string_builder, float float_val, uint decimal_places )
		{
			string_builder.Concat (float_val, decimal_places, 0, ms_default_pad_char );
			return string_builder;
		}

		//! Convert a given float value to a string and concatenate onto the stringbuilder.
		public static StringBuilder Concat(this StringBuilder string_builder, float float_val, uint decimal_places, uint pad_amount)
		{
			string_builder.Concat(float_val, decimal_places, pad_amount, ms_default_pad_char);
			return string_builder;
		}
	}
}
