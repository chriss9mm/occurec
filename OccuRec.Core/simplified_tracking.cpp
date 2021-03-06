/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

#include "stdafx.h"
#include "simplified_tracking.h"
#include "stdio.h"
#include "stdlib.h"
#include "psf_fit.h"

#define _USE_MATH_DEFINES
#include "math.h"

#include <vector>
#include <map>
#include <algorithm>

using namespace std;


static double MAX_ELONGATION;
static double MIN_FWHM;
static double MAX_FWHM;
static double MIN_CERTAINTY;
static double MIN_GUIDING_STAR_CERTAINTY;


TrackedObject::TrackedObject(long objectId, bool isFixedAperture, bool isOccultedStar, double startingX, double startingY, double apertureInPixels)
{
	ObjectId = objectId;
	IsFixedAperture = isFixedAperture;
	IsOccultedStar = isOccultedStar;
	StartingX = startingX;
	StartingY = startingY;
	ApertureInPixels = apertureInPixels;
	CurrentPsfFit = new PsfFit(DataRange8Bit);
	CurrentPsfFit->FittingMethod = MAX_ELONGATION == 0 ? NonLinearFit : NonLinearAsymetricFit;
}

TrackedObject::~TrackedObject()
{
	if (NULL != CurrentPsfFit)
	{
		delete CurrentPsfFit;
		CurrentPsfFit = NULL;
	}	
}

void TrackedObject::NextFrame()
{
	// TODO: Do we copy the current center to last known position here?
	
	TrackingFlags = 0;
	IsLocated = false;
	IsOffScreen = false;	
}

void TrackedObject::InitialiseNewTracking()
{
	CenterXDouble = StartingX;
	CenterYDouble = StartingY;
	CenterX = (long)(StartingX + 0.5); // rounding
	CenterY = (long)(StartingY + 0.5); // rounding
	
	LastKnownGoodPositionXDouble = CenterXDouble;
	LastKnownGoodPositionYDouble = CenterYDouble;
}


void TrackedObject::SetIsTracked(bool isLocated, NotMeasuredReasons reason, double x, double y)
{
	if (isLocated)
	{
		LastKnownGoodPositionXDouble = CenterXDouble;
		LastKnownGoodPositionYDouble = CenterYDouble;
		CenterXDouble = x;
		CenterYDouble = y;
		CenterX = (long)(x + 0.5); // rounding
		CenterY = (long)(y + 0.5); // rounding
	}

	IsLocated = isLocated;
	TrackingFlags = (unsigned int)reason;
}

SimplifiedTracker::SimplifiedTracker(long width, long height, long numTrackedObjects, bool isFullDisappearance)
{
	m_Width = width;
	m_Height = height;
	m_NumTrackedObjects = numTrackedObjects;
	m_IsFullDisappearance = isFullDisappearance;
	
	m_TrackedObjects = (TrackedObject**)malloc(numTrackedObjects * sizeof(TrackedObject*));
	for(int i = 0; i < numTrackedObjects; i ++)
		m_TrackedObjects[i] = NULL;	
		
	m_AreaPixels = (unsigned long*)malloc(sizeof(unsigned long) * MAX_MATRIX_SIZE * MAX_MATRIX_SIZE);		
}

SimplifiedTracker::~SimplifiedTracker()
{
	if (NULL != m_TrackedObjects)
	{
		for(int i = 0; i < m_NumTrackedObjects; i ++)
		{
			if (NULL != m_TrackedObjects[i])
			{
				delete m_TrackedObjects[i];
				m_TrackedObjects[i] = NULL;
			}
		}		
	}
	
	if (NULL != m_AreaPixels)
	{
		delete m_AreaPixels;
		m_AreaPixels = NULL;
	}	
}

