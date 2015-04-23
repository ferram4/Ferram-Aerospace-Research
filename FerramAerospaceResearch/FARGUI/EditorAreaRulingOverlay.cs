using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI
{
    class EditorAreaRulingOverlay
    {
        LineRenderer _areaRenderer;
        LineRenderer _derivRenderer;
        Color _axisColor;
        Color _crossSectionColor;
        Color _derivColor;

        Material _rendererMaterial;
        
        public EditorAreaRulingOverlay(Color axisColor, Color crossSectionColor, Color derivColor)
        {
            _axisColor = axisColor;
            _crossSectionColor = crossSectionColor;
            _derivColor = derivColor;

            //Based on Kronal Vessel Viewer CoM axes rendering
            _rendererMaterial = new Material("Shader \"Lines/Colored Blended\" {" +
                    "SubShader { Pass { " +
                    "    Blend SrcAlpha OneMinusSrcAlpha " +
                    "    ZWrite Off ZTest Always Cull Off Fog { Mode Off } " +
                    "    BindChannels {" +
                    "      Bind \"vertex\", vertex Bind \"color\", color }" +
                    "} } }");
            _rendererMaterial.hideFlags = HideFlags.HideAndDontSave;
            _rendererMaterial.shader.hideFlags = HideFlags.HideAndDontSave;

            _areaRenderer = CreateNewRenderer(_crossSectionColor, 0.1f, _rendererMaterial);
            _derivRenderer = CreateNewRenderer(_derivColor, 0.1f, _rendererMaterial);
        }

        ~EditorAreaRulingOverlay()
        {
            if(_areaRenderer)
                GameObject.Destroy(_areaRenderer.gameObject);
            if(_derivRenderer)
                GameObject.Destroy(_derivRenderer.gameObject);
        }

        LineRenderer CreateNewRenderer(Color color, float width, Material material)
        {
            GameObject o = new GameObject();
            
            LineRenderer renderer = o.gameObject.AddComponent<LineRenderer>();

            renderer.useWorldSpace = false;
            renderer.material = material;
            renderer.SetColors(color, color);
            renderer.enabled = false;
            renderer.SetWidth(width, width);
            renderer.sortingOrder = 1;

            return renderer;
        }

        public void ToggleVisibility()
        {
            _areaRenderer.enabled = !_areaRenderer.enabled;
            _derivRenderer.enabled = !_derivRenderer.enabled;
        }

        public void SetVisibility(bool visible)
        {
            _areaRenderer.enabled = visible;
            _derivRenderer.enabled = visible;
        }

        public void UpdateAeroData(Matrix4x4 voxelLocalToWorldMatrix, double[] xCoords, double[] yCoordsCrossSection, double[] yCoordsDeriv)
        {
            UpdateRenderer(_areaRenderer, voxelLocalToWorldMatrix, xCoords, yCoordsCrossSection);
            UpdateRenderer(_derivRenderer, voxelLocalToWorldMatrix, xCoords, yCoordsDeriv);
        }

        void UpdateRenderer(LineRenderer renderer, Matrix4x4 transformMatrix, double[] xCoords, double[] yCoords)
        {
            renderer.transform.parent = EditorLogic.RootPart.transform;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;

            renderer.SetVertexCount(xCoords.Length);

            for (int i = 0; i < xCoords.Length; i++)
            {
                Vector3 vec = Vector3.up * (float)xCoords[i] - Vector3.forward * (float)yCoords[i];
                vec = transformMatrix.MultiplyVector(vec);
                renderer.SetPosition(i, vec);
            }
        }
    }
}
