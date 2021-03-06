﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OccuRec.OCR
{
    internal class OcrZoneChecker
    {
        private List<OcrZone> zones = new List<OcrZone>();

        private List<OcredChar> ocredCharsOdd = new List<OcredChar>();
        private List<OcredChar> ocredCharsEven = new List<OcredChar>();

        private int width;
        private int height;

        public int[,] OcrPixelMap;
	    private OcrConfiguration ocrConfig;

        public OcrZoneChecker(OcrConfiguration ocrConfig, int width, int height, List<OcrZone> zones, List<int> leftCharPositions)
        {
	        this.ocrConfig = ocrConfig;

            for (int i = 0; i < leftCharPositions.Count; i++)
            {
				int leftPos = ocrConfig.Alignment.CharPositions[i];
				ocredCharsOdd.Add(new OcredChar(i, leftPos, ocrConfig.Alignment.CharWidth, ocrConfig.Alignment.CharHeight));
				ocredCharsEven.Add(new OcredChar(i, leftPos, ocrConfig.Alignment.CharWidth, ocrConfig.Alignment.CharHeight));
            }

            this.zones.AddRange(zones);

            OcrPixelMap = new int[height, width];
            this.width = width;
            this.height = height;

            BuildOcrPixelMap();
        }

        public int CheckPixel(OcredChar currChar, int charLeft, int charTop, out int pixelId)
        {
            foreach(OcrZone zone in zones)
            {
                for (int i = 0; i < zone.Pixels.Count; i++)
                {
                    OcrZonePixel pixel = zone.Pixels[i];
                    if (pixel.X == charLeft && pixel.Y == charTop)
                    {
                        pixelId = i;
                        return zone.ZoneId;
                    }
                }
            }

            pixelId = -1;
            return -1;
        }

        private void BuildOcrPixelMap()
        {
			int OCR_LINES_FROM = ocrConfig.Alignment.FrameTopOdd;
			int OCR_LINES_TO = ocrConfig.Alignment.FrameTopEven + 2 * ocrConfig.Alignment.CharHeight;

            for(int y = 0; y < height; y++)
            {
                bool runOCR = y >= OCR_LINES_FROM && y <= OCR_LINES_TO;
                bool isOddFieldLine = (y - OCR_LINES_FROM) % 2 == 0;
                List<OcredChar> charFieldList = isOddFieldLine ? ocredCharsOdd : ocredCharsEven;
                int charIdx = 0;
                OcredChar currChar = charFieldList[0];

                for(int x = 0; x < width; x++)
                {
                    if (runOCR)
                    {
                        if (x >= currChar.LeftFrom)
                        {
                            if (x <= currChar.LeftTo)
                            {
                                int charLeft = x - currChar.LeftFrom;
                                int charTop = (y - OCR_LINES_FROM) / 2;

                                int pixelId;
                                int zoneId = CheckPixel(currChar, charLeft, charTop, out pixelId);

                                if (zoneId != -1)
                                {
                                    OcrPixelMap[y, x] = GetPackedValue(currChar.CharId, isOddFieldLine, zoneId, pixelId);
                                }
                                else
                                    OcrPixelMap[y, x] = 0;                                
                            }
                            else
                            {
                                if (charIdx < charFieldList.Count - 1)
                                {
                                    charIdx++;
                                    currChar = charFieldList[charIdx];                                    
                                }

                                OcrPixelMap[y, x] = 0;
                            }
                        }
                        else
                            OcrPixelMap[y, x] = 0;
                    }
                    else
                        OcrPixelMap[y, x] = 0;
                }
            }
        }

        public static int GetPackedValue(int charId, bool isOddField, int zoneId, int zonePixelId)
        {
            return
                (isOddField ? 0x01000000 : 0x02000000) +
                ((charId & 0xFF) << 16) +
                ((zoneId & 0xFF) << 8) +
                ((zonePixelId & 0xFF));
        }

        public static void UnpackValue(int packed, out int charId, out bool isOddField, out int zoneId, out int zonePixelId)
        {
            isOddField = (packed & 0x01000000) != 0;
            charId = (packed >> 16) & 0xFF;
            zoneId = (packed >> 8) & 0xFF;
            zonePixelId = packed & 0xFF;
        }
    }
}
