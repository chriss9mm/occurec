// AAVRec.Core.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"

#include "recording_buffer.h"

#include "AAVRec.Core.h"
#include "stdlib.h"
#include <vector>
#include <stdio.h>
#include "IntegratedFrame.h";
#include "aav_lib.h"
#include <windows.h>
#include <process.h>

#include "IotaVtiOcr.h"

#define MAX_INTEGRATION 256

long IMAGE_WIDTH;
long IMAGE_HEIGHT;
long IMAGE_STRIDE;
long IMAGE_TOTAL_PIXELS;
long MONOCHROME_CONVERSION_MODE;

bool FLIP_VERTICALLY;
bool FLIP_HORIZONTALLY;

bool IS_INTEGRATING_CAMERA;
float SIGNATURE_DIFFERENCE_FACTOR;
float MINIMUM_SIGNATURE_DIFFERENCE;

bool INTEGRATION_LOCKED;

unsigned char* prtPreviousDiffArea = NULL;
__int64 numberOfDiffSignaturesCalculated; 
long numberOfIntegratedFrames;

double* integratedPixels = NULL;

__int64 idxFrameNumber = 0;
__int64 idxFirstFrameNumber = 0;
__int64 idxLastFrameNumber = 0;
__int64 idxFirstFrameTimestamp = 0;
__int64 idxLastFrameTimestamp = 0;
__int64 idxIntegratedFrameNumber = 0;

unsigned char* latestIntegratedFrame = NULL;
ImageStatus latestImageStatus;
ImageStatus latestDetectedIntegrationFrameImageStatus;

HANDLE hRecordingThread = NULL;
bool recording = false;
char cameraModel[128];

float pastSignaturesAverage = 0;
float pastSignaturesSum = 0;
float pastSignaturesResidualSquareSum = 0;
float pastSignaturesSigma = 0;
int pastSignaturesCount = 0;
float pastSignatures[MAX_INTEGRATION];
float newIntegrationPeriodCutOffRatio;
float currentSignatureRatio;


void DebugViewPrint(const wchar_t* formatText, ...)
{
	wchar_t debug512CharBuffer[512];
    va_list args;
    va_start(args, formatText);
	vswprintf(debug512CharBuffer, 512, formatText, args);
    
	OutputDebugString(debug512CharBuffer);
	va_end(args);
}

void ClearResourses()
{
	if (NULL != prtPreviousDiffArea)
	{
		delete prtPreviousDiffArea;
		prtPreviousDiffArea = NULL;
	}

	if (NULL != integratedPixels)
	{
		delete integratedPixels;
		integratedPixels = NULL;
	}
}

HRESULT LockIntegration(bool lock)
{
	INTEGRATION_LOCKED = lock;

	return S_OK;
}

bool IsNewIntegrationPeriod(float diffSignature)
{
	if (!IS_INTEGRATING_CAMERA)
		return true;

	float diff = abs(pastSignaturesAverage - diffSignature);

	bool isNewIntegrationPeriod = 
		pastSignaturesCount >= MAX_INTEGRATION || 
		(pastSignaturesCount > 1 && diff > MINIMUM_SIGNATURE_DIFFERENCE && diff > SIGNATURE_DIFFERENCE_FACTOR * pastSignaturesSigma);

	DebugViewPrint(L"PSC:%d DF:%.5f D:%.5f %.5f SM:%.3f AVG:%.5f RSSM:%.5f SGM:%.5f\n", pastSignaturesCount, diffSignature, diff, SIGNATURE_DIFFERENCE_FACTOR * pastSignaturesSigma, pastSignaturesSum, pastSignaturesAverage, pastSignaturesResidualSquareSum, pastSignaturesSigma); 

	if (pastSignaturesCount < MAX_INTEGRATION && pastSignaturesCount > 1)
		currentSignatureRatio = diff / (SIGNATURE_DIFFERENCE_FACTOR * pastSignaturesSigma);
	else
		currentSignatureRatio = 0;

	if (isNewIntegrationPeriod)
	{
		pastSignaturesAverage = 0;
		pastSignaturesCount = 0;
		pastSignaturesSum = 0;
		pastSignaturesResidualSquareSum = 0;
		pastSignaturesSigma = 0;

		newIntegrationPeriodCutOffRatio = diff / (SIGNATURE_DIFFERENCE_FACTOR * pastSignaturesSigma);

		return true;
	}
	else
	{
		pastSignaturesSum+=diffSignature;
		pastSignaturesCount++;
		pastSignatures[pastSignaturesCount - 1] = diffSignature;
		pastSignaturesAverage = pastSignaturesSum / pastSignaturesCount;

		pastSignaturesResidualSquareSum = 0;

		for (int i=0; i<pastSignaturesCount; i++)
		{
			pastSignaturesResidualSquareSum += (pastSignaturesAverage - pastSignatures[i]) * (pastSignaturesAverage - pastSignatures[i]);
		}
		
		pastSignaturesSigma = sqrt(pastSignaturesResidualSquareSum) / pastSignaturesCount;

		return false;
	}
}


