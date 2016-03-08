/*
Ferram Aerospace Research v0.15.5.7 "Johnson"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2015, Michael Ferrara, aka Ferram4

   This file is part of Ferram Aerospace Research.

   Ferram Aerospace Research is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   Ferram Aerospace Research is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

   Serious thanks:		a.g., for tons of bugfixes and code-refactorings   
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values  
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
                        	Regex, for adding RPM support  
				DaMichel, for some ferramGraph updates and some control surface-related features  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using KSPAssets;
using KSPAssets.Loaders;
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
        LineRenderer _coeffRenderer;
        List<LineRenderer> _markingRenderers;

        public enum OverlayType
        {
            AREA,
            DERIV,
            COEFF
        }

        Color _axisColor;
        Color _crossSectionColor;
        Color _derivColor;
        double _yScaleMaxDistance;
        double _yAxisGridScale;
        int _numGridLines;

        static Material _rendererMaterial;

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

                Shader lineShader;

                if (!FARAssets.shaderDict.TryGetValue("FARCrossSectionGraph", out lineShader))
                {
                    //TODO: definitely replace this with a proper shader when we can
                    Debug.Log("Could not load cross-section shader; using fallback shader");
                    lineShader = Shader.Find("Sprites/Default");
                }

                _rendererMaterial = new Material(lineShader);
                _rendererMaterial.hideFlags = HideFlags.HideAndDontSave;
                _rendererMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
                _rendererMaterial.renderQueue = 4500;
            }

            _areaRenderer = CreateNewRenderer(_crossSectionColor, 0.1f, _rendererMaterial);
            _derivRenderer = CreateNewRenderer(_derivColor, 0.1f, _rendererMaterial);
            _coeffRenderer = CreateNewRenderer(Color.cyan, 0.1f, _rendererMaterial);

            _markingRenderers = new List<LineRenderer>();
            _markingRenderers.Add(CreateNewRenderer(_axisColor, 0.1f, _rendererMaterial));
        }

        private void Cleanup()
        {
            if (_areaRenderer)
                GameObject.Destroy(_areaRenderer.gameObject);
            if (_derivRenderer)
                GameObject.Destroy(_derivRenderer.gameObject);
            if (_coeffRenderer)
                GameObject.Destroy(_coeffRenderer.gameObject);
            if (_markingRenderers != null)
                for (int i = 0; i < _markingRenderers.Count; i++)
                {
                    if (_markingRenderers[i])
                        GameObject.Destroy(_markingRenderers[i].gameObject);
                }

            _markingRenderers = null;
        }

        public void RestartOverlay()
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

        public bool IsVisible(OverlayType type)
        {
            switch (type)
            {
                case OverlayType.AREA:
                    return (_areaRenderer != null) ? _areaRenderer.enabled : false;
                case OverlayType.DERIV:
                    return (_derivRenderer != null) ? _derivRenderer.enabled : false;
                case OverlayType.COEFF:
                    return (_coeffRenderer != null) ? _coeffRenderer.enabled : false;
                default:
                    return false;
            }
        }

        public bool AnyVisible()
        {
            return _areaRenderer && (_areaRenderer.enabled || _derivRenderer.enabled || _coeffRenderer.enabled);
        }

        public void SetVisibility(OverlayType type, bool visible)
        {
            if (!_areaRenderer)
                RestartOverlay();

            switch (type)
            {
                case OverlayType.AREA:
                    _areaRenderer.enabled = visible;
                    break;
                case OverlayType.DERIV:
                    _derivRenderer.enabled = visible;
                    break;
                case OverlayType.COEFF:
                     _coeffRenderer.enabled = visible;
                    break;
            }

            bool anyVisible = AnyVisible();
            for (int i = 0; i < _markingRenderers.Count; i++)
            {
                _markingRenderers[i].enabled = anyVisible;
                if (i > _numGridLines)
                    _markingRenderers[i].enabled = false;
            }
        }

        /*public void Display()
        {
            if (!display)
                return;

            Transform lineTransform = EditorLogic.Rootpart.partTransform;

            Vector.DrawLine3D(_areaLine, lineTransform);
            Vector.DrawLine3D(_derivLine, lineTransform);
            for (int i = 0; i < _markingLines.Count; i++)
                Vector.DrawLine3D(_markingLines[i], lineTransform);
        }*/

        public void UpdateAeroData(Matrix4x4 voxelLocalToWorldMatrix, double[] xCoords, double[] yCoordsCrossSection, double[] yCoordsDeriv, double[] yCoordsPressureCoeffs, double maxValue)
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
            UpdateRenderer(_coeffRenderer, voxelLocalToWorldMatrix, xCoords, yCoordsPressureCoeffs, 10);

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
            renderer.transform.parent = EditorLogic.RootPart.partTransform;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.SetAsFirstSibling();

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