void SimplifiedTracker::ConfigureObject(long objectId, bool isFixedAperture, bool isOccultedStar, double startingX, double startingY, double apertureInPixels)
{
	if (objectId >=0 && objectId < m_NumTrackedObjects)
		m_TrackedObjects[objectId] = new TrackedObject(objectId, isFixedAperture, isOccultedStar, startingX, startingY, apertureInPixels);
}

void SimplifiedTracker::UpdatePsfFittingMethod()
{
	for (int i = 0; i < m_NumTrackedObjects; i++)
		m_TrackedObjects[i]->CurrentPsfFit->FittingMethod = MAX_ELONGATION == 0 ? NonLinearFit : NonLinearAsymetricFit;
}

void SimplifiedTracker::InitialiseNewTracking()
{
	for (int i = 0; i < m_NumTrackedObjects; i++)
		m_TrackedObjects[i]->InitialiseNewTracking();
}

bool SimplifiedTracker::IsTrackedSuccessfully()
{
	return this->m_IsTrackedSuccessfully;
}

unsigned long* SimplifiedTracker::GetPixelsArea(unsigned long* pixels, long centerX, long centerY, long squareWidth)
{
	long x0 = centerX;
	long y0 = centerY;

	long halfWidth = squareWidth / 2;

	for (long x = x0 - halfWidth; x <= x0 + halfWidth; x++)
		for (long y = y0 - halfWidth; y <= y0 + halfWidth; y++)
		{
			unsigned long pixelVal = 0;

			if (x >= 0 && x < m_Width && y >= 0 & y < m_Height)
			{
				pixelVal = *(pixels + x + y * m_Width);
			}

			*(m_AreaPixels + x - x0 + halfWidth + (y - y0 + halfWidth) * squareWidth) = pixelVal;
		}

	return m_AreaPixels;
}

unsigned long* SimplifiedTracker::GetPixelsAreaInt8(unsigned char* pixels, long centerX, long centerY, long squareWidth)
{
	long x0 = centerX;
	long y0 = centerY;

	long halfWidth = squareWidth / 2;

	for (long x = x0 - halfWidth; x <= x0 + halfWidth; x++)
		for (long y = y0 - halfWidth; y <= y0 + halfWidth; y++)
		{
			unsigned long pixelVal = 0;

			if (x >= 0 && x < m_Width && y >= 0 & y < m_Height)
			{
				pixelVal = (long)*(pixels + x + y * m_Width);
			}

			*(m_AreaPixels + x - x0 + halfWidth + (y - y0 + halfWidth) * squareWidth) = pixelVal;
		}

	return m_AreaPixels;
}

/*
unsigned long* SimplifiedTracker::GetPixelsArea(unsigned long* pixels, long centerX, long centerY, long squareWidth)
{
	long halfWidth = squareWidth / 2;
	int areaLine = 0;
	for (int y = centerY - halfWidth; y <centerY + halfWidth; y++, areaLine++)
	{
		memcpy(m_AreaPixels + (areaLine * squareWidth), pixels + (y * m_Width + centerX - halfWidth), squareWidth);
	}
	
	for (int y = 0 ; y < squareWidth; y++)
	{
		for (int x = 0; x < squareWidth; x++)
		{
			int val = *(m_AreaPixels + y * squareWidth + x) / 256;
			printf("%d", val);
		}
		printf("\n\r");
	}
	
	return m_AreaPixels;
}*/

