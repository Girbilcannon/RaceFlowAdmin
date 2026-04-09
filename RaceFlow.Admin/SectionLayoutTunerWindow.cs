using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RaceFlow.Core.Runtime;

namespace RaceFlow.Admin
{
    public sealed class SectionLayoutTunerWindow : Form
    {
        private readonly Action _onValuesChanged;
        private RuntimeGraph? _graph;

        private readonly Dictionary<string, SectionEditors> _editors =
            new Dictionary<string, SectionEditors>(StringComparer.OrdinalIgnoreCase);

        public SectionLayoutTunerWindow(Action onValuesChanged)
        {
            _onValuesChanged = onValuesChanged ?? throw new ArgumentNullException(nameof(onValuesChanged));

            Text = "Section Layout Tuner";
            StartPosition = FormStartPosition.Manual;
            Size = new Size(360, 420);
            MinimumSize = new Size(360, 420);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            BackColor = Color.FromArgb(24, 26, 32);

            BuildUi();
        }

        public void BindGraph(RuntimeGraph? graph)
        {
            _graph = graph;
            LoadValuesFromGraph();
        }

        private void BuildUi()
        {
            var root = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = BackColor
            };

            Controls.Add(root);

            root.Controls.Add(CreateSectionGroup("left", "LEFT"));
            root.Controls.Add(CreateSectionGroup("top", "TOP"));
            root.Controls.Add(CreateSectionGroup("right", "RIGHT"));
            root.Controls.Add(CreateSectionGroup("bottom", "BOTTOM"));
        }

        private Control CreateSectionGroup(string sideKey, string title)
        {
            var group = new GroupBox
            {
                Text = title,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 34, 40),
                Width = 320,
                Height = 86,
                Padding = new Padding(10)
            };

            var lblScale = new Label
            {
                Text = "Scale",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Left = 12,
                Top = 28,
                Width = 40
            };

            var nudScale = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.01m,
                Minimum = 0.10m,
                Maximum = 5.00m,
                Value = 1.00m,
                Left = 58,
                Top = 24,
                Width = 68
            };

            var lblX = new Label
            {
                Text = "X",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Left = 142,
                Top = 28,
                Width = 14
            };

            var nudX = new NumericUpDown
            {
                DecimalPlaces = 0,
                Increment = 1m,
                Minimum = -5000m,
                Maximum = 5000m,
                Value = 0m,
                Left = 160,
                Top = 24,
                Width = 62
            };

            var lblY = new Label
            {
                Text = "Y",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Left = 236,
                Top = 28,
                Width = 14
            };

            var nudY = new NumericUpDown
            {
                DecimalPlaces = 0,
                Increment = 1m,
                Minimum = -5000m,
                Maximum = 5000m,
                Value = 0m,
                Left = 254,
                Top = 24,
                Width = 62
            };

            void HandleValueChanged(object? sender, EventArgs e)
            {
                ApplyEditorValuesToGraph(sideKey);
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

            _editors[sideKey] = new SectionEditors
            {
                Scale = nudScale,
                X = nudX,
                Y = nudY
            };

            return group;
        }

        private void LoadValuesFromGraph()
        {
            foreach (var pair in _editors)
            {
                string side = pair.Key;
                SectionEditors editors = pair.Value;

                RuntimeSection? section = FindSection(side);

                if (section == null)
                {
                    editors.Scale.Value = 1.00m;
                    editors.X.Value = 0m;
                    editors.Y.Value = 0m;

                    editors.Scale.Enabled = false;
                    editors.X.Enabled = false;
                    editors.Y.Enabled = false;
                    continue;
                }

                editors.Scale.Enabled = true;
                editors.X.Enabled = true;
                editors.Y.Enabled = true;

                decimal scale = ClampDecimal((decimal)section.VisualScale, editors.Scale.Minimum, editors.Scale.Maximum);
                decimal x = ClampDecimal((decimal)section.OffsetX, editors.X.Minimum, editors.X.Maximum);
                decimal y = ClampDecimal((decimal)section.OffsetY, editors.Y.Minimum, editors.Y.Maximum);

                editors.Scale.Value = scale;
                editors.X.Value = x;
                editors.Y.Value = y;
            }
        }

        private void ApplyEditorValuesToGraph(string side)
        {
            RuntimeSection? section = FindSection(side);
            if (section == null)
                return;

            SectionEditors editors = _editors[side];

            section.VisualScale = (float)editors.Scale.Value;
            section.OffsetX = (float)editors.X.Value;
            section.OffsetY = (float)editors.Y.Value;

            _onValuesChanged();
        }

        private RuntimeSection? FindSection(string side)
        {
            if (_graph == null)
                return null;

            return _graph.Sections.FirstOrDefault(s =>
                string.Equals(s.Side?.Trim(), side, StringComparison.OrdinalIgnoreCase));
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private sealed class SectionEditors
        {
            public NumericUpDown Scale { get; set; } = null!;
            public NumericUpDown X { get; set; } = null!;
            public NumericUpDown Y { get; set; } = null!;
        }
    }
}