﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace OccuRec.Utilities
{
	public class ConvMatrix
	{
		public float TopLeft = 0, TopMid = 0, TopRight = 0;
		public float MidLeft = 0, Pixel = 1, MidRight = 0;
		public float BottomLeft = 0, BottomMid = 0, BottomRight = 0;
		public int Factor = 1;
		public int Offset = 0;

		public void SetAll(int nVal)
		{
			TopLeft = TopMid = TopRight = MidLeft = Pixel = MidRight = BottomLeft = BottomMid = BottomRight = nVal;
		}

		public ConvMatrix()
		{ }

		public ConvMatrix(float[,] matrix)
		{
			TopLeft = matrix[0, 0];
			TopMid = matrix[0, 1];
			TopRight = matrix[0, 2];

			MidLeft = matrix[1, 0];
			Pixel = matrix[1, 1];
			MidRight = matrix[1, 2];

			BottomLeft = matrix[2, 0];
			BottomMid = matrix[2, 1];
			BottomRight = matrix[2, 2];
		}
	}

	public class ConvMatrix5x5
	{
		public readonly float[,] Values;

		public ConvMatrix5x5(float[,] matrix)
		{
			Values = matrix;
		}
	}

	public static class Convolution
	{
		private static uint GetMaxValueForBitPix(int bitPix)
		{
			if (bitPix == 8)
				return byte.MaxValue;
			else if (bitPix == 12)
				return 4095;
			else if (bitPix == 14)
				return 16383;
			else if (bitPix == 16)
				return ushort.MaxValue;
			else
				return uint.MaxValue;
		}

		// http://www.student.kuleuven.be/~m0216922/CG/filtering.html

		public static uint[,] Conv3x3(uint[,] data, int bpp, ConvMatrix m)
		{
			// Avoid divide by zero errors
			if (0 == m.Factor)
				return data;

			uint[,] result = new uint[data.GetLength(0), data.GetLength(1)];

			int nWidth = data.GetLength(0) - 2;
			int nHeight = data.GetLength(1) - 2;

			int nPixel;

			for (int y = 0; y < nHeight; ++y)
			{
				for (int x = 0; x < nWidth; ++x)
				{
					nPixel = (int)Math.Round((((data[x, y] * m.TopLeft) +
												(data[x + 1, y] * m.TopMid) +
												(data[x + 2, y] * m.TopRight) +
												(data[x, y + 1] * m.MidLeft) +
												(data[x + 1, y + 1] * m.Pixel) +
												(data[x + 2, y + 1] * m.MidRight) +
												(data[x, y + 2] * m.BottomLeft) +
												(data[x + 1, y + 2] * m.BottomMid) +
												(data[x + 2, y + 2] * m.BottomRight))
											   / m.Factor) + m.Offset);

					uint maxValue = GetMaxValueForBitPix(bpp);

					if (nPixel < 0)
						result[x + 1, y + 1] = 0;
					else if (nPixel > maxValue)
						result[x + 1, y + 1] = maxValue;
					else
						result[x + 1, y + 1] = (uint)nPixel;
				}
			}

			return result;
		}

		public static uint[] Conv3x3(uint[] data, int bpp, int width, int height, ConvMatrix m)
		{
			uint average = 0;
			return Conv3x3(data, bpp, width, height, m, ref average, false, false);
		}

		public static uint[] Conv3x3(uint[] data, int bpp, int width, int height, ConvMatrix m, ref uint average, bool calculateAverage, bool cutEdges)
		{
			// Avoid divide by zero errors
			if (0 == m.Factor)
				return data;

			if (width * height != data.Length)
				throw new ArgumentException();

			uint[] result = new uint[cutEdges ? (width - 1) * (height - 1) : data.Length];

			uint maxValue = GetMaxValueForBitPix(bpp);

			int nPixel;
			double sum = 0;
			const double FOUR_PIXEL_FACTOR = 9.0 / 4.0;
			const double SIX_PIXEL_FACTOR = 9.0 / 6.0;

			for (int y = 0; y < height; ++y)
			{
				for (int x = 0; x < width; ++x)
				{

					if (cutEdges && (x == 0 || y == 0 || x == width - 1 || y == height - 1))
						continue;

					if (y == 0 && x == 0)
					{
						// . . .
						// . # #
						// . # #
						nPixel = (int)Math.Round((
													(data[0] * m.Pixel) +
													(data[1] * m.MidRight) +
													(data[width] * m.BottomMid) +
													(data[width + 1] * m.BottomRight)
													) * FOUR_PIXEL_FACTOR);
					}
					else if (y == height - 1 && x == 0)
					{
						// . # #
						// . # #
						// . . .
						nPixel = (int)Math.Round((
													(data[width * (height - 2)] * m.TopMid) +
													(data[width * (height - 2) + 1] * m.TopRight) +
													(data[width * (height - 1)] * m.Pixel) +
													(data[width * (height - 1) + 1] * m.MidRight)
													) * FOUR_PIXEL_FACTOR);
					}
					else if (y == 0 && x == width - 1)
					{
						// . . .
						// # # .
						// # # .
						nPixel = (int)Math.Round((
													(data[width - 2] * m.MidLeft) +
													(data[width - 1] * m.Pixel) +
													(data[2 * width - 2] * m.BottomLeft) +
													(data[2 * width - 1] * m.BottomMid)
												   ) * FOUR_PIXEL_FACTOR);
					}
					else if (y == height - 1 && x == width - 1)
					{
						// # # .
						// # # .
						// . . .
						nPixel = (int)Math.Round((
													(data[width * height - width - 1] * m.TopLeft) +
													(data[width * height - width - 2] * m.TopMid) +
													(data[width * height - 2] * m.MidLeft) +
													(data[width * height - 1] * m.Pixel)
													) * FOUR_PIXEL_FACTOR);

					}
					else if (y == 0)
					{
						// . . .
						// # # #
						// # # #
						nPixel = (int)Math.Round((
													(data[x - 1 + y * width] * m.MidLeft) +
													(data[x + y * width] * m.Pixel) +
													(data[x + 1 + y * width] * m.MidRight) +
													(data[x - 1 + (y + 1) * width] * m.BottomLeft) +
													(data[x + (y + 1) * width] * m.BottomMid) +
													(data[x + 1 + (y + 1) * width] * m.BottomRight)
													) * SIX_PIXEL_FACTOR);
					}
					else if (x == 0)
					{
						// . # #
						// . # #
						// . # #
						nPixel = (int)Math.Round((
													(data[x + (y - 1) * width] * m.TopMid) +
													(data[x + 1 + (y - 1) * width] * m.TopRight) +
													(data[x + y * width] * m.Pixel) +
													(data[x + 1 + y * width] * m.MidRight) +
													(data[x + (y + 1) * width] * m.BottomMid) +
													(data[x + 1 + (y + 1) * width] * m.BottomRight)
													) * SIX_PIXEL_FACTOR);
					}
					else if (y == height - 1)
					{
						// # # #
						// # # #
						// . . .
						nPixel = (int)Math.Round((
													(data[x - 1 + (y - 1) * width] * m.TopLeft) +
													(data[x + (y - 1) * width] * m.TopMid) +
													(data[x + 1 + (y - 1) * width] * m.TopRight) +
													(data[x - 1 + y * width] * m.MidLeft) +
													(data[x + y * width] * m.Pixel) +
													(data[x + 1 + y * width] * m.MidRight)
													) * SIX_PIXEL_FACTOR);
					}
					else if (x == width - 1)
					{
						// # # .
						// # # .
						// # # .
						nPixel = (int)Math.Round((
													(data[x - 1 + (y - 1) * width] * m.TopLeft) +
													(data[x + (y - 1) * width] * m.TopMid) +
													(data[x - 1 + y * width] * m.MidLeft) +
													(data[x + y * width] * m.Pixel) +
													(data[x - 1 + (y + 1) * width] * m.BottomLeft) +
													(data[x + (y + 1) * width] * m.BottomMid)
													) * SIX_PIXEL_FACTOR);
					}
					else
					{
						// # # #
						// # # #
						// # # #
						nPixel = (int)Math.Round(
													(data[x - 1 + (y - 1) * width] * m.TopLeft) +
													(data[x + (y - 1) * width] * m.TopMid) +
													(data[x + 1 + (y - 1) * width] * m.TopRight) +
													(data[x - 1 + y * width] * m.MidLeft) +
													(data[x + y * width] * m.Pixel) +
													(data[x + 1 + y * width] * m.MidRight) +
													(data[x - 1 + (y + 1) * width] * m.BottomLeft) +
													(data[x + (y + 1) * width] * m.BottomMid) +
													(data[x + 1 + (y + 1) * width] * m.BottomRight));
					}

					if (cutEdges)
					{
						if (nPixel < 0)
							result[(x - 1) + (y - 1) * width] = 0;
						else if (nPixel > maxValue)
							result[(x - 1) + (y - 1) * width] = maxValue;
						else
							result[(x - 1) + (y - 1) * width] = (uint)nPixel;

						if (calculateAverage)
							sum += result[(x - 1) + (y - 1) * width];
					}
					else
					{
						//if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
						//	nPixel = (int)data[x + y * width];

						if (nPixel < 0)
							result[x + y * width] = 0;
						else if (nPixel > maxValue)
							result[x + y * width] = maxValue;
						else
							result[x + y * width] = (uint)nPixel;

						if (calculateAverage)
							sum += result[x + y * width];
					}
				}
			}

			if (calculateAverage)
				average = (uint)Math.Round(sum / (width * height));

			return result;
		}
	}

	public static class BitmapFilter
	{
		// http://www.echoview.com/WebHelp/Reference/Algorithms/Operators/Convolution_algorithms.htm
		private static ConvMatrix LOW_PASS_FILTER_MATRIX =
			new ConvMatrix(new float[,]
            {
                { 1 / 16.0f, 1 / 8.0f, 1 / 16.0f }, 
                { 1 / 8.0f, 1 / 4.0f, 1 / 8.0f }, 
                { 1 / 16.0f, 1 / 8.0f, 1 / 16.0f }
            });

		private static ConvMatrix SHARPEN_MATRIX =
			new ConvMatrix(new float[,]
            {
                { -1f, -1f, -1f }, 
                { -1f,  9f, -1f }, 
                { -1f, -1f, -1f }
            });

		private static ConvMatrix DENOISE_MATRIX =
			new ConvMatrix(new float[,]
            {
                { 1/9f, 1/9f, 1/9f }, 
                { 1/9f, 1/9f, 1/9f }, 
                { 1/9f, 1/9f, 1/9f }
            });

		private static ConvMatrix MEAN_FILTER_MATRIX =
			new ConvMatrix(new float[,]
            {
                { 1, 1, 1 }, 
                { 1, 1, 1 }, 
                { 1, 1, 1 }
            });

		private static ConvMatrix MEAN_LESS_FILTER_MATRIX =
			new ConvMatrix(new float[,]
            {
                { 1 / 9.0f, 1 / 9.0f, 1 / 9.0f }, 
                { 1 / 9.0f, 1 / 9.0f, 1 / 9.0f }, 
                { 1 / 9.0f, 1 / 9.0f, 1 / 9.0f }
            });

		public static uint[] GaussianBlur(uint[] pixels, int bpp, int width, int height)
		{
			return Convolution.Conv3x3(pixels, bpp, width, height, LOW_PASS_FILTER_MATRIX);
		}

		public static uint[] Sharpen(uint[] pixels, int bpp, int width, int height, out uint average)
		{
			average = 0;
			return Convolution.Conv3x3(pixels, bpp, width, height, SHARPEN_MATRIX, ref average, true, false);
		}

		public static uint[] Denoise(uint[] pixels, int bpp, int width, int height, out uint average, bool cutEdges)
		{
			average = 0;
			return Convolution.Conv3x3(pixels, bpp, width, height, DENOISE_MATRIX, ref average, true, cutEdges);
		}

		private static uint[,] SetArrayEdgesToZero(uint[,] data)
		{
			int nWidth = data.GetLength(0);
			int nHeight = data.GetLength(1);

			for (int i = 0; i < nWidth; i++)
			{
				data[i, 0] = 0;
				data[i, nHeight - 1] = 0;
			}

			for (int j = 0; j < nHeight; j++)
			{
				data[0, j] = 0;
				data[nWidth - 1, j] = 0;
			}

			return data;
		}

		public static uint[,] CutArrayEdges(uint[,] data, int linesToCut)
		{
			int nWidth = data.GetLength(0);
			int nHeight = data.GetLength(1);

			uint[,] cutData = new uint[nWidth - 2 * linesToCut, nHeight - 2 * linesToCut];

			for (int i = linesToCut; i < nWidth - linesToCut; i++)
				for (int j = linesToCut; j < nHeight - linesToCut; j++)
				{
					cutData[i - linesToCut, j - linesToCut] = data[i, j];
				}

			return cutData;
		}

		public static uint[,] LowPassFilter(uint[,] b, int bpp, bool cutEdges)
		{
			uint[,] data = Convolution.Conv3x3(b, bpp, LOW_PASS_FILTER_MATRIX);
			if (cutEdges)
				return CutArrayEdges(data, 1);
			else
			{
				data = SetArrayEdgesToZero(data);
				return data;
			}
		}

		public static uint[,] LowPassFilter(uint[,] b, int bpp)
		{
			return Convolution.Conv3x3(b, bpp, LOW_PASS_FILTER_MATRIX);
		}

		public static uint[,] LowPassDifferenceFilter(uint[,] b, int bpp, bool cutEdges)
		{
			uint[,] data = LowPassDifferenceFilter(b, bpp, cutEdges);
			if (cutEdges)
				return CutArrayEdges(data, 1);
			else
				return data;
		}

		public static Bitmap ToVideoFields(Bitmap frameBitmap)
		{
			int width = frameBitmap.Width;
			int height = frameBitmap.Height;

			Bitmap fieldBitmap = new Bitmap(width, height, frameBitmap.PixelFormat);

			BitmapData bmData = frameBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, frameBitmap.PixelFormat);
			BitmapData bmResult = fieldBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, frameBitmap.PixelFormat);

			int stride = bmData.Stride;

			unsafe
			{
				int destRow = 0;

				for (int y = 0; y < height; y += 2)
				{
					int srcOffset = y * stride;
					int destOffset = destRow * stride;

					byte[] row = new byte[stride];
					Marshal.Copy((IntPtr)((long)(void*)bmData.Scan0 + srcOffset), row, 0, stride);
					Marshal.Copy(row, 0, (IntPtr)((long)(void*)bmResult.Scan0 + destOffset), stride);
					destRow++;
				}

				for (int y = 1; y < height; y += 2)
				{
					int srcOffset = y * stride;
					int destOffset = destRow * stride;


					byte[] row = new byte[stride];
					Marshal.Copy(new IntPtr(bmData.Scan0.ToInt32() + srcOffset), row, 0, stride);
					Marshal.Copy(row, 0, new IntPtr(bmResult.Scan0.ToInt32() + destOffset), stride);
					destRow++;
				}
			}

			frameBitmap.UnlockBits(bmData);
			fieldBitmap.UnlockBits(bmResult);

			return fieldBitmap;
		}

		private static float LO_GAMMA = 0.45f;
		private static float HI_GAMMA = 0.25f;

		private static byte[] LO_GAMMA_TABLE = new byte[256];
		private static byte[] HI_GAMMA_TABLE = new byte[256];

		private static byte[] HUE_INTENCITY_RED = new byte[256];
		private static byte[] HUE_INTENCITY_GREEN = new byte[256];
		private static byte[] HUE_INTENCITY_BLUE = new byte[256];

		static BitmapFilter()
		{
			double lowGammaMax = 255.0 / Math.Pow(255, LO_GAMMA);
			double highGammaMax = 255.0 / Math.Pow(255, HI_GAMMA);

			Color hueColor = Color.Black;

			for (int i = 0; i <= 255; i++)
			{
				double lowGammaValue = lowGammaMax * Math.Pow(i, LO_GAMMA);
				double highGammaValue = highGammaMax * Math.Pow(i, HI_GAMMA);

				LO_GAMMA_TABLE[i] = (byte)Math.Max(0, Math.Min(255, Math.Round(lowGammaValue)));
				HI_GAMMA_TABLE[i] = (byte)Math.Max(0, Math.Min(255, Math.Round(highGammaValue)));


				// HUE Table 1
				//if (i <= 47)
				//{
				//    hueColor = ColorFromAhsb(0, 160, 240, 73 + i);
				//}
				//else 
				//if (i <= 208)
				//{
				//    hueColor = ColorFromAhsb(0, 160 - (i - 48), 240, 120);
				//}
				//else
				//{
				//    hueColor = ColorFromAhsb(0, 0, 240, 120 - (i - 209));
				//}

				// HUE Table 2
				//if (i <= 16)
				//{
				//	hueColor = ColorFromAhsb(0, 160, 240, 104 + i);
				//}
				//else if (i <= 160 + 17)
				//{
				//	hueColor = ColorFromAhsb(0, 160 - (i - 17), 240, 120);
				//}
				//else
				//{
				//	hueColor = ColorFromAhsb(0, 0, 240, 120 - (i - 160 - 18));
				//}

				// HUE Table 3
				if (i <= 180)
				{
					hueColor = ColorFromAhsb(0, 160 - i, 240, 120);
				}
				else
				{
					hueColor = ColorFromAhsb(0, 0, 240, 120 - (i - 160));
				}

				HUE_INTENCITY_RED[i] = hueColor.R;
				HUE_INTENCITY_GREEN[i] = hueColor.G;
				HUE_INTENCITY_BLUE[i] = hueColor.B;
			}
		}

		public static Color ColorFromAhsb(int a, float h, float s, float b)
		{
			h = 360 * h / 240;
			s = s / 240;
			b = b / 240;

			if (0 == s)
			{
				return Color.FromArgb(a, Convert.ToInt32(b * 255),
				  Convert.ToInt32(b * 255), Convert.ToInt32(b * 255));
			}

			float fMax, fMid, fMin;
			int iSextant, iMax, iMid, iMin;

			if (0.5 < b)
			{
				fMax = b - (b * s) + s;
				fMin = b + (b * s) - s;
			}
			else
			{
				fMax = b + (b * s);
				fMin = b - (b * s);
			}

			iSextant = (int)Math.Floor(h / 60f);
			if (300f <= h)
			{
				h -= 360f;
			}
			h /= 60f;
			h -= 2f * (float)Math.Floor(((iSextant + 1f) % 6f) / 2f);
			if (0 == iSextant % 2)
			{
				fMid = h * (fMax - fMin) + fMin;
			}
			else
			{
				fMid = fMin - h * (fMax - fMin);
			}

			iMax = Convert.ToInt32(fMax * 255);
			iMid = Convert.ToInt32(fMid * 255);
			iMin = Convert.ToInt32(fMin * 255);

			switch (iSextant)
			{
				case 1:
					return Color.FromArgb(a, iMid, iMax, iMin);
				case 2:
					return Color.FromArgb(a, iMin, iMax, iMid);
				case 3:
					return Color.FromArgb(a, iMin, iMid, iMax);
				case 4:
					return Color.FromArgb(a, iMid, iMin, iMax);
				case 5:
					return Color.FromArgb(a, iMax, iMin, iMid);
				default:
					return Color.FromArgb(a, iMax, iMid, iMin);
			}
		}

        public static Bitmap CloneBitmap(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            Bitmap clone = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            CopyBitmap(bitmap, clone);

            return clone;
        }

        public static void CopyBitmap(Bitmap bitmapSrc, Bitmap bitmapDest)
        {
            int width = bitmapSrc.Width;
            int height = bitmapSrc.Height;

            BitmapData bmData = bitmapSrc.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData destData = bitmapDest.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int stride = bmData.Stride;

            unsafe
            {
                byte* p = (byte*)(void*)bmData.Scan0;
                byte* pDest = (byte*)(void*)destData.Scan0;

                int nOffset = stride - bmData.Width * 3;

                for (int y = 0; y < bmData.Height; ++y)
                {
                    for (int x = 0; x < bmData.Width; ++x)
                    {
                        pDest[0] = p[0];
                        pDest[1] = p[1];
                        pDest[2] = p[2];

                        p += 3;
                        pDest += 3;
                    }
                    p += nOffset;
                    pDest += nOffset;
                }
            }

            bitmapDest.UnlockBits(destData);
            bitmapSrc.UnlockBits(bmData);
        }

	    public static void ApplyGamma(Bitmap bitmap, bool hiGamma, bool invertAfterGamma, bool hueIntensity)
		{
			int width = bitmap.Width;
			int height = bitmap.Height;

			BitmapData bmData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

			int stride = bmData.Stride;

			byte minVal = 255;
			double sum = 0;
			int sumCount = 0;
			if (hueIntensity)
			{
				unsafe
				{
					byte* p = (byte*)(void*)bmData.Scan0;

					int nOffset = stride - bmData.Width * 3;

					for (int y = 0; y < bmData.Height; ++y)
					{
						for (int x = 0; x < bmData.Width; ++x)
						{
							if (p[0] < minVal)
								minVal = p[0];

							sum += p[0];
							sumCount++;

							p += 3;
						}
						p += nOffset;
					}
				}

				minVal = (byte)(sum / sumCount);
			}
			else
				minVal = 0;

			minVal = hiGamma ? HI_GAMMA_TABLE[minVal] : LO_GAMMA_TABLE[minVal];

			unsafe
			{
				byte* p = (byte*)(void*)bmData.Scan0;

				int nOffset = stride - bmData.Width * 3;

				for (int y = 0; y < bmData.Height; ++y)
				{
					for (int x = 0; x < bmData.Width; ++x)
					{
						p[0] = hiGamma ? HI_GAMMA_TABLE[p[0]] : LO_GAMMA_TABLE[p[0]];
						p[1] = hiGamma ? HI_GAMMA_TABLE[p[1]] : LO_GAMMA_TABLE[p[1]];
						p[2] = hiGamma ? HI_GAMMA_TABLE[p[2]] : LO_GAMMA_TABLE[p[2]];

						if (invertAfterGamma)
						{
							p[0] = (byte)(Math.Min(255, 255 - p[0]));
							p[1] = (byte)(Math.Min(255, 255 - p[1]));
							p[2] = (byte)(Math.Min(255, 255 - p[2]));
						}

						if (hueIntensity)
						{
							if (invertAfterGamma)
							{
								p[2] = HUE_INTENCITY_RED[Math.Min(255, p[2] + minVal)];
								p[1] = HUE_INTENCITY_GREEN[Math.Min(255, p[1] + minVal)];
								p[0] = HUE_INTENCITY_BLUE[Math.Min(255, p[0] + minVal)];
							}
							else
							{
								p[2] = HUE_INTENCITY_RED[Math.Max(0, p[2] - minVal)];
								p[1] = HUE_INTENCITY_GREEN[Math.Max(0, p[1] - minVal)];
								p[0] = HUE_INTENCITY_BLUE[Math.Max(0, p[0] - minVal)];
							}
						}
						p += 3;
					}
					p += nOffset;
				}
			}

			bitmap.UnlockBits(bmData);

		}

		public static void ProcessInvertAndHueIntensity(Bitmap bitmap, bool invert, bool hueIntensity)
		{
			int width = bitmap.Width;
			int height = bitmap.Height;

			BitmapData bmData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

			int stride = bmData.Stride;

			byte minVal = 255;
			double sum = 0;
			int sumCount = 0;
			if (hueIntensity)
			{
				unsafe
				{
					byte* p = (byte*)(void*)bmData.Scan0;

					int nOffset = stride - bmData.Width * 3;

					for (int y = 0; y < bmData.Height; ++y)
					{
						for (int x = 0; x < bmData.Width; ++x)
						{
							if (p[0] < minVal)
								minVal = p[0];

							sum += p[0];
							sumCount++;

							p += 3;
						}
						p += nOffset;
					}
				}

				minVal = (byte)(sum / sumCount);
			}
			else
				minVal = 0;

			unsafe
			{
				byte* p = (byte*)(void*)bmData.Scan0;

				int nOffset = stride - bmData.Width * 3;

				for (int y = 0; y < bmData.Height; ++y)
				{
					for (int x = 0; x < bmData.Width; ++x)
					{
						if (invert)
						{
							p[0] = (byte)(Math.Min(255, 255 - p[0]));
							p[1] = (byte)(Math.Min(255, 255 - p[1]));
							p[2] = (byte)(Math.Min(255, 255 - p[2]));
						}

						if (hueIntensity)
						{
							if (invert)
							{
								p[2] = HUE_INTENCITY_RED[Math.Min(255, p[2] + minVal)];
								p[1] = HUE_INTENCITY_GREEN[Math.Min(255, p[1] + minVal)];
								p[0] = HUE_INTENCITY_BLUE[Math.Min(255, p[0] + minVal)];
							}
							else
							{
								p[2] = HUE_INTENCITY_RED[Math.Max(0, p[2] - minVal)];
								p[1] = HUE_INTENCITY_GREEN[Math.Max(0, p[1] - minVal)];
								p[0] = HUE_INTENCITY_BLUE[Math.Max(0, p[0] - minVal)];
							}
						}

						p += 3;
					}
					p += nOffset;
				}
			}

			bitmap.UnlockBits(bmData);

		}

		public delegate void BitmapPixelOperationCallback(int x, int y, byte r, byte g, byte b);

		public static void BitmapPixelOperation(Bitmap bitmap, BitmapPixelOperationCallback operation)
		{
			int width = bitmap.Width;
			int height = bitmap.Height;

			BitmapData bmData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

			int stride = bmData.Stride;

			unsafe
			{
				byte* p = (byte*)(void*)bmData.Scan0;

				int nOffset = stride - bmData.Width * 3;

				for (int y = 0; y < bmData.Height; ++y)
				{
					for (int x = 0; x < bmData.Width; ++x)
					{
						operation(x, y, p[2], p[1], p[0]);

						p += 3;
					}
					p += nOffset;
				}
			}

			bitmap.UnlockBits(bmData);
		}
	}
}