void SimplifiedTracker::NextFrame(int frameNo, unsigned long* pixels)
{
	m_IsTrackedSuccessfully = false;

	// For each of the non manualy positioned Tracked objects do a PSF fit in the area of its previous location 
	for (int i = 0; i < m_NumTrackedObjects; i++)
	{
		TrackedObject* trackedObject = m_TrackedObjects[i];
		trackedObject->NextFrame();

		if (trackedObject->IsFixedAperture || (trackedObject->IsOccultedStar && m_IsFullDisappearance))
		{
			// Star position will be determined after the rest of the stars are found 
		}
		else
		{
			unsigned long* areaPixels = GetPixelsArea(pixels, trackedObject->CenterX, trackedObject->CenterY, 17);
			
			trackedObject->UseCurrentPsfFit = false;
			trackedObject->CurrentPsfFit->Fit(trackedObject->CenterX, trackedObject->CenterY, areaPixels, 17);

			if (trackedObject->CurrentPsfFit->IsSolved())
			{
				if (trackedObject->CurrentPsfFit->Certainty() < MIN_GUIDING_STAR_CERTAINTY)
				{
					trackedObject->SetIsTracked(false, ObjectCertaintyTooSmall, 0, 0);
				}
				else 
				if (trackedObject->CurrentPsfFit->FWHM() < MIN_FWHM || trackedObject->CurrentPsfFit->FWHM() > MAX_FWHM)
				{
					trackedObject->SetIsTracked(false, FWHMOutOfRange, 0, 0);
				}
				else if (MAX_ELONGATION > 0 && trackedObject->CurrentPsfFit->ElongationPercentage() > MAX_ELONGATION)
				{
					trackedObject->SetIsTracked(false, ObjectTooElongated, 0, 0);
				}
				else
				{
					trackedObject->UseCurrentPsfFit = true;
					trackedObject->SetIsTracked(true, TrackedSuccessfully, trackedObject->CurrentPsfFit->XCenter(), trackedObject->CurrentPsfFit->YCenter());
				}
			}
		}					
	}

	bool atLeastOneObjectLocated = false;

	for (int i = 0; i < m_NumTrackedObjects; i++)
	{
		TrackedObject* trackedObject = m_TrackedObjects[i];

		bool needsPostGuidingStarLocating = trackedObject->IsFixedAperture ||  (trackedObject->IsOccultedStar && m_IsFullDisappearance) || !trackedObject->IsLocated;
		
		if (!needsPostGuidingStarLocating && trackedObject->IsLocated)
			atLeastOneObjectLocated = true;
			
		if (!needsPostGuidingStarLocating) 
			continue;
		
		double totalX = 0;
		double totalY = 0;
		int numReferences = 0;
		
		for (int j = 0; j < m_NumTrackedObjects; j++)
		{
			TrackedObject* referenceObject = m_TrackedObjects[j];
			bool relativeReference = referenceObject->IsFixedAperture || (referenceObject->IsOccultedStar && m_IsFullDisappearance) || !trackedObject->IsLocated;
		
			if (referenceObject->IsLocated && !relativeReference)
			{
				totalX += (trackedObject->StartingX - referenceObject->StartingX) + referenceObject->CenterXDouble;
				totalY += (trackedObject->StartingY - referenceObject->StartingY) + referenceObject->CenterYDouble;
				numReferences++;
			}
		}

		if (numReferences == 0)
		{
			trackedObject->UseCurrentPsfFit = false;
			trackedObject->SetIsTracked(false, FitSuspectAsNoGuidingStarsAreLocated, 0, 0);
		}
		else
		{
			double x_double = totalX / numReferences;
			double y_double = totalY / numReferences;

			if (trackedObject->IsFixedAperture)
			{
				trackedObject->UseCurrentPsfFit = false;
				trackedObject->SetIsTracked(true, FixedObject, x_double, y_double);
			}
			else if (trackedObject->IsOccultedStar)
			{
				long x = (long)(x_double + 0.5); // rounded
				long y = (long)(y_double + 0.5); // rounded

				long matrixSize = (long)(trackedObject->ApertureInPixels * 1.5 + 0.5); // rounded
				if (matrixSize % 2 == 0) matrixSize++;
				if (matrixSize > 17) matrixSize = 17;

				unsigned long* areaPixels = GetPixelsArea(pixels, x, y, matrixSize);

				trackedObject->UseCurrentPsfFit = false;
				
				trackedObject->CurrentPsfFit->Fit(x, y, areaPixels, matrixSize);

				if (trackedObject->CurrentPsfFit->IsSolved() && trackedObject->CurrentPsfFit->Certainty() > MIN_CERTAINTY)
				{
					trackedObject->SetIsTracked(true, TrackedSuccessfully, trackedObject->CurrentPsfFit->XCenter(), trackedObject->CurrentPsfFit->YCenter());
					trackedObject->UseCurrentPsfFit = true;
				}
				else if (m_IsFullDisappearance)
					trackedObject->SetIsTracked(false, FullyDisappearingStarMarkedTrackedWithoutBeingFound, 0, 0);
				else
					trackedObject->SetIsTracked(false, ObjectCertaintyTooSmall, 0, 0);
			}
		}
	}

	m_IsTrackedSuccessfully = atLeastOneObjectLocated;
}


