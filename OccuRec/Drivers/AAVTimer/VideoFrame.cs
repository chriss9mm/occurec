﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using OccuRec.Drivers.AAVTimer.VideoCaptureImpl;
using OccuRec.Helpers;
using OccuRec.Tracking;
using OccuRec.Utilities;
using OccuRec.Utilities.Exceptions;

namespace OccuRec.Drivers.AAVTimer
{
	public enum VideoFrameLayout
	{
		Monochrome,
		Color,
		BayerRGGB
	}

	public enum MonochromePixelMode
	{
		R,
		G,
		B,
		GrayScale
	}

	public class VideoFrame : IVideoFrame
	{
		private long? frameNumber;
		private string imageInfo;
		private double? exposureDuration;
		private string exposureStartTime;
		private object pixels;
		private object pixelsVariant;
	    private Bitmap previewBitmap;

		private static int s_Counter = 0;

		internal static VideoFrame FakeFrame(int width, int height)
		{
			var rv = new VideoFrame();
			s_Counter++;
			rv.frameNumber = s_Counter;

			rv.pixels = new int[0, 0];
			return rv;
		}

		internal static VideoFrame CreateFrameVariant(int width, int height, VideoCameraFrame cameraFrame)
		{
			return InternalCreateFrame(width, height, cameraFrame, true);
		}

		internal static VideoFrame CreateFrame(int width, int height, VideoCameraFrame cameraFrame)
		{
			return InternalCreateFrame(width, height, cameraFrame, false);
		}

		private static VideoFrame InternalCreateFrame(int width, int height, VideoCameraFrame cameraFrame, bool variant)
		{
			var rv = new VideoFrame();

			if (cameraFrame.ImageLayout == VideoFrameLayout.Monochrome)
			{
				if (variant)
				{
					rv.pixelsVariant = new object[height, width];
					rv.pixels = null;
				}
				else
				{
					rv.pixels = new int[height, width];
					rv.pixelsVariant = null;
				}

				if (variant)
					Array.Copy((int[,])cameraFrame.Pixels, (object[,])rv.pixelsVariant, ((int[,])cameraFrame.Pixels).Length);
				else
					rv.pixels = (int[,])cameraFrame.Pixels;
			}
			else if (cameraFrame.ImageLayout == VideoFrameLayout.Color)
			{
				if (variant)
				{
					rv.pixelsVariant = new object[height, width, 3];
					rv.pixels = null;
				}
				else
				{
					rv.pixels = new int[height, width, 3];
					rv.pixelsVariant = null;
				}

				if (variant)
					Array.Copy((int[, ,])cameraFrame.Pixels, (object[, ,])rv.pixelsVariant, ((int[, ,])cameraFrame.Pixels).Length);
				else
					rv.pixels = (int[, ,])cameraFrame.Pixels;
			}
			else if (cameraFrame.ImageLayout == VideoFrameLayout.BayerRGGB)
			{
				throw new NotSupportedException();
			}
			else
				throw new NotSupportedException();

            rv.previewBitmap = cameraFrame.PreviewBitmap;

			rv.frameNumber = cameraFrame.FrameNumber;
            rv.exposureStartTime = new DateTime(cameraFrame.ImageStatus.StartExposureSystemTime).ToString("HH:mm:ss.fff");
			rv.exposureDuration = null;
			rv.imageInfo = string.Format("INT:{0};SFID:{1};EFID:{2};CTOF:{3};UFID:{4};IFID:{5};DRPD:{6}",
				cameraFrame.ImageStatus.DetectedIntegrationRate, 
				cameraFrame.ImageStatus.StartExposureFrameNo, 
				cameraFrame.ImageStatus.EndExposureFrameNo, 
				cameraFrame.ImageStatus.CutOffRatio, 
				cameraFrame.ImageStatus.UniqueFrameNo,
				cameraFrame.ImageStatus.IntegratedFrameNo,
				cameraFrame.ImageStatus.DropedFramesSinceIntegrationLock);

			if (cameraFrame.ImageStatus.PerformedAction > 0)
			{
				rv.imageInfo += string.Format(";ACT:{0};ACT%:{1}", cameraFrame.ImageStatus.PerformedAction, cameraFrame.ImageStatus.PerformedActionProgress);
			}

			if (cameraFrame.ImageStatus.OcrWorking > 0)
			{
				rv.imageInfo += string.Format(";ORER:{0}", cameraFrame.ImageStatus.OcrErrorsSinceLastReset);
			}

			if (cameraFrame.ImageStatus.UserIntegratonRateHint > 0)
			{
				rv.imageInfo += string.Format(";USRI:{0}", cameraFrame.ImageStatus.UserIntegratonRateHint);
			}

			if (TrackingContext.Current.IsTracking)
			{
				TrackingContext.Current.UpdateFromFrameStatus(cameraFrame.FrameNumber, cameraFrame.ImageStatus);
			}
			return rv;
		}

        private static DateTime SYSTEMTIME2DateTime(SYSTEMTIME st)
        {
            if (st.Year == 0 || st == SYSTEMTIME.MinValue)
                return DateTime.MinValue;
            if (st == SYSTEMTIME.MaxValue)
                return DateTime.MaxValue;
            return new DateTime(st.Year, st.Month, st.Day, st.Hour, st.Minute, st.Second, st.Milliseconds, DateTimeKind.Local);
        }

		public object ImageArray
		{
			get
			{
				return pixels;
			}
		}

		public object ImageArrayVariant
		{
			get
			{
				return pixelsVariant;
			}
		}

        public Bitmap PreviewBitmap
        {
            get { return previewBitmap; }
        }

		public long FrameNumber
		{
			get
			{
				if (frameNumber.HasValue)
					return frameNumber.Value;

				return -1;
			}
		}

		public double ExposureDuration
		{
			[DebuggerStepThrough]
			get
			{
				if (exposureDuration.HasValue)
					return exposureDuration.Value;

				throw new PropertyNotImplementedException("Current camera doesn't support frame timing.");
			}
		}
		public string ExposureStartTime
		{
			[DebuggerStepThrough]
			get
			{
				if (exposureStartTime != null)
					return exposureStartTime;

				throw new PropertyNotImplementedException("Current camera doesn't support frame timing.");
			}
		}

		public string ImageInfo
		{
			get { return imageInfo; }
		}

	}
}