HRESULT SetupCamera(long width, long height, LPCTSTR szCameraModel, long monochromeConversionMode, bool flipHorizontally, bool flipVertically, bool isIntegrating, float signDiffFactor, float minSignDiff)
{
	// TODO: szCameraModel - should end up in the ADV  header

	IMAGE_WIDTH = width;
	IMAGE_HEIGHT = height;
	IMAGE_TOTAL_PIXELS = width * height;
	IMAGE_STRIDE = width * 3;

	MONOCHROME_CONVERSION_MODE = monochromeConversionMode;

	FLIP_VERTICALLY = flipVertically;
	FLIP_HORIZONTALLY = flipHorizontally;

	IS_INTEGRATING_CAMERA = isIntegrating;
	SIGNATURE_DIFFERENCE_FACTOR = signDiffFactor;
	MINIMUM_SIGNATURE_DIFFERENCE= minSignDiff;

	INTEGRATION_LOCKED = false;

	ClearResourses();

	prtPreviousDiffArea = (unsigned char*)malloc(IMAGE_TOTAL_PIXELS);
	::ZeroMemory(prtPreviousDiffArea, IMAGE_TOTAL_PIXELS);

	integratedPixels = (double*)malloc(IMAGE_TOTAL_PIXELS * sizeof(double));
	::ZeroMemory(integratedPixels, IMAGE_TOTAL_PIXELS * sizeof(double));

	latestIntegratedFrame = (unsigned char*)malloc(IMAGE_TOTAL_PIXELS);
	::ZeroMemory(latestIntegratedFrame, IMAGE_TOTAL_PIXELS);

	idxFrameNumber = 0;
	numberOfDiffSignaturesCalculated = 0;
	numberOfIntegratedFrames = 0;
	idxIntegratedFrameNumber = 0;

	latestImageStatus.UniqueFrameNo = 0;

	strcpy(&cameraModel[0], (char *)szCameraModel);

	recording = false;

	ghMutex = CreateMutex( NULL, FALSE, NULL); 
	
	DebugViewPrint(L"SetupCamera(SIGNATURE_DIFFERENCE_FACTOR = %.2f; INTEGRATION_LOCKED = %d)\n", SIGNATURE_DIFFERENCE_FACTOR, INTEGRATION_LOCKED); 

	return S_OK;
}

HRESULT GetCurrentImageStatus(ImageStatus* imageStatus)
{
	imageStatus->CountedFrames = latestImageStatus.CountedFrames;
	imageStatus->StartExposureFrameNo = latestImageStatus.StartExposureFrameNo;
	imageStatus->StartExposureTicks = latestImageStatus.StartExposureTicks;
	imageStatus->EndExposureFrameNo = latestImageStatus.EndExposureFrameNo;
	imageStatus->EndExposureTicks = latestImageStatus.EndExposureTicks;
	imageStatus->IntegratedFrameNo = latestImageStatus.IntegratedFrameNo;
	imageStatus->CutOffRatio = latestImageStatus.CutOffRatio;
	imageStatus->UniqueFrameNo = latestImageStatus.UniqueFrameNo;

	return S_OK;
}

