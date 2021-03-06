﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OccuRec.Helpers
{
    public class UIThreadCaller
    {
        public delegate void CallInUIThreadCallback(IWin32Window applicationWindow, params object[] additionalParams);

        public static void InvokeOnNewMessageLoop(CallInUIThreadCallback action, params object[] additionalParams)
        {
            if (!hasOwnMessageLoop)
            {
                syncContext = null;
                ThreadPool.QueueUserWorkItem(RunAppThread);
                while (syncContext == null) Thread.Sleep(10);
            }

            if (syncContext != null)
            {
                bool callFinished = false;
                syncContext.Post(new SendOrPostCallback(delegate(object state)
                {
                    action.Invoke(null, additionalParams);
                    callFinished = true;
                }), null);
                while (!callFinished) Thread.Sleep(10);
            }
            else
            {
                action.Invoke(null, additionalParams);
            }
        }

        public static void Invoke(CallInUIThreadCallback action, params object[] additionalParams)
        {
            Form appFormWithMessageLoop = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x != null && x.Owner == null);

            if (appFormWithMessageLoop != null && (Application.MessageLoop || hasOwnMessageLoop))
            {
                if (appFormWithMessageLoop.InvokeRequired)
                {
                    appFormWithMessageLoop.Invoke(action, appFormWithMessageLoop, additionalParams);
                }
                else
                {
                    action.Invoke(appFormWithMessageLoop, additionalParams);
                }
            }
            else if (!Application.MessageLoop && syncContext == null)
            {
                if (syncContext == null)
                {
                    ThreadPool.QueueUserWorkItem(RunAppThread);
                    while (syncContext == null) Thread.Sleep(10);
                }

                if (syncContext != null)
                {
                    bool callFinished = false;
                    syncContext.Post(new SendOrPostCallback(delegate(object state)
                    {
                        action.Invoke(appFormWithMessageLoop != null && !appFormWithMessageLoop.InvokeRequired ? appFormWithMessageLoop : null, additionalParams);
                        callFinished = true;
                    }), null);
                    while (!callFinished) Thread.Sleep(10);
                }
                else
                {
                    action.Invoke(appFormWithMessageLoop != null && !appFormWithMessageLoop.InvokeRequired ? appFormWithMessageLoop : null, additionalParams);
                }
            }
            else
            {
                if (syncContext == null)
                {
                    syncContext = new WindowsFormsSynchronizationContext();
                }

                if (syncContext != null)
                {
                    bool callFinished = false;
                    syncContext.Post(new SendOrPostCallback(delegate(object state)
                    {
                        action.Invoke(appFormWithMessageLoop != null && !appFormWithMessageLoop.InvokeRequired ? appFormWithMessageLoop : null, additionalParams);
                        callFinished = true;
                    }), null);
                    while (!callFinished) Thread.Sleep(10);
                }
                else
                {
                    action.Invoke(appFormWithMessageLoop != null && !appFormWithMessageLoop.InvokeRequired ? appFormWithMessageLoop : null, additionalParams);
                }
            }
        }

        private static WindowsFormsSynchronizationContext syncContext;
        private static bool hasOwnMessageLoop = false;

        private static void RunAppThread(object state)
        {
            var ownMessageLoopMainForm = new Form();
            ownMessageLoopMainForm.ShowInTaskbar = false;
            ownMessageLoopMainForm.Width = 0;
            ownMessageLoopMainForm.Height = 0;
            ownMessageLoopMainForm.Load += ownerForm_Load;

            Application.Run(ownMessageLoopMainForm);

            if (syncContext != null)
            {
                syncContext.Dispose();
                syncContext = null;
            }
        }

        static void ownerForm_Load(object sender, EventArgs e)
        {
            Form form = (Form)sender;
            form.Left = -5000;
            form.Top = -5000;
            form.Hide();

            syncContext = new WindowsFormsSynchronizationContext();

            hasOwnMessageLoop = true;
        }
    }
}
