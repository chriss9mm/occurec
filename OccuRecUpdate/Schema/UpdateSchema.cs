/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace OccuRecUpdate.Schema
{
    class UpdateSchema
    {
        internal List<UpdateObject> AllUpdateObjects = new List<UpdateObject>();

        internal Schema.OccuRecUpdate OccuRecUpdate;
        internal Schema.OccuRecMainUpdate OccuRec;

        public UpdateSchema(XmlDocument xml)
        {
            foreach(XmlNode el in xml.DocumentElement.ChildNodes)
            {
                if ("OccuRecUpdate".Equals(el.Name))
                {
                    OccuRecUpdate = new Schema.OccuRecUpdate(el as XmlElement);
                    AllUpdateObjects.Add(OccuRecUpdate);
                }
                else if ("Update".Equals(el.Name))
                {
                    OccuRec = new Schema.OccuRecMainUpdate(el as XmlElement);
                    AllUpdateObjects.Add(OccuRec);
                }
                else if ("ModuleUpdate".Equals(el.Name))
                    AllUpdateObjects.Add(new Schema.ModuleUpdate(el as XmlElement));
            }
        }

        public bool NewUpdatesAvailable(string occuRecPath)
        {
            foreach (UpdateObject obj in AllUpdateObjects)
				if (obj.NewUpdatesAvailable(occuRecPath)) return true;

            return false;
        }
    }
}