HRESULT GetCurrentImage(BYTE* bitmapPixels)
{
	unsigned char* prtPixels = latestIntegratedFrame;

	// define the bitmap information header 
	BITMAPINFOHEADER bih;
	bih.biSize = sizeof(BITMAPINFOHEADER); 
	bih.biPlanes = 1; 
	bih.biBitCount = 24;                          // 24-bit 
	bih.biCompression = BI_RGB;                   // no compression 
	bih.biSizeImage = IMAGE_WIDTH * abs(IMAGE_HEIGHT) * 3;    // width * height * (RGB bytes) 
	bih.biXPelsPerMeter = 0; 
	bih.biYPelsPerMeter = 0; 
	bih.biClrUsed = 0; 
	bih.biClrImportant = 0; 
	bih.biWidth = IMAGE_WIDTH;                          // bitmap width 
	if (FLIP_VERTICALLY)
		bih.biHeight = -IMAGE_HEIGHT;                        // bitmap height 
	else
		bih.biHeight = IMAGE_HEIGHT;

	// and BitmapInfo variable-length UDT
	BYTE memBitmapInfo[40];
	RtlMoveMemory(memBitmapInfo, &bih, sizeof(bih));

	BITMAPFILEHEADER bfh;
	bfh.bfType=19778;    //BM header
	bfh.bfSize=55 + bih.biSizeImage;
	bfh.bfReserved1=0;
	bfh.bfReserved2=0;
	bfh.bfOffBits=sizeof(BITMAPINFOHEADER) + sizeof(BITMAPFILEHEADER); //54

	// Copy the display bitmap including the header
	RtlMoveMemory(bitmapPixels, &bfh, sizeof(bfh));
	RtlMoveMemory(bitmapPixels + sizeof(bfh), &memBitmapInfo, sizeof(memBitmapInfo));

	bitmapPixels = bitmapPixels + sizeof(bfh) + sizeof(memBitmapInfo);

	long currLinePos = 0;
	int length = IMAGE_WIDTH * IMAGE_HEIGHT;
	
	bitmapPixels += 3 * (length + IMAGE_WIDTH);

	if (FLIP_HORIZONTALLY)
		bitmapPixels -= 3 * IMAGE_WIDTH + 3;

	int total = IMAGE_WIDTH * IMAGE_HEIGHT;
	while(total--)
	{
		if (currLinePos == 0) 
		{
			currLinePos = IMAGE_WIDTH;

			if (!FLIP_HORIZONTALLY)
				bitmapPixels -= 6 * IMAGE_WIDTH;
		};

		unsigned char val = *prtPixels;
		prtPixels++;

		BYTE btVal = (BYTE)(val & 0xFF);
		
		*bitmapPixels = btVal;
		*(bitmapPixels + 1) = btVal;
		*(bitmapPixels + 2) = btVal;

		if (FLIP_HORIZONTALLY)
			bitmapPixels-=3; 
		else
			bitmapPixels+=3;

		currLinePos--;
	}

	return S_OK;
}

float CalculateDiffSignature(unsigned char* bmpBits)
{
	numberOfDiffSignaturesCalculated++;

	unsigned char* ptrBuf = bmpBits + IMAGE_STRIDE * (IMAGE_HEIGHT / 2 - 1) + (3 * (IMAGE_WIDTH / 2));

	unsigned char* ptrPrevPixels = prtPreviousDiffArea + (numberOfDiffSignaturesCalculated % 2 == 0 ? IMAGE_WIDTH * 3 : 0);
	unsigned char* ptrThisPixels = prtPreviousDiffArea + (numberOfDiffSignaturesCalculated % 2 == 1 ? IMAGE_WIDTH * 3 : 0);

	float signature = 0;

	for(int y = 0; y < 32; y++)
	{
		for(int x = 0; x < 32; x++)
		{
			*ptrThisPixels = *ptrBuf;

			signature += abs((float)*ptrThisPixels - (float)*ptrPrevPixels) / 2.0;

			ptrThisPixels++;
			ptrPrevPixels++;
			ptrBuf+=3;
		}

		ptrBuf+= IMAGE_STRIDE - (32 * 3);
	}

	return signature / 1024.0;
}

