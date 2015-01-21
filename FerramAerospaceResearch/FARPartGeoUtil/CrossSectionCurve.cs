using System;
using System.Collections.Generic;
using FerramAerospaceResearch.FARCollections;
using ferram4;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    class CrossSectionCurve
    {
        LLRedBlackTree<CrossSection> crossSections;

        public CrossSectionCurve()
        {
            crossSections = new LLRedBlackTree<CrossSection>();
        }

        public void AddCrossSection(CrossSection section)
        {
            crossSections.Insert(section);
        }

        public void Clear()
        {
            crossSections.Clear();
        }

        public CrossSection GetCrossSectionAtStation(double station)
        {
            CrossSection section = new CrossSection();
            section.station = station;

            CrossSection lowerSection, upperSection;
            crossSections.FindNearestData(section, out lowerSection, out upperSection);

            section.radius = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.radius, upperSection.radius, station);
            section.area = section.radius * section.radius * Math.PI;
            section.centroid.x = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.centroid.x, upperSection.centroid.x, station);
            section.centroid.y = FARMathUtil.Lerp(lowerSection.station, upperSection.station, lowerSection.centroid.y, upperSection.centroid.y, station);

            return section;
        }
    }
}
