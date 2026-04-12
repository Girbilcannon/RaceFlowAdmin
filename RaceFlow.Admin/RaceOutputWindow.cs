using System;
using System.Drawing;
using System.Windows.Forms;
using RaceFlow.Core.Runtime;

namespace RaceFlow.Admin
{
    public sealed class RaceOutputWindow : Form
    {
        private readonly RaceMapRenderControl _renderControl;

        private RuntimeGraph? _graph;
        private RaceOutputFrame? _frame;
        private string? _themeFile;
        private int _mapDelayMs;

        private float _outputScale = 1.0f;
        private float _outputOffsetX = 0f;
        private float _outputOffsetY = 0f;
        private float _outputNodeTextScale = 1.0f;
        private float _outputRacerTextScale = 1.0f;

        public RaceOutputWindow()
        {
            Text = "Race Output";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1280, 720);
            MinimumSize = new Size(900, 540);
            BackColor = Color.FromArgb(14, 16, 20);

            _renderControl = new RaceMapRenderControl
            {
                Dock = DockStyle.Fill
            };

            Controls.Add(_renderControl);
        }

        public RuntimeGraph? CurrentGraph => _graph;
        public RaceOutputFrame? CurrentFrame => _frame;
        public int CurrentMapDelayMs => _mapDelayMs;

        public void UpdateScene(
            RuntimeGraph? graph,
            RaceOutputFrame? frame,
            int mapDelayMs,
            string? themeFile = null,
            float outputScale = 1.0f,
            float outputOffsetX = 0f,
            float outputOffsetY = 0f,
            float outputNodeTextScale = 1.0f,
            float outputRacerTextScale = 1.0f)
        {
            _graph = graph;
            _frame = frame;
            _mapDelayMs = mapDelayMs;
            _themeFile = themeFile;

            _outputScale = outputScale;
            _outputOffsetX = outputOffsetX;
            _outputOffsetY = outputOffsetY;
            _outputNodeTextScale = outputNodeTextScale;
            _outputRacerTextScale = outputRacerTextScale;

            _renderControl.UpdateScene(
                graph,
                frame,
                themeFile,
                outputScale,
                outputOffsetX,
                outputOffsetY,
                outputNodeTextScale,
                outputRacerTextScale);
        }

        public void RefreshScene()
        {
            _renderControl.UpdateScene(
                _graph,
                _frame,
                _themeFile,
                _outputScale,
                _outputOffsetX,
                _outputOffsetY,
                _outputNodeTextScale,
                _outputRacerTextScale);
        }
    }
}