﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using ASCOM.DriverAccess;
using OccuRec.ASCOM.Interfaces.Devices;
using OccuRec.Utilities;

namespace OccuRec.ASCOM.Server
{
	[Serializable]
	public class IsolatedDevice : MarshalByRefObject, IASCOMDevice, IDisposable
	{
		protected AscomDriver m_Device;

		public string ProgId { get; private set; }

		public Guid UniqueId { get; private set; }

		protected void SetIsolatedDevice(AscomDriver device, string progId)
		{
			m_Device = device;
			ProgId = progId;
			UniqueId = Guid.NewGuid();

            Trace.WriteLine(string.Format("OccuRec: ASCOMServer::new('{0}'). InterfaceVersion={1}", progId, m_Device.InterfaceVersion));

		    if (m_Device.Connected)
                // If we are instantiating a connected device, we disconnect first (make this optional?)
		        m_Device.Connected = false;
		}

	    protected virtual void OnConnected()
	    { }

		public bool Connected
		{
			get { return m_Device.Connected; }
			set
			{
                if (m_Device.Connected != value)
                {
			        m_Device.Connected = value;
                    Trace.WriteLine(string.Format("OccuRec: ASCOMServer::{0}::Connected = {1}", ProgId, value));
                    if (value) OnConnected();
                }
			}
		}

		public string Description
		{
			get { return m_Device.Name; }
		}

		public string DriverVersion
		{
			get { return m_Device.DriverVersion; }
		}

		protected virtual void Dispose(bool disposing)
		{
            Trace.WriteLine(string.Format("OccuRec: ASCOMServer::{0}::Dispose()", ProgId));

            try
            {
                m_Device.Connected = false;
                m_Device.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.GetFullStackTrace());
            }

		    RemotingServices.Disconnect(this);
		}

		public void Dispose()
		{
			Dispose(true);
		}
	}
}
