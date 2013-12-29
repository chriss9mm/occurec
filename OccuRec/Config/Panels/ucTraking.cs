﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OccuRec.Helpers;
using OccuRec.Properties;

namespace OccuRec.Config.Panels
{
	public partial class ucTraking : SettingsPanel
	{
		public ucTraking()
		{
			InitializeComponent();
		}

		public override void LoadSettings()
		{
			nudMaxElongation.SetNUDValue((double)Settings.Default.TrackingMaxElongation);
			cbxTestPSFElongation.Checked = Settings.Default.TrackingCheckElongation;
			nudMaxElongation.Enabled = cbxTestPSFElongation.Checked;

			nudMinFWHM.SetNUDValue((double)Settings.Default.TrackingMinFWHM);
			nudMaxFWHM.SetNUDValue((double)Settings.Default.TrackingMaxFWHM);
			nudDetectionCertainty.SetNUDValue((double)Settings.Default.TrackingMinCertainty);
		}

		public override void SaveSettings()
		{
			Settings.Default.TrackingMaxElongation = (double)nudMaxElongation.Value;
			Settings.Default.TrackingCheckElongation = cbxTestPSFElongation.Checked;

			Settings.Default.TrackingMinFWHM = (double)nudMinFWHM.Value;
			Settings.Default.TrackingMaxFWHM = (double)nudMaxFWHM.Value;
			Settings.Default.TrackingMinCertainty = (double)nudDetectionCertainty.Value;
		}

		private void cbxTestPSFElongation_CheckedChanged(object sender, EventArgs e)
		{
			nudMaxElongation.Enabled = cbxTestPSFElongation.Checked;
		}
	}
}