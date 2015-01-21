using System;
using System.Collections.Generic;
using FerramAerospaceResearch.FARCollections;
using ferram4;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    class CrossSectionCurve
    {
        LLRedBlackTree<PartCrossSection> crossSections;

        public CrossSectionCurve()
        {
            crossSections = new LLRedBlackTree<PartCrossSection>();
        }

        public void AddCrossSection(PartCrossSection section)
        {
            crossSections.Insert(section);
        }

        public void Clear()
        {
            crossSections.Clear();
        }

        public PartCrossSection GetCrossSectionAtStation(double station)
        {
            PartCrossSection section = new PartCrossSection();
            section.station = station;

            PartCrossSection lowerSection, upperSection;
            crossSections.FindNearestData(section, out lowerSection, out upperSection);

            section.radius = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.radius, upperSection.radius, station);
            section.area = section.radius * section.radius * Math.PI;
            section.centroid.x = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.centroid.x, upperSection.centroid.x, station);
            section.centroid.y = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.centroid.y, upperSection.centroid.y, station);

            return section;
        }
    }
}
