using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace ferram4
{
    public static class FARAeroStress
    {
        public static List<FARPartStressTemplate> StressTemplates = new List<FARPartStressTemplate>();

        public static void LoadStressTemplates()
        {
            StressTemplates.Clear();
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("FARAeroStress"))
                foreach(ConfigNode template in node.GetNodes("FARPartStressTemplate"))
                    StressTemplates.Add(CreateFARPartStressTemplate(template));

        }

        private static FARPartStressTemplate CreateFARPartStressTemplate(ConfigNode template)
        {
            FARPartStressTemplate parsedTemplate = new FARPartStressTemplate();
            parsedTemplate.XZmaxStress = 500;
            parsedTemplate.YmaxStress = 500;
            parsedTemplate.name = "default";
            parsedTemplate.minNumResources = 0;
            parsedTemplate.resources = new List<string>();
            parsedTemplate.excludeResources = new List<string>();
            parsedTemplate.rejectUnlistedResources = false;
            parsedTemplate.crewed = false;
            parsedTemplate.flowModeNeeded = false;
            parsedTemplate.flowMode = ResourceFlowMode.NO_FLOW;

            if (template.HasValue("name"))
                parsedTemplate.name = template.GetValue("name");
            if(template.HasValue("YmaxStress"))
                double.TryParse(template.GetValue("YmaxStress"), out parsedTemplate.YmaxStress);
            if (template.HasValue("XZmaxStress"))
                double.TryParse(template.GetValue("XZmaxStress"), out parsedTemplate.XZmaxStress);
            if (template.HasValue("requiresCrew"))
                bool.TryParse(template.GetValue("requiresCrew"), out parsedTemplate.crewed);

            if (template.HasNode("Resources"))
            {
                ConfigNode resources = template.GetNode("Resources");
                if(resources.HasValue("numReq"))
                    int.TryParse(resources.GetValue("numReq"), out parsedTemplate.minNumResources);

                if (resources.HasValue("rejectUnlistedResources"))
                    bool.TryParse(resources.GetValue("rejectUnlistedResources"), out parsedTemplate.rejectUnlistedResources);

                if (resources.HasValue("flowMode"))
                {
                    parsedTemplate.flowModeNeeded = true;
                    string flowString = resources.GetValue("flowMode").ToLowerInvariant();

                    if(flowString == "all_vessel")
                        parsedTemplate.flowMode = ResourceFlowMode.ALL_VESSEL;
                    else if (flowString == "stack_priority_search")
                        parsedTemplate.flowMode = ResourceFlowMode.STACK_PRIORITY_SEARCH;
                    else if (flowString == "stage_priority_flow")
                        parsedTemplate.flowMode = ResourceFlowMode.STAGE_PRIORITY_FLOW;
                }

                PartResourceLibrary l = PartResourceLibrary.Instance;
                foreach (string resString in resources.GetValues("res"))
                {
                    if (l.resourceDefinitions.Contains(resString))
                        parsedTemplate.resources.Add(resString);
                }
                foreach (string resString in resources.GetValues("excludeRes"))
                {
                    if (l.resourceDefinitions.Contains(resString))
                        parsedTemplate.excludeResources.Add(resString);
                }
            }

            return parsedTemplate;
        }

        public static FARPartStressTemplate DetermineStressTemplate(Part p)
        {
            FARPartStressTemplate template = StressTemplates[0];

            int resCount = p.Resources.Count;
            bool crewed = p.CrewCapacity > 0;

            foreach (FARPartStressTemplate candidate in StressTemplates)
            {
                if (candidate.crewed != crewed)
                    continue;

                if (resCount < candidate.minNumResources)
                    continue;

                if (candidate.rejectUnlistedResources)
                {
                    bool cont = true;
                    int numRes = 0;
                    foreach (PartResource res in p.Resources.list)
                    {
                        if (candidate.resources.Contains(res.info.name))
                        {
                            numRes++;
                            cont = false;
                        }
                        else
                        {
                            cont = true;
                            break;
                        }
                    }

                    if (cont || numRes < candidate.minNumResources)
                        continue;
                }
                else
                {
                    int numRes = 0;
                    foreach (PartResource res in p.Resources.list)
                        if (!candidate.excludeResources.Contains(res.info.name))
                            if(!candidate.flowModeNeeded || res.info.resourceFlowMode == candidate.flowMode)
                                numRes++;
                    

                        
                    if (numRes < candidate.minNumResources)
                        continue;
                }

                template = candidate;
            }


            return template;
        }

        public static bool PartIsGreeble(Part p, double crossSectionalArea, double finenessRatio, double area)
        {
            bool isGreeble = false;

            if (p.parent)
            {
                Part parent = p.parent;
                if (parent.Modules.Contains("FARBasicDragModel"))
                {
                    FARBasicDragModel d = parent.GetComponent<FARBasicDragModel>();
                    Vector3 parentVector = (p.transform.worldToLocalMatrix * parent.transform.localToWorldMatrix).MultiplyVector(d.localUpVector);

                    double dotProd = Vector3.Dot(parentVector, Vector3.up);
                    if (Math.Abs(dotProd) < 0.3)
                        if (crossSectionalArea / d.S <= 0.1 && d.S > area * 0.2 * Math.Sqrt(1 - dotProd * dotProd))
                            isGreeble = true;
                }
                else if (parent.Modules.Contains("FARWingAerodynamicModel"))
                {
                    FARWingAerodynamicModel w = parent.GetComponent<FARWingAerodynamicModel>();

                    if (w.S * 0.5 > area)
                        isGreeble = true;
                }
            }

            return isGreeble;
        }
    }

    public struct FARPartStressTemplate
    {
        public string name;
        public double YmaxStress;
        public double XZmaxStress;
        public List<string> resources;
        public List<string> excludeResources;
        public ResourceFlowMode flowMode;
        public bool flowModeNeeded;
        public int minNumResources;
        public bool rejectUnlistedResources;
        public bool crewed;
    }
}
