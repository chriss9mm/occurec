﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OccuRec.Properties;
using OccuRec.Utilities;

namespace OccuRec.Config.Panels
{
	public partial class ucAAV : SettingsPanel
	{
		public ucAAV()
		{
			InitializeComponent();
		}

		public override void LoadSettings()
		{
			nudSignDiffRatio.Value = Math.Min(50, Math.Max(1, (decimal)Settings.Default.MinSignatureDiffRatio));
			nudMinSignDiff.Value = Math.Min(10, Math.Max(0, (decimal)Settings.Default.MinSignatureDiff));
			nudGammaDiff.Value = (decimal)Settings.Default.GammaDiff;
			nudCalibrIntegrRate.Value = Settings.Default.CalibrationIntegrationRate;
			cbForceIntegrationRateRestrictions.Checked = Settings.Default.ForceIntegrationRatesRestrictions;
			cbxFrameProcessingMode.SelectedIndex = Settings.Default.UsesBufferedFrameProcessing ? 0 : 1;
			rbIntegrationBin.Checked = Settings.Default.Use16BitAAV;
		}

		public override void SaveSettings()
		{
			Settings.Default.MinSignatureDiffRatio = (float)nudSignDiffRatio.Value;
			Settings.Default.MinSignatureDiff = (float)nudMinSignDiff.Value;
			Settings.Default.GammaDiff = (float)nudGammaDiff.Value;
			Settings.Default.CalibrationIntegrationRate = (int)nudCalibrIntegrRate.Value;
			Settings.Default.ForceIntegrationRatesRestrictions = cbForceIntegrationRateRestrictions.Checked;

			Settings.Default.UsesBufferedFrameProcessing = cbxFrameProcessingMode.SelectedIndex == 0;
			Settings.Default.Use16BitAAV = rbIntegrationBin.Checked;
		}
	}
}
