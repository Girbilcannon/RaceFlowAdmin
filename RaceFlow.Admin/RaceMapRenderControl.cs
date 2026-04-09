using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using RaceFlow.Core.Enums;
using RaceFlow.Core.Runtime;

namespace RaceFlow.Admin
{
    public sealed class RaceMapRenderControl : Control
    {
        private RuntimeGraph? _graph;
        private RaceOutputFrame? _frame;
        private string? _themeFile;
        private ThemeSettings? _themeSettings;
        private readonly Dictionary<string, Image> _imageCache = new(StringComparer.OrdinalIgnoreCase);

        public RaceMapRenderControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            BackColor = Color.FromArgb(12, 14, 18);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Image image in _imageCache.Values)
                    image.Dispose();
                _imageCache.Clear();
            }

            base.Dispose(disposing);
        }

        public void UpdateScene(RuntimeGraph? graph, RaceOutputFrame? frame, string? themeFile = null)
        {
            _graph = graph;
            _frame = frame;
            _themeFile = themeFile;
            _themeSettings = LoadThemeSettings(themeFile);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle client = ClientRectangle;
            if (client.Width <= 0 || client.Height <= 0)
                return;

            if (_graph == null || _graph.Nodes.Count == 0)
            {
                DrawCenteredMessage(g, client, "No flow loaded.");
                return;
            }

            Dictionary<FlowSide, RectangleF> sideRegions = BuildSideRegions(client);
            Dictionary<string, SegmentLayout> segmentLayouts = BuildSegmentLayouts(_graph, sideRegions);

            if (segmentLayouts.Count == 0)
            {
                DrawCenteredMessage(g, client, "Flow has no drawable segments.");
                return;
            }

            PointF Transform(RuntimeNode node)
            {
                if (segmentLayouts.TryGetValue(node.SegmentId, out SegmentLayout? layout))
                    return layout.Transform(node);

                return new PointF(0f, 0f);
            }

            DrawEdges(g, _graph, Transform, _themeSettings);
            DrawNodes(g, _graph, Transform, _themeSettings, _imageCache);
            DrawRacers(g, _graph, _frame, Transform, _themeSettings);
        }

        private static Dictionary<FlowSide, RectangleF> BuildSideRegions(Rectangle client)
        {
            float outerMargin = 16f;

            RectangleF full = new RectangleF(
                outerMargin,
                outerMargin,
                Math.Max(1, client.Width - (outerMargin * 2)),
                Math.Max(1, client.Height - (outerMargin * 2)));

            float sideThickness = Math.Max(110f, full.Width * 0.16f);
            float topBottomThickness = Math.Max(110f, full.Height * 0.16f);

            return new Dictionary<FlowSide, RectangleF>
            {
                [FlowSide.Left] = new RectangleF(full.Left, full.Top, sideThickness, full.Height),
                [FlowSide.Top] = new RectangleF(full.Left, full.Top, full.Width, topBottomThickness),
                [FlowSide.Right] = new RectangleF(full.Right - sideThickness, full.Top, sideThickness, full.Height),
                [FlowSide.Bottom] = new RectangleF(full.Left, full.Bottom - topBottomThickness, full.Width, topBottomThickness)
            };
        }

        private static Dictionary<string, SegmentLayout> BuildSegmentLayouts(
            RuntimeGraph graph,
            Dictionary<FlowSide, RectangleF> sideRegions)
        {
            var result = new Dictionary<string, SegmentLayout>(StringComparer.OrdinalIgnoreCase);

            foreach (RuntimeSection section in graph.Sections)
            {
                FlowSide side = GetSideOrUnknown(section.Side);
                if (side == FlowSide.Unknown)
                    continue;

                if (!sideRegions.TryGetValue(side, out RectangleF sideRect))
                    continue;

                List<RuntimeSegment> sectionSegments = graph.Segments
                    .Where(s => GetSideOrUnknown(s.Side) == side)
                    .OrderBy(s => s.Index)
                    .ToList();

                if (sectionSegments.Count == 0)
                    continue;

                for (int i = 0; i < sectionSegments.Count; i++)
                {
                    RuntimeSegment segment = sectionSegments[i];

                    List<RuntimeNode> segmentNodes = graph.Nodes
                        .Where(n => string.Equals(n.SegmentId, segment.Id, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(n => n.Index)
                        .ToList();

                    if (segmentNodes.Count == 0)
                        continue;

                    string effectiveDirection = GetEffectiveDirection(section.Direction, segment.Direction, side);
                    RectangleF sourceBounds = GetTransformedSourceBounds(segmentNodes, side, effectiveDirection);
                    RectangleF targetRect = GetSegmentTargetRect(sideRect, side, effectiveDirection, i, sectionSegments.Count);

                    result[segment.Id] = new SegmentLayout(section, side, effectiveDirection, targetRect, sourceBounds);
                }
            }

            return result;
        }

        private static string GetEffectiveDirection(string? sectionDirection, string? segmentDirection, FlowSide side)
        {
            string segment = (segmentDirection ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(segment))
                return segment;

            string section = (sectionDirection ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(section))
                return section;

            return side switch
            {
                FlowSide.Left => "top-to-bottom",
                FlowSide.Right => "top-to-bottom",
                FlowSide.Top => "left-to-right",
                FlowSide.Bottom => "left-to-right",
                _ => string.Empty
            };
        }

        private static RectangleF GetSegmentTargetRect(RectangleF sideRect, FlowSide side, string direction, int segmentIndex, int segmentCount)
        {
            float innerPadX = 6f;
            float innerPadY = 6f;

            RectangleF contentRect = new RectangleF(
                sideRect.Left + innerPadX,
                sideRect.Top + innerPadY,
                Math.Max(1f, sideRect.Width - (innerPadX * 2f)),
                Math.Max(1f, sideRect.Height - (innerPadY * 2f)));

            int slotIndex = segmentIndex;

            if (side == FlowSide.Left || side == FlowSide.Right)
            {
                bool reverseSlotOrder = string.Equals(direction, "bottom-to-top", StringComparison.OrdinalIgnoreCase);
                if (reverseSlotOrder)
                    slotIndex = (segmentCount - 1) - segmentIndex;

                float segmentHeight = contentRect.Height / Math.Max(1, segmentCount);
                return new RectangleF(contentRect.Left, contentRect.Top + (segmentHeight * slotIndex), contentRect.Width, Math.Max(1f, segmentHeight));
            }

            bool reverseHorizontal = string.Equals(direction, "right-to-left", StringComparison.OrdinalIgnoreCase);
            if (reverseHorizontal)
                slotIndex = (segmentCount - 1) - segmentIndex;

            float segmentWidth = contentRect.Width / Math.Max(1, segmentCount);
            return new RectangleF(contentRect.Left + (segmentWidth * slotIndex), contentRect.Top, Math.Max(1f, segmentWidth), contentRect.Height);
        }

        private static RectangleF GetTransformedSourceBounds(IEnumerable<RuntimeNode> nodes, FlowSide side, string direction)
        {
            List<PointF> points = nodes.Select(node => TransformRawPoint(node.OverlayX, node.OverlayY, side)).ToList();

            float minX = points.Min(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxX = points.Max(p => p.X);
            float maxY = points.Max(p => p.Y);

            RectangleF orientedBounds = new RectangleF(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));

            List<PointF> directedPoints = points.Select(p => ApplyDirectionFlip(p, orientedBounds, side, direction)).ToList();

            float directedMinX = directedPoints.Min(p => p.X);
            float directedMinY = directedPoints.Min(p => p.Y);
            float directedMaxX = directedPoints.Max(p => p.X);
            float directedMaxY = directedPoints.Max(p => p.Y);

            return new RectangleF(directedMinX, directedMinY, Math.Max(1f, directedMaxX - directedMinX), Math.Max(1f, directedMaxY - directedMinY));
        }

        private static PointF TransformRawPoint(float x, float y, FlowSide side)
        {
            if (side == FlowSide.Left || side == FlowSide.Right)
                return new PointF(y, x);

            return new PointF(x, y);
        }

        private static PointF ApplyDirectionFlip(PointF point, RectangleF bounds, FlowSide side, string direction)
        {
            bool vertical = side == FlowSide.Left || side == FlowSide.Right;
            float x = point.X;
            float y = point.Y;

            if (vertical)
            {
                if (string.Equals(direction, "bottom-to-top", StringComparison.OrdinalIgnoreCase))
                {
                    float relativeY = y - bounds.Top;
                    y = bounds.Top + (bounds.Height - relativeY);
                }
            }
            else if (string.Equals(direction, "right-to-left", StringComparison.OrdinalIgnoreCase))
            {
                float relativeX = x - bounds.Left;
                x = bounds.Left + (bounds.Width - relativeX);
            }

            return new PointF(x, y);
        }

        private static void DrawCenteredMessage(Graphics g, Rectangle client, string message)
        {
            using var font = new Font("Segoe UI", 16f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Gainsboro);
            SizeF size = g.MeasureString(message, font);

            g.DrawString(message, font, brush, client.Left + ((client.Width - size.Width) * 0.5f), client.Top + ((client.Height - size.Height) * 0.5f));
        }

        private static void DrawEdges(Graphics g, RuntimeGraph graph, Func<RuntimeNode, PointF> transform, ThemeSettings? theme)
        {
            if (theme?.Settings.LineVisibility == false)
                return;

            foreach (RuntimeEdge edge in graph.Edges)
            {
                if (edge.FromNode == null || edge.ToNode == null)
                    continue;

                PointF aCenter = transform(edge.FromNode);
                PointF bCenter = transform(edge.ToNode);
                (PointF a, PointF b) = GetTrimmedEdgePoints(edge.FromNode, edge.ToNode, aCenter, bCenter, theme);

                SegmentOverride? segmentOverride = theme?.GetSegmentOverride(edge.FromNode.SegmentId);
                Color lineColor = theme?.ResolveLineColor(edge.FromNode, edge.ToNode, segmentOverride) ?? Color.FromArgb(95, 120, 140);
                float thickness = theme?.ResolveLineThickness(segmentOverride) ?? 3f;

                using var pen = new Pen(lineColor, thickness)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                if (theme?.Settings.ShadowEnabled == true)
                {
                    int alpha = (int)Math.Round(255f * Math.Max(0f, Math.Min(1f, theme.Settings.ShadowOpacity)));
                    using var shadowPen = new Pen(Color.FromArgb(alpha, 0, 0, 0), thickness + 2f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };

                    g.DrawLine(
                        shadowPen,
                        a.X + theme.Settings.ShadowOffsetX,
                        a.Y + theme.Settings.ShadowOffsetY,
                        b.X + theme.Settings.ShadowOffsetX,
                        b.Y + theme.Settings.ShadowOffsetY);
                }

                g.DrawLine(pen, a, b);
            }
        }

        private static void DrawNodes(
            Graphics g,
            RuntimeGraph graph,
            Func<RuntimeNode, PointF> transform,
            ThemeSettings? theme,
            Dictionary<string, Image> imageCache)
        {
            if (theme?.Settings.NodeVisibility == false)
                return;

            float titleScale = theme?.Settings.TitleScale ?? 1f;
            float titleOffsetX = theme?.Settings.TitleOffsetX ?? 0f;
            float titleOffsetY = theme?.Settings.TitleOffsetY ?? -40f;
            using var labelFont = new Font("Segoe UI", Math.Max(6f, 9f * titleScale), FontStyle.Bold);
            using var labelBrush = new SolidBrush(Color.White);

            foreach (RuntimeNode node in graph.Nodes.OrderBy(n => n.Index))
            {
                PointF p = transform(node);
                NodeVisual visual = theme?.ResolveNodeVisual(node) ?? NodeVisual.ForDefaults(node.NodeType);

                if (!visual.Visible)
                    continue;

                float size = GetNodeSize(node, theme, visual);
                RectangleF rect = new RectangleF(p.X - (size * 0.5f), p.Y - (size * 0.5f), size, size);

                bool drewImage = false;
                Image? nodeImage = TryGetThemeImage(theme, visual.Image, imageCache);
                if (nodeImage != null)
                {
                    g.DrawImage(nodeImage, rect);
                    drewImage = true;
                }

                if (!drewImage)
                {
                    using var fill = new SolidBrush(GetNodeColor(node));
                    using var outline = new Pen(Color.FromArgb(230, 240, 250), 2f);

                    if (node.NodeType == FlowNodeType.Split || node.NodeType == FlowNodeType.Converge)
                    {
                        g.FillRectangle(fill, rect);
                        g.DrawRectangle(outline, rect.X, rect.Y, rect.Width, rect.Height);
                    }
                    else
                    {
                        g.FillEllipse(fill, rect);
                        g.DrawEllipse(outline, rect);
                    }
                }

                if (visual.TitleVisible)
                {
                    string label = node.Label;
                    SizeF labelSize = g.MeasureString(label, labelFont);
                    g.DrawString(label, labelFont, labelBrush, p.X - (labelSize.Width * 0.5f) + titleOffsetX, p.Y + titleOffsetY + (size * 0.5f));
                }
            }
        }

        private static void DrawRacers(
            Graphics g,
            RuntimeGraph graph,
            RaceOutputFrame? frame,
            Func<RuntimeNode, PointF> transform,
            ThemeSettings? theme)
        {
            if (frame == null || frame.Racers.Count == 0)
                return;

            RacerThemeSettings racerSettings = theme?.Racers ?? new RacerThemeSettings();

            float activeDotSize = Math.Max(2f, racerSettings.DotSize);
            float inactiveDotSize = Math.Max(2f, racerSettings.InactiveDotSize);
            float glowScale = Math.Max(1f, racerSettings.GlowScale);
            float nameSize = Math.Max(1f, 8.5f * racerSettings.NameScale);
            float nameOffsetX = racerSettings.NameOffsetX;
            float nameOffsetY = racerSettings.NameOffsetY;

            using var nameFont = new Font("Segoe UI", nameSize, FontStyle.Bold);

            foreach (RaceOutputRacerVisual racer in frame.Racers)
            {
                PointF? point = GetRacerPoint(graph, racer, transform);
                if (!point.HasValue)
                    continue;

                Color fillColor = ParseColor(racer.ColorHex);
                float dotSize = racer.IsActive ? activeDotSize : inactiveDotSize;

                RectangleF dotRect = new RectangleF(
                    point.Value.X - (dotSize * 0.5f),
                    point.Value.Y - (dotSize * 0.5f),
                    dotSize,
                    dotSize);

                int glowAlpha = (int)Math.Round(255f * Math.Max(0f, Math.Min(1f, racerSettings.GlowOpacity)));
                using var glow = new SolidBrush(Color.FromArgb(glowAlpha, fillColor));
                using var fill = new SolidBrush(fillColor);
                using var outline = new Pen(Color.White, 2f);

                float glowRadius = dotSize * glowScale;
                RectangleF glowRect = new RectangleF(
                    point.Value.X - (glowRadius * 0.5f),
                    point.Value.Y - (glowRadius * 0.5f),
                    glowRadius,
                    glowRadius);

                g.FillEllipse(glow, glowRect);
                g.FillEllipse(fill, dotRect);
                g.DrawEllipse(outline, dotRect);

                if (racerSettings.NameVisible)
                {
                    using var textBrush = new SolidBrush(Color.White);
                    g.DrawString(
                        racer.RacerName,
                        nameFont,
                        textBrush,
                        point.Value.X + nameOffsetX,
                        point.Value.Y + nameOffsetY);
                }
            }
        }

        private static PointF? GetRacerPoint(RuntimeGraph graph, RaceOutputRacerVisual racer, Func<RuntimeNode, PointF> transform)
        {
            if (!string.IsNullOrWhiteSpace(racer.LastConfirmedNodeId) &&
                !string.IsNullOrWhiteSpace(racer.TargetNodeId) &&
                graph.NodesById.TryGetValue(racer.LastConfirmedNodeId, out RuntimeNode? fromNode) &&
                graph.NodesById.TryGetValue(racer.TargetNodeId, out RuntimeNode? toNode))
            {
                PointF a = transform(fromNode);
                PointF b = transform(toNode);
                float t = (float)Math.Max(0d, Math.Min(1d, racer.EdgeProgress));
                return new PointF(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
            }

            if (!string.IsNullOrWhiteSpace(racer.LastConfirmedNodeId) && graph.NodesById.TryGetValue(racer.LastConfirmedNodeId, out RuntimeNode? node))
                return transform(node);

            return null;
        }

        private static (PointF a, PointF b) GetTrimmedEdgePoints(RuntimeNode fromNode, RuntimeNode toNode, PointF aCenter, PointF bCenter, ThemeSettings? theme)
        {
            float dx = bCenter.X - aCenter.X;
            float dy = bCenter.Y - aCenter.Y;
            float length = (float)Math.Sqrt((dx * dx) + (dy * dy));

            if (length <= 0.001f)
                return (aCenter, bCenter);

            float ux = dx / length;
            float uy = dy / length;
            float startTrim = GetNodeSize(fromNode, theme, theme?.ResolveNodeVisual(fromNode)) * 0.5f;
            float endTrim = GetNodeSize(toNode, theme, theme?.ResolveNodeVisual(toNode)) * 0.5f;

            PointF a = new PointF(aCenter.X + (ux * startTrim), aCenter.Y + (uy * startTrim));
            PointF b = new PointF(bCenter.X - (ux * endTrim), bCenter.Y - (uy * endTrim));
            return (a, b);
        }

        private static ThemeSettings? LoadThemeSettings(string? themeFile)
        {
            if (string.IsNullOrWhiteSpace(themeFile))
                return null;

            try
            {
                string themesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");
                string themePath = Path.Combine(themesRoot, themeFile);
                if (!File.Exists(themePath))
                    return null;

                string json = File.ReadAllText(themePath);
                using JsonDocument doc = JsonDocument.Parse(json);

                ThemeSettings settings = new
                (
                    themeFile,
                    Path.Combine(themesRoot, Path.GetFileNameWithoutExtension(themeFile))
                );

                if (doc.RootElement.TryGetProperty("nodes", out JsonElement nodes) && nodes.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in nodes.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                            settings.NodeImages[property.Name] = property.Value.GetString() ?? string.Empty;
                    }
                }

                if (doc.RootElement.TryGetProperty("lines", out JsonElement lines) && lines.ValueKind == JsonValueKind.Object)
                {
                    if (lines.TryGetProperty("defaultColor", out JsonElement defaultColor) && defaultColor.ValueKind == JsonValueKind.String)
                        settings.DefaultLineColor = ParseThemeColor(defaultColor.GetString(), settings.DefaultLineColor);
                    if (lines.TryGetProperty("splitColor", out JsonElement splitColor) && splitColor.ValueKind == JsonValueKind.String)
                        settings.SplitLineColor = ParseThemeColor(splitColor.GetString(), settings.SplitLineColor);
                    if (lines.TryGetProperty("convergeColor", out JsonElement convergeColor) && convergeColor.ValueKind == JsonValueKind.String)
                        settings.ConvergeLineColor = ParseThemeColor(convergeColor.GetString(), settings.ConvergeLineColor);
                    if (lines.TryGetProperty("thickness", out JsonElement thickness) && thickness.ValueKind == JsonValueKind.Number)
                        settings.LineThickness = (float)thickness.GetDouble();
                }

                if (doc.RootElement.TryGetProperty("settings", out JsonElement themeSettings) && themeSettings.ValueKind == JsonValueKind.Object)
                {
                    if (themeSettings.TryGetProperty("shadowEnabled", out JsonElement shadowEnabled) && (shadowEnabled.ValueKind == JsonValueKind.True || shadowEnabled.ValueKind == JsonValueKind.False))
                        settings.Settings.ShadowEnabled = shadowEnabled.GetBoolean();
                    if (themeSettings.TryGetProperty("shadowOpacity", out JsonElement shadowOpacity) && shadowOpacity.ValueKind == JsonValueKind.Number)
                        settings.Settings.ShadowOpacity = (float)shadowOpacity.GetDouble();
                    if (themeSettings.TryGetProperty("shadowOffsetX", out JsonElement shadowOffsetX) && shadowOffsetX.ValueKind == JsonValueKind.Number)
                        settings.Settings.ShadowOffsetX = (float)shadowOffsetX.GetDouble();
                    if (themeSettings.TryGetProperty("shadowOffsetY", out JsonElement shadowOffsetY) && shadowOffsetY.ValueKind == JsonValueKind.Number)
                        settings.Settings.ShadowOffsetY = (float)shadowOffsetY.GetDouble();
                    if (themeSettings.TryGetProperty("nodeScale", out JsonElement nodeScale) && nodeScale.ValueKind == JsonValueKind.Number)
                        settings.Settings.NodeScale = (float)nodeScale.GetDouble();
                    if (themeSettings.TryGetProperty("nodeVisibility", out JsonElement nodeVisibility) && (nodeVisibility.ValueKind == JsonValueKind.True || nodeVisibility.ValueKind == JsonValueKind.False))
                        settings.Settings.NodeVisibility = nodeVisibility.GetBoolean();
                    if (themeSettings.TryGetProperty("lineVisibility", out JsonElement lineVisibility) && (lineVisibility.ValueKind == JsonValueKind.True || lineVisibility.ValueKind == JsonValueKind.False))
                        settings.Settings.LineVisibility = lineVisibility.GetBoolean();
                    if (themeSettings.TryGetProperty("titleVisible", out JsonElement titleVisible) && (titleVisible.ValueKind == JsonValueKind.True || titleVisible.ValueKind == JsonValueKind.False))
                        settings.Settings.TitleVisible = titleVisible.GetBoolean();
                    if (themeSettings.TryGetProperty("titleScale", out JsonElement titleScale) && titleScale.ValueKind == JsonValueKind.Number)
                        settings.Settings.TitleScale = (float)titleScale.GetDouble();
                    if (themeSettings.TryGetProperty("titleOffsetX", out JsonElement titleOffsetX) && titleOffsetX.ValueKind == JsonValueKind.Number)
                        settings.Settings.TitleOffsetX = (float)titleOffsetX.GetDouble();
                    if (themeSettings.TryGetProperty("titleOffsetY", out JsonElement titleOffsetY) && titleOffsetY.ValueKind == JsonValueKind.Number)
                        settings.Settings.TitleOffsetY = (float)titleOffsetY.GetDouble();
                }

                if (doc.RootElement.TryGetProperty("racers", out JsonElement racers) && racers.ValueKind == JsonValueKind.Object)
                {
                    if (racers.TryGetProperty("dotSize", out JsonElement dotSize) && dotSize.ValueKind == JsonValueKind.Number)
                        settings.Racers.DotSize = (float)dotSize.GetDouble();
                    if (racers.TryGetProperty("inactiveDotSize", out JsonElement inactiveDotSize) && inactiveDotSize.ValueKind == JsonValueKind.Number)
                        settings.Racers.InactiveDotSize = (float)inactiveDotSize.GetDouble();
                    if (racers.TryGetProperty("glowScale", out JsonElement glowScale) && glowScale.ValueKind == JsonValueKind.Number)
                        settings.Racers.GlowScale = (float)glowScale.GetDouble();
                    if (racers.TryGetProperty("glowOpacity", out JsonElement glowOpacity) && glowOpacity.ValueKind == JsonValueKind.Number)
                        settings.Racers.GlowOpacity = (float)glowOpacity.GetDouble();
                    if (racers.TryGetProperty("nameVisible", out JsonElement nameVisible) && (nameVisible.ValueKind == JsonValueKind.True || nameVisible.ValueKind == JsonValueKind.False))
                        settings.Racers.NameVisible = nameVisible.GetBoolean();
                    if (racers.TryGetProperty("nameScale", out JsonElement nameScale) && nameScale.ValueKind == JsonValueKind.Number)
                        settings.Racers.NameScale = (float)nameScale.GetDouble();
                    if (racers.TryGetProperty("nameOffsetX", out JsonElement nameOffsetX) && nameOffsetX.ValueKind == JsonValueKind.Number)
                        settings.Racers.NameOffsetX = (float)nameOffsetX.GetDouble();
                    if (racers.TryGetProperty("nameOffsetY", out JsonElement nameOffsetY) && nameOffsetY.ValueKind == JsonValueKind.Number)
                        settings.Racers.NameOffsetY = (float)nameOffsetY.GetDouble();
                }

                if (doc.RootElement.TryGetProperty("segmentOverrides", out JsonElement segmentOverrides) && segmentOverrides.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in segmentOverrides.EnumerateObject())
                    {
                        SegmentOverride segment = new();
                        JsonElement value = property.Value;

                        if (value.TryGetProperty("lineColor", out JsonElement lineColor) && lineColor.ValueKind == JsonValueKind.String)
                            segment.LineColor = ParseThemeColor(lineColor.GetString(), settings.DefaultLineColor);
                        if (value.TryGetProperty("thickness", out JsonElement segThickness) && segThickness.ValueKind == JsonValueKind.Number)
                            segment.Thickness = (float)segThickness.GetDouble();
                        if (value.TryGetProperty("nodeImage", out JsonElement nodeImage) && nodeImage.ValueKind == JsonValueKind.String)
                            segment.NodeImage = nodeImage.GetString();
                        if (value.TryGetProperty("nodeScale", out JsonElement nodeScale) && nodeScale.ValueKind == JsonValueKind.Number)
                            segment.NodeScale = (float)nodeScale.GetDouble();
                        if (value.TryGetProperty("titleVisible", out JsonElement segTitleVisible) && (segTitleVisible.ValueKind == JsonValueKind.True || segTitleVisible.ValueKind == JsonValueKind.False))
                            segment.TitleVisible = segTitleVisible.GetBoolean();

                        settings.SegmentOverrides[property.Name] = segment;
                    }
                }

                if (doc.RootElement.TryGetProperty("nodeOverrides", out JsonElement nodeOverrides) && nodeOverrides.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in nodeOverrides.EnumerateObject())
                    {
                        NodeOverride nodeOverride = new();
                        JsonElement value = property.Value;

                        if (value.TryGetProperty("image", out JsonElement image) && image.ValueKind == JsonValueKind.String)
                            nodeOverride.Image = image.GetString();
                        if (value.TryGetProperty("scale", out JsonElement scale) && scale.ValueKind == JsonValueKind.Number)
                            nodeOverride.Scale = (float)scale.GetDouble();
                        if (value.TryGetProperty("titleVisible", out JsonElement titleVisible) && (titleVisible.ValueKind == JsonValueKind.True || titleVisible.ValueKind == JsonValueKind.False))
                            nodeOverride.TitleVisible = titleVisible.GetBoolean();

                        settings.NodeOverrides[property.Name] = nodeOverride;
                    }
                }

                return settings;
            }
            catch
            {
                return null;
            }
        }

        private static Image? TryGetThemeImage(ThemeSettings? theme, string? imageName, Dictionary<string, Image> imageCache)
        {
            if (theme == null || string.IsNullOrWhiteSpace(imageName) || string.IsNullOrWhiteSpace(theme.ThemeAssetDirectory))
                return null;

            if (imageCache.TryGetValue(imageName, out Image? cached))
                return cached;

            try
            {
                string fullPath = Path.Combine(theme.ThemeAssetDirectory, imageName);
                if (!File.Exists(fullPath))
                    return null;

                Image image = Image.FromFile(fullPath);
                imageCache[imageName] = image;
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static float GetNodeSize(RuntimeNode node, ThemeSettings? theme, NodeVisual? visual)
        {
            float baseSize = node.NodeType switch
            {
                FlowNodeType.Start => 20f,
                FlowNodeType.End => 22f,
                FlowNodeType.Boss => 24f,
                FlowNodeType.Split => 22f,
                FlowNodeType.Converge => 22f,
                _ => 16f
            };

            float scale = visual?.Scale ?? theme?.Settings.NodeScale ?? 1f;
            return Math.Max(4f, baseSize * scale);
        }

        private static Color GetNodeColor(RuntimeNode node)
        {
            return node.NodeType switch
            {
                FlowNodeType.Start => Color.FromArgb(44, 180, 90),
                FlowNodeType.Checkpoint => Color.FromArgb(70, 150, 225),
                FlowNodeType.Split => Color.FromArgb(225, 150, 55),
                FlowNodeType.Converge => Color.FromArgb(160, 95, 210),
                FlowNodeType.End => Color.FromArgb(205, 70, 70),
                FlowNodeType.Boss => Color.FromArgb(220, 60, 120),
                _ => Color.Gray
            };
        }

        private static FlowSide GetSideOrUnknown(string? rawSide)
        {
            return rawSide?.Trim().ToLowerInvariant() switch
            {
                "left" => FlowSide.Left,
                "top" => FlowSide.Top,
                "right" => FlowSide.Right,
                "bottom" => FlowSide.Bottom,
                _ => FlowSide.Unknown
            };
        }

        private static Color ParseColor(string? colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
                return Color.White;

            string text = colorHex.Trim();
            if (text.StartsWith("#"))
                text = text.Substring(1);
            if (text.Length != 6)
                return Color.White;

            if (!int.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r))
                return Color.White;
            if (!int.TryParse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g))
                return Color.White;
            if (!int.TryParse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
                return Color.White;

            return Color.FromArgb(r, g, b);
        }

        private static Color ParseThemeColor(string? text, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;
            string value = text.Trim();
            if (value.StartsWith("#"))
                value = value.Substring(1);
            if (value.Length != 6)
                return fallback;
            if (!int.TryParse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r))
                return fallback;
            if (!int.TryParse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g))
                return fallback;
            if (!int.TryParse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
                return fallback;
            return Color.FromArgb(r, g, b);
        }

        private sealed class SegmentLayout
        {
            public RuntimeSection Section { get; }
            public FlowSide Side { get; }
            public string Direction { get; }
            public RectangleF TargetRect { get; }
            public RectangleF SourceBounds { get; }

            private readonly float _scale;
            private readonly float _offsetX;
            private readonly float _offsetY;

            public SegmentLayout(RuntimeSection section, FlowSide side, string direction, RectangleF targetRect, RectangleF sourceBounds)
            {
                Section = section;
                Side = side;
                Direction = direction;
                TargetRect = targetRect;
                SourceBounds = sourceBounds;

                float usableWidth = Math.Max(1f, targetRect.Width - 8f);
                float usableHeight = Math.Max(1f, targetRect.Height - 8f);
                float scaleX = usableWidth / Math.Max(1f, sourceBounds.Width);
                float scaleY = usableHeight / Math.Max(1f, sourceBounds.Height);
                float baseScale = Math.Min(scaleX, scaleY);

                _scale = baseScale * section.VisualScale;

                float scaledWidth = sourceBounds.Width * _scale;
                float scaledHeight = sourceBounds.Height * _scale;

                _offsetX = targetRect.Left + ((targetRect.Width - scaledWidth) * 0.5f) - (sourceBounds.Left * _scale) + section.OffsetX;
                _offsetY = targetRect.Top + ((targetRect.Height - scaledHeight) * 0.5f) - (sourceBounds.Top * _scale) + section.OffsetY;
            }

            public PointF Transform(RuntimeNode node)
            {
                PointF point = TransformRawPoint(node.OverlayX, node.OverlayY, Side);
                point = ApplyDirectionFlip(point, SourceBounds, Side, Direction);
                return new PointF((point.X * _scale) + _offsetX, (point.Y * _scale) + _offsetY);
            }
        }

        private sealed class ThemeSettings
        {
            public string ThemeFileName { get; }
            public string ThemeAssetDirectory { get; }

            public Color DefaultLineColor { get; set; } = Color.FromArgb(95, 120, 140);
            public Color SplitLineColor { get; set; } = Color.FromArgb(170, 130, 60);
            public Color ConvergeLineColor { get; set; } = Color.FromArgb(140, 90, 170);
            public float LineThickness { get; set; } = 3f;

            public Dictionary<string, string> NodeImages { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, SegmentOverride> SegmentOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, NodeOverride> NodeOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);
            public ThemeFlags Settings { get; } = new();
            public RacerThemeSettings Racers { get; } = new();

            public ThemeSettings(string themeFileName, string themeAssetDirectory)
            {
                ThemeFileName = themeFileName;
                ThemeAssetDirectory = themeAssetDirectory;
            }

            public SegmentOverride? GetSegmentOverride(string? segmentId)
            {
                if (string.IsNullOrWhiteSpace(segmentId))
                    return null;
                SegmentOverrides.TryGetValue(segmentId, out SegmentOverride? value);
                return value;
            }

            public NodeVisual ResolveNodeVisual(RuntimeNode node)
            {
                SegmentOverride? segment = GetSegmentOverride(node.SegmentId);
                NodeOverrides.TryGetValue(node.Id, out NodeOverride? nodeOverride);

                string typeKey = node.NodeType.ToString().ToLowerInvariant();
                NodeImages.TryGetValue(typeKey, out string? baseImage);
                if (string.IsNullOrWhiteSpace(baseImage) && typeKey == "boss")
                    NodeImages.TryGetValue("end", out baseImage);

                return new NodeVisual
                {
                    Image = nodeOverride?.Image ?? segment?.NodeImage ?? baseImage,
                    Scale = nodeOverride?.Scale ?? segment?.NodeScale ?? Settings.NodeScale,
                    TitleVisible = nodeOverride?.TitleVisible ?? segment?.TitleVisible ?? Settings.TitleVisible,
                    Visible = Settings.NodeVisibility
                };
            }

            public Color ResolveLineColor(RuntimeNode fromNode, RuntimeNode toNode, SegmentOverride? segment)
            {
                if (segment?.LineColor != null)
                    return segment.LineColor.Value;
                if (fromNode.NodeType == FlowNodeType.Split)
                    return SplitLineColor;
                if (toNode.NodeType == FlowNodeType.Converge)
                    return ConvergeLineColor;
                return DefaultLineColor;
            }

            public float ResolveLineThickness(SegmentOverride? segment)
            {
                return Math.Max(1f, segment?.Thickness ?? LineThickness);
            }
        }

        private sealed class RacerThemeSettings
        {
            public float DotSize { get; set; } = 18f;
            public float InactiveDotSize { get; set; } = 14f;
            public float GlowScale { get; set; } = 1.8f;
            public float GlowOpacity { get; set; } = 0.28f;
            public bool NameVisible { get; set; } = true;
            public float NameScale { get; set; } = 1.0f;
            public float NameOffsetX { get; set; } = 12f;
            public float NameOffsetY { get; set; } = -16f;
        }

        private sealed class ThemeFlags
        {
            public bool ShadowEnabled { get; set; }
            public float ShadowOpacity { get; set; } = 0.5f;
            public float ShadowOffsetX { get; set; } = 4f;
            public float ShadowOffsetY { get; set; } = 4f;
            public bool NodeVisibility { get; set; } = true;
            public bool LineVisibility { get; set; } = true;
            public bool TitleVisible { get; set; } = true;
            public float NodeScale { get; set; } = 1f;
            public float TitleScale { get; set; } = 1f;
            public float TitleOffsetX { get; set; } = 0f;
            public float TitleOffsetY { get; set; } = -40f;
        }

        private sealed class SegmentOverride
        {
            public Color? LineColor { get; set; }
            public float? Thickness { get; set; }
            public string? NodeImage { get; set; }
            public float? NodeScale { get; set; }
            public bool? TitleVisible { get; set; }
        }

        private sealed class NodeOverride
        {
            public string? Image { get; set; }
            public float? Scale { get; set; }
            public bool? TitleVisible { get; set; }
        }

        private sealed class NodeVisual
        {
            public string? Image { get; set; }
            public float Scale { get; set; } = 1f;
            public bool TitleVisible { get; set; } = true;
            public bool Visible { get; set; } = true;

            public static NodeVisual ForDefaults(FlowNodeType nodeType)
            {
                return new NodeVisual();
            }
        }
    }
}