void SimplifiedTracker::NextFrameInt8(int frameNo, unsigned char* pixels)
{
	m_IsTrackedSuccessfully = false;

	// For each of the non manualy positioned Tracked objects do a PSF fit in the area of its previous location 
	for (int i = 0; i < m_NumTrackedObjects; i++)
	{
		TrackedObject* trackedObject = m_TrackedObjects[i];
		trackedObject->NextFrame();

		if (trackedObject->IsFixedAperture || (trackedObject->IsOccultedStar && m_IsFullDisappearance))
		{
			// Star position will be determined after the rest of the stars are found 
		}
		else
		{
			unsigned long* areaPixels = GetPixelsAreaInt8(pixels, trackedObject->CenterX, trackedObject->CenterY, 17);
			
			trackedObject->UseCurrentPsfFit = false;
			trackedObject->CurrentPsfFit->Fit(trackedObject->CenterX, trackedObject->CenterY, areaPixels, 17);

			if (trackedObject->CurrentPsfFit->IsSolved())
			{
				if (trackedObject->CurrentPsfFit->Certainty() < MIN_GUIDING_STAR_CERTAINTY)
				{
					trackedObject->SetIsTracked(false, ObjectCertaintyTooSmall, 0, 0);
				}
				else 
				if (trackedObject->CurrentPsfFit->FWHM() < MIN_FWHM || trackedObject->CurrentPsfFit->FWHM() > MAX_FWHM)
				{
					trackedObject->SetIsTracked(false, FWHMOutOfRange, 0, 0);
				}
				else if (MAX_ELONGATION > 0 && trackedObject->CurrentPsfFit->ElongationPercentage() > MAX_ELONGATION)
				{
					trackedObject->SetIsTracked(false, ObjectTooElongated, 0, 0);
				}
				else
				{
					trackedObject->UseCurrentPsfFit = true;
					trackedObject->SetIsTracked(true, TrackedSuccessfully, trackedObject->CurrentPsfFit->XCenter(), trackedObject->CurrentPsfFit->YCenter());
				}
			}
		}					
	}

	bool atLeastOneObjectLocated = false;

	for (int i = 0; i < m_NumTrackedObjects; i++)
	{
		TrackedObject* trackedObject = m_TrackedObjects[i];

		bool needsPostGuidingStarLocating = trackedObject->IsFixedAperture || (trackedObject->IsOccultedStar && m_IsFullDisappearance) || !trackedObject->IsLocated;
		
		if (!needsPostGuidingStarLocating && trackedObject->IsLocated)
			atLeastOneObjectLocated = true;
			
		if (!needsPostGuidingStarLocating) 
			continue;
		
		double totalX = 0;
		double totalY = 0;
		int numReferences = 0;
		
		for (int j = 0; j < m_NumTrackedObjects; j++)
		{
			TrackedObject* referenceObject = m_TrackedObjects[j];
			bool relativeReference = referenceObject->IsFixedAperture || (referenceObject->IsOccultedStar && m_IsFullDisappearance) || !trackedObject->IsLocated;
		
			if (referenceObject->IsLocated && !relativeReference)
			{
				totalX += (trackedObject->StartingX - referenceObject->StartingX) + referenceObject->CenterXDouble;
				totalY += (trackedObject->StartingY - referenceObject->StartingY) + referenceObject->CenterYDouble;
				numReferences++;
			}
		}

		if (numReferences == 0)
		{
			trackedObject->UseCurrentPsfFit = false;
			trackedObject->SetIsTracked(false, FitSuspectAsNoGuidingStarsAreLocated, 0, 0);
		}
		else
		{
			double x_double = totalX / numReferences;
			double y_double = totalY / numReferences;

			if (trackedObject->IsFixedAperture)
			{
				trackedObject->UseCurrentPsfFit = false;
				trackedObject->SetIsTracked(true, FixedObject, x_double, y_double);
			}
			else if (trackedObject->IsOccultedStar)
			{
				long x = (long)(x_double + 0.5); // rounded
				long y = (long)(y_double + 0.5); // rounded

				long matrixSize = (long)(trackedObject->ApertureInPixels * 1.5 + 0.5); // rounded
				if (matrixSize % 2 == 0) matrixSize++;
				if (matrixSize > 17) matrixSize = 17;

				unsigned long* areaPixels = GetPixelsAreaInt8(pixels, x, y, matrixSize);

				trackedObject->UseCurrentPsfFit = false;
				
				trackedObject->CurrentPsfFit->Fit(x, y, areaPixels, matrixSize);

				if (trackedObject->CurrentPsfFit->IsSolved() && trackedObject->CurrentPsfFit->Certainty() > MIN_CERTAINTY)
				{
					trackedObject->SetIsTracked(true, TrackedSuccessfully, trackedObject->CurrentPsfFit->XCenter(), trackedObject->CurrentPsfFit->YCenter());
					trackedObject->UseCurrentPsfFit = true;
				}
				else if (m_IsFullDisappearance)
					trackedObject->SetIsTracked(false, FullyDisappearingStarMarkedTrackedWithoutBeingFound, 0, 0);
				else
					trackedObject->SetIsTracked(false, ObjectCertaintyTooSmall, 0, 0);
			}
		}
	}

	m_IsTrackedSuccessfully = atLeastOneObjectLocated;
}

