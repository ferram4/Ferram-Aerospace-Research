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
            parsedTemplate.resources = new List<PartResourceDefinition>();
            parsedTemplate.rejectUnlistedResources = false;

            if (template.HasValue("name"))
                parsedTemplate.name = template.GetValue("name");
            if(template.HasValue("YmaxStress"))
                double.TryParse(template.GetValue("YmaxStress"), out parsedTemplate.YmaxStress);
            if (template.HasValue("XZmaxStress"))
                double.TryParse(template.GetValue("XZmaxStress"), out parsedTemplate.XZmaxStress);

            if (template.HasNode("Resources"))
            {
                ConfigNode resources = template.GetNode("Resources");
                if(resources.HasValue("numReq"))
                    int.TryParse(template.GetValue("numReq"), out parsedTemplate.minNumResources);

                if (resources.HasValue("rejectUnlistedResources"))
                    bool.TryParse(template.GetValue("rejectUnlistedResources"), out parsedTemplate.rejectUnlistedResources);

                PartResourceLibrary l = PartResourceLibrary.Instance;
                foreach (string resString in resources.GetValues("res"))
                {
                    if (l.resourceDefinitions.Contains(resString))
                        parsedTemplate.resources.Add(l.resourceDefinitions[resString]);
                }
            }

            return parsedTemplate;
        }

        public static FARPartStressTemplate DetermineStressTemplate(Part p)
        {
            FARPartStressTemplate template = StressTemplates[0];

            int resCount = p.Resources.Count;

            foreach (FARPartStressTemplate candidate in StressTemplates)
            {
                //If it doesn't even contain enough resources, it'll never be this template
                if (resCount < candidate.minNumResources)
                    continue;

                if (candidate.rejectUnlistedResources)
                {
                    bool cont = false;
                    foreach (PartResourceDefinition res in p.Resources)
                        if (!candidate.resources.Contains(res))
                        {
                            cont = true;
                            break;
                        }
                    if (cont)
                        continue;
                }
                else
                {
                    bool cont = true;
                    int numRes = 0;
                    foreach (PartResourceDefinition res in p.Resources)
                        if (candidate.resources.Contains(res))
                        {
                            numRes++;
                            cont = false;
                        }
                    if (cont || numRes < candidate.minNumResources)
                        continue;
                }


                template = candidate;
            }


            return template;
        }
    }

    public struct FARPartStressTemplate
    {
        public string name;
        public double YmaxStress;
        public double XZmaxStress;
        public List<PartResourceDefinition> resources;
        public int minNumResources;
        public bool rejectUnlistedResources;
    }
}
