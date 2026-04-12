using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RaceFlow.Core.Models;
using RaceFlow.Core.Runtime;

namespace RaceFlow.Admin
{
    public sealed class SectionLayoutTunerWindow : Form
    {
        private readonly Action _onValuesChanged;

        private RuntimeGraph? _graph;
        private FlowMapAdminSettings? _admin;
        private bool _isLoading;

        private FlowLayoutPanel _root = null!;
        private FlowLayoutPanel _segmentHost = null!;

        private readonly Dictionary<string, SectionEditors> _sectionEditors =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, SegmentEditors> _segmentEditors =
            new(StringComparer.OrdinalIgnoreCase);

        private OutputEditors _outputEditors = null!;

        public SectionLayoutTunerWindow(Action onValuesChanged)
        {
            _onValuesChanged = onValuesChanged ?? throw new ArgumentNullException(nameof(onValuesChanged));

            Text = "Section Layout Tuner";
            StartPosition = FormStartPosition.Manual;
            Size = new Size(420, 760);
            MinimumSize = new Size(420, 640);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            BackColor = Color.FromArgb(24, 26, 32);

            BuildUi();
        }

        public void BindData(RuntimeGraph? graph, FlowMapAdminSettings? admin)
        {
            _graph = graph;
            _admin = admin;
            LoadAllValues();
        }

        private void BuildUi()
        {
            _root = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = BackColor
            };

            Controls.Add(_root);

            _root.Controls.Add(CreateOutputGroup());
            _root.Controls.Add(CreateSectionGroup("left", "LEFT"));
            _root.Controls.Add(CreateSectionGroup("top", "TOP"));
            _root.Controls.Add(CreateSectionGroup("right", "RIGHT"));
            _root.Controls.Add(CreateSectionGroup("bottom", "BOTTOM"));

            var segmentsGroup = new GroupBox
            {
                Text = "SEGMENTS",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 34, 40),
                Width = 380,
                Height = 320,
                Padding = new Padding(10)
            };