long SimplifiedTracker::TrackerGetTargetState(long objectId, NativeTrackedObjectInfo* trackingInfo, NativePsfFitInfo* psfInfo, double* residuals)
{
	if (objectId < 0 || objectId >= m_NumTrackedObjects)
		return E_FAIL;
	
	TrackedObject* obj = m_TrackedObjects[objectId];
	
	trackingInfo->CenterXDouble = obj->CenterXDouble;
	trackingInfo->CenterYDouble = obj->CenterYDouble;
	trackingInfo->LastKnownGoodPositionXDouble = obj->LastKnownGoodPositionXDouble;
	trackingInfo->LastKnownGoodPositionYDouble = obj->LastKnownGoodPositionYDouble;
	trackingInfo->IsLocated = obj->IsLocated ? 1 : 0;
	trackingInfo->IsOffScreen = obj->IsOffScreen ? 1 : 0;
	trackingInfo->TrackingFlags = obj->TrackingFlags;
	
	PsfFit* psfFit = obj->CurrentPsfFit;
	if (obj->UseCurrentPsfFit)
	{
		psfInfo->FWHM = psfFit->FWHM();
		psfInfo->I0 = psfFit->I0();
		psfInfo->IMax = psfFit->IMax();
		psfInfo->IsAsymmetric = psfFit->FittingMethod == NonLinearAsymetricFit;
		psfInfo->IsSolved = psfFit->IsSolved();
		psfInfo->MatrixSize = psfFit->MatrixSize();
		psfInfo->R0 = psfInfo->IsAsymmetric ? psfFit->RX0 : psfFit->R0;
		psfInfo->R02 = psfInfo->IsAsymmetric ? psfFit->RY0 : 0;
		psfInfo->X0 = psfFit->X0();
		psfInfo->Y0 = psfFit->Y0();
		psfInfo->XCenter = psfFit->XCenter();
		psfInfo->YCenter = psfFit->YCenter();
		
		psfFit->CopyResiduals(residuals, psfInfo->MatrixSize);
	}
}


