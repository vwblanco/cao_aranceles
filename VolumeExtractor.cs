using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace AddinsEscalera
{
    public static class VolumeExtractor
    {
        public static double GetElementRealVolume(Element element)
        {
            double volumeCubicFeet = 0;

            try
            {
                Options options = new Options
                {
                    DetailLevel = ViewDetailLevel.Fine
                };

                GeometryElement geometryElement = element.get_Geometry(options);

                if (geometryElement == null)
                    return 0.0;

                foreach (GeometryObject geometryObject in geometryElement)
                {
                    Solid solid = geometryObject as Solid;
                    if (solid != null && solid.Volume > 0)
                    {
                        volumeCubicFeet += solid.Volume;
                        continue;
                    }

                    GeometryInstance geometryInstance = geometryObject as GeometryInstance;
                    if (geometryInstance != null)
                    {
                        foreach (GeometryObject innerObject in geometryInstance.GetInstanceGeometry())
                        {
                            Solid innerSolid = innerObject as Solid;
                            if (innerSolid != null && innerSolid.Volume > 0)
                            {
                                volumeCubicFeet += innerSolid.Volume;
                            }
                        }
                    }
                }

                double volumeCubicMeters = UnitUtils.ConvertFromInternalUnits(
                    volumeCubicFeet,
                    UnitTypeId.CubicMeters
                );

                return Math.Round(volumeCubicMeters, 2);
            }
            catch
            {
                return 0.0;
            }
        }

        public static void ExtractVolumes(List<Element> elements)
        {
            foreach (Element element in elements)
            {
                if (element.Category != null)
                {
                    if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Stairs)
                    {
                        double stairVolume = GetElementRealVolume(element);
                    }
                    else
                    {
                        Parameter volumeParam = element.get_Parameter(
                            BuiltInParameter.HOST_VOLUME_COMPUTED
                        );

                        if (volumeParam != null && volumeParam.HasValue)
                        {
                            double internalVolume = volumeParam.AsDouble();
                            double cubicMeters = UnitUtils.ConvertFromInternalUnits(
                                internalVolume,
                                UnitTypeId.CubicMeters
                            );
                        }
                    }
                }
            }
        }
    }
}