            _segmentHost = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 34, 40)
            };

            segmentsGroup.Controls.Add(_segmentHost);
            _root.Controls.Add(segmentsGroup);
        }

        private Control CreateOutputGroup()
        {
            var group = new GroupBox
            {
                Text = "OUTPUT",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 34, 40),
                Width = 380,
                Height = 180,
                Padding = new Padding(10)
            };

            var lblScale = MakeLabel("Output Scale", 12, 28, 110);
            var nudScale = MakeDecimal(130, 24, 82, 0.01m, 0.10m, 5.00m, 1.00m);

            var lblX = MakeLabel("Output X", 220, 28, 68);
            var nudX = MakeWhole(294, 24, 72, -5000, 5000, 0);

            var lblY = MakeLabel("Output Y", 12, 64, 110);
            var nudY = MakeWhole(130, 60, 82, -5000, 5000, 0);

            var lblNodeText = MakeLabel("Node Text", 220, 64, 68);
            var nudNodeText = MakeDecimal(294, 60, 72, 0.01m, 0.10m, 5.00m, 1.00m);

            var lblRacerText = MakeLabel("Racer Text", 12, 100, 110);
            var nudRacerText = MakeDecimal(130, 96, 82, 0.01m, 0.10m, 5.00m, 1.00m);

            void Handle(object? sender, EventArgs e)
            {
                if (_isLoading || _admin == null)
                    return;

                _admin.OutputScale = (double)nudScale.Value;
                _admin.OutputOffsetX = (float)nudX.Value;
                _admin.OutputOffsetY = (float)nudY.Value;
                _admin.OutputNodeTextScale = (double)nudNodeText.Value;
                _admin.OutputRacerTextScale = (double)nudRacerText.Value;

                _onValuesChanged();
            }

            nudScale.ValueChanged += Handle;
            nudX.ValueChanged += Handle;
            nudY.ValueChanged += Handle;
            nudNodeText.ValueChanged += Handle;
            nudRacerText.ValueChanged += Handle;

            group.Controls.Add(lblScale);
            group.Controls.Add(nudScale);
            group.Controls.Add(lblX);
            group.Controls.Add(nudX);
            group.Controls.Add(lblY);
            group.Controls.Add(nudY);
            group.Controls.Add(lblNodeText);
            group.Controls.Add(nudNodeText);
            group.Controls.Add(lblRacerText);
            group.Controls.Add(nudRacerText);

            _outputEditors = new OutputEditors
            {
                OutputScale = nudScale,
                OutputX = nudX,
                OutputY = nudY,
                NodeTextScale = nudNodeText,
                RacerTextScale = nudRacerText
            };

            return group;
        }

        private Control CreateSectionGroup(string sideKey, string title)
        {
            var group = new GroupBox
            {
                Text = title,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 34, 40),
                Width = 380,
                Height = 86,
                Padding = new Padding(10)
            };

            var lblScale = MakeLabel("Scale", 12, 28, 40);
            var nudScale = MakeDecimal(58, 24, 68, 0.01m, 0.10m, 5.00m, 1.00m);

            var lblX = MakeLabel("X", 142, 28, 14);
            var nudX = MakeWhole(160, 24, 62, -5000, 5000, 0);

            var lblY = MakeLabel("Y", 236, 28, 14);
            var nudY = MakeWhole(254, 24, 62, -5000, 5000, 0);

            void HandleValueChanged(object? sender, EventArgs e)
            {
                if (_isLoading)
                    return;

                ApplySectionEditorValuesToGraph(sideKey);
            }

            nudScale.ValueChanged += HandleValueChanged;
            nudX.ValueChanged += HandleValueChanged;
            nudY.ValueChanged += HandleValueChanged;

            group.Controls.Add(lblScale);
            group.Controls.Add(nudScale);
            group.Controls.Add(lblX);
            group.Controls.Add(nudX);
            group.Controls.Add(lblY);
            group.Controls.Add(nudY);

            _sectionEditors[sideKey] = new SectionEditors
            {
                Scale = nudScale,
                X = nudX,
                Y = nudY
            };

            return group;
        }

        private Control CreateSegmentGroup(RuntimeSegment segment)
        {
            var group = new GroupBox
            {
                Text = $"{segment.Side.ToUpperInvariant()} :: {segment.Label}",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(36, 40, 46),
                Width = 340,
                Height = 86,
                Padding = new Padding(10)
            };

            var lblScale = MakeLabel("Scale", 12, 28, 40);
            var nudScale = MakeDecimal(58, 24, 68, 0.01m, 0.10m, 5.00m, 1.00m);

            var lblX = MakeLabel("X", 142, 28, 14);
            var nudX = MakeWhole(160, 24, 62, -5000, 5000, 0);

            var lblY = MakeLabel("Y", 236, 28, 14);
            var nudY = MakeWhole(254, 24, 62, -5000, 5000, 0);

            void HandleValueChanged(object? sender, EventArgs e)
            {
                if (_isLoading)
                    return;

                ApplySegmentEditorValuesToGraph(segment.Id);
            }

            nudScale.ValueChanged += HandleValueChanged;
            nudX.ValueChanged += HandleValueChanged;
            nudY.ValueChanged += HandleValueChanged;

            group.Controls.Add(lblScale);
            group.Controls.Add(nudScale);
            group.Controls.Add(lblX);
            group.Controls.Add(nudX);
            group.Controls.Add(lblY);
            group.Controls.Add(nudY);

            _segmentEditors[segment.Id] = new SegmentEditors
            {
                Scale = nudScale,
                X = nudX,
                Y = nudY
            };

            return group;
        }

        private void LoadAllValues()
        {
            _isLoading = true;
            try
            {
                LoadOutputValues();
                LoadSectionValues();
                RebuildSegmentEditors();
                LoadSegmentValues();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void LoadOutputValues()
        {
            bool enabled = _admin != null;

            _outputEditors.OutputScale.Enabled = enabled;
            _outputEditors.OutputX.Enabled = enabled;
            _outputEditors.OutputY.Enabled = enabled;
            _outputEditors.NodeTextScale.Enabled = enabled;
            _outputEditors.RacerTextScale.Enabled = enabled;

            if (_admin == null)
            {
                _outputEditors.OutputScale.Value = 1.00m;
                _outputEditors.OutputX.Value = 0;
                _outputEditors.OutputY.Value = 0;
                _outputEditors.NodeTextScale.Value = 1.00m;
                _outputEditors.RacerTextScale.Value = 1.00m;
                return;
            }

            _outputEditors.OutputScale.Value = ClampDecimal((decimal)_admin.OutputScale, _outputEditors.OutputScale.Minimum, _outputEditors.OutputScale.Maximum);
            _outputEditors.OutputX.Value = ClampDecimal((decimal)_admin.OutputOffsetX, _outputEditors.OutputX.Minimum, _outputEditors.OutputX.Maximum);
            _outputEditors.OutputY.Value = ClampDecimal((decimal)_admin.OutputOffsetY, _outputEditors.OutputY.Minimum, _outputEditors.OutputY.Maximum);
            _outputEditors.NodeTextScale.Value = ClampDecimal((decimal)_admin.OutputNodeTextScale, _outputEditors.NodeTextScale.Minimum, _outputEditors.NodeTextScale.Maximum);
            _outputEditors.RacerTextScale.Value = ClampDecimal((decimal)_admin.OutputRacerTextScale, _outputEditors.RacerTextScale.Minimum, _outputEditors.RacerTextScale.Maximum);
        }

        private void LoadSectionValues()
        {
            foreach (var pair in _sectionEditors)
            {
                string side = pair.Key;
                SectionEditors editors = pair.Value;

                RuntimeSection? section = FindSection(side);

                if (section == null)
                {
                    editors.Scale.Value = 1.00m;
                    editors.X.Value = 0;
                    editors.Y.Value = 0;

                    editors.Scale.Enabled = false;
                    editors.X.Enabled = false;
                    editors.Y.Enabled = false;
                    continue;
                }

                editors.Scale.Enabled = true;
                editors.X.Enabled = true;
                editors.Y.Enabled = true;

                editors.Scale.Value = ClampDecimal((decimal)section.VisualScale, editors.Scale.Minimum, editors.Scale.Maximum);
                editors.X.Value = ClampDecimal((decimal)section.OffsetX, editors.X.Minimum, editors.X.Maximum);
                editors.Y.Value = ClampDecimal((decimal)section.OffsetY, editors.Y.Minimum, editors.Y.Maximum);
            }
        }

        private void RebuildSegmentEditors()
        {
            _segmentHost.SuspendLayout();
            try
            {
                _segmentHost.Controls.Clear();
                _segmentEditors.Clear();

                if (_graph == null)
                    return;

                foreach (RuntimeSegment segment in _graph.Segments.OrderBy(s => s.Side).ThenBy(s => s.Index))
                {
                    _segmentHost.Controls.Add(CreateSegmentGroup(segment));
                }
            }
            finally
            {
                _segmentHost.ResumeLayout();
            }
        }

        private void LoadSegmentValues()
        {
            foreach (var pair in _segmentEditors)
            {
                string segmentId = pair.Key;
                SegmentEditors editors = pair.Value;

                RuntimeSegment? segment = FindSegment(segmentId);

                if (segment == null)
                {
                    editors.Scale.Value = 1.00m;
                    editors.X.Value = 0;
                    editors.Y.Value = 0;

                    editors.Scale.Enabled = false;
                    editors.X.Enabled = false;
                    editors.Y.Enabled = false;
                    continue;
                }

                editors.Scale.Enabled = true;
                editors.X.Enabled = true;
                editors.Y.Enabled = true;

                editors.Scale.Value = ClampDecimal((decimal)segment.VisualScale, editors.Scale.Minimum, editors.Scale.Maximum);
                editors.X.Value = ClampDecimal((decimal)segment.OffsetX, editors.X.Minimum, editors.X.Maximum);
                editors.Y.Value = ClampDecimal((decimal)segment.OffsetY, editors.Y.Minimum, editors.Y.Maximum);
            }
        }

        private void ApplySectionEditorValuesToGraph(string side)
        {
            RuntimeSection? section = FindSection(side);
            if (section == null)
                return;

            SectionEditors editors = _sectionEditors[side];

            section.VisualScale = (float)editors.Scale.Value;
            section.OffsetX = (float)editors.X.Value;
            section.OffsetY = (float)editors.Y.Value;

            _onValuesChanged();
        }

        private void ApplySegmentEditorValuesToGraph(string segmentId)
        {
            RuntimeSegment? segment = FindSegment(segmentId);
            if (segment == null)
                return;

            SegmentEditors editors = _segmentEditors[segmentId];

            segment.VisualScale = (float)editors.Scale.Value;
            segment.OffsetX = (float)editors.X.Value;
            segment.OffsetY = (float)editors.Y.Value;

            _onValuesChanged();
        }

        private RuntimeSection? FindSection(string side)
        {
            if (_graph == null)
                return null;

            return _graph.Sections.FirstOrDefault(s =>
                string.Equals(s.Side?.Trim(), side, StringComparison.OrdinalIgnoreCase));
        }

        private RuntimeSegment? FindSegment(string segmentId)
        {
            if (_graph == null)
                return null;

            return _graph.Segments.FirstOrDefault(s =>
                string.Equals(s.Id, segmentId, StringComparison.OrdinalIgnoreCase));
        }

        private static Label MakeLabel(string text, int left, int top, int width)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Left = left,
                Top = top,
                Width = width
            };
        }

        private static NumericUpDown MakeDecimal(int left, int top, int width, decimal increment, decimal min, decimal max, decimal value)
        {
            return new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = increment,
                Minimum = min,
                Maximum = max,
                Value = value,
                Left = left,
                Top = top,
                Width = width
            };
        }

        private static NumericUpDown MakeWhole(int left, int top, int width, decimal min, decimal max, decimal value)
        {
            return new NumericUpDown
            {
                DecimalPlaces = 0,
                Increment = 1m,
                Minimum = min,
                Maximum = max,
                Value = value,
                Left = left,
                Top = top,
                Width = width
            };
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private sealed class OutputEditors
        {
            public NumericUpDown OutputScale { get; set; } = null!;
            public NumericUpDown OutputX { get; set; } = null!;
            public NumericUpDown OutputY { get; set; } = null!;
            public NumericUpDown NodeTextScale { get; set; } = null!;
            public NumericUpDown RacerTextScale { get; set; } = null!;
        }

        private sealed class SectionEditors
        {
            public NumericUpDown Scale { get; set; } = null!;
            public NumericUpDown X { get; set; } = null!;
            public NumericUpDown Y { get; set; } = null!;
        }

        private sealed class SegmentEditors
        {
            public NumericUpDown Scale { get; set; } = null!;
            public NumericUpDown X { get; set; } = null!;
            public NumericUpDown Y { get; set; } = null!;
        }
    }
}