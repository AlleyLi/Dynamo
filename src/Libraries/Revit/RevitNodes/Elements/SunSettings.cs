﻿using System;

using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using Autodesk.Revit.DB;

using Revit.GeometryConversion;

using Point = Autodesk.DesignScript.Geometry.Point;

namespace Revit.Elements
{
    public class SunSettings : Element
    {
        [IsVisibleInDynamoLibrary(false)]
        public static SunSettings Current()
        {
            return new SunSettings(Document.ActiveView.SunAndShadowSettings);
        }

        private SunSettings(SunAndShadowSettings settings)
        {
            InternalSunAndShadowSettings = settings;
            InternalElementId = settings.Id;
            InternalUniqueId = settings.UniqueId;
            IsRevitOwned = true;
        }

        internal SunAndShadowSettings InternalSunAndShadowSettings { get; private set; }

        public override Autodesk.Revit.DB.Element InternalElement
        {
            get { return InternalSunAndShadowSettings; }
        }

        /// <summary>
        ///     Calculates the direction of the sun.
        /// </summary>
        public Vector SunDirection
        {
            get
            {
                var initialDirection = Vector.YAxis();
                var altitude =
                    InternalSunAndShadowSettings.GetFrameAltitude(
                        InternalSunAndShadowSettings.ActiveFrame);
                var altitudeRotation = CoordinateSystem.Identity()
                    .Rotate(Point.Origin(), Vector.XAxis(), altitude.ToDegrees());
                var altitudeDirection = initialDirection.Transform(altitudeRotation);

                var azimuth =
                    InternalSunAndShadowSettings.GetFrameAzimuth(
                        InternalSunAndShadowSettings.ActiveFrame);
                var actualAzimuth = 2*Math.PI - azimuth;
                
                var azimuthRotation = CoordinateSystem.Identity()
                    .Rotate(Point.Origin(), Vector.ZAxis(), actualAzimuth.ToDegrees());
                var sunDirection = altitudeDirection.Transform(azimuthRotation);

                return sunDirection.Scale(100);
            }
        }

        /// <summary>
        ///     Extracts the Altitude.
        /// </summary>
        public double Altitude
        {
            get { return InternalSunAndShadowSettings.Altitude; }
        }

        /// <summary>
        ///     Extracts the Azimuth.
        /// </summary>
        public double Azimuth
        {
            get { return InternalSunAndShadowSettings.Azimuth; }
        }

        /// <summary>
        ///     Gets the Start Date and Time of the solar study.
        /// </summary>
        public DateTime StartDateTime
        {
            get { return InternalSunAndShadowSettings.StartDateAndTime; }
        }
        
        /// <summary>
        ///     Gets the End Date and Time of the solar study.
        /// </summary>
        public DateTime EndDateTime
        {
            get { return InternalSunAndShadowSettings.EndDateAndTime; }
        }

        /// <summary>
        ///     Gets the Date and Time for the current frame of the solar study.
        /// </summary>
        public DateTime CurrentDateTime
        {
            get { return InternalSunAndShadowSettings.ActiveFrameTime; }
        }

        /// <summary>
        /// Get Element for further processing
        /// </summary>
        /// 
        public Autodesk.Revit.DB.Element GetElement
        {
            get { return InternalSunAndShadowSettings as Autodesk.Revit.DB.Element; }
        }

        ///// <summary>
        /////     Gets the SunPath geometry for the current frame of the solar study.
        ///// </summary>
        //public GeometryElement CurrentSunPathGeometry
        //{

        //    get
        //    {

        //        var geomOptions = new Autodesk.Revit.DB.Options();
        //        geomOptions.ComputeReferences = true;
        //        return InternalSunAndShadowSettings.get_Geometry(geomOptions);
        //    }
        //}

    }
}