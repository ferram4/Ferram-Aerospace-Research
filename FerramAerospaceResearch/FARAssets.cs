using System;
using System.Collections.Generic;
using UnityEngine;
using KSPAssets;
using KSPAssets.Loaders;

namespace FerramAerospaceResearch
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    class FARAssets : MonoBehaviour
    {

        public static Dictionary<string, Shader> shaderDict;

        void Start()
        {
            shaderDict = new Dictionary<string, Shader>();

            Debug.Log("Asset bundles");
            Debug.Log(AssetLoader.BundleDefinitions.Count);
            foreach (BundleDefinition b in AssetLoader.BundleDefinitions)
            {
                Debug.Log(b.name + " " + b.createdTime + " " + b.path + " " + b.info + " " + b.urlName);
            }
            Debug.Log(AssetLoader.AssetDefinitions.Count);
            foreach (AssetDefinition a in AssetLoader.AssetDefinitions)
            {
                Debug.Log(a.name + " " + a.type + " " + a.path);
            }

            AssetLoader.LoadAssets(LoadAssets, AssetLoader.GetAssetDefinitionWithName("FerramAerospaceResearch/Shaders/farshaders", "FARCrossSectionGraph"));
        }

        void LoadAssets(AssetLoader.Loader loader)
        {
            for (int i = 0; i < loader.definitions.Length; i++ )
            {
                UnityEngine.Object o = loader.objects[i];
                if (o == null)
                    continue;

                Type oType = o.GetType();

                if (oType == typeof(Shader))
                    shaderDict.Add(loader.definitions[i].name, o as Shader);
            }
        }
    }
}