static SimplifiedTracker* s_Tracker;

HRESULT TrackerSettings(double maxElongation, double minFWHM, double maxFWHM, double minCertainty, double minGuidingStarCertainty)
{
	MAX_ELONGATION = maxElongation;
	MIN_FWHM = minFWHM;
	MAX_FWHM = maxFWHM;
	MIN_CERTAINTY = minCertainty;
	MIN_GUIDING_STAR_CERTAINTY = minGuidingStarCertainty;
	
	if (NULL != s_Tracker)
		s_Tracker->UpdatePsfFittingMethod();
	
	return S_OK;
}

HRESULT TrackerNewConfiguration(long width, long height, long numTrackedObjects, bool isFullDisappearance)
{
	if (NULL != s_Tracker)
	{
		delete s_Tracker;
		s_Tracker = NULL;
	}
	
	s_Tracker = new SimplifiedTracker(width, height, numTrackedObjects, isFullDisappearance);
	
	return S_OK;
}

HRESULT TrackerConfigureObject(long objectId, bool isFixedAperture, bool isOccultedStar, double startingX, double startingY, double apertureInPixels)
{
	if (NULL != s_Tracker)
	{
		s_Tracker->ConfigureObject(objectId, isFixedAperture, isOccultedStar, startingX, startingY, apertureInPixels);
		return S_OK;
	}
	
	return E_POINTER;
}

HRESULT TrackerInitialiseNewTracking()
{
	if (NULL != s_Tracker)
	{
		s_Tracker->InitialiseNewTracking();
		return 0;
	}
	
	return -2;	
}

HRESULT TrackerNextFrame(long frameId, unsigned long* pixels)
{
	if (NULL != s_Tracker)
	{
		s_Tracker->NextFrame(frameId, pixels);
		return s_Tracker->IsTrackedSuccessfully() ? 0 : -1;
	}
	
	return -2;
}

HRESULT TrackerNextFrame_int8(long frameId, unsigned char* pixels)
{
	if (NULL != s_Tracker)
	{
		s_Tracker->NextFrameInt8(frameId, pixels);
		return s_Tracker->IsTrackedSuccessfully() ? 0 : -1;
	}
	
	return -2;
}

float MeasureObjectUsingAperturePhotometry(
	unsigned long* data, float aperture, 
	long nWidth, long nHeight, float x0, float y0, unsigned long saturationValue, float innerRadiusOfBackgroundApertureInSignalApertures, long numberOfPixelsInBackgroundAperture,
	float* totalPixels, bool* hasSaturatedPixels)
{
    float totalReading = 0;
    *totalPixels = 0;
	*hasSaturatedPixels = false;
	vector<unsigned long> allBackgroundReadings;

	float innerRadius = (float)(innerRadiusOfBackgroundApertureInSignalApertures * aperture);
    float outernRadius = (float)sqrt(numberOfPixelsInBackgroundAperture/M_PI + innerRadius * innerRadius);

    for (int x = 0; x < nWidth; x++)
	{
        for (int y = 0; y < nHeight; y++)
        {
            double dist = sqrt((x0 - x) * (x0 - x) + (y0 - y) * (y0 - y));
            if (dist + 1.5 <= aperture)
            {
                // If the point plus 1 pixel diagonal is still in the aperture
                // then add the reading directly

                totalReading += *(data + x + nWidth* y);

                (*totalPixels) += 1;

				if (*(data + x + nWidth* y) >= saturationValue) 
					*hasSaturatedPixels = true;
            }
            else if (dist - 1.5 <= aperture)
            {
                float subpixels = 0;

                // Represent the pixels as 5x5 subpixels with 5 times lesses intencity and then add up
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        double xx = x + dx / 5.0;
                        double yy = y + dy / 5.0;
                        dist = sqrt((x0 - xx) * (x0 - xx) + (y0 - yy) * (y0 - yy));
                        if (dist <= aperture)
                            subpixels += 1.0f / 25;
                    }

                totalReading += *(data + x + nWidth* y) * subpixels;

                (*totalPixels) += subpixels;
            }

			if (dist >= innerRadius && dist <= outernRadius)
            {
                unsigned long reading = *(data + x + nWidth* y);
				allBackgroundReadings.push_back(reading);
            }
        }
	}

	size_t n = allBackgroundReadings.size() / 2;
	std::nth_element(allBackgroundReadings.begin(), allBackgroundReadings.begin()+n, allBackgroundReadings.end());
	long medianBackground = allBackgroundReadings[n];

	return totalReading - (medianBackground *  *totalPixels);
}

