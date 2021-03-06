﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using OccuRec.CameraDrivers;
using OccuRec.Config;
using OccuRec.Context;
using OccuRec.Helpers;
using OccuRec.OCR;
using OccuRec.Properties;
using DirectShowLib;
using System.Diagnostics;

namespace OccuRec
{
    public partial class frmChooseCamera : Form
    {
        public frmChooseCamera()
        {
            InitializeComponent();

            if (string.IsNullOrEmpty(Settings.Default.OutputLocation))
            {
                Settings.Default.OutputLocation = Path.GetFullPath(string.Format("{0}\\Videos", AppDomain.CurrentDomain.BaseDirectory));
                if (!Directory.Exists(Settings.Default.OutputLocation))
                    Directory.CreateDirectory(Settings.Default.OutputLocation);
            }
        }

		public IOccuRecCameraController CameraControlDriver { get; private set; }

	    private void frmChooseCamera_Load(object sender, EventArgs e)
        {
			CameraControlDriver = null;

			cbFileSIM.Enabled = Settings.Default.OcrSimulatorTestMode && File.Exists(Settings.Default.SimulatorFilePath);

#if !DEBUG
			cbFileSIM.Checked = false;
			cbFileSIM.Visible = false;
			cbFileSIM.Enabled = false;
#endif

            cbxCameraModel.Text = Settings.Default.CameraModel;

            cbxCaptureDevices.Items.Clear();
            foreach (DsDevice ds in DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice))
            {
                cbxCaptureDevices.Items.Add(ds.Name);
            }

            if (cbxCaptureDevices.Items.Count > 0)
            {
                if (cbxCaptureDevices.Items.Contains(Settings.Default.PreferredCaptureDevice))
                    cbxCaptureDevices.SelectedIndex = cbxCaptureDevices.Items.IndexOf(Settings.Default.PreferredCaptureDevice);
                else
                    cbxCaptureDevices.SelectedIndex = 0;
            }

