using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI
{
    class EditorAreaRulingOverlay
    {
        //VectorLine _areaLine;
        //VectorLine _derivLine;
        //List<VectorLine> _markingLines;
        LineRenderer _areaRenderer;
        LineRenderer _derivRenderer;
        List<LineRenderer> _markingRenderers;

        Color _axisColor;
        Color _crossSectionColor;
        Color _derivColor;
        double _yScaleMaxDistance;
        double _yAxisGridScale;
        int _numGridLines;

        static Material _rendererMaterial;

        bool display = false;
        
        public EditorAreaRulingOverlay(Color axisColor, Color crossSectionColor, Color derivColor, double yScaleMaxDistance, double yAxisGridScale)
        {
            _axisColor = axisColor;
            _crossSectionColor = crossSectionColor;
            _derivColor = derivColor;
            _yScaleMaxDistance = yScaleMaxDistance;
            _yAxisGridScale = yAxisGridScale;

            Initialize();
        }

        ~EditorAreaRulingOverlay()
        {
            Cleanup();
        }

        private void Initialize()
        {
            //Based on Kronal Vessel Viewer CoM axes rendering
            if (_rendererMaterial == null)
            {
                _rendererMaterial = new Material("Shader \"Lines/Colored Blended\" {" +
                        "SubShader { Pass { " +
                        "    Blend SrcAlpha OneMinusSrcAlpha " +
                        "    ZWrite Off ZTest Always Cull Off Fog { Mode Off } " +
                        "    BindChannels {" +
                        "      Bind \"vertex\", vertex Bind \"color\", color }" +
                        "} } }");
                _rendererMaterial.hideFlags = HideFlags.HideAndDontSave;
                _rendererMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
            }

            _areaRenderer = CreateNewRenderer(_crossSectionColor, 0.1f, _rendererMaterial);
            _derivRenderer = CreateNewRenderer(_derivColor, 0.1f, _rendererMaterial);

            _markingRenderers = new List<LineRenderer>();
            _markingRenderers.Add(CreateNewRenderer(_axisColor, 0.1f, _rendererMaterial));
        }

        private void Cleanup()
        {
          if (_areaRenderer)
                GameObject.Destroy(_areaRenderer.gameObject);
            if (_derivRenderer)
                GameObject.Destroy(_derivRenderer.gameObject);
            if(_markingRenderers != null)
                for (int i = 0; i < _markingRenderers.Count; i++)
                {
                    if (_markingRenderers[i])
                        GameObject.Destroy(_markingRenderers[i].gameObject);
                }

            _markingRenderers = null;
        }

        private void RestartOverlay()
        {
            Cleanup();
            Initialize();
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
            display = !display;
            
            if (!_areaRenderer)
                RestartOverlay();

            _areaRenderer.enabled = !_areaRenderer.enabled;
            _derivRenderer.enabled = !_derivRenderer.enabled;

            for (int i = 0; i < _markingRenderers.Count; i++)
            {
                LineRenderer marking = _markingRenderers[i];
                marking.enabled = !marking.enabled;
                if (i > _numGridLines)
                    _markingRenderers[i].enabled = false;
            }
        }

        public void SetVisibility(bool visible)
        {
            display = visible;
            
            if (!_areaRenderer)
                RestartOverlay();

            _areaRenderer.enabled = visible;
            _derivRenderer.enabled = visible;
            for (int i = 0; i < _markingRenderers.Count; i++)
            {
                _markingRenderers[i].enabled = visible;
                if (i > _numGridLines)
                    _markingRenderers[i].enabled = false;
            }
        }

        /*public void Display()
        {
            if (!display)
                return;

            Transform lineTransform = EditorLogic.RootPart.transform;

            Vector.DrawLine3D(_areaLine, lineTransform);
            Vector.DrawLine3D(_derivLine, lineTransform);
            for (int i = 0; i < _markingLines.Count; i++)
                Vector.DrawLine3D(_markingLines[i], lineTransform);
        }*/

        public void UpdateAeroData(Matrix4x4 voxelLocalToWorldMatrix, double[] xCoords, double[] yCoordsCrossSection, double[] yCoordsDeriv, double maxValue)
        {
            _numGridLines = (int)Math.Ceiling(maxValue / _yAxisGridScale);       //add one to account for the xAxis
            double gridScale = _yScaleMaxDistance / (double)_numGridLines;
            double scalingFactor = _yScaleMaxDistance / (_yAxisGridScale * _numGridLines);

            /*if (_areaLine == null)
                _areaLine = BuildLine(xCoords, yCoordsCrossSection, "area", scalingFactor, _crossSectionColor);
            else
                _areaLine = BuildLine(_areaLine, xCoords, yCoordsCrossSection, scalingFactor);


            if (_derivLine == null)
                _derivLine = BuildLine(xCoords, yCoordsDeriv, "deriv", scalingFactor, _derivColor);
            else
                _derivLine = BuildLine(_areaLine, xCoords, yCoordsCrossSection, scalingFactor);

            double[] shortXCoords = new double[] {xCoords[0], xCoords[xCoords.Length - 1]};


            for (int i = 0; i <= _numGridLines; i++)
            {
                double height = i * gridScale;
                if(i >= _markingLines.Count)
                {
                    VectorLine line = BuildLine(shortXCoords, new double[] { height, height }, "marker" + i, 1, _axisColor);
                    _markingLines.Add(line);
                }
                else
                {
                    _markingLines[i] = BuildLine(_markingLines[i], shortXCoords, new double[] { height, height }, 1);
                }
            }*/

            if (!_areaRenderer)
                RestartOverlay();

            UpdateRenderer(_areaRenderer, voxelLocalToWorldMatrix, xCoords, yCoordsCrossSection, scalingFactor);
            UpdateRenderer(_derivRenderer, voxelLocalToWorldMatrix, xCoords, yCoordsDeriv, scalingFactor);

            while (_markingRenderers.Count <= _numGridLines)
            {
                LineRenderer newMarkingRenderer = CreateNewRenderer(_axisColor, 0.1f, _rendererMaterial);
                newMarkingRenderer.enabled = _areaRenderer.enabled;
                _markingRenderers.Add(newMarkingRenderer);
            }


            double[] shortXCoords = new double[] {xCoords[0], xCoords[xCoords.Length - 1]};

            for (int i = 0; i < _markingRenderers.Count; i++)
            {
                double height = i * gridScale;
                UpdateRenderer(_markingRenderers[i], voxelLocalToWorldMatrix, shortXCoords, new double[] { height, height });
                if (i > _numGridLines)
                    _markingRenderers[i].enabled = false;
                else
                    _markingRenderers[i].enabled = _areaRenderer.enabled;
            }
        }

        /*VectorLine BuildLine(double[] xCoords, double[] yCoords, string name, double yScalingFactor, Color color)
        {
            Vector3[] points = new Vector3[xCoords.Length];

            for(int i = 0; i < points.Length; i++)
            {
                points[i].y = (float)xCoords[i];
                points[i].z = -(float)(yCoords[i] * yScalingFactor);
            }
            VectorLine line = new VectorLine(name, points, color, _rendererMaterial, 2);

            line.vectorObject.renderer.sortingOrder = 30;
            line.depth = 30;

            return line;
        }

        VectorLine BuildLine(VectorLine line, double[] xCoords, double[] yCoords, double yScalingFactor)
        {
            Vector3[] points = new Vector3[xCoords.Length];

            for (int i = 0; i < points.Length; i++)
            {
                points[i].y = (float)xCoords[i];
                points[i].z = -(float)(yCoords[i] * yScalingFactor);
            }
            line.points3 = points;

            return line;
        }*/


        void UpdateRenderer(LineRenderer renderer, Matrix4x4 transformMatrix, double[] xCoords, double[] yCoords)
        {
            UpdateRenderer(renderer, transformMatrix, xCoords, yCoords, 1);
        }

        void UpdateRenderer(LineRenderer renderer, Matrix4x4 transformMatrix, double[] xCoords, double[] yCoords, double yScalingFactor)
        {
            renderer.transform.parent = EditorLogic.RootPart.transform;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;

            renderer.SetVertexCount(xCoords.Length);

            for (int i = 0; i < xCoords.Length; i++)
            {
                Vector3 vec = Vector3.up * (float)xCoords[i] - Vector3.forward * (float)(yCoords[i] * yScalingFactor);
                vec = transformMatrix.MultiplyVector(vec);
                renderer.SetPosition(i, vec);
            }
        }
    }
}
