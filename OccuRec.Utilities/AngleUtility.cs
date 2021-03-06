﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OccuRec.Utilities
{
	public static class AngleUtility
	{
		public static double Range(double value, double maxVal)
		{
			while (value > maxVal) value -= maxVal;
			while (value < 0) value += maxVal;

			return value;
		}

		public static double Elongation(double raDeg1, double deDeg1, double raDeg2, double deDeg2)
		{
			// http://www.movable-type.co.uk/scripts/latlong.html

			double dLat = (deDeg2 - deDeg1) * System.Math.PI / 180;
			double dLong = (raDeg2 - raDeg1) * System.Math.PI / 180;

			double a = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2) +
					System.Math.Cos(deDeg1 * System.Math.PI / 180) * System.Math.Cos(deDeg2 * System.Math.PI / 180) *
					System.Math.Sin(dLong / 2) * System.Math.Sin(dLong / 2);

			double c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));

			if (c > Math.PI) c = 2 * Math.PI - c;

			return c * 180 / Math.PI;
		}
	}
}