long BufferNewIntegratedFrame(bool isNewIntegrationPeriod)
{
	// TODO: Prepare a new IntegratedFrame object, copy the averaged integratedPixels, set the start/end frame and timestamp, put in the buffer for recording.
	// TODO: Have a second copy for display??
	long numItems = 0;

	idxIntegratedFrameNumber++;

	if (numberOfIntegratedFrames == 0)
	{
		::ZeroMemory(latestIntegratedFrame, IMAGE_TOTAL_PIXELS);
	}
	else
	{
		IntegratedFrame* frame = new IntegratedFrame(IMAGE_TOTAL_PIXELS);

		double* ptrPixels = integratedPixels;
		unsigned char* ptr8BitPixels = latestIntegratedFrame;

		unsigned char* ptrFramePixels = frame->Pixels;

		for (int i=0; i < IMAGE_TOTAL_PIXELS; i++)
		{
			long averageValue = (long)(*ptrPixels / (INTEGRATION_LOCKED ? numberOfIntegratedFrames : 1));

			if (averageValue <= 0)
			{
				*ptr8BitPixels = 0;
				*ptrFramePixels = 0;
			}
			else if (averageValue >= 255)
			{
				*ptr8BitPixels = 255;
				*ptrFramePixels = 255;
			}
			else
			{
				*ptr8BitPixels = averageValue;
				*ptrFramePixels = averageValue;
			}

			ptr8BitPixels++;
			ptrFramePixels++;
			ptrPixels++;
		}

		MarkTimeStampAreas(latestIntegratedFrame);

		if (INTEGRATION_LOCKED || isNewIntegrationPeriod)
		{
			latestImageStatus.CountedFrames = latestDetectedIntegrationFrameImageStatus.CountedFrames = numberOfIntegratedFrames;
			latestImageStatus.StartExposureFrameNo = latestDetectedIntegrationFrameImageStatus.StartExposureFrameNo = idxFirstFrameNumber;
			latestImageStatus.StartExposureTicks = latestDetectedIntegrationFrameImageStatus.StartExposureTicks = idxFirstFrameTimestamp;
			latestImageStatus.EndExposureFrameNo = latestDetectedIntegrationFrameImageStatus.EndExposureFrameNo = idxLastFrameTimestamp;
			latestImageStatus.EndExposureTicks = latestDetectedIntegrationFrameImageStatus.EndExposureTicks = numberOfIntegratedFrames;
			latestImageStatus.CutOffRatio = latestDetectedIntegrationFrameImageStatus.CutOffRatio = newIntegrationPeriodCutOffRatio;
			latestImageStatus.IntegratedFrameNo = latestDetectedIntegrationFrameImageStatus.IntegratedFrameNo = idxIntegratedFrameNumber;
		}
		else
		{
			// Copy the values from the previous 'REAL' detected integration frame			
			latestImageStatus.CountedFrames = latestDetectedIntegrationFrameImageStatus.CountedFrames;
			latestImageStatus.StartExposureFrameNo = latestDetectedIntegrationFrameImageStatus.StartExposureFrameNo;
			latestImageStatus.StartExposureTicks = latestDetectedIntegrationFrameImageStatus.StartExposureTicks;
			latestImageStatus.EndExposureFrameNo = latestDetectedIntegrationFrameImageStatus.EndExposureFrameNo;
			latestImageStatus.EndExposureTicks = latestDetectedIntegrationFrameImageStatus.EndExposureTicks;
			latestImageStatus.CutOffRatio = latestDetectedIntegrationFrameImageStatus.CutOffRatio;
			latestImageStatus.IntegratedFrameNo = latestDetectedIntegrationFrameImageStatus.IntegratedFrameNo;
		}

		latestImageStatus.UniqueFrameNo++;

		if (recording)
		{
			frame->NumberOfIntegratedFrames = numberOfIntegratedFrames;
			frame->StartFrameId = idxFirstFrameNumber;
			frame->EndFrameId = idxLastFrameNumber;
			frame->StartTimeStamp = idxFirstFrameTimestamp;
			frame->EndTimeStamp = idxLastFrameTimestamp;
			frame->FrameNumber = idxIntegratedFrameNumber;

			numItems = AddFrameToRecordingBuffer(frame);
		}
		else
		{
			delete frame;
		}

		return numItems;
	}
}

