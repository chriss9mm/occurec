﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OccuRec.ASCOM.Interfaces;
using OccuRec.ASCOM.Interfaces.Devices;

namespace OccRec.ASCOMWrapper.Devices
{
	internal class Focuser : DeviceBase, IASCOMFocuser
	{
		private IASCOMFocuser m_IsolatedFocuser;

		internal Focuser(IASCOMFocuser isolatedFocuser)
			: base(isolatedFocuser)
		{
			m_IsolatedFocuser = isolatedFocuser;
		}
	}
}
