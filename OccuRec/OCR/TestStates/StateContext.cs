﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OccuRec.OCR.TestStates
{
    public enum TestFrameResult
    {
        Undefined = 0,
        Okay = 1,
        ErrorSaveScreenShotImages = 2
    }

    public class StateContext
    {
        private StateBase currentState;

        public OsdFrameInfo LastTimeStamp;

        public StateContext()
        {
            Reset();
        }

        public void Reset()
        {
            currentState = UncalibratedState.Instance;
            currentState.Reset(this);

            LastTimeStamp = null;
        }

        public void TransitionToState(StateBase newState)
        {
            currentState = newState;
            newState.Reset(this);
        }

        public TestFrameResult TestTimeStamp(OsdFrameInfo frameTimestamp, bool ts)
        {
            TestFrameResult suggestedResult = currentState.TestTimeStamp(this, frameTimestamp);

            if (!frameTimestamp.FrameInfoIsOk() || suggestedResult == TestFrameResult.ErrorSaveScreenShotImages)
            {
                return TestFrameResult.ErrorSaveScreenShotImages;
            }
            else
                return suggestedResult;
        }
    }
}