float squareRoot(float x)
{
  unsigned int i = *(unsigned int*) &x;

  // adjust bias
  i  += 127 << 23;
  // approximation of square root
  i >>= 1;

  return *(float*) &i;
}

float MeasureObjectUsingAperturePhotometry_int8(
	unsigned char* data, float aperture, 
	long nWidth, long nHeight, float x0, float y0, unsigned char saturationValue, float innerRadiusOfBackgroundApertureInSignalApertures, long numberOfPixelsInBackgroundAperture,
	float* totalPixels, bool* hasSaturatedPixels)
{
    float totalReading = 0;
    *totalPixels = 0;
	*hasSaturatedPixels = false;
	vector<unsigned char> allBackgroundReadings;

	float innerRadius = (float)(innerRadiusOfBackgroundApertureInSignalApertures * aperture);
    float outernRadius = (float)sqrt(numberOfPixelsInBackgroundAperture/M_PI + innerRadius * innerRadius);

	for (int x = 0; x < nWidth; x++)
	{
        for (int y = 0; y < nHeight; y++)
        {	
			float f1 = (x0 - x) * (x0 - x) + (y0 - y) * (y0 - y);
			
            float dist = squareRoot(f1);

			if (dist + 1.5 <= aperture)
            {
                // If the point plus 1 pixel diagonal is still in the aperture
                // then add the reading directly

                totalReading += *(data + x + nWidth* y);

                (*totalPixels) += 1;

				if (*(data + x + nWidth* y) >= saturationValue) 
					*hasSaturatedPixels = true;
            }
            else if (dist - 1.5 <= aperture)
            {
                float subpixels = 0;

                // Represent the pixels as 5x5 subpixels with 5 times lesses intencity and then add up
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        float xx = x + dx / 5.0;
                        float yy = y + dy / 5.0;
                        dist = sqrt((x0 - xx) * (x0 - xx) + (y0 - yy) * (y0 - yy));
                        if (dist <= aperture)
                            subpixels += 1.0f / 25;
                    }

                totalReading += *(data + x + nWidth* y) * subpixels;

                (*totalPixels) += subpixels;
            }

			if (dist >= innerRadius && dist <= outernRadius)
            {
                unsigned char reading = *(data + x + nWidth* y);
				allBackgroundReadings.push_back(reading);
            } 
        }
	}

	size_t n = allBackgroundReadings.size() / 2;
	std::nth_element(allBackgroundReadings.begin(), allBackgroundReadings.begin()+n, allBackgroundReadings.end());
	unsigned char medianBackground = allBackgroundReadings[n];

	return totalReading - (medianBackground *  *totalPixels);
}


HRESULT TrackerGetTargetState(long objectId, NativeTrackedObjectInfo* trackingInfo, NativePsfFitInfo* psfInfo, double* residuals)
{
	if (NULL != s_Tracker)
	{
		return s_Tracker->TrackerGetTargetState(objectId, trackingInfo, psfInfo, residuals);
	}
	
	return E_POINTER;
}