HRESULT ProcessVideoFrame(LPVOID bmpBits, __int64 currentUtcDayAsTicks, FrameProcessingStatus* frameInfo)
{
	frameInfo->FrameDiffSignature = 0;

	unsigned char* buf = reinterpret_cast<unsigned char*>(bmpBits);

	float diffSignature = CalculateDiffSignature(buf);

	idxFrameNumber++;

	bool isNewIntegrationPeriod = IsNewIntegrationPeriod(diffSignature);

	if (isNewIntegrationPeriod || !INTEGRATION_LOCKED)
	{
		BufferNewIntegratedFrame(isNewIntegrationPeriod);
		::ZeroMemory(integratedPixels, IMAGE_TOTAL_PIXELS * sizeof(double));

		if (isNewIntegrationPeriod)
		{
			numberOfIntegratedFrames = 0;

			idxFirstFrameNumber = idxFrameNumber;
			idxFirstFrameTimestamp = currentUtcDayAsTicks;
			idxLastFrameNumber = 0;
			idxLastFrameTimestamp = 0;
		}
	}

	frameInfo->FrameDiffSignature  = diffSignature;
	frameInfo->CurrentSignatureRatio  = currentSignatureRatio;

	long stride = 3 * IMAGE_WIDTH;
	unsigned char* ptrPixelItt = buf + (IMAGE_HEIGHT - 1) * IMAGE_STRIDE;

	double* ptrPixels = integratedPixels;
	
	for (int y = 0; y < IMAGE_HEIGHT; y++)
	{
		for (int x = 0; x < IMAGE_WIDTH; x++)
		{
			if (MONOCHROME_CONVERSION_MODE == 0)
				*ptrPixels += *(ptrPixelItt + 2); //R
			else if (MONOCHROME_CONVERSION_MODE == 1)
				*ptrPixels += *(ptrPixelItt + 1); //G
			else if (MONOCHROME_CONVERSION_MODE == 2)
				*ptrPixels += *(ptrPixelItt); //B
			else if (MONOCHROME_CONVERSION_MODE == 3)
			{
				// YUV Conversion (PAL & NTSC)
				// Luma = 0.299 R + 0.587 G + 0.114 B
				double luma = 0.299* *(ptrPixelItt) + 0.587* *(ptrPixelItt + 1) + 0.114* *(ptrPixelItt + 2);

				if (luma < 0)
					*ptrPixels += 0;
				else if (luma > 255)
					*ptrPixels += 255;
				else
					*ptrPixels += (unsigned char)luma;
			}

			ptrPixels++;
			ptrPixelItt+=3;
		}

		ptrPixelItt = ptrPixelItt - 2 * IMAGE_STRIDE;
	}

	numberOfIntegratedFrames++;

	idxLastFrameNumber = idxFrameNumber;
	idxLastFrameTimestamp = currentUtcDayAsTicks;

	frameInfo->CameraFrameNo = idxFrameNumber;
	frameInfo->IntegratedFrameNo = idxIntegratedFrameNumber;
	frameInfo->IntegratedFramesSoFar = numberOfIntegratedFrames;

	return S_OK;
}

void ProcessCurrentFrame(IntegratedFrame* nextFrame)
{
	// TODO: Use OCR to read the HH:MM:SS.FFF
	long hour = 0;
	long minute = 0;
	long sec = 0;
	long millisec = 0;

	long long timeStamp = DateTimeToAavTicks((nextFrame->StartTimeStamp + nextFrame->EndTimeStamp) / 2, hour, minute, sec, millisec * 10);
	unsigned int exposureIn10thMilliseconds = (nextFrame->EndTimeStamp - nextFrame->StartTimeStamp) / 1000;

	unsigned int elapsedTimeMilliseconds = 0; // since the first recorded frame was taken

	bool frameStartedOk = AavBeginFrame(timeStamp, elapsedTimeMilliseconds, exposureIn10thMilliseconds);

	AavFrameAddImage(1, nextFrame->Pixels);
	
	AavEndFrame();
}

void RecorderThreadProc( void* pContext )
{
	while(recording)
	{
		IntegratedFrame* nextFrame = FetchFrameFromRecordingBuffer();

		if (nextFrame != NULL)
		{
			unsigned char* dataToSave;

			ProcessCurrentFrame(nextFrame);

			delete nextFrame;
		}
	};

    _endthread();
}

HRESULT StartRecording(LPCTSTR szFileName)
{
	AavNewFile((const char*)szFileName);		
	
	AavAddFileTag("AAVR-SOFTWARE-VERSION", "1.0");
	AavAddFileTag("RECORDER", "ASTRO ANALOGUE VIDEO");
	AavAddFileTag("FSTF-TYPE", "AAV");
	AavAddFileTag("AAV-VERSION", "1");

	AavAddFileTag("CAMERA-MODEL", cameraModel);

	AavDefineImageSection(IMAGE_WIDTH, IMAGE_HEIGHT);
	
	AavDefineImageLayout(1, "FULL-IMAGE-RAW", "UNCOMPRESSED", 8, 0, NULL);
	AavDefineImageLayout(2, "FULL-IMAGE-DIFFERENTIAL-CODING", "QUICKLZ", 8, 32, "PREV-FRAME");
	AavDefineImageLayout(3, "FULL-IMAGE-RAW", "QUICKLZ", 8, 0, NULL);


	ClearRecordingBuffer();

	recording = true;

	// Create a new thread
	hRecordingThread = (HANDLE)_beginthread(RecorderThreadProc, 0, NULL);

	return S_OK;
}

HRESULT StopRecording(long* pixels)
{
	recording = false;

	WaitForSingleObject(hRecordingThread, INFINITE); // wait for thread to exit

	AavEndFile();

	return S_OK;
}