            cbxIsIntegrating.Checked = Settings.Default.IsIntegrating;
            cbxFlipHorizontally.Checked = Settings.Default.HorizontalFlip;
            cbxFlipVertically.Checked = Settings.Default.VerticalFlip;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {            
            if (pnlCrossbar.Visible && cbxCrossbarInput.Enabled)
            {
                var selectedItem = (CrossbarHelper.CrossbarPinEntry)cbxCrossbarInput.SelectedItem;

                if (selectedItem == null)
                {
                    MessageBox.Show(
                        this,
                        "Please select a crossbar source.",
                        "OccuRec",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    cbxCameraModel.Focus();
                    return;
                }

                Settings.Default.CrossbarInputPin = selectedItem.PinIndex;
            }

            Settings.Default.CameraModel = cbxCameraModel.Text;
            Settings.Default.PreferredCaptureDevice = (string)cbxCaptureDevices.SelectedItem;

            if (string.IsNullOrEmpty(cbxCameraModel.Text))
            {
                MessageBox.Show(
                    this,
                    "Please specify a camera model.",
                    "OccuRec",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                cbxCameraModel.Focus();
                return;
            }

			if (CameraControlDriver != null)
			{
				if (!CameraControlDriver.IsConfigured)
				{
					while (!CameraControlDriver.IsConfigured && ConfigureCurrentCameraDriver())
					{ }

					if (CameraControlDriver != null && !CameraControlDriver.IsConfigured)
					{
						MessageBox.Show(
							this,
							"Please complete the camera driver configuration or choose a different driver.",
							"OccuRec",
							MessageBoxButtons.OK,
							MessageBoxIcon.Error);

						cbxCameraDriver.Focus();
						return;						
					}					
				}

				Settings.Default.CameraControlDriver = CameraControlDriver.DriverName;
			}

			if (Settings.Default.EasyCAPOCR)
			{
				var selectedFormat = (VideoFormatHelper.SupportedVideoFormat) cbxVideoFormats.SelectedItem;
				if (!OcrConfigEntry.EasyCAP.IsCompatible(selectedFormat))
				{
					MessageBox.Show(
						string.Format("'{0}' is not compatible with the current video mode '{1}'.", OcrConfigEntry.EasyCAP.ToString(), selectedFormat.ToString()),
						"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else
				{
					Settings.Default.SelectedOcrConfiguration = OcrConfigEntry.EasyCAP.Name;
					Settings.Default.PreserveVTIEnabled = true;
					Settings.Default.AavOcrEnabled = true;
				}
			}
			else
			{
				Settings.Default.PreserveVTIEnabled = true;
				Settings.Default.AavOcrEnabled = false;				
			}

			//var selectedFormat = (VideoFormatHelper.SupportedVideoFormat)cbxVideoFormats.SelectedItem;
			//if (selectedFormat != null)
			//{
			//	Settings.Default.PreserveVTIFirstRow = (int)nudPreserveVTITopRow.Value;
			//	Settings.Default.PreserveVTILastRow = (int)nudPreserveVTIBottomRow.Value;
			//	Settings.Default.PreserveVTIWidth = selectedFormat.Width;
			//	Settings.Default.PreserveVTIHeight = selectedFormat.Height;
			//	Settings.Default.Save();
			//}

            Settings.Default.FileSimulation = cbFileSIM.Checked;

            Settings.Default.IsIntegrating = cbxIsIntegrating.Checked;
            Settings.Default.HorizontalFlip = cbxFlipHorizontally.Checked;
            Settings.Default.VerticalFlip = cbxFlipVertically.Checked;

            Settings.Default.Save();

	        OccuRecContext.Current.IsAAV = true;

            DialogResult = DialogResult.OK;
            Close();
        }

        void cbxCrossbarInput_SelectedIndexChanged(object sender, EventArgs e)
        {
            string deviceName = (string)cbxCaptureDevices.SelectedItem;

            if (!string.IsNullOrEmpty(deviceName))
            {
                CrossbarHelper.CrossbarPinEntry selectedItem = (CrossbarHelper.CrossbarPinEntry) cbxCrossbarInput.SelectedItem;
                if (selectedItem != null)
                {
                    Cursor = Cursors.WaitCursor;
                    Update();
                    try
                    {
                        CrossbarHelper.ConnectToCrossbarSource(deviceName, selectedItem.PinIndex);

                        Settings.Default.SelectedCrossbarInputPin = selectedItem.PinName;
                        Settings.Default.Save();
                    }
                    finally
                    {
                        Cursor = Cursors.Default;
                    }
                }
            }
        }

		private void cbxVideoFormats_SelectedIndexChanged(object sender, EventArgs e)
		{
			var selectedFormat = (VideoFormatHelper.SupportedVideoFormat)cbxVideoFormats.SelectedItem;
			Settings.Default.SelectedVideoFormat = selectedFormat.AsSerialized();
			Settings.Default.Save();
		}

        private void cbxCaptureDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            string deviceName = (string)cbxCaptureDevices.SelectedItem;

            if (!string.IsNullOrEmpty(deviceName))
            {
                Cursor = Cursors.WaitCursor;
                Update();

                cbxCrossbarInput.Items.Clear();
                cbxCrossbarInput.SelectedIndexChanged -= new EventHandler(cbxCrossbarInput_SelectedIndexChanged);
                try
                {
                    CrossbarHelper.LoadCrossbarSources(deviceName, cbxCrossbarInput);
                    pnlCrossbar.Visible = cbxCrossbarInput.Items.Count > 0;
                }
                finally
                {
                    cbxCrossbarInput.SelectedIndexChanged += new EventHandler(cbxCrossbarInput_SelectedIndexChanged);
                    Cursor = Cursors.Default;
                }

                CrossbarHelper.CrossbarPinEntry selectedEntry = null;
                foreach (CrossbarHelper.CrossbarPinEntry entry in cbxCrossbarInput.Items)
                {
                    if (entry.PinName == Settings.Default.SelectedCrossbarInputPin)
                    {
                        selectedEntry = entry;
                        break;
                    }
                }
                if (selectedEntry != null)
                    cbxCrossbarInput.SelectedItem = selectedEntry;

                cbxVideoFormats.Items.Clear();
                cbxVideoFormats.SelectedIndexChanged -= new EventHandler(cbxVideoFormats_SelectedIndexChanged);
                try
                {
	                bool palSupported;
	                bool ntscSupported;
					VideoFormatHelper.LoadSupportedVideoFormats(deviceName, cbxVideoFormats, out palSupported, out ntscSupported);

					if (palSupported)
					{
						rbPAL.ForeColor = Color.Green;
						toolTipControl.SetToolTip(rbPAL, null);
					}
					else
					{
						rbPAL.ForeColor = Color.OrangeRed;
						toolTipControl.SetToolTip(rbPAL, string.Format("{0} doesn't report to support standard PAL (720x576 @25 fps). This video mode may still work though.", deviceName));
					}
	                
					if (ntscSupported)
					{
						rbNTSC.ForeColor = Color.Green;
						toolTipControl.SetToolTip(rbNTSC, null);
					}
					else
					{
						rbNTSC.ForeColor = Color.OrangeRed;
						toolTipControl.SetToolTip(rbNTSC, string.Format("{0} doesn't report to support standard NTSC (720x480 @29.97 fps). This video mode may still work though.", deviceName));
					}					
                }
                finally
                {
                    cbxVideoFormats.SelectedIndexChanged += new EventHandler(cbxVideoFormats_SelectedIndexChanged);
                    Cursor = Cursors.Default;
                }

                VideoFormatHelper.SupportedVideoFormat selectedVideoFormat = null;
                foreach (VideoFormatHelper.SupportedVideoFormat format in cbxVideoFormats.Items)
                {
                    if (Settings.Default.SelectedVideoFormat == format.AsSerialized())
                    {
                        selectedVideoFormat = format;
                        break;
                    }
                }

                if (selectedVideoFormat != null)
                    cbxVideoFormats.SelectedItem = selectedVideoFormat;
                else
                    cbxVideoFormats.SelectedIndex = 0;

	            UpdateSelectedVideoFormatControls(selectedVideoFormat);
            }
        }

		private void cbxCameraModel_SelectedIndexChanged(object sender, EventArgs e)
		{
			List<IOccuRecCameraController> availableDrivers = OccuRecVideoDrivers.GetAvailableDriversForCamera((string) cbxCameraModel.SelectedItem);
			cbxCameraDriver.Items.Clear();
			cbxCameraDriver.Items.Add("");
			cbxCameraDriver.Items.AddRange(availableDrivers.ToArray());
			cbxCameraDriver.Enabled = availableDrivers.Count > 0;
			if (cbxCameraDriver.Items.Count == 0)
				cbxCameraDriver.Items.Add("No Video Drivers Available");

			IOccuRecCameraController selectedController = availableDrivers.SingleOrDefault(x => x.DriverName == Settings.Default.CameraControlDriver);
			if (selectedController != null)
				cbxCameraDriver.SelectedIndex = cbxCameraDriver.Items.IndexOf(selectedController);
			else
				cbxCameraDriver.SelectedIndex = -1;
		}

		private void cbxCameraDriver_SelectedIndexChanged(object sender, EventArgs e)
		{
			var camController = cbxCameraDriver.SelectedItem as IOccuRecCameraController;
			btnConfigureCameraDriver.Visible = camController != null;
			btnConfigureCameraDriver.Enabled = camController != null && camController.RequiresConfiguration;

			CameraControlDriver = camController;
			if (CameraControlDriver != null)
				CameraControlDriver.Configuration = OccuRecVideoDrivers.GetDriverSettings(CameraControlDriver);
		}

		private void btnConfigureCameraDriver_Click(object sender, EventArgs e)
		{
			ConfigureCurrentCameraDriver();
		}

		private bool ConfigureCurrentCameraDriver()
		{
			bool configChanged = false;

			if (CameraControlDriver != null)
			{
				CameraControlDriver.Configuration = OccuRecVideoDrivers.GetDriverSettings(CameraControlDriver);
				configChanged = CameraControlDriver.ConfigureConnectionSettings(this);
				if (configChanged)
					OccuRecVideoDrivers.SetDriverSettings(CameraControlDriver);
			}

			return configChanged;
		}

		private void UpdateSelectedVideoFormatControls(VideoFormatHelper.SupportedVideoFormat selectedVideoFormat)
		{
			if (selectedVideoFormat != null)
			{
				if (selectedVideoFormat.IsPal())
				{
					rbPAL.Checked = true;
					pnlSimpleFrameRate.Visible = true;
					cbxVideoFormats.Visible = false;
				}
				else if (selectedVideoFormat.IsNtsc())
				{
					rbNTSC.Checked = true;
					pnlSimpleFrameRate.Visible = true;
					cbxVideoFormats.Visible = false;
				}
				else
				{
					pnlSimpleFrameRate.Visible = false;
					cbxVideoFormats.Visible = true;
				}
			}
			else
			{
				pnlSimpleFrameRate.Visible = true;
				cbxVideoFormats.Visible = false;
			}			
		}

		private void rbOtherMode_CheckedChanged(object sender, EventArgs e)
		{
			if (rbOtherMode.Checked)
			{
				pnlSimpleFrameRate.Visible = false;
				cbxVideoFormats.Visible = true;				
			}
		}
    }
}
