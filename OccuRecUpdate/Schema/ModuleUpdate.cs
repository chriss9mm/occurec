/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Globalization;

namespace OccuRecUpdate.Schema
{
    class ModuleUpdate : UpdateObject 
    {
        internal readonly int VerReq = 0;        
        internal readonly DateTime DateCreated = DateTime.MaxValue;
        internal readonly string m_Created = null;
        internal readonly bool m_NonEnglishOnly = false;

		//<ModuleUpdate VerReq="1" File="OccuRec.LangRes.dll" MustExist="false" Version="30000" ReleaseDate="21 Mar 2008" ModuleName="Translation Resources">
		//    <File Path="/OccuRec_3_0_0_0/OccuRec.LangRes.zip" LocalPath="OccuRec.LangRes.dll" Archived="true"/>
        //</ModuleUpdate>
        //
		//<ModuleUpdate VerReq="2" File="/Documentation/OccuRec.pdf" MustExist="false" Created="17/03/2008" ReleaseDate="21 Mar 2008" ModuleName="Documentation">
		//    <File Path="/OccuRec_3_0_0_0/OccuRec.EN.pdf" LocalPath="/Documentation/OccuRec.pdf" Action="ShellExecute">
		//      <Language Id="1" Path="/OccuRec_3_0_0_0/OccuRec.EN.pdf" />
		//      <Language Id="2" Path="/OccuRec_3_0_0_0/OccuRec.DE.pdf" />
        //    </File>
        //</ModuleUpdate>
        public ModuleUpdate(XmlElement node)
            : base(node)
        {
            VerReq = int.Parse(node.Attributes["VerReq"].Value, CultureInfo.InvariantCulture);
            if (node.Attributes["Version"] != null)
                m_Version = int.Parse(node.Attributes["Version"].Value, CultureInfo.InvariantCulture);
            else if (node.Attributes["Created"] != null)
                m_Created = node.Attributes["Created"].Value;

            m_File = node.Attributes["File"].Value;
            m_ReleaseDate = node.Attributes["ReleaseDate"].Value;
            m_ModuleName = node.Attributes["ModuleName"].Value;

            if (node.Attributes["MustExist"] != null)
                m_MustExist = Convert.ToBoolean(node.Attributes["MustExist"].Value, CultureInfo.InvariantCulture);
            else
                m_MustExist = true;

            if (node.Attributes["NonEnglishOnly"] != null)
                m_NonEnglishOnly = Convert.ToBoolean(node.Attributes["NonEnglishOnly"].Value, CultureInfo.InvariantCulture);
            else
                m_NonEnglishOnly = false;

            if (node.Attributes["VersionStr"] != null)
                m_VersionStr = node.Attributes["VersionStr"].Value;
        }

        public override bool NewUpdatesAvailable(string occuRecPath)
        {
            if (VerReq > 1 &&
                !string.IsNullOrEmpty(m_Created))
            {
				string fullLocalFileName = System.IO.Path.GetFullPath(occuRecPath + "\\" + this.File);
                if (!System.IO.File.Exists(fullLocalFileName) &&
                    !m_MustExist)
                {
                    // The file doesn't have to exist and because it actually doesn't 
                    // this is why it must be downloaded i.e. a newer version is available
                    Trace.WriteLine(string.Format("Update required for '{0}': The file is not found locally", File));
                    return true;
                }
                else
                {
                    DateTime localModifiedDate = System.IO.File.GetLastWriteTime(fullLocalFileName);
                    DateTime serverModifiedUTCDate = DateTime.Parse(m_Created, CultureInfo.InvariantCulture);

                    if (localModifiedDate.ToUniversalTime().CompareTo(serverModifiedUTCDate) < 0)
                    {
                        Trace.WriteLine(string.Format("Update required for '{0}': local last modified: {1}; server last modified: {2}", File, localModifiedDate.ToUniversalTime(), serverModifiedUTCDate));
                        return true;
                    }
                    else
                        return false;
                }
            }
            else
                return base.NewUpdatesAvailable(occuRecPath);
        }

        protected override void OnFileUpdated(Schema.File file, string localFilePath)
        {
            if (!string.IsNullOrEmpty(this.m_Created))
            {
                // Set the file time. All updates files will get the date & time of the check file, which is fine!
                DateTime utcModifiedTime = DateTime.Parse(m_Created, CultureInfo.InvariantCulture);
                System.IO.File.SetLastWriteTimeUtc(localFilePath, utcModifiedTime);
            }
        }
    }
}
