﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OccuRec.Properties;

namespace OccuRec.Helpers
{
    public class OverlayManager
    {
        private Queue<string> errorMessagesQueue = new Queue<string>();
        private string currentOcrStamp = null;

        private string currMessageToDisplay = null;
        private DateTime displayMessageUntil = DateTime.MinValue;

        private static Font overlayMessagesFont = new Font(FontFamily.GenericMonospace, 10);

        private int imageWidth;
        private int imageHeight;
	    private int framesWithoutTimestams;

	    private OverlayState overlayState;

        private object syncRoot = new object();

        public OverlayManager(int width, int height, List<string> initializationErrorMessages)
        {
            imageWidth = width;
            imageHeight = height;
            currentOcrStamp = null;

            foreach (string message in initializationErrorMessages)
                errorMessagesQueue.Enqueue(message);
        }

		public void ChangeOverlayState<TNewState>() where TNewState : OverlayState, new()
		{
			if (overlayState != null)
				overlayState.Finalise();

			overlayState = new TNewState();
			overlayState.Initialise();
		}

        public void OnEvent(int eventId, string eventData)
        {
            if (eventId == 0)
                currentOcrStamp = eventData;
        }

        public void OnError(int errorCode, string errorMessage)
        {
            errorMessagesQueue.Enqueue(errorMessage);
        }

        public void Finalise()
        {
            lock (syncRoot)
            {
                errorMessagesQueue.Clear();

                displayMessageUntil = DateTime.MinValue;
                currMessageToDisplay = null;
            }

			if (overlayState != null)
				overlayState.Finalise();

	        overlayState = null;
        }

	    private SizeF timestampMeasurement = SizeF.Empty;

        public void ProcessFrame(Graphics g)
        {
			if (overlayState != null)
				overlayState.ProcessFrame(g);

            ProcessErrorMessages(g);

			if (Settings.Default.OcrSimulatorTestMode && !Settings.Default.OcrSimulatorNativeCode)
			{
				// Only show OCR timestamp when running in managed simulated test mode
				string ocrStampToDisplay = currentOcrStamp;
				currentOcrStamp = null;
				if (ocrStampToDisplay != null)
				{
					framesWithoutTimestams = 0;
					timestampMeasurement = g.MeasureString(ocrStampToDisplay, overlayMessagesFont);

					g.FillRectangle(Brushes.DarkSlateGray, imageWidth - timestampMeasurement.Width - 9, imageHeight - timestampMeasurement.Height - 39, timestampMeasurement.Width + 6, timestampMeasurement.Height + 6);
					g.DrawString(ocrStampToDisplay, overlayMessagesFont, Brushes.Lime, imageWidth - timestampMeasurement.Width - 6, imageHeight - timestampMeasurement.Height - 36);
				}
				else
				{
					framesWithoutTimestams++;
					if (framesWithoutTimestams < 100)
						g.FillRectangle(Brushes.DarkSlateGray, imageWidth - timestampMeasurement.Width - 9, imageHeight - timestampMeasurement.Height - 39, timestampMeasurement.Width + 6, timestampMeasurement.Height + 6);
					else
						timestampMeasurement = SizeF.Empty;
				}				
			}
        }

        private void PrintCurrentErrorMessage(Graphics g)
        {
            SizeF msgMeasurement = g.MeasureString(currMessageToDisplay, overlayMessagesFont);

            g.FillRectangle(Brushes.DarkSlateGray, imageWidth - msgMeasurement.Width - 9, 3, msgMeasurement.Width + 6, msgMeasurement.Height + 6);
            g.DrawString(currMessageToDisplay, overlayMessagesFont, Brushes.OrangeRed, imageWidth - msgMeasurement.Width - 6, 6);
        }

        private void ProcessErrorMessages(Graphics g)
        {
            if (displayMessageUntil != DateTime.MinValue && currMessageToDisplay != null)
            {
                if (DateTime.Now.Ticks > displayMessageUntil.Ticks)
                {
                    displayMessageUntil = DateTime.MinValue;
                    currMessageToDisplay = null;
                }
                else
                    PrintCurrentErrorMessage(g);
            }
            else
            {
                lock (syncRoot)
                {
                    if (errorMessagesQueue.Count > 0)
                    {
                        currMessageToDisplay = errorMessagesQueue.Dequeue();
                        displayMessageUntil = DateTime.Now.AddSeconds(10);
                        PrintCurrentErrorMessage(g);
                    }
                }
            }
        }

		public void MouseMove(MouseEventArgs e)
		{
			if (overlayState != null)
				overlayState.MouseMove(e);
		}

		public void MouseLeave(EventArgs e)
		{
			if (overlayState != null)
				overlayState.MouseLeave(e);
		}

		public void MouseDown(MouseEventArgs e)
		{
			if (overlayState != null)
				overlayState.MouseDown(e);
		}

		public void MouseUp(MouseEventArgs e)
		{
			if (overlayState != null)
				overlayState.MouseUp(e);
		}
    }
}